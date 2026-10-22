# Thử VietOCR chữ viết tay chạy local

## Bộ thử ảnh vở mới: Qwen3-VL (22/10/2026)

**Mở `chay_thu_qwen.cmd` để thử ảnh vở ô ly.** Chọn 1 = 4B ưu tiên chất lượng hoặc 2 = 2B nhanh hơn, chọn ảnh, khoanh vùng chữ và chờ file kết quả. Hướng dẫn, nguồn và số đo trên hai ảnh thực tế: [HUONG_DAN_QWEN.md](HUONG_DAN_QWEN.md), [NGUON_QWEN.md](NGUON_QWEN.md). Qwen chạy local bằng llama.cpp, nhìn ảnh trực tiếp; không dùng bộ xóa ô ly/tách dòng bên dưới.

Trên ảnh screenshot 4 dòng đã gửi: 2B còn sai 5/28 từ, 4B còn sai 3/28 từ theo bản chép đối chiếu tạm. Hai ảnh chứa cùng nội dung, không phải hai mẫu độc lập; kết quả vẫn cần đọc lại. Bộ VietOCR dưới đây giữ để nghiên cứu/đối chứng.

Đây là khu vực NGHIÊN CỨU, chưa kết nối PhotocopyAPI/web/mobile. Không train lại, không dùng API AI trả phí. Nhận ảnh **một dòng chữ** hoặc thử **cả trang một cột bằng tách dòng**; chưa nhận PDF. Model và nguồn của tác giả được ghi tại [NGUON_MODEL.md](NGUON_MODEL.md).

## Thử cả trang (mới)

1. Nhấp **chay_thu_ca_trang.cmd**, chọn **1** cho ảnh vở ô ly hoặc **2** cho ảnh nền trắng không đường kẻ. Sau đó chọn ảnh JPG/PNG; có thể kéo thả ảnh vào CMD. Nếu chưa cài OpenCV: `.\.venv\Scripts\python.exe -m pip install -r requirements.txt`.
2. Cửa sổ khoanh vùng hiện ra: kéo chuột khoanh sát toàn bộ phần chữ, bỏ bàn/tay, logo và vùng trống, rồi bấm **Đọc vùng đã chọn**. Bấm Hủy sẽ không chạy model. Giới hạn 10 MB, 20 megapixel; hỗ trợ văn bản một cột.
3. Chương trình tìm dòng, nạp model một lần, rồi đọc tuần tự trên GPU/CPU và in tiến độ từng dòng. Không cần train lại và không gửi ảnh ra ngoài.
4. Khi xong, mở file chữ trong ứng dụng mặc định của Windows. Mỗi lần chạy lưu thư mục mới `reports/trang-<thời gian>/` chứa `ket_qua.txt`, `ket_qua.json`, `cac_dong.png` (ảnh đánh số vùng đọc), `vung_chu.png`, `anh_xu_ly.png` và `dong/` (ảnh từng dòng đưa vào model). Tọa độ dòng tính trong vùng chữ đã khoanh; JSON lưu cả tọa độ vùng trên ảnh gốc.
5. Đối chiếu **cac_dong.png** với ảnh gốc trước: nếu thiếu dòng, tách dấu thành dòng riêng hoặc gộp hai dòng, kết quả chữ sẽ sai theo. File TXT chỉ giữ xuống dòng, không tái tạo bố cục Word.

Lệnh tương đương:

```powershell
.\.venv\Scripts\python.exe thu_ocr.py page "D:\anh\ca-trang.png" --select-region --notebook --open-result
```

`doc_trang.py` kết hợp bộ tách dòng theo khoảng trắng (giấy trắng), hoặc lọc ô ly/tìm đỉnh mật độ mực (vở ô ly), với model nhận dạng từng dòng của tác giả. Vùng tách quá lớn sẽ bị chặn trước khi nạp model. Chưa hỗ trợ nhiều cột, bảng biểu, phối cảnh/nghiêng hoặc công thức. Lọc ô ly có thể mất nét chữ/dấu; tách đúng dòng không bảo đảm đọc đúng. Không áp dụng CER của 30 ảnh dòng trước đây cho cả trang.

**Kết quả ảnh chụp thực tế ngày 22/10/2026: CHƯA ĐẠT.** Ảnh vở ô ly người dùng gửi trước đây bị gom cả ảnh thành một dòng và sinh câu không liên quan. Bản sửa khoanh vùng/lọc ô ly tách được 10 dòng, nhưng model vẫn đọc sai nhiều. Giữ kết quả thô ở `reports/trang-20261022-090707-536961/`; chưa có nhãn đầy đủ nên không công bố CER/WER cho ảnh này. Chỉ dùng để nghiên cứu, chưa dùng kết quả này để làm bản in chính thức.

Kiểm thử ngày 21/10/2026: 18 bài kiểm thử qua, 1 bài model đơn dòng bỏ qua trong lượt này; có chạy model thật cho trang ghép 8 ảnh dòng VNOnDB. Tách được 8/8 vùng, xuất đủ TXT/JSON/ảnh vùng; tổng 18,13 giây, gồm 11,35 giây nạp model trên CUDA. Đây là kiểm thử trang ghép nền sạch, không phải đo chất lượng ảnh chụp thực tế. Ảnh thử ở `reports/trang-mau-8-dong.png`.

## Chạy ngay trên máy đã cài

