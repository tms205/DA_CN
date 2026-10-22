# Thử OCR bằng Qwen3-VL local

## Chạy trên máy hiện tại

1. Mở `chay_thu_qwen.cmd` trong thư mục này.
2. Chọn **1 = Qwen 4B**, ưu tiên chất lượng; hoặc **2 = Qwen 2B**, nhanh và nhẹ hơn.
3. Chọn ảnh JPG/PNG. Kéo chuột khoanh toàn bộ phần chữ cần đọc, rồi bấm **Đọc vùng đã chọn**. Bỏ phần bàn, tay và giấy trống; giữ các dấu tiếng Việt sát mép.
4. Chờ nạp model và nhận dạng. File TXT tự mở khi xong. Có thể kéo thả ảnh vào CMD để bỏ bước chọn file.

Mỗi lượt lưu một thư mục `reports/qwen-<thời gian>/`, gồm ảnh đưa vào model `anh-01.png`, văn bản thô `ket_qua-01.txt`, `ket_qua.json` và `llama.log`. Với nhiều ảnh qua CLI sẽ có các file đánh số tiếp theo. JSON lưu nguồn, vùng khoanh, kích thước, prompt, model/revision, thời gian, token và lý do kết thúc.

Qwen nhìn trực tiếp ảnh màu, không chạy bộ xóa ô ly/tách dòng của VietOCR. Đường kẻ vẫn xuất hiện trong ảnh đầu vào. Ảnh lớn được thu nhỏ giữ tỉ lệ, cạnh dài tối đa 1600 pixel mặc định. File ảnh gốc được giữ nguyên. Kết quả có thể sai hoặc đoán chữ, cần đối chiếu trước khi sử dụng. Nếu hết giới hạn token, console sẽ cảnh báo `kết quả có thể chưa đầy đủ`; giới hạn hiện tại là 2048 token đầu ra với context 4096.

## Kết quả trên hai ảnh người dùng gửi ngày 22/10/2026

Hai ảnh chứa **cùng một đoạn chữ 4 dòng**, một ảnh toàn trang và một ảnh cắt sát chữ. Đây là đối chứng nhỏ, không phải bộ kiểm thử độc lập hoặc đánh giá toàn bộ chữ viết tiếng Việt.

| Model | Ảnh 1.jpg toàn trang | Ảnh screenshot cắt sát chữ | Lỗi từ trên ảnh cắt sát chữ |
|---|---:|---:|---:|
| Qwen 2B Q4_K_M | 20,25 giây | 8,04 giây | 5/28 từ |
| Qwen 4B Q4_K_M | 31,27 giây | 13,39 giây | 3/28 từ |

Thời gian trên là từng lượt nhận dạng, không gồm nạp model. 2B mất thêm 3,67 giây nạp, 4B thêm 6,67 giây trong lượt đo. Các kết quả không bị cắt token. Chép tay đối chiếu từ ảnh được ghi trong `reports/doi-chieu-qwen-20261022.json`, cần nhóm xác nhận lại dấu/câu. Cách đếm từ là tách theo khoảng trắng, giữ dấu/chữ hoa/dấu câu; không phải đánh giá chuyên gia tiếng Việt.

Trên ảnh cắt sát chữ, 4B vẫn nhầm ví dụ `mây → mấy`, `đi → cứ`, `trả → xả`. Không tự sửa những từ này trong file kết quả để làm đẹp số đo. CER tương ứng: 2B toàn trang 10,17%, 2B crop 5,93%, 4B toàn trang 6,78%, 4B crop 4,24% trên bản chép đối chiếu 118 ký tự sau chuẩn hóa khoảng trắng. Không diễn giải thành “độ chính xác 95,76%” hoặc suy rộng cho trang khác.

Kết quả thô:

- 2B: `reports/qwen-20261022-092943-409207/`.
- 4B: `reports/qwen-20261022-093412-571375/`.
- So sánh với VietOCR: bản cũ cho nhiều câu không liên quan ngay cả khi khoanh từng dòng; xem `reports/doi-chung-4-dong-20261022-092248/`. Hai bộ dùng phương thức xử lý khác nhau, không coi đây là so sánh kiểm soát hoàn toàn.

