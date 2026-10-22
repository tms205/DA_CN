"""Khoanh vùng chữ trên ảnh; tọa độ trả về luôn theo ảnh gốc sau xoay EXIF."""
import tkinter as tk
from tkinter import ttk

from PIL import Image, ImageTk


def select_region(image):
    window = tk.Tk()
    window.title('Khoanh vùng chữ cần đọc')
    window.attributes('-topmost', True)
    ratio = min(1, (window.winfo_screenwidth() - 120) / image.width,
                (window.winfo_screenheight() - 220) / image.height)
    size = (max(1, round(image.width * ratio)), max(1, round(image.height * ratio)))
    preview = ImageTk.PhotoImage(image.resize(size, Image.Resampling.LANCZOS), master=window)
    ttk.Label(window, text='Kéo chuột khoanh sát toàn bộ phần chữ. Bỏ bàn, tay, bìa và vùng giấy trống.',
              padding=10, wraplength=max(300, size[0])).pack()
    canvas = tk.Canvas(window, width=size[0], height=size[1], highlightthickness=0)
    canvas.pack(padx=10)
    canvas.create_image(0, 0, anchor='nw', image=preview)
    note = ttk.Label(window, text='Chưa chọn vùng chữ.', padding=8)
    note.pack()
    state = dict(start=None, box=None, rectangle=None, result=None)

    def point(event):
        return (max(0, min(size[0], event.x)), max(0, min(size[1], event.y)))

    def begin(event):
        if state['rectangle'] is not None:
            canvas.delete(state['rectangle'])
        state['start'] = point(event)
        state['box'] = None
        accept.configure(state='disabled')
        x, y = state['start']
        state['rectangle'] = canvas.create_rectangle(x, y, x, y, outline='#dc2626', width=2)

    def move(event):
        if state['start'] is None:
            return
        x, y = state['start']
        x2, y2 = point(event)
        box = (min(x, x2), min(y, y2), max(x, x2), max(y, y2))
        canvas.coords(state['rectangle'], *box)
        state['box'] = box

    def end(event):
        move(event)
        if state['box'] is None:
            return
        x, y, x2, y2 = state['box']
        valid = (x2 - x) / (size[0] / image.width) >= 40 and (y2 - y) / (size[1] / image.height) >= 20
        accept.configure(state='normal' if valid else 'disabled')
        note.configure(text='Đã khoanh vùng. Bấm Đọc vùng đã chọn.' if valid else 'Vùng quá nhỏ, hãy kéo chọn lại.')

    def confirm():
        box = state['box']
        if box is None:
            return
        state['result'] = [round(box[0] * image.width / size[0]),
                           round(box[1] * image.height / size[1]),
                           round(box[2] * image.width / size[0]),
                           round(box[3] * image.height / size[1])]
        window.destroy()

    buttons = ttk.Frame(window, padding=10)
    buttons.pack(fill='x')
    ttk.Button(buttons, text='Hủy', command=window.destroy).pack(side='right', padx=5)
    accept = ttk.Button(buttons, text='Đọc vùng đã chọn', command=confirm, state='disabled')
    accept.pack(side='right', padx=5)
    canvas.bind('<ButtonPress-1>', begin)
    canvas.bind('<B1-Motion>', move)
    canvas.bind('<ButtonRelease-1>', end)
    window.bind('<Escape>', lambda _: window.destroy())
    window.mainloop()
    return state['result']
