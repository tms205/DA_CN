import tkinter as tk
from tkinter import ttk
import unittest
from unittest.mock import patch

from PIL import Image

from chon_vung import select_region


class RegionPickerTests(unittest.TestCase):
    def test_mouse_drag_returns_original_image_coordinates(self):
        window = tk.Tk()

        def select():
            window.update_idletasks()
            canvas = next(w for w in window.winfo_children() if isinstance(w, tk.Canvas))
            canvas.event_generate('<ButtonPress-1>', x=124, y=96)
            canvas.event_generate('<B1-Motion>', x=12, y=24)
            canvas.event_generate('<ButtonRelease-1>', x=12, y=24)
            frame = next(w for w in window.winfo_children() if isinstance(w, ttk.Frame))
            next(b for b in frame.winfo_children() if b.cget('text') == 'Đọc vùng đã chọn').invoke()

        window.after(100, select)
        window.after(3000, window.destroy)
        with patch('chon_vung.tk.Tk', return_value=window):
            self.assertEqual(select_region(Image.new('RGB', (400, 200))), [12, 24, 124, 96])

    def test_cancel_returns_none(self):
        window = tk.Tk()
        window.after(100, window.destroy)
        with patch('chon_vung.tk.Tk', return_value=window):
            self.assertIsNone(select_region(Image.new('RGB', (400, 200))))


if __name__ == '__main__':
    unittest.main()
