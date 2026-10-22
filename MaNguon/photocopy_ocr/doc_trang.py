"""Tách dòng theo khoảng trắng cho trang một cột; không phải model phân tích bố cục."""
from datetime import datetime
import json
from pathlib import Path
import time

import numpy as np
from PIL import Image, ImageDraw, ImageOps

from thu_ocr import LocalRecognizer, MODEL_ID, MODEL_REVISION, ROOT, read_image


def runs(active):
    edges = np.diff(np.r_[False, active, False].astype(np.int8))
    return list(zip(np.flatnonzero(edges == 1), np.flatnonzero(edges == -1)))


def line_boxes(image):
    """Trả bbox trên ảnh gốc, từ trên xuống; giới hạn ảnh phân tích rộng 1600px."""
    scale = min(1.0, 1600 / image.width)
    small = image.resize((max(1, round(image.width * scale)),
                          max(1, round(image.height * scale))), Image.Resampling.LANCZOS)
    gray = np.asarray(ImageOps.autocontrast(small.convert('L')))
    if np.ptp(gray) < 20:
        return []
    # Otsu: tách mực tối khỏi nền sáng mà không thêm thư viện xử lý ảnh.
    hist = np.bincount(gray.ravel(), minlength=256).astype(float)
    weights = hist.cumsum()
    sums = (hist * np.arange(256)).cumsum()
    denominator = weights * (weights[-1] - weights)
    variance = np.zeros(256)
    np.divide((sums[-1] * weights - sums * weights[-1]) ** 2,
              denominator, out=variance, where=denominator > 0)
    threshold = min(210, int(variance.argmax()))
    ink = gray <= threshold
    # Bỏ đường kẻ ngang mỏng chạy gần hết trang, chỉ trong mặt nạ tìm dòng.
    for top, bottom in runs(ink.sum(axis=1) > small.width * .75):
        if bottom - top <= max(3, small.height * .003):
            ink[top:bottom] = False
    active = ink.sum(axis=1) >= max(2, round(small.width * .002))
    # Nối khoảng hở nhỏ giữa dấu tiếng Việt và thân chữ.
    bands = runs(active)
    merged = []
    join_gap = max(3, round(small.height * .003))
    for top, bottom in bands:
        if merged and top - merged[-1][1] <= join_gap:
            merged[-1][1] = bottom
        else:
            merged.append([top, bottom])
    boxes = []
    for index, (top, bottom) in enumerate(merged):
        columns = np.flatnonzero(ink[top:bottom].any(axis=0))
        if bottom - top < 4 or len(columns) < 8:
            continue
        left, right = int(columns[0]), int(columns[-1]) + 1
        padding = max(3, round((bottom - top) * .15))
        # Không lấy lấn sang dòng liền kề khi thêm lề cho crop.
        upper = (merged[index - 1][1] + top) // 2 if index else 0
        lower = (bottom + merged[index + 1][0]) // 2 if index + 1 < len(merged) else small.height
        boxes.append([max(0, int((left - padding) / scale)),
                      max(0, int(max(upper, top - padding) / scale)),
                      min(image.width, int(np.ceil((right + padding) / scale))),
                      min(image.height, int(np.ceil(min(lower, bottom + padding) / scale)))])
    if len(boxes) > 150:
        raise ValueError('Phát hiện quá 150 vùng: ảnh nhiều nhiễu hoặc sai bố cục. Hãy chụp lại trang rõ hơn.')
    return boxes


