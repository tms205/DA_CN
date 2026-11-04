"""Bộ đo local có manifest/nhãn, không gửi ảnh ra ngoài, không tạo đơn hoặc in."""
import argparse
import ctypes
import hashlib
import json
import platform
import subprocess
import threading
import time
import unicodedata
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from ocr_api import prepare_image
from thu_qwen import LocalQwen, MODELS


def distance(a, b):
    previous = list(range(len(b) + 1))
    for i, x in enumerate(a, 1):
        current = [i]
        for j, y in enumerate(b, 1):
            current.append(min(current[-1] + 1, previous[j] + 1, previous[j - 1] + (x != y)))
        previous = current
    return previous[-1]


def normalized(text):
    return ' '.join(unicodedata.normalize('NFC', text).casefold().split())


def metrics(reference, predicted):
    ref, out = normalized(reference), normalized(predicted)
    if not ref:
        raise ValueError('Nhãn tham chiếu không được rỗng.')
    return dict(cer=distance(ref, out) / len(ref),
                wer=distance(ref.split(), out.split()) / len(ref.split()),
                normalization='Unicode NFC, casefold, collapse whitespace; giữ dấu và dấu câu')


def prepare(folder, handwritten):
    folder.mkdir(parents=True, exist_ok=True)
    text = 'Cửa hàng PhotocopyChain\nKhách hàng: Nguyễn Văn An\nSố trang: 12. Số bản: 2.\nNhận tài liệu tại quầy.'
    font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 44)
    manifest = []
    for grid in (False, True):
        image = Image.new('RGB', (1280, 800), 'white')
        draw = ImageDraw.Draw(image)
        if grid:
            for x in range(0, image.width, 35):
                draw.line((x, 0, x, image.height), fill=(194, 210, 225))
            for y in range(0, image.height, 35):
                draw.line((0, y, image.width, y), fill=(194, 210, 225))
        draw.multiline_text((80, 150), text, font=font, spacing=30, fill=(25, 40, 65))
        path = folder / ('typed_grid.png' if grid else 'typed_white.png')
        image.save(path)
        manifest.append(dict(id=path.stem, image=str(path), reference=text, label_status='known_generated_text',
                             category='chữ in tổng hợp, không đại diện chữ viết tay', crop=None))
    if handwritten and handwritten.is_file():
        # Bản chép từ ảnh người dùng đã gửi; không coi là nhãn chuẩn khi nhóm chưa xác nhận.
        reference = 'Nếu mây kia mang người yêu anh\nđi về nơi nào xa vời, trả lại anh về\nvới chính mình, phải mất bao lâu anh\nquên nỗi nhớ.'
        for name, crop in [('handwritten_full', None), ('handwritten_crop', [0.20, 0.35, 0.65, 0.34])]:
            manifest.append(dict(id=name, image=str(handwritten), reference=reference,
                                 label_status='manual_draft_requires_team_review', category='ảnh viết tay người dùng cung cấp', crop=crop))
    for item in manifest:
        item['sha256'] = hashlib.sha256(Path(item['image']).read_bytes()).hexdigest()
    (folder / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    return manifest


class Resources:
    def __init__(self, process):
        self.process, self.stop = process, threading.Event()
        self.vram_mb = self.ram_mb = 0
        self.thread = threading.Thread(target=self.sample, daemon=True)

    def sample(self):
        class Counters(ctypes.Structure):
            _fields_ = [('cb', ctypes.c_ulong), ('PageFaultCount', ctypes.c_ulong)] + [
                (name, ctypes.c_size_t) for name in ('PeakWorkingSetSize', 'WorkingSetSize', 'QuotaPeakPagedPoolUsage',
                                                   'QuotaPagedPoolUsage', 'QuotaPeakNonPagedPoolUsage',
                                                   'QuotaNonPagedPoolUsage', 'PagefileUsage', 'PeakPagefileUsage')]
        while not self.stop.is_set():
            try:
                result = subprocess.run(['nvidia-smi', '--query-gpu=memory.used', '--format=csv,noheader,nounits'],
                                        capture_output=True, text=True, timeout=5,
                                        creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
                if result.returncode == 0:
                    self.vram_mb = max(self.vram_mb, int(result.stdout.strip().splitlines()[0]))
                counters = Counters()
                counters.cb = ctypes.sizeof(counters)
                if ctypes.windll.psapi.GetProcessMemoryInfo(ctypes.c_void_p(self.process._handle), ctypes.byref(counters), counters.cb):
                    self.ram_mb = max(self.ram_mb, counters.PeakWorkingSetSize / 1048576)
            except (OSError, ValueError, subprocess.TimeoutExpired, AttributeError):
                pass
            self.stop.wait(1)

    def __enter__(self):
        self.thread.start()
        return self

    def __exit__(self, *_):
        self.stop.set()
        self.thread.join(timeout=7)


def run(folder, manifest):
    results = []
    environment = dict(platform=platform.platform(), python=platform.python_version(),
                       note='Một máy, một tiến trình model tại một thời điểm. VRAM là tổng GPU, RAM là peak working set llama-server.')
    for model in ('2b', '4b'):
        model_folder = folder / model
        model_folder.mkdir(exist_ok=True)
        try:
            with LocalQwen(model_folder, model=model) as reader, Resources(reader.process) as monitor:
                for item in manifest:
                    if hashlib.sha256(Path(item['image']).read_bytes()).hexdigest() != item['sha256']:
                        raise ValueError('Ảnh benchmark thay đổi so với manifest.')
                    started = time.perf_counter()
                    try:
                        output = reader.predict(prepare_image(item['image'], item['crop']))
                        results.append(dict(model=model, sample=item['id'], label_status=item['label_status'],
                                            reference=item['reference'], predicted=output['text'],
                                            load_seconds=reader.load_seconds, inference_seconds=output['seconds'],
                                            total_sample_seconds=time.perf_counter() - started,
                                            truncated=output['finish_reason'] == 'length', usage=output['usage'],
                                            **metrics(item['reference'], output['text'])))
                    except Exception as exc:
                        results.append(dict(model=model, sample=item['id'], error=str(exc), label_status=item['label_status']))
                    (folder / 'results.json').write_text(json.dumps(dict(environment=environment, results=results), ensure_ascii=False, indent=2), encoding='utf-8')
                    print(f"{model} / {item['id']}: {results[-1].get('inference_seconds', 'error')}", flush=True)
                for row in results:
                    if row['model'] == model:
                        row.update(gpu_total_peak_mb=monitor.vram_mb, llama_peak_working_set_mb=monitor.ram_mb,
                                   model_revision=MODELS[model]['revision'])
        except Exception as exc:
            results.append(dict(model=model, error=str(exc)))
            print(f'{model}: {exc}', flush=True)
        (folder / 'results.json').write_text(json.dumps(dict(environment=environment, results=results), ensure_ascii=False, indent=2), encoding='utf-8')
    lines = ['# Thực nghiệm Qwen local', '',
             'Bộ thử nhỏ phục vụ kiểm tra tích hợp, không suy rộng thành độ chính xác OCR tiếng Việt nói chung.',
             'Ảnh viết tay có nhãn dự thảo; nhóm phải xác nhận trước khi dùng điểm số trong báo cáo chính thức.', '',
             '| Model | Ảnh | Nhãn | CER | WER | Nạp (s) | Xử lý (s) | Cắt kết quả |', '|---|---|---|---|---|---|---|---|']
    for row in results:
        if 'error' in row:
            lines.append(f"| {row['model']} | {row.get('sample', 'nạp model')} | — | — | — | — | {row['error'].replace('|', '/')} | — |")
        else:
            lines.append(f"| {row['model']} | {row['sample']} | {row['label_status']} | {row['cer']:.3f} | {row['wer']:.3f} | {row['load_seconds']:.2f} | {row['inference_seconds']:.2f} | {row['truncated']} |")
    lines += ['', 'CER/WER có thể lớn hơn 1 khi model sinh thêm nhiều ký tự/từ. Không trừ các ca lỗi khỏi báo cáo.',
              'Nạp model đo riêng; mỗi model được giữ để chạy các ảnh liên tiếp. API hiện nạp lại cho từng tác vụ nên độ trễ thực tế còn bao gồm chi phí nạp.',
              'Xem results.json để đối chiếu nguyên văn dự đoán, nhãn, revision và số liệu bộ nhớ.']
    (folder / 'BAO_CAO.md').write_text('\n'.join(lines), encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--handwritten', type=Path)
    parser.add_argument('--prepare-only', action='store_true')
    args = parser.parse_args()
    prepared = prepare(args.output, args.handwritten)
    if not args.prepare_only:
        run(args.output, prepared)