Với dữ liệu này, 4B phù hợp hơn để tiếp tục thử có người kiểm tra; 2B dùng khi ưu tiên thời gian. Chưa kết luận model đọc tốt mọi ảnh vở, công thức, bảng hoặc mọi kiểu chữ.

## Cấu hình và bộ nhớ đã thử

- Máy i7-12700H, RTX 3050 Laptop 4 GB VRAM, RAM 16 GB, driver 566.07.
- `llama.cpp b6907` bản CUDA 12.4, trọng số ngôn ngữ Q4_K_M, vision projector Q8_0.
- 2B: nạp 29/29 lớp lên GPU; buffer model GPU 1050 MiB, KV 448 MiB, compute 75 MiB trong log.
- 4B: nạp 24/37 lớp lên GPU, phần còn lại ở RAM; buffer GPU model 1381 MiB, KV 384 MiB, compute 380 MiB. Quan sát trong lúc chạy: toàn GPU dùng khoảng 3226 MiB (gồm cả ứng dụng khác); process working set khoảng 4105 MiB. Đây là mẫu quan sát, không phải phép đo peak VRAM độc lập.
- Vision projector chạy CPU để giảm VRAM. Bản 4B chậm hơn và dùng nhiều RAM hơn. Không nạp hai model cùng lúc.
- Trước mỗi lượt chạy kiểm SHA256 của model/projector. Runtime chỉ mở server tạm trên `127.0.0.1`, port trống do chương trình chọn; ảnh được gửi tới server local. Chương trình tắt đúng tiến trình server do nó tạo khi xong/lỗi. Không cần API key hoặc tài khoản trả phí.

## Chạy bằng dòng lệnh

Đứng trong thư mục `photocopy_ocr`:

```powershell
# Đọc ảnh nguyên khung, mặc định 2B
.\.venv\Scripts\python.exe thu_qwen.py "C:\anh\trang-vo.jpg"

# 4B, khoanh vùng chữ và mở kết quả
.\.venv\Scripts\python.exe thu_qwen.py "C:\anh\trang-vo.jpg" --model 4b --select-region --open-result

# Nhiều ảnh, nạp model một lần
.\.venv\Scripts\python.exe thu_qwen.py "C:\anh\1.jpg" "C:\anh\2.png" --model 4b

# Khi GPU không đủ VRAM hoặc CUDA không hoạt động: thử CPU (chậm hơn)
.\.venv\Scripts\python.exe thu_qwen.py "C:\anh\trang-vo.jpg" --model 2b --cpu
```

File ảnh phải là JPG/PNG tĩnh, tối đa 10 MB và 20 megapixel. Hủy chọn ảnh/vùng thì không nạp model. `--max-edge` nhận giá trị 512..2000; tăng kích thước có thể tăng RAM/thời gian và không bảo đảm tăng chất lượng. Nhận dạng timeout 600 giây, nạp server timeout 180 giây. Nếu runtime báo lỗi, xem `llama.log`; không khởi động lại API/web để xử lý lỗi này.

## Cài lại trên máy khác

1. Tạo `.venv` Python 3.11 và cài dependency theo README gốc, gồm PyTorch CUDA 12.4. Runtime dùng DLL CUDA trong `torch/lib`, không yêu cầu cài CUDA Toolkit riêng.
2. Tải model/runtime bằng script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tai_qwen.ps1 -Model 2b
powershell -NoProfile -ExecutionPolicy Bypass -File .\tai_qwen.ps1 -Model 4b
```

Script tải từ Qwen và ggml-org, kiểm SHA256, hỗ trợ tải tiếp file `.part`; model 2B khoảng 1,55 GB, 4B khoảng 2,95 GB, runtime ZIP khoảng 180 MB. Giữ model card và LICENSE cạnh file. Xem `NGUON_QWEN.md` về revision, giấy phép và hash. Dữ liệu ảnh/kết quả/model không đưa vào Git.

## Những phần cần tiếp tục đánh giá

Tập 20–30 ảnh thực tế từ nhiều người, mỗi ảnh có bản chép đối chiếu; ghi CER/WER, lỗi dấu/chữ và thiếu dòng; tách ảnh dùng điều chỉnh prompt khỏi ảnh kiểm tra cuối. Tính năng hiện là bộ thử local, chưa nối web/mobile. Nhóm phải ghi nhận đây là model có sẵn được tích hợp, không phải do nhóm tự huấn luyện.
