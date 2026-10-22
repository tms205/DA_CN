import sys
from pathlib import Path
import unittest
from unittest.mock import patch

import thu_ocr


class ChooseImageTests(unittest.TestCase):
    def test_picker_returns_unicode_path(self):
        self.assertTrue(hasattr(thu_ocr, 'choose_image'), 'Chưa có cửa sổ chọn ảnh')
        name = 'C:/ảnh thử/viết tay.png'
        # Chỉ thay thao tác của người dùng trong hộp thoại; root Tk vẫn tạo thật.
        with patch('tkinter.filedialog.askopenfilename', return_value=name) as dialog:
            self.assertEqual(thu_ocr.choose_image(), Path(name))
        self.assertEqual(dialog.call_args.kwargs['filetypes'][0][1], '*.jpg *.jpeg *.png')

    def test_cancel_exits_before_loading_model(self):
        self.assertTrue(hasattr(thu_ocr, 'choose_image'), 'Chưa có cửa sổ chọn ảnh')
        with patch.object(sys, 'argv', ['thu_ocr.py', 'predict']), \
                patch('tkinter.filedialog.askopenfilename', return_value=''), \
                patch.object(thu_ocr, 'LocalRecognizer', side_effect=AssertionError('Không nạp model khi hủy')):
            self.assertEqual(thu_ocr.main(), 0)

    def test_benchmark_requires_manifest_without_opening_picker(self):
        with patch.object(sys, 'argv', ['thu_ocr.py', 'benchmark']), \
                patch('tkinter.filedialog.askopenfilename', side_effect=AssertionError('Benchmark không mở picker')):
            with self.assertRaises(SystemExit) as result:
                thu_ocr.main()
        self.assertEqual(result.exception.code, 2)


if __name__ == '__main__':
    unittest.main()