def notebook_lines(image):
    """Lọc đường kẻ trên vùng chữ đã khoanh, tìm đỉnh mật độ mực từng dòng."""
    import cv2
    gray = np.array(image.convert('L'))
    ink = cv2.adaptiveThreshold(gray, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
                                cv2.THRESH_BINARY_INV, 41, 20)
    horizontal = cv2.morphologyEx(ink, cv2.MORPH_OPEN,
                                 np.ones((1, max(40, image.width // 11)), np.uint8))
    vertical = cv2.morphologyEx(ink, cv2.MORPH_OPEN,
                               np.ones((max(40, image.height // 10), 1), np.uint8))
    grid = cv2.dilate(horizontal | vertical, np.ones((3, 3), np.uint8))
    ink &= ~grid
    count, labels, stats, _ = cv2.connectedComponentsWithStats(ink)
    keep = np.zeros(count, dtype=np.uint8)
    keep[1:] = ((stats[1:, cv2.CC_STAT_AREA] >= 6) &
                (stats[1:, cv2.CC_STAT_HEIGHT] >= 2))
    ink = keep[labels] * 255
    letters = stats[1:]
    letters = letters[(letters[:, 4] > 12) & (letters[:, 3] > 8) &
                      (letters[:, 3] < 80) & (letters[:, 2] < 80)]
    if not len(letters):
        return Image.fromarray(255 - ink).convert('RGB'), []
    height = float(np.percentile(letters[:, 3], 75))
    density = np.count_nonzero(ink, axis=1).astype(float)
    smooth = cv2.GaussianBlur(density.reshape(-1, 1), (1, 0), sigmaX=0,
                              sigmaY=max(2, height * .18)).ravel()
    candidates = [i for i in range(1, len(smooth) - 1)
                  if smooth[i] >= smooth[i - 1] and smooth[i] > smooth[i + 1]
                  and smooth[i] > max(3, smooth.max() * .15)]
    peaks = []
    for index in sorted(candidates, key=lambda i: smooth[i], reverse=True):
        if all(abs(index - other) > max(20, height * 1.2) for other in peaks):
            peaks.append(index)
    peaks.sort()
    cuts = [0]
    for first, second in zip(peaks, peaks[1:]):
        margin = max(1, round((second - first) * .12))
        start, end = first + margin, second - margin
        cuts.append(start + int(np.argmin(smooth[start:end])))
    cuts.append(image.height)
    boxes = []
    for top, bottom in zip(cuts, cuts[1:]):
        ys, xs = np.where(ink[top:bottom] > 0)
        if not len(xs):
            continue
        boxes.append([max(0, int(xs.min()) - 4), max(top, top + int(ys.min()) - 4),
                      min(image.width, int(xs.max()) + 5),
                      min(bottom, top + int(ys.max()) + 5)])
    return Image.fromarray(255 - ink).convert('RGB'), boxes


def validate_lines(image, boxes):
    if not boxes:
        raise ValueError('Không tìm thấy dòng chữ. Hãy khoanh sát vùng chữ, chọn đúng loại giấy.')
    if len(boxes) > 150 or any(bottom - top > max(120, image.width * .45)
                               for left, top, right, bottom in boxes):
        raise ValueError('Tách dòng chưa đúng: có vùng quá lớn hoặc quá nhiều vùng. '
                         'Hãy khoanh sát phần chữ và chọn chế độ giấy ô ly nếu có đường kẻ. '
                         'Chưa chạy model để tránh sinh câu không liên quan.')


def read_page(input_path, device='auto', region=None, notebook=False):
    started = time.perf_counter()
    original = read_image(input_path)
    if region is not None:
        if (len(region) != 4 or not 0 <= region[0] < region[2] <= original.width
                or not 0 <= region[1] < region[3] <= original.height):
            raise ValueError('Vùng chọn nằm ngoài ảnh hoặc có kích thước không hợp lệ.')
        image = original.crop(region)
    else:
        image = original
    if notebook:
        processed, boxes = notebook_lines(image)
    else:
        processed, boxes = image, line_boxes(image)
    validate_lines(image, boxes)
    folder = ROOT / 'reports' / ('trang-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
    folder.mkdir(parents=True, exist_ok=False)
    crops = folder / 'dong'
    crops.mkdir()
    image.save(folder / 'vung_chu.png')
    processed.save(folder / 'anh_xu_ly.png')
    preview = image.copy()
    draw = ImageDraw.Draw(preview)
    print(f'Tìm thấy {len(boxes)} vùng dòng. Đang nạp model…', flush=True)
    reader = LocalRecognizer(device)
    print(f'Thiết bị: {reader.device}. Đang đọc từng dòng…', flush=True)
    results = []
    for index, box in enumerate(boxes, 1):
        crop_path = crops / f'{index:03}.png'
        processed.crop(box).save(crop_path)
        prediction = reader.predict(crop_path)
        results.append(dict(number=index, box=box, **prediction))
        draw.rectangle(box, outline='#c62828', width=max(2, image.width // 700))
        draw.text((box[0] + 2, box[1] + 2), str(index), fill='#c62828')
        print(f'[{index}/{len(boxes)}] {prediction["text"]}', flush=True)
    text = '\n'.join(row['text'] for row in results)
    text_file = folder / 'ket_qua.txt'
    text_file.write_text(text + '\n', encoding='utf-8-sig')
    preview.save(folder / 'cac_dong.png')
    result = dict(unit='page', method='notebook_projection_peaks' if notebook else 'single_column_whitespace_segmentation',
                  model=MODEL_ID, model_revision=MODEL_REVISION, device=reader.device,
                  source=str(Path(input_path).resolve()), input_size=list(original.size),
                  region=region, processed_size=list(image.size), box_coordinates='relative_to_region',
                  line_count=len(results), text=text, lines=results,
                  seconds=time.perf_counter() - started, load_seconds=reader.load_seconds,
                  requires_review=True, text_file=str(text_file),
                  preview_file=str(folder / 'cac_dong.png'),
                  limitations='Chỉ thử trang một cột, chụp thẳng. Chưa hỗ trợ bảng, nhiều cột, sửa phối cảnh. '
                              'Lọc ô ly có thể mất nét/dấu. Model vẫn có thể sinh câu sai, chưa có điểm tin cậy được hiệu chuẩn.')
    (folder / 'ket_qua.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    return result