1. Mở thư mục này, nhấp `chay_thu.cmd`: cửa sổ chọn ảnh hiện ra, chọn thư mục và ảnh JPG/PNG rồi bấm **Open/Mở**. Bấm Hủy sẽ không nạp model.
2. Cắt ảnh còn **một dòng** trước khi thử. Vẫn có thể kéo file ảnh vào `chay_thu.cmd` để nhận dạng trực tiếp.
3. Đọc kết quả ở cửa sổ lệnh. Phải kiểm tra lại chữ/dấu trước khi sử dụng; hiện chưa xuất Word/PDF hoặc gửi in.
4. `auto` dùng CUDA nếu hoạt động, nếu không dùng CPU. Các lỗi CUDA trong quá trình chạy được báo ra, không che giấu bằng chuyển CPU ngầm.

PowerShell, đứng trong thư mục này:

```powershell
.\.venv\Scripts\python.exe thu_ocr.py predict "D:\anh\mot-dong.png" --device cuda
.\.venv\Scripts\python.exe thu_ocr.py predict "D:\anh\mot-dong.png" --device cpu
.\.venv\Scripts\python.exe thu_ocr.py benchmark samples\labels.csv --device cuda --output reports\thu-gpu-moi.json
.\.venv\Scripts\python.exe -m unittest discover -s tests -v
```

File output phải là tên mới để không ghi đè kết quả nghiên cứu trước. CER/WER ở JSON là tỷ lệ 0..n, ví dụ 0.1 tương ứng 10%; CER có thể lớn hơn 1. Không diễn giải 1-CER thành độ chính xác.

## Cấu trúc

```text
photocopy_ocr/
  third_party/vietocr/            Mã nguồn gốc, có LICENSE/README/notebook
  models/vietocr-handwritten/     Trọng số + config + model card + evaluation của tác giả
  samples/images/                30 dòng VNOnDB để chạy thử
  samples/labels.csv              Nhãn và split gốc
  reports/                       JSON kết quả chạy thực tế
  thu_ocr.py                     Bộ chạy/đo kiểm do nhóm bổ sung
  tests/                         Kiểm thử input và tính metrics
  chay_thu.cmd                    Mở hộp chọn ảnh hoặc kéo thả một ảnh dòng
  NGUON_MODEL.md                  Nguồn, revision, SHA256, giấy phép
  KET_QUA_THU_NGHIEM.md           Kết quả và hạn chế, sau khi đo xong
```

## Luồng chạy và những chỗ nên đọc

`thu_ocr.py` → kiểm SHA256 config/weights → build_model của upstream → torch.load(weights_only=True), load_state_dict(strict=True) → ảnh RGB resize theo cấu hình gốc (cao 32, rộng 32..512) → CNN VGG19 → Transformer encoder/decoder → greedy decode → text và thời gian.

- `third_party/vietocr/vietocr/model/transformerocr.py`: kiến trúc.
- `third_party/vietocr/vietocr/model/backbone/vgg.py`: CNN.
- `third_party/vietocr/vietocr/model/seqmodel/transformer.py`: encoder/decoder.
- `third_party/vietocr/vietocr/tool/translate.py`: tiền xử lý và decoding.
- `third_party/vietocr/vietocr/model/vocab.py`: bảng ký tự tiếng Việt.
- `models/vietocr-handwritten/config.yml`: cấu hình của checkpoint; không thay vocab tùy tiện.
- `thu_ocr.py`: kiểm tra file, CER/WER, benchmark. Không có HTTP server, không upload ảnh.

Model chỉ nạp một lần trong mỗi lần benchmark. Chạy CMD nhiều lần sẽ nạp lại. Thời gian benchmark loại warmup, ghi load_seconds riêng. peak_allocated_vram_mb là bộ nhớ tensor do PyTorch theo dõi, không phải toàn bộ VRAM của Windows/CUDA.

## Cài lại khi cần (không cần làm nếu máy hiện tại đã chạy)

Python 3.11, driver NVIDIA tương thích CUDA 12.4. Không cần cài CUDA Toolkit riêng để dùng wheel PyTorch; kiểm tra driver và torch.cuda.is_available().

```powershell
py -3.11 -m venv .venv
.\.venv\Scripts\python.exe -m pip install torch==2.6.0 torchvision==0.21.0 --index-url https://download.pytorch.org/whl/cu124
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
git clone https://github.com/pbcquoc/vietocr.git third_party/vietocr
git -C third_party/vietocr checkout --detach fe8c3a7fc714aec57ab81cec844eb3adf0c1636c
```

Tải 4 file `transformerocr.pth`, `config.yml`, `README.md`, `evaluation_results.json` từ revision `0827b42e17cb0e1c1bbda5aac11f7cc5c816e0ca` trên model repo vào `models/vietocr-handwritten`. Không chạy script lạ từ model repo; không cần token để tải các file công khai. Đối chiếu SHA256 tại NGUON_MODEL.md. Không chạy pip install toàn bộ source upstream: bản nghiên cứu chỉ cần các dependency inference trong requirements.txt, không cài bộ training/augmentation.

## OCR cũ và cách khôi phục

Toàn bộ thư mục OCR cũ đã chuyển tới:

`C:\Users\USER\Desktop\DA_CN\photocopy_ocr_backup_20261021`

Không xóa ZIP/dataset/source cũ, không đụng checkpoint trên Google Drive. Muốn khôi phục: dừng tiến trình OCR, đổi tên bộ thử này sang tên khác, rồi chuyển thư mục backup về `MaNguon\photocopy_ocr`. Python venv thường gắn với đường dẫn nên tạo lại venv nếu không chạy được. Các tài liệu kế hoạch train ngày 19/10 trong docs/plans là lịch sử, không còn là hướng dẫn chạy bộ mới.
