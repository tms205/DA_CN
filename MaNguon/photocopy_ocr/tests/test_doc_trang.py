import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image, ImageDraw

from doc_trang import line_boxes, notebook_lines, read_page, validate_lines
from thu_ocr import ROOT, main, read_image, read_manifest, score


class PageSegmentationTests(unittest.TestCase):
    def test_rejects_entire_page_as_one_line(self):
        with self.assertRaisesRegex(ValueError, 'Tách dòng chưa đúng'):
            validate_lines(Image.new('RGB', (863, 1920)), [[0, 0, 863, 1920]])

    def test_crop_bounds_rejected_before_loading_model(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'page.png'
            Image.new('RGB', (800, 1000), 'white').save(path)
            with self.assertRaisesRegex(ValueError, 'Vùng chọn'):
                read_page(path, region=[0, 0, 900, 1000])

    def test_grid_does_not_join_all_text_lines(self):
        image = Image.new('RGB', (730, 525), '#eeeeee')
        draw = ImageDraw.Draw(image)
        for x in range(0, 730, 44):
            draw.line((x, 0, x, 524), fill='#8195ae', width=1)
        for y in range(0, 525, 44):
            draw.line((0, y, 729, y), fill='#8195ae', width=1)
        for y in (40, 110, 180, 250, 320):
            for x in range(30, 650, 30):
                draw.line((x, y, x + 5, y - 22, x + 15, y, x + 20, y - 5),
                          fill='#25324a', width=3)
        processed, boxes = notebook_lines(image)
        self.assertEqual(processed.size, image.size)
        self.assertEqual(len(boxes), 5)
        validate_lines(image, boxes)

    def test_region_picker_cancel_does_not_load_model(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'page.png'
            Image.new('RGB', (800, 1000), 'white').save(path)
            with patch.object(sys, 'argv', ['thu_ocr.py', 'page', str(path), '--select-region']), \
                    patch('chon_vung.select_region', return_value=None), \
                    patch('doc_trang.LocalRecognizer', side_effect=AssertionError('Không được nạp')):
                self.assertEqual(main(), 0)

    def test_blank_page_has_no_lines(self):
        self.assertEqual(line_boxes(Image.new('RGB', (800, 1200), 'white')), [])

    def test_three_lines_in_order_with_diacritics_and_ruling(self):
        image = Image.new('RGB', (800, 1000), 'white')
        draw = ImageDraw.Draw(image)
        for y in (100, 240, 380):
            for x in range(60, 600, 30):
                draw.rectangle((x, y, x + 18, y + 24), fill='black')
                draw.rectangle((x + 5, y - 5, x + 8, y - 3), fill='black')
        draw.line((0, 200, 799, 200), fill='black', width=2)
        boxes = line_boxes(image)
        self.assertEqual(len(boxes), 3)
        for box, y in zip(boxes, (100, 240, 380)):
            self.assertLessEqual(box[1], y - 5)
            self.assertGreater(box[3], y + 24)

    def test_page_picker_cancel_does_not_load_model(self):
        with patch.object(sys, 'argv', ['thu_ocr.py', 'page']), \
                patch('thu_ocr.choose_image', return_value=None), \
                patch('doc_trang.LocalRecognizer', side_effect=AssertionError('Không được nạp')):
            self.assertEqual(main(), 0)

    def test_blank_page_rejected_before_model_load(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'blank.png'
            Image.new('RGB', (800, 1000), 'white').save(path)
            with patch('doc_trang.LocalRecognizer', side_effect=AssertionError('Không được nạp')):
                with self.assertRaisesRegex(ValueError, 'Không tìm thấy'):
                    read_page(path)

    @unittest.skipUnless(os.environ.get('OCR_TEST_PAGE') == '1', 'Bật OCR_TEST_PAGE=1 để chạy model thật')
    def test_real_model_on_page_composed_from_eight_sample_lines(self):
        rows = read_manifest(ROOT / 'samples' / 'labels.csv')[:8]
        lines = []
        for row in rows:
            line = read_image(row['path'])
            line.thumbnail((1500, 65))
            lines.append(line)
        page = Image.new('RGB', (1700, 1200), 'white')
        for index, line in enumerate(lines):
            page.paste(line, (80, 70 + index * 130))
        sample = ROOT / 'reports' / 'trang-mau-8-dong.png'
        # Đây là ảnh ghép kiểm tra pipeline, không phải ảnh chụp trang vở thật.
        page.save(sample)
        result = read_page(sample)
        self.assertEqual(result['line_count'], 8)
        self.assertTrue(all(row['text'].strip() for row in result['lines']))
        self.assertTrue(Path(result['text_file']).is_file())
        self.assertTrue(Path(result['preview_file']).is_file())
        print('PAGE_SMOKE_RESULT:', result['text_file'])
        print('SYNTHETIC_PAGE_METRICS:', score([r['text'] for r in rows], [r['text'] for r in result['lines']]))


if __name__ == '__main__':
    unittest.main()
