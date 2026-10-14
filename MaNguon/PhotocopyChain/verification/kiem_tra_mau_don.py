"""Kiểm tra PDF trong catalog và kết xuất ảnh từ chính PDF, không dựng lại biểu mẫu.

Chạy bằng Python có pypdf, pypdfium2, Pillow; chỉ dành cho bảo trì tài nguyên.
Không cần Python/Office khi người dùng chọn và đặt in mẫu trên ứng dụng.
"""
import argparse
import hashlib
import json
from pathlib import Path

import pypdfium2 as pdfium
from pypdf import PdfReader
from PIL import Image, ImageDraw
from pypdf.generic import IndirectObject


def has_active_content(value, seen):
    if isinstance(value, IndirectObject):
        key = (value.idnum, value.generation)
        if key in seen:
            return False
        seen.add(key)
        value = value.get_object()
    if isinstance(value, dict):
        if value.get('/S') in ['/JavaScript', '/Launch'] or '/JS' in value or '/EmbeddedFiles' in value:
            return True
        return any(has_active_content(child, seen) for child in value.values())
    if isinstance(value, list):
        return any(has_active_content(child, seen) for child in value)
    return False


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--folder", type=Path, required=True)
    parser.add_argument("--qa", type=Path, required=True)
    args = parser.parse_args()
    folder = args.folder.resolve()
    catalog = json.loads((folder / "catalog.json").read_text(encoding="utf-8"))
    preview = folder / "XemTruoc"
    preview.mkdir(exist_ok=True)
    args.qa.mkdir(parents=True, exist_ok=True)
    thumbnails = []
    seen = set()
    for item in catalog:
        name = item["TenFile"]
        if Path(name).name != name or item["MaMau"] in seen:
            raise ValueError("Tên file hoặc mã mẫu không hợp lệ")
        seen.add(item["MaMau"])
        path = folder / name
        if hashlib.sha256(path.read_bytes()).hexdigest() != item["Sha256"]:
            raise ValueError(f"Sai SHA256: {name}")
        reader = PdfReader(path)
        if has_active_content(reader.trailer, set()):
            raise ValueError(f"PDF chứa script/launch/file nhúng: {name}")
        # Một số PDF cho xem/in nhưng khóa sửa. Giữ nguyên file và quyền của nguồn.
        if len(reader.pages) != item["SoTrang"] or (reader.is_encrypted and not reader.user_access_permissions & 4):
            raise ValueError(f"Sai số trang hoặc PDF không cho phép in: {name}")
        with pdfium.PdfDocument(str(path)) as document:
            for index in range(len(document)):
                if not reader.pages[index].extract_text().strip():
                    raise ValueError(f"Thiếu chữ: {name}, trang {index + 1}")
                page = document[index]
                bitmap = page.render(scale=150 / 72)
                image = bitmap.to_pil().convert("RGB")
                image.save(preview / f"{path.stem}-trang-{index + 1}.png")
                # Giữ đủ bản lớn để kiểm tra bằng mắt; tổng hợp chỉ để chọn trang nhanh.
                image.save(args.qa / f"{path.stem}-{index + 1}.png")
                image.thumbnail((290, 400))
                thumb = Image.new("RGB", (310, 430), "#e5e7eb")
                thumb.paste(image, ((310 - image.width) // 2, 8))
                ImageDraw.Draw(thumb).text((8, 411), f"{path.stem} / {index + 1}", fill="black")
                thumbnails.append(thumb)
                bitmap.close()
                page.close()
        print(f"OK {item['MaMau']}: {item['SoTrang']} page(s)")
    for batch in range(0, len(thumbnails), 8):
        sheet = Image.new("RGB", (310 * 4, 430 * 2), "white")
        for index, thumb in enumerate(thumbnails[batch:batch + 8]):
            sheet.paste(thumb, ((index % 4) * 310, (index // 4) * 430))
        sheet.save(args.qa / f"contact-{batch // 8 + 1}.png")
    print(f"Verified {len(catalog)} templates / {len(thumbnails)} pages")


if __name__ == "__main__":
    main()
