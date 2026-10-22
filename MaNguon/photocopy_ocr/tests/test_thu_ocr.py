import csv
import importlib.util
from pathlib import Path
import tempfile
import unittest


class LocalOcrTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.available = importlib.util.find_spec('thu_ocr') is not None

    def api(self):
        self.assertTrue(self.available, 'Chưa có bộ chạy thử thu_ocr')
        import thu_ocr
        return thu_ocr

    def test_exact_match_keeps_vietnamese(self):
        result = self.api().score(['Tiếng Việt'], ['Tiếng Việt'])
        self.assertEqual(result['cer'], 0)
        self.assertEqual(result['exact_line_rate'], 1)

    def test_metrics_are_corpus_weighted_and_keep_case(self):
        result = self.api().score(['a', 'bbbb'], ['A', 'bbbc'])
        self.assertEqual(result['cer'], 2 / 5)
        self.assertEqual(result['wer'], 1)

    def test_cer_can_exceed_one(self):
        self.assertEqual(self.api().score(['a'], ['aaaa'])['cer'], 3)

    def test_unicode_and_spaces_normalized(self):
        self.assertEqual(self.api().score(['ế  a'], ['e\u0302\u0301 a'])['cer'], 0)

    def test_reject_invalid_labels(self):
        api = self.api()
        for refs, preds in [([], []), ([''], ['a']), (['a'], [])]:
            with self.assertRaises(ValueError):
                api.score(refs, preds)

    def test_hash_verification(self):
        api = self.api()
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'model'
            path.write_bytes(b'abc')
            api.verify_hash(path, 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad')
            with self.assertRaises(ValueError):
                api.verify_hash(path, '0' * 64)

    def test_image_rejects_not_an_image(self):
        api = self.api()
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'bad.png'
            path.write_bytes(b'not a png')
            with self.assertRaises(ValueError):
                api.read_image(path)

    def test_image_accepts_rgb_and_rejects_gif(self):
        api = self.api()
        from PIL import Image
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'valid.png'
            Image.new('L', (80, 32), 255).save(path)
            self.assertEqual(api.read_image(path).mode, 'RGB')
            path = Path(tmp) / 'invalid.gif'
            Image.new('RGB', (80, 32)).save(path)
            with self.assertRaises(ValueError):
                api.read_image(path)

    def test_manifest_blocks_escape(self):
        api = self.api()
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'labels.csv'
            for image in ['../secret.png', 'C:/Windows/secret.png', '/secret.png']:
                with path.open('w', encoding='utf-8', newline='') as stream:
                    writer = csv.DictWriter(stream, fieldnames=['image', 'text'])
                    writer.writeheader()
                    writer.writerow({'image': image, 'text': 'xin chào'})
                with self.assertRaises(ValueError):
                    api.read_manifest(path)

    def test_manifest_requires_text(self):
        api = self.api()
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'labels.csv'
            path.write_text('image,text\na.png,\n', encoding='utf-8')
            with self.assertRaises(ValueError):
                api.read_manifest(path)


if __name__ == '__main__':
    unittest.main()
