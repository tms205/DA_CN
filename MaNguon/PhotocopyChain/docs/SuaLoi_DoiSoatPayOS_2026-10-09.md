# Sửa thanh toán payOS chưa cập nhật tại hàng đợi

## Nguyên nhân đã kiểm tra

- Đơn DH00010: payOS trả PAID, số tiền cần trả và đã nhận đều 17.500đ, có một giao dịch. Database vẫn CHUA_THANH_TOAN.
- URL Quick Tunnel đang cấu hình không phân giải được DNS và không có cloudflared chạy tại thời điểm kiểm tra. Webhook không tới được API.
- ReturnUrl của Web đi thẳng về trang chủ để tránh phụ thuộc tunnel. Trang hàng đợi chưa nhận orderCode do payOS thêm vào URL và chưa gọi API đối soát khi quay lại.
- API đã có đối soát dự phòng tại GET api/thanh-toan/{id}: kiểm tra quyền, đọc trạng thái trực tiếp từ payOS qua SDK, kiểm tra mã đơn và đủ đúng số tiền, rồi mới gọi thủ tục ghi nhận thanh toán/xếp in.

## Thay đổi

- PhotocopyAdmin/DoiSoatThanhToanWeb.cs: đọc mã đơn quay lại từ orderCode hoặc maDonHang cũ; gọi API bằng HttpClient đã xác thực. Không tự đánh dấu thanh toán dựa vào status trên URL.
- PhotocopyAdmin/Pages/HangDoiDon.razor: tự đối soát một lần khi nhận mã đơn quay lại; tải lại bảng/chi tiết; thêm nút Kiểm tra thanh toán cho đơn VietQR chưa xác nhận, kể cả đơn dùng link cũ.
- PhotocopyApi.Tests/DoiSoatThanhToanWebTests.cs: 9 kiểm thử cho mã đơn, trạng thái từ API và phản hồi không được phép/không tìm thấy.
- Không sửa cấu trúc database, không giả webhook, không thu tiền mới và không ghi thủ công DA_THANH_TOAN.

## Cách kiểm tra

1. Web Admin đã build và khởi động lại, API/máy in đang chạy không bị dừng bởi thay đổi này.
2. Ctrl + F5 trên Web đang đăng nhập; mở DH00010 và bấm Kiểm tra thanh toán nếu dùng link cũ.
3. API xác minh payOS rồi mới cập nhật trạng thái. Đơn đủ điều kiện file/vật tư/máy sẽ tiếp tục vào luồng in tự động hiện có.
4. Link mới quay về Web có orderCode sẽ tự gọi đối soát. Nếu payOS chưa xác nhận tiền lúc quay lại hoặc API tạm lỗi, dùng nút kiểm tra lại.
5. Để webhook cập nhật ngay cả khi khách không quay về Web/App: vẫn cần chạy tunnel hợp lệ, cập nhật PayOS__PublicBaseUrl trong .env và đăng ký webhook đúng URL đang hoạt động. Không đưa khóa API vào giao diện.

## Kết quả

- Web Admin build: 0 lỗi, 0 cảnh báo.
- 28 kiểm thử liên quan payOS đạt; kiểm thử hồi quy không phụ thuộc bộ dữ liệu SQL có 94 bài đạt. Không bao gồm PhamViCSDLTests và CaVaPhieuNhapCSDLTests.
- Chữ ký webhook sai vẫn bị từ chối. Số tiền chưa đủ và trạng thái chưa thanh toán không được giao diện nâng thành đã trả.
- Đã đối chiếu trạng thái PAID thật từ API payOS và SDK. Bạn đã bấm Kiểm tra thanh toán trên Web mới và xác nhận đơn chuyển sang in.
- Kiểm tra database sau thao tác: DH00010 DA_THANH_TOAN, số tiền 17.500đ, có mã giao dịch; đơn DANG_IN, được phân máy 1; chỉ một thông báo Thanh toán thành công. Không cần chuyển khoản lần nữa.
- Nếu dùng link mới và quay về URL có orderCode, Web gọi cùng bước đối soát một cách tự động. Link cũ mất mã đơn khi chuyển hướng vẫn dùng nút kiểm tra tại chi tiết đơn.

Tài liệu API đối chiếu: https://payos.vn/docs/api/ và https://payos.vn/docs/du-lieu-tra-ve/return-url/
