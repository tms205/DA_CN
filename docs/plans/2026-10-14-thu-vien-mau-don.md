# Thư viện mẫu đơn tải sẵn

## Phạm vi đã được yêu cầu

- Tạm dừng kết nối máy in thật; không thay backend thanh toán/in.
- Tìm và tải biểu mẫu có nguồn rõ ràng, lưu trong một folder của dự án.
- Giữ bản trống để viết tay; không tự điền thông tin khách.
- Web/app chọn loại mẫu, xem trước đúng tài liệu, sau đó đưa PDF vào luồng báo giá/đặt in hiện có.
- Không thêm bảng/cột, không sửa/xóa file đơn hàng cũ; chỉ thay cách cung cấp mẫu có sẵn.

## Quyết định và giả định

- Dùng folder `PhotocopyApi/TaiNguyen/MauDon`, catalog JSON kèm tên, nguồn, phạm vi, số trang, hash và tên file. Giữ bản gốc, PDF và ảnh xem trước cùng folder.
- Ưu tiên nguồn trường/cơ quan công bố. Mẫu gắn cơ quan riêng phải ghi rõ, không gọi là mẫu pháp lý dùng mọi nơi. Ngày tải 14/10/2026 không có nghĩa mẫu mới ban hành ngày đó.
- PDF tải sẵn hoặc chuyển từ Word gốc; không vẽ/retype nội dung như code cũ. Office chạy kín, tắt macro/cập nhật link; không chạy executable từ nguồn tải.
- Xem trước web/mobile bằng ảnh render từng trang từ chính PDF, có phóng/thu và nút chuyển trang; không thêm plugin PDF nặng. Web dùng native dialog; PDF iframe đã bị loại vì kiểm tra ảnh chụp thấy khung trắng trong Chrome.
- Chỉ đọc file theo mã catalog, không nhận URL/path tùy ý từ client. Copy nội dung mẫu vào file của khách theo quyền upload cũ, không dùng chung một file khách hàng.
- Quy mô khoảng 10–20 mẫu trống, file nhỏ; đọc local nên mất mạng Internet tới nguồn gốc không ảnh hưởng. App vẫn cần nối API, lỗi có Thử lại; không đặt đơn offline.
- Không tự cập nhật nội dung biểu mẫu từ Internet khi chạy. Người quản trị kiểm tra nguồn trước khi thay phiên bản; copy đầy đủ tài nguyên khi build/publish.

## Mobile checkpoint

- Android/Flutter, khách hàng trên điện thoại, giữ navigation và theme hiện có.
- MFRI: (5+5)−(2+2+2)=4. Một trang ảnh được nạp mỗi lần, không nạp toàn bộ PDF/ảnh trong danh sách.
- Đã đọc mobile-design-thinking, touch-psychology, mobile-performance, mobile-backend, mobile-testing, mobile-debugging và platform-android.
- Áp dụng: nút ≥48dp, nguồn/phạm vi rõ, loading/error/retry, back không tạo file, chỉ Chọn để in mới gửi POST; cleanup controller và bỏ response cũ.
- Không làm gesture-only hoặc truyền bearer token vào URL; không đổi typography/theme toàn app.

## Các bước

1. Download và kiểm tra magic/type, malware-active content, nội dung trống, nguồn/phạm vi; giữ hash.
2. Ưu tiên PDF gốc tải được, render PNG; kiểm tra font/lề/đủ trang. Word HUIT/UTT đã tải nhưng chuyển Office bị treo nên giữ trong NguonGoc, không kích hoạt và không phục vụ khách.
3. Test RED cho catalog/file thật, bytes preview bằng bytes chọn in, mã lạ/path traversal và tài nguyên build.
4. Thay MauDonPdf đọc folder; giữ endpoint và quyền cũ, thêm endpoint ảnh từng trang.
5. Web/app thêm tìm/lọc và nguồn/số trang; app xem trước rồi chọn, không tự tạo file khi chỉ xem.
6. Kiểm tra xUnit/Flutter, HTTP/render, build/publish/APK. Người dùng đã đồng ý restart API/web sau khi xác nhận không có giao dịch; không gửi in/thu tiền thật để kiểm thử.
7. README nguồn/hướng dẫn thêm mẫu và bàn giao; ghi đúng các giới hạn chưa nghiệm thu.

## UI checkpoint

- Industrial / Utilitarian Administration, giữ Be Vietnam Pro, màu navy và CSS tokens hiện có. DFII: (3+4+4+4)−3=12.
- Thư viện dùng tên/nguồn/số trang, tìm kiếm và nhóm thay vì trang trí. Web dialog có điều khiển trang/phóng to, ESC/đóng; mobile có viewer riêng, nút 48dp.
- Nguồn và phạm vi luôn có trước bước chọn; không thay theme hay cỡ chữ toàn ứng dụng.
