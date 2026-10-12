# Bổ sung in ảnh thẻ và mẫu đơn trống

## Phạm vi đã được đồng ý

- Web tại quầy và Flutter Android: chọn tài liệu, mẫu đơn trống hoặc ảnh thẻ.
- Ảnh thẻ: 2x3, 3x4, 4x6, 3.5x4.5, 5x5 cm; 1–100 ảnh mỗi file; giá mỗi ảnh riêng theo chi nhánh.
- Ghép đúng số ảnh vào PDF A4. Số trang PDF dùng cho máy in, thời gian và giấy; số ảnh dùng tính tiền. Không tăng số bản để tăng số ảnh.
- Giữ nguyên luồng thanh toán, phân máy, kho và các đơn cũ; migration bổ sung, không xóa dữ liệu.
- Mẫu đơn tham khảo để in và viết tay, không điền dữ liệu cá nhân tự động.

## Các bước

Đã hoàn tất các bước triển khai và kiểm thử bên dưới. Kết quả và giới hạn nghiệm thu thiết bị thật ghi trong `docs/InAnhThe_MauDon_2026-10-12.md`.

1. Kiểm thử quy cách, bố trí ảnh, metadata và mẫu PDF trước khi triển khai.
2. Thêm metadata vào dịch vụ, file và chi tiết đơn; cập nhật cách tính tiền và chặn dùng sai dịch vụ.
3. Seed 5 dịch vụ và giá từng chi nhánh, giấy ảnh A4 tồn ban đầu 0. Không giả lập đã nhập kho.
4. Lưu migration 010, kiểm tra database thật và chạy sau khi sao lưu thủ tục. Đồng bộ SQL gốc.
5. API tạo file ảnh thẻ và lấy mẫu đơn, dùng lại kiểm tra quyền tải file.
6. Web/mobile chọn dịch vụ; không cho gia công hoặc nhân bản PDF ảnh thẻ ngoài số ảnh đã yêu cầu.
7. Chạy test, build .NET, analyze/test Flutter; kiểm tra PDF thật và lập báo cáo bàn giao.

## Thiết kế giao diện

Giữ bố cục nghiệp vụ và màu hiện có (utilitarian, DFII 13). Ba lựa chọn dịch vụ rõ ràng; kích thước/số ảnh nằm trước nút tải ảnh, thông báo cách căn ảnh và đơn vị tính tiền.

Mobile checkpoint: Android, Flutter, Navigator stack hiện có, điện thoại khách hàng; không đặt đơn offline. Đã đọc mobile-design-thinking, touch-psychology, mobile-performance, mobile-backend, mobile-testing, mobile-debugging, platform-android. MFRI = (5+5)-(2+2+2)=4: bổ sung test dữ liệu và widget. Nút tối thiểu 48dp, hiển thị đang tải/lỗi/thử lại, xử lý mounted/dispose. Không thêm gesture bắt buộc hay state manager mới; PDF xử lý ở server để giảm bộ nhớ điện thoại.

## Giá khởi tạo

3x4: 5.000đ/ảnh; 4x6: 10.000đ/ảnh (tham khảo gói của ruaanh.com, không phải báo giá bán lẻ từng ảnh). 2x3: 3.000đ; 3.5x4.5: 7.000đ; 5x5: 10.000đ là giá đề xuất, quản trị có thể sửa. Không coi đây là tiêu chuẩn ảnh hộ chiếu/visa: khách cần kiểm tra yêu cầu nơi nhận hồ sơ.
