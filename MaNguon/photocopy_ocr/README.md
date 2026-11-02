# OCR chữ viết tay — Qwen chạy trên máy chủ

Chức năng nhận dạng ảnh JPG/PNG bằng Qwen3-VL 2B hoặc 4B trên máy Windows chạy PhotocopyApi. Web và điện thoại gửi ảnh tới API của hệ thống; điện thoại không chạy model trực tiếp. Model là model có sẵn của Qwen, không phải do nhóm tự huấn luyện. Xem `NGUON_QWEN.md` để biết nguồn, phiên bản và giấy phép.

## Sử dụng

1. Chạy `MaNguon\scripts\chay_web.cmd`. Cần chạy bản API và web đã build từ mã mới. Ứng dụng mobile cần build/cài lại bản mới.
2. Đăng nhập. Trên web mở **Vận hành → Nhận dạng chữ**; trên mobile mở **Đặt in → Nhận dạng chữ viết tay từ ảnh**.
3. Thêm tối đa 10 ảnh JPG/PNG (10 MB/ảnh, tổng 50 MB). Đổi thứ tự, xóa ảnh hoặc chọn từng ảnh để cắt vùng chữ.
4. Chọn Qwen 2B/4B rồi nhận dạng. Hệ thống xử lý từng ảnh; yêu cầu từ web/app khác được xếp hàng chung. Giao diện hiển thị ảnh đang chạy hoặc thứ tự chờ. Có thể dừng và thử lại riêng ảnh lỗi.
5. Đối chiếu từng ảnh, chỉnh sửa rồi xuất tất cả ra một file Word. Mỗi ảnh bắt đầu ở trang mới; chữ dài có thể tràn trang. Xuất bị chặn nếu còn ảnh lỗi/chưa chạy/đổi vùng chưa nhận dạng lại. Tổng nội dung tối đa 100.000 ký tự.
6. Muốn đặt in: lưu Word, kiểm tra rồi tải file lên quy trình Đặt in/Lập đơn tại quầy đang có. OCR không tự tạo đơn, thu tiền hay gửi in.

Không thêm bảng/cột hoặc lịch sử OCR. Ảnh máy chủ được dọn khi tác vụ kết thúc; kết quả tác vụ chỉ giữ tạm tối đa 15 phút (có giới hạn bộ nhớ). Bản người dùng sửa nằm trên giao diện: tải Word trước khi rời màn hình. API khởi động lại làm mất hàng chờ tạm; cần gửi lại ảnh. Chi tiết: `../../docs/HUONG_DAN_OCR_NHIEU_ANH.md`.

AI có thể đoán sai, bỏ dấu, thiếu dòng hoặc trả kết quả chưa đầy đủ. Nếu nhận thông báo bị cắt thì nên tách ảnh thành nhiều phần và thử lại. Không coi kết quả là bản chép chính xác khi chưa kiểm tra.

## Thành phần cần có

- `.venv` với Python và Pillow; các DLL CUDA trong `torch/lib` cần cho runtime GPU Windows hiện tại.
- `models/qwen3-vl-2b`, `models/qwen3-vl-4b` gồm model GGUF và vision projector.
- `third_party/llama-b6907` chứa llama.cpp, `thu_qwen.py`, `qwen_support.py`, `ocr_api.py`.
- `tai_qwen.ps1`, `NGUON_QWEN.md` giữ lại để tải và kiểm tra nguồn/model. Model và runtime không được đưa lên Git.

Trên máy khác cần cài lại môi trường tương thích, tải đủ hai model/runtime theo `HUONG_DAN_QWEN.md`, đặt đúng thư mục và kiểm tra GPU; API không tự tải model. Máy hiện tại dùng RTX 3050 4 GB, 4B có thể chậm và sử dụng cả RAM. Nếu GPU/runtime không hoạt động, API sẽ báo lỗi và cần cấu hình lại máy chủ. Ảnh không được gửi tới dịch vụ AI bên ngoài, nhưng vẫn đi qua mạng giữa điện thoại và API theo cấu hình kết nối hiện tại.

Công cụ thử độc lập vẫn có thể chạy `chay_thu_qwen.cmd`; tài liệu đo thử cũ và các thành phần VietOCR được lưu trong bản sao lưu ngoài dự án. Thư mục sao lưu có thể chứa ảnh và dữ liệu thử nghiệm, cần quản lý quyền truy cập phù hợp.

## Cài lại trên Windows khi cần

Từ thư mục `photocopy_ocr`, dùng Python 3.11 và driver NVIDIA phù hợp CUDA 12.4. Các lệnh sau tải các gói và model từ Internet:

```powershell
py -3.11 -m venv .venv
.\.venv\Scripts\python.exe -m pip install torch==2.6.0 --index-url https://download.pytorch.org/whl/cu124
.\.venv\Scripts\python.exe -m pip install Pillow==12.3.0
powershell -NoProfile -ExecutionPolicy Bypass -File .\tai_qwen.ps1 -Model 2b
powershell -NoProfile -ExecutionPolicy Bypass -File .\tai_qwen.ps1 -Model 4b
```

Giữ nguyên cấu trúc thư mục so với `PhotocopyApi` trong mã nguồn; `tai_qwen.ps1` kiểm tra SHA256 của các file tải về. Chạy `chay_thu_qwen.cmd` để kiểm tra model trước khi dùng trên web/app.

## Cách áp dụng vùng cắt

Giao diện gửi ảnh gốc cùng tọa độ vùng chọn theo tỉ lệ. Worker chuẩn hóa hướng EXIF, kiểm tra vùng nằm trong ảnh, cắt trên pixel gốc rồi mới thu nhỏ tới cạnh dài 1600 pixel để model nhận dạng. Ảnh gốc không bị ghi đè. Hủy thao tác cắt sẽ giữ vùng trước đó. Nếu cắt lại sau khi đã có văn bản, văn bản vẫn được giữ và có nhắc rằng nó thuộc lần nhận dạng trước.
