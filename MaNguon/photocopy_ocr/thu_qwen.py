"""Thử OCR ảnh trực tiếp bằng Qwen3-VL qua llama.cpp local, không gửi ảnh ra Internet."""
import argparse
import base64
from datetime import datetime
import io
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request

from thu_ocr import ROOT, choose_image, read_image, verify_hash

MODEL_DIR = ROOT / 'models' / 'qwen3-vl-2b'
RUNTIME_DIR = ROOT / 'third_party' / 'llama-b6907' / 'bin'
MODEL_FILE = MODEL_DIR / 'Qwen3VL-2B-Instruct-Q4_K_M.gguf'
PROJECTOR_FILE = MODEL_DIR / 'mmproj-Qwen3VL-2B-Instruct-Q8_0.gguf'
MODEL_SHA = '089d75c52f4b7ffc56ba998ffc50aae89fcafc755f9e7208aacca281dca6c2ae'
PROJECTOR_SHA = 'f9a68fabba69c3b81e153367b2c7521030b0fa8bb0de400c9599c8e6725f9c82'
MODEL_REVISION = '52d6c8ffea26cc873ac5ad116f8631268d7eb503'
MODELS = {
    '2b': dict(repo='Qwen/Qwen3-VL-2B-Instruct-GGUF', revision=MODEL_REVISION,
               directory=MODEL_DIR, model=MODEL_FILE, projector=PROJECTOR_FILE,
               model_sha=MODEL_SHA, projector_sha=PROJECTOR_SHA, gpu_layers=99),
    '4b': dict(repo='Qwen/Qwen3-VL-4B-Instruct-GGUF', revision='1cd86afb9a95c410a6038ab3b40d8b578c892266',
               directory=ROOT / 'models' / 'qwen3-vl-4b',
               model=ROOT / 'models' / 'qwen3-vl-4b' / 'Qwen3VL-4B-Instruct-Q4_K_M.gguf',
               projector=ROOT / 'models' / 'qwen3-vl-4b' / 'mmproj-Qwen3VL-4B-Instruct-Q8_0.gguf',
               model_sha='66358cb18bb6b3b1b6675aa412c7a88ef01d228f481184d13668e5201c730a0a',
               projector_sha='30ba2c7dd3127a4561b6cba9d13d0f711c91bdb38742e2f56d73c8cb596bd06d',
               gpu_layers=24),
}
PROMPT = ('Transcribe the Vietnamese handwritten text in the image. '
          'Keep the original language, spelling, accents and line breaks. '
          'Ignore notebook grid lines, logos and background objects. '
          'Do not complete sentences or add words that are not visible. '
          'Write [không đọc rõ] for illegible words. Output only the transcription.')


def image_content(image):
    stream = io.BytesIO()
    image.save(stream, format='PNG')
    return 'data:image/png;base64,' + base64.b64encode(stream.getvalue()).decode('ascii')


class LocalQwen:
    def __init__(self, folder, cpu=False, model='2b'):
        self.folder, self.cpu = folder, cpu
        self.config = MODELS[model]
        self.process = self.log = None
        # Bỏ qua proxy hệ thống: các request này chỉ đi tới server do tiến trình này tạo.
        self.http = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def __enter__(self):
        started = time.perf_counter()
        config = self.config
        for path, sha in ((config['model'], config['model_sha']),
                          (config['projector'], config['projector_sha'])):
            if not path.is_file():
                raise FileNotFoundError(f'Thiếu {path.name}. Xem HUONG_DAN_QWEN.md.')
            verify_hash(path, sha)
        server = RUNTIME_DIR / 'llama-server.exe'
        if not server.is_file():
            raise FileNotFoundError('Thiếu llama-server.exe. Xem HUONG_DAN_QWEN.md.')
        with socket.socket() as probe:
            probe.bind(('127.0.0.1', 0))
            self.port = probe.getsockname()[1]
        self.url = f'http://127.0.0.1:{self.port}'
        command = [str(server), '-m', str(config['model']), '--mmproj', str(config['projector']),
                   '--host', '127.0.0.1', '--port', str(self.port), '--ctx-size', '4096',
                   '--n-gpu-layers', '0' if self.cpu else str(config['gpu_layers']), '--no-mmproj-offload',
                   '--threads', '8', '--batch-size', '256', '--ubatch-size', '128',
                   '--parallel', '1', '--jinja']
        environment = os.environ.copy()
        torch_lib = ROOT / '.venv' / 'Lib' / 'site-packages' / 'torch' / 'lib'
        environment['PATH'] = str(torch_lib) + os.pathsep + environment.get('PATH', '')
        self.log = (self.folder / 'llama.log').open('wb')
        try:
            self.process = subprocess.Popen(command, cwd=RUNTIME_DIR, env=environment,
                                            stdout=self.log, stderr=subprocess.STDOUT,
                                            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            deadline = time.monotonic() + 180
            while time.monotonic() < deadline:
                if self.process.poll() is not None:
                    raise RuntimeError(f'Không nạp được Qwen, xem {self.folder / "llama.log"}')
                try:
                    with self.http.open(self.url + '/health', timeout=1) as response:
                        if response.status == 200:
                            if not self.cpu:
                                log_text = (self.folder / 'llama.log').read_text(encoding='utf-8', errors='replace')
                                if not re.search(r'offloaded [1-9]\d*/\d+ layers to GPU', log_text):
                                    raise RuntimeError('Runtime chưa nạp model lên GPU. Xem llama.log hoặc thử --cpu.')
                            self.load_seconds = time.perf_counter() - started
                            return self
                except (OSError, urllib.error.URLError):
                    time.sleep(.3)
            raise TimeoutError('Nạp Qwen quá 180 giây. Xem llama.log.')
        except BaseException:
            self.close()
            raise

    def predict(self, image):
        body = dict(messages=[dict(role='user', content=[
            dict(type='image_url', image_url=dict(url=image_content(image))),
            dict(type='text', text=PROMPT)])],
            max_tokens=2048, temperature=0, seed=42, stream=False)
        request = urllib.request.Request(self.url + '/v1/chat/completions',
                                         data=json.dumps(body).encode('utf-8'),
                                         headers={'Content-Type': 'application/json'})
        started = time.perf_counter()
        try:
            with self.http.open(request, timeout=600) as response:
                result = json.load(response)
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode('utf-8', errors='replace')
            raise RuntimeError(f'Qwen trả lỗi HTTP {exc.code}: {detail[:1000]}') from exc
        choice = result['choices'][0]
        return dict(text=choice['message']['content'], finish_reason=choice.get('finish_reason'),
                    seconds=time.perf_counter() - started, usage=result.get('usage'),
                    timings=result.get('timings'), requires_review=True)

    def close(self):
        if self.process is not None and self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=10)
        if self.log is not None:
            self.log.close()

    def __exit__(self, *_):
        self.close()


