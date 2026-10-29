import tempfile
from pathlib import Path
import unittest
from PIL import Image
from ocr_api import prepare_image

class CropTests(unittest.TestCase):
    def test_crop_uses_original_pixels_before_resize(self):
        with tempfile.TemporaryDirectory() as d:
            path=Path(d)/'test.png'
            image=Image.new('RGB',(100,100),'white')
            for x in range(25,75):
                for y in range(20,80):image.putpixel((x,y),(255,0,0))
            image.save(path)
            crop=prepare_image(path,[.25,.2,.5,.6])
            self.assertEqual(crop.size,(50,60))
            self.assertEqual(crop.getpixel((0,0)),(255,0,0))
            self.assertEqual(crop.getpixel((49,59)),(255,0,0))
    def test_invalid_or_tiny_crop_is_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            path=Path(d)/'test.png';Image.new('RGB',(100,100)).save(path)
            for crop in [[-.1,0,1,1],[0,0,0,1],[0,0,.01,.01],[0,0,1], [0,0,float('nan'),1], [0,0,'1',1]]:
                with self.subTest(crop=crop),self.assertRaises(ValueError):prepare_image(path,crop)
    def test_crop_follows_exif_orientation(self):
        with tempfile.TemporaryDirectory() as d:
            path=Path(d)/'camera.jpg'
            image=Image.new('RGB',(80,100),'white')
            for x in range(80):
                for y in range(50):image.putpixel((x,y),(255,0,0))
            exif=Image.Exif();exif[274]=6
            image.save(path,exif=exif)
            crop=prepare_image(path,[.5,0,.5,1])
            self.assertEqual(crop.size,(50,80))
            self.assertGreater(crop.getpixel((25,40))[0],240)
            self.assertLess(crop.getpixel((25,40))[1],20)
    def test_no_crop_preserves_whole_image(self):
        with tempfile.TemporaryDirectory() as d:
            path=Path(d)/'test.png';Image.new('RGB',(100,80)).save(path)
            self.assertEqual(prepare_image(path,None).size,(100,80))
if __name__=='__main__':unittest.main()
