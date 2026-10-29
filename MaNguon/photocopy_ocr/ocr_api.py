"""Nhận dạng một ảnh cho API; xử lý cục bộ và không lưu ảnh sau yêu cầu."""
import json
import math
import sys
from pathlib import Path
from qwen_support import read_image
from thu_qwen import LocalQwen

def prepare_image(path, crop=None):
    image = read_image(path)  # EXIF được xoay trước khi áp dụng tọa độ từ giao diện.
    if crop is not None:
        if (not isinstance(crop, list) or len(crop) != 4
                or any(type(v) not in (int, float) or not math.isfinite(v) for v in crop)):
            raise ValueError('Vùng cắt không hợp lệ.')
        x, y, width, height = crop
        if x < 0 or y < 0 or width <= 0 or height <= 0 or x + width > 1.000000001 or y + height > 1.000000001:
            raise ValueError('Vùng cắt phải nằm bên trong ảnh.')
        box = (math.floor(x * image.width), math.floor(y * image.height),
               min(image.width, math.ceil((x + width) * image.width)),
               min(image.height, math.ceil((y + height) * image.height)))
        if box[2] - box[0] < 8 or box[3] - box[1] < 8:
            raise ValueError('Vùng cắt quá nhỏ. Hãy chọn vùng chữ lớn hơn.')
        image = image.crop(box)
    image.thumbnail((1600, 1600))
    return image


def main():
    image_path, model, output = Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3])
    try:
        if model not in ('2b', '4b'):
            raise ValueError('Model không hợp lệ.')
        crop = json.loads(sys.argv[4]) if len(sys.argv) > 4 else None
        image = prepare_image(image_path, crop)
        with LocalQwen(output.parent, model=model) as reader:
            result = reader.predict(image)
        payload = {'ok': True, 'text': result['text'], 'truncated': result['finish_reason'] == 'length'}
    except (ValueError, OSError) as exc:
        payload = {'ok': False, 'message': str(exc)}
    except Exception:
        payload = {'ok': False, 'message': 'Không nạp được model OCR. Kiểm tra runtime và GPU trên máy chủ.'}
    output.write_text(json.dumps(payload, ensure_ascii=False), encoding='utf-8')

if __name__ == '__main__':
    main()
