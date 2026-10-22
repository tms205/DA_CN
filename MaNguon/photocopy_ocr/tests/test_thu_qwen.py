import base64
import io
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image

import thu_qwen


class QwenRunnerTests(unittest.TestCase):
    def test_image_transport_is_lossless_png(self):
        image = Image.new('RGB', (137, 84), '#235679')
        payload = thu_qwen.image_content(image)
        self.assertTrue(payload.startswith('data:image/png;base64,'))
        decoded = Image.open(io.BytesIO(base64.b64decode(payload.split(',', 1)[1])))
        self.assertEqual(decoded.size, image.size)
        self.assertEqual(decoded.getpixel((0, 0)), image.getpixel((0, 0)))

    def test_cancel_does_not_start_model(self):
        with patch.object(sys, 'argv', ['thu_qwen.py']), \
                patch('thu_qwen.choose_image', return_value=None), \
                patch('thu_qwen.LocalQwen', side_effect=AssertionError('Không được nạp')):
            self.assertEqual(thu_qwen.main(), 0)

    def test_cancel_region_does_not_start_model(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'page.png'
            Image.new('RGB', (640, 480), 'white').save(path)
            with patch.object(sys, 'argv', ['thu_qwen.py', str(path), '--select-region']), \
                    patch('chon_vung.select_region', return_value=None), \
                    patch('thu_qwen.LocalQwen', side_effect=AssertionError('Không được nạp')):
                self.assertEqual(thu_qwen.main(), 0)

    def test_invalid_image_rejected_before_starting_model(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'bad.png'
            path.write_bytes(b'not an image')
            with patch.object(sys, 'argv', ['thu_qwen.py', str(path)]), \
                    patch('thu_qwen.LocalQwen', side_effect=AssertionError('Không được nạp')):
                self.assertEqual(thu_qwen.main(), 1)

    def test_failed_start_closes_owned_resources(self):
        with tempfile.TemporaryDirectory() as tmp:
            runner = thu_qwen.LocalQwen(Path(tmp))
            runner.config = dict(runner.config, model=Path(tmp) / 'model', projector=Path(tmp) / 'projector')
            runner.config['model'].touch()
            runner.config['projector'].touch()
            with patch('thu_qwen.verify_hash'), patch.object(Path, 'is_file', return_value=True), \
                    patch('thu_qwen.subprocess.Popen', side_effect=OSError('Test start failure')):
                with self.assertRaisesRegex(OSError, 'Test start failure'):
                    runner.__enter__()
            self.assertTrue(runner.log.closed)
            self.assertIsNone(runner.process)


if __name__ == '__main__':
    unittest.main()