def main():
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('images', type=Path, nargs='*')
    parser.add_argument('--cpu', action='store_true', help='Chạy CPU; mặc định nạp phần ngôn ngữ lên GPU')
    parser.add_argument('--model', choices=list(MODELS), default='2b')
    parser.add_argument('--select-region', action='store_true', help='Khoanh vùng bằng chuột')
    parser.add_argument('--open-result', action='store_true')
    parser.add_argument('--max-edge', type=int, default=1600)
    args = parser.parse_args()
    if not 512 <= args.max_edge <= 2000:
        parser.error('--max-edge phải từ 512 đến 2000.')
    try:
        paths = args.images
        if not paths:
            selected = choose_image('Chọn ảnh trang chữ để thử Qwen3-VL')
            if selected is None:
                print('Đã hủy chọn ảnh.')
                return 0
            paths = [selected]
        prepared = []
        for path in paths:
            image = read_image(path)
            original_size, region = image.size, None
            if args.select_region:
                from chon_vung import select_region
                region = select_region(image)
                if region is None:
                    print('Đã hủy khoanh vùng.')
                    return 0
                image = image.crop(region)
            image.thumbnail((args.max_edge, args.max_edge))
            prepared.append((path, image, original_size, region))
        folder = ROOT / 'reports' / ('qwen-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
        folder.mkdir(parents=True, exist_ok=False)
        print('Đang nạp Qwen local; lần đầu có thể cần chờ. Không tải/upload ảnh khi nhận dạng.', flush=True)
        results = []
        with LocalQwen(folder, args.cpu, args.model) as reader:
            for index, (path, image, original_size, region) in enumerate(prepared, 1):
                print(f'[{index}/{len(prepared)}] Đang đọc {path.name}…', flush=True)
                image.save(folder / f'anh-{index:02}.png')
                result = reader.predict(image)
                target = folder / f'ket_qua-{index:02}.txt'
                target.write_text(result['text'] + '\n', encoding='utf-8-sig')
                results.append(dict(source=str(path.resolve()), input_size=original_size,
                                    resized_size=image.size, region=region,
                                    text_file=str(target), **result))
                print(result['text'], flush=True)
                print(f'Thời gian nhận dạng: {result["seconds"]:.2f}s; file: {target}', flush=True)
                if result['finish_reason'] == 'length':
                    print('CẢNH BÁO: Đã hết giới hạn token, kết quả có thể chưa đầy đủ.', flush=True)
            report = dict(model=reader.config['repo'], revision=reader.config['revision'],
                          quantization='Q4_K_M + mmproj Q8_0', runtime='llama.cpp b6907',
                          cpu_only=args.cpu, requested_gpu_layers=0 if args.cpu else reader.config['gpu_layers'],
                          vision_on_cpu=True, prompt=PROMPT,
                          load_seconds=reader.load_seconds, predictions=results,
                          requires_review=True)
        (folder / 'ket_qua.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
        print(f'Đã lưu kết quả: {folder}\nCần đọc lại để đối chiếu, model vẫn có thể đoán sai.', flush=True)
        if args.open_result and sys.platform == 'win32':
            for result in results:
                os.startfile(result['text_file'])
        return 0
    except (OSError, ValueError, RuntimeError, ImportError, KeyError, IndexError) as exc:
        print(f'Lỗi: {exc}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
