"""Chạy VietOCR local trên ảnh một dòng hoặc tách dòng để đọc một trang."""
import argparse
import csv
import hashlib
import json
from pathlib import Path, PureWindowsPath
import platform
import statistics
import sys
import time
import unicodedata

ROOT = Path(__file__).resolve().parent
MODEL_DIR = ROOT / 'models' / 'vietocr-handwritten'
MODEL_ID = 'DungHugging/vietocr-handwritten-finetune'
MODEL_REVISION = '0827b42e17cb0e1c1bbda5aac11f7cc5c816e0ca'
SOURCE_REVISION = 'fe8c3a7fc714aec57ab81cec844eb3adf0c1636c'
MODEL_HASH = 'd5357730c328e40dd1aeae7288994d8ad85979f8334c03c9bfad53c320114fa7'
CONFIG_HASH = '6de3d0e0be9546a120c6919f95c65a8cab9c4965f6bcb9b10c8a77cc46627fa4'


def normalize(text):
    return ' '.join(unicodedata.normalize('NFC', text).split())


def edit_distance(expected, actual):
    previous = list(range(len(actual) + 1))
    for i, left in enumerate(expected, 1):
        current = [i]
        for j, right in enumerate(actual, 1):
            current.append(min(current[-1] + 1, previous[j] + 1,
                               previous[j - 1] + (left != right)))
        previous = current
    return previous[-1]


def score(references, predictions):
    if not references or len(references) != len(predictions):
        raise ValueError('Cần cùng số lượng nhãn và dự đoán, ít nhất một dòng.')
    chars = words = char_errors = word_errors = exact = 0
    for expected, actual in zip(references, predictions):
        expected, actual = normalize(expected), normalize(actual)
        if not expected:
            raise ValueError('Nhãn đối chiếu không được rỗng.')
        chars += len(expected)
        words += len(expected.split())
        char_errors += edit_distance(expected, actual)
        word_errors += edit_distance(expected.split(), actual.split())
        exact += expected == actual
    return dict(cer=char_errors / chars, wer=word_errors / words,
                exact_line_rate=exact / len(references), lines=len(references),
                character_errors=char_errors, characters=chars,
                word_errors=word_errors, words=words)


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


def read_manifest(path):
    path = Path(path).resolve()
    with path.open(encoding='utf-8-sig', newline='') as stream:
        rows = list(csv.DictReader(stream))
    if not rows or len(rows) > 10_000:
        raise ValueError('Manifest phải có từ 1 đến 10.000 dòng.')
    for row in rows:
        name = row.get('image', '')
        image = (path.parent / name).resolve()
        if (not name or PureWindowsPath(name).drive or Path(name).is_absolute()
                or not image.is_relative_to(path.parent)):
            raise ValueError('Ảnh phải nằm trong thư mục chứa manifest.')
        if not normalize(row.get('text', '')):
            raise ValueError('Manifest thiếu nhãn text.')
        row['path'] = image
    return rows


class LocalRecognizer:
    def __init__(self, device='auto'):
        started = time.perf_counter()
        import torch
        import yaml
        # Nạp đúng mã nguồn upstream đi kèm, không pip tải thêm lúc nhận dạng.
        sys.path.insert(0, str(ROOT / 'third_party' / 'vietocr'))
        from vietocr.tool.translate import build_model, process_input, translate
        verify_hash(MODEL_DIR / 'transformerocr.pth', MODEL_HASH)
        verify_hash(MODEL_DIR / 'config.yml', CONFIG_HASH)
        config = yaml.safe_load((MODEL_DIR / 'config.yml').read_text(encoding='utf-8'))
        if device == 'auto':
            device = 'cuda' if torch.cuda.is_available() else 'cpu'
        if device == 'cuda' and not torch.cuda.is_available():
            raise RuntimeError('CUDA chưa hoạt động. Thử --device cpu hoặc kiểm tra bản PyTorch.')
        torch.set_num_threads(4)
        config['device'] = device
        # Checkpoint có đủ CNN: không cần tải trọng số ImageNet khi khởi tạo.
        config['cnn']['pretrained'] = False
        self.torch, self.config, self.device = torch, config, device
        self.process_input, self.translate = process_input, translate
        self.model, self.vocab = build_model(config)
        state = torch.load(MODEL_DIR / 'transformerocr.pth', map_location='cpu', weights_only=True)
        self.model.load_state_dict(state, strict=True)
        self.model.eval()
        self.load_seconds = time.perf_counter() - started

    def predict(self, image_path):
        started = time.perf_counter()
        image = read_image(image_path)
        if image.height > image.width:
            raise ValueError('Ảnh có dạng cả trang, không phù hợp model một dòng. '
                             'Hãy dùng chay_thu_ca_trang.cmd và khoanh vùng chữ.')
        dataset = self.config['dataset']
        tensor = self.process_input(image, dataset['image_height'],
                                    dataset['image_min_width'], dataset['image_max_width'])
        tensor = tensor.to(self.device)
        with self.torch.inference_mode():
            # Giữ thuật toán greedy upstream; tăng giới hạn 128 -> 256 cho dòng dài.
            tokens, _ = self.translate(tensor, self.model, max_seq_length=256)
        if self.device == 'cuda':
            self.torch.cuda.synchronize()
        ids = tokens[0].tolist()
        return dict(text=self.vocab.decode(ids), seconds=time.perf_counter() - started,
                    hit_length_limit=self.vocab.eos not in ids, requires_review=True,
                    unit='line', input_size=list(image.size))


