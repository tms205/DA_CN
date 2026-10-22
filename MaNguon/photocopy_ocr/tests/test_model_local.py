"""Kiểm thử model thật, chỉ chạy khi OCR_TEST_MODEL=1 (không tải mạng)."""
import os
import socket
import unittest
from unittest.mock import patch

from thu_ocr import LocalRecognizer, ROOT, read_manifest


@unittest.skipUnless(os.environ.get('OCR_TEST_MODEL') == '1', 'Bật OCR_TEST_MODEL=1 để thử weights thật')
class ModelLocalTests(unittest.TestCase):
    def test_actual_model_runs_without_network(self):
        # Chặn mạng ở ranh giới socket; không mock model/tiền xử lý/decoding.
        with patch.object(socket.socket, 'connect', side_effect=AssertionError('Không được gọi mạng')):
            reader = LocalRecognizer('auto')
            sample = read_manifest(ROOT / 'samples' / 'labels.csv')[0]
            result = reader.predict(sample['path'])
        self.assertFalse(reader.config['cnn']['pretrained'])
        self.assertTrue(result['text'].strip())
        self.assertEqual(result['unit'], 'line')
        self.assertTrue(result['requires_review'])
        self.assertGreater(result['seconds'], 0)


if __name__ == '__main__':
    unittest.main()
