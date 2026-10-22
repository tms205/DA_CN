# Thử model chữ viết tay có sẵn — 21/10/2026

## Phạm vi đã chốt với người dùng

- Thay bộ train TrOCR cũ bằng khu vực nghiên cứu nhận dạng local, không huấn luyện lại.
- Lấy nguồn VietOCR của pbcquoc và trọng số DungHugging/vietocr-handwritten-finetune; giữ ghi công.
- Chạy một người dùng, một dòng mỗi lần trên i7-12700H / RTX 3050 4 GB / RAM 16 GB.
- Không upload ảnh lên dịch vụ AI; tải model một lần, inference offline.
- Đo tốc độ thực tế, CER/WER và lỗi từng dòng; không hứa chất lượng trước khi đo.
- Không sửa API/web/mobile/database/launcher chung. Chưa làm cả trang, tách dòng hoặc giao diện sản phẩm.

## Quyết định

1. Chọn VietOCR nhỏ thay vì tiếp tục train TrOCR; Vintern/ONNX là phương án khác chưa triển khai.
2. Thay vì xóa vĩnh viễn, chuyển toàn bộ OCR cũ ra ngoài repo tới `C:/Users/USER/Desktop/DA_CN/photocopy_ocr_backup_20261021`. Giữ ZIP, dữ liệu, source và môi trường cũ; venv cũ có thể phải tạo lại nếu chạy tại đường dẫn mới.
3. Giữ nguồn upstream nguyên trạng trong third_party; wrapper của nhóm chỉ nạp model local, kiểm tra hash, xử lý đầu vào và đo kiểm.
4. Dùng 30 dòng validation VNOnDB đã có, lấy ngẫu nhiên seed 42. Đây là smoke benchmark trên ảnh raster nét bút, không đại diện cho ảnh chụp vở và không chứng minh độc lập với dữ liệu tác giả đã train.
5. Môi trường Python 3.11 riêng; CUDA 12.4/PyTorch 2.6.0 theo driver đang có. Batch 1. Không thay môi trường hệ thống.
6. Giới hạn ảnh JPG/PNG, 10 MB/20 megapixel; luôn nhắc kiểm tra kết quả và không ghi đè báo cáo cũ.

## Các bước

1. Kiểm tra liên kết dự án, sao lưu OCR cũ (đã thực hiện).
2. Lấy nguồn/model cố định revision, kiểm tra SHA256.
3. Viết test trước cho tính lỗi, input, manifest và kiểm tra tính toàn vẹn.
4. Chạy GPU/CPU với model thật; ghi output, thời gian, VRAM.
5. Viết README, hướng dẫn chạy và kết quả; đối chiếu trạng thái Git, không commit thay người dùng.