def benchmark(recognizer, manifest):
    rows = read_manifest(manifest)
    # Warmup không được đưa vào thời gian trung bình.
    recognizer.predict(rows[0]['path'])
    if recognizer.device == 'cuda':
        recognizer.torch.cuda.reset_peak_memory_stats()
    results = []
    for index, row in enumerate(rows, 1):
        prediction = recognizer.predict(row['path'])
        result = dict(image=row['image'], reference=row['text'], **prediction)
        result['metrics'] = score([row['text']], [prediction['text']])
        results.append(result)
        print(f'{index}/{len(rows)} | {prediction["seconds"]:.3f}s | {prediction["text"]}', flush=True)
    times = [row['seconds'] for row in results]
    return dict(
        model=MODEL_ID, model_revision=MODEL_REVISION, model_sha256=MODEL_HASH,
        source_revision=SOURCE_REVISION, device=recognizer.device,
        gpu=recognizer.torch.cuda.get_device_name(0) if recognizer.device == 'cuda' else None,
        torch=recognizer.torch.__version__, python=platform.python_version(),
        load_seconds=recognizer.load_seconds, warmup_excluded=True,
        mean_seconds=statistics.mean(times), median_seconds=statistics.median(times),
        max_seconds=max(times), total_prediction_seconds=sum(times),
        peak_allocated_vram_mb=(recognizer.torch.cuda.max_memory_allocated() / 1024**2
                                if recognizer.device == 'cuda' else None),
        metrics=score([r['text'] for r in rows], [r['text'] for r in results]),
        limitations='Smoke test trên dòng VNOnDB raster, không phải ảnh chụp vở; chưa xác minh độc lập dữ liệu train của tác giả.',
        predictions=results)


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


def main():
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['predict', 'page', 'benchmark'])
    parser.add_argument('input', type=Path, nargs='?', help='Ảnh một dòng/cả trang; bỏ trống để chọn ảnh. Benchmark cần manifest CSV.')
    parser.add_argument('--device', choices=['auto', 'cpu', 'cuda'], default='auto')
    parser.add_argument('--output', type=Path, help='Lưu JSON mới, không ghi đè file cũ')
    parser.add_argument('--open-result', action='store_true', help='Mở file chữ sau khi đọc cả trang (Windows)')
    parser.add_argument('--select-region', action='store_true', help='Khoanh vùng chữ bằng chuột trước khi đọc trang')
    parser.add_argument('--region', nargs=4, type=int, metavar=('LEFT', 'TOP', 'RIGHT', 'BOTTOM'), help='Tọa độ vùng chữ theo ảnh gốc')
    parser.add_argument('--notebook', action='store_true', help='Lọc đường kẻ và tách dòng trên ảnh vở ô ly')
    args = parser.parse_args()
    if args.mode == 'benchmark' and args.input is None:
        parser.error('Benchmark cần đường dẫn manifest CSV.')
    if args.mode != 'page' and (args.select_region or args.region or args.notebook):
        parser.error('Tùy chọn khoanh vùng/ô ly chỉ dùng cho chế độ page.')
    if args.select_region and args.region:
        parser.error('Chỉ chọn một cách: --select-region hoặc --region.')
    try:
        if args.input is None:
            args.input = choose_image('Chọn ảnh CẢ TRANG chữ viết tay (một cột)' if args.mode == 'page'
                                      else 'Chọn ảnh MỘT DÒNG chữ viết tay để nhận dạng')
            if args.input is None:
                print('Đã hủy chọn ảnh.')
                return 0
        if args.output and args.output.exists():
            raise ValueError('File kết quả đã có. Hãy chọn tên khác.')
        if args.mode == 'page':
            from doc_trang import read_page
            region = args.region
            if args.select_region:
                from chon_vung import select_region
                region = select_region(read_image(args.input))
                if region is None:
                    print('Đã hủy khoanh vùng.')
                    return 0
            result = read_page(args.input, args.device, region=region, notebook=args.notebook)
        else:
            recognizer = LocalRecognizer(args.device)
            print(f'Model: {MODEL_ID}; thiết bị: {recognizer.device}; nhận dạng từng dòng.', flush=True)
        if args.mode == 'predict':
            result = recognizer.predict(args.input)
        elif args.mode == 'benchmark':
            result = benchmark(recognizer, args.input)
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            with args.output.open('x', encoding='utf-8') as stream:
                json.dump(result, stream, ensure_ascii=False, indent=2)
            print(f'Đã lưu: {args.output.resolve()}')
        summary = {key: value for key, value in result.items() if key not in {'predictions', 'lines'}}
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        print('Kết quả AI cần được đọc lại; không tự động xác nhận để in.')
        if args.mode == 'page' and args.open_result and sys.platform == 'win32':
            import os
            os.startfile(result['text_file'])
        return 0
    except (OSError, ValueError, RuntimeError, ImportError) as exc:
        print(f'Lỗi: {exc}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
