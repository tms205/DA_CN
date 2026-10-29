"""Các tiện ích ảnh và kiểm tra file cho Qwen."""
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent

def verify_hash(path, expected):
    with Path(path).open('rb') as stream:
        actual = hashlib.file_digest(stream, 'sha256').hexdigest()
    if actual != expected:
        raise ValueError(f'File không đúng SHA256 đã chốt: {path}')

def read_image(path):
    from PIL import Image, ImageOps, UnidentifiedImageError
    path = Path(path)
    if path.suffix.lower() not in {'.jpg', '.jpeg', '.png'}:
        raise ValueError('Chỉ nhận ảnh JPG/PNG.')
    if path.stat().st_size > 10 * 1024 * 1024:
        raise ValueError('Ảnh vượt giới hạn 10 MB.')
    try:
        with Image.open(path) as image:
            if image.format not in {'JPEG', 'PNG'} or getattr(image, 'n_frames', 1) != 1:
                raise ValueError('Chỉ nhận JPG/PNG tĩnh.')
            if image.width * image.height > 20_000_000:
                raise ValueError('Ảnh vượt giới hạn 20 megapixel.')
            return ImageOps.exif_transpose(image).convert('RGB')
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError) as exc:
        raise ValueError(f'Không đọc được ảnh: {path}') from exc

def choose_image(title='Chọn ảnh MỘT DÒNG chữ viết tay để nhận dạng'):
    import tkinter as tk
    from tkinter import filedialog
    window = tk.Tk()
    window.withdraw()
    try:
        window.attributes('-topmost', True)
        selected = filedialog.askopenfilename(
            parent=window,
            title=title,
            initialdir=str(Path.home() / 'Pictures'),
            filetypes=[('Ảnh JPG / PNG', '*.jpg *.jpeg *.png')],
        )
        return Path(selected) if selected else None
    finally:
        window.update_idletasks()
        window.destroy()
