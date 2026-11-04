import tempfile
import unittest
from pathlib import Path
from benchmark_qwen import metrics, prepare

class BenchmarkTests(unittest.TestCase):
    def test_normalization_and_known_edit_distances(self):
        self.assertEqual(metrics('Chào\nBẠN', 'chào bạn')['cer'], 0)
        self.assertEqual(metrics('abc', 'adc')['cer'], 1/3)
        self.assertEqual(metrics('a b', 'a c')['wer'], .5)
        self.assertGreater(metrics('a', 'abc')['cer'], 1)
        with self.assertRaises(ValueError): metrics(' ', 'text')
    def test_prepared_set_has_explicit_labels_and_source_hash(self):
        with tempfile.TemporaryDirectory() as temp:
            items=prepare(Path(temp),None)
            self.assertEqual(len(items),2)
            self.assertTrue(all(x['label_status']=='known_generated_text' for x in items))
            self.assertTrue(all(len(x['sha256'])==64 for x in items))
            self.assertTrue(all(Path(x['image']).is_file() for x in items))
            self.assertEqual(items[0]['reference'],items[1]['reference'])
if __name__=='__main__':unittest.main()
