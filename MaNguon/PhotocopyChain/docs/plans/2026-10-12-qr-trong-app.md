# QR trong app Implementation Plan

> Thực hiện từng bước trong phiên hiện tại; không tạo worktree hoặc subagent để tránh ảnh hưởng các thay đổi đang có.

**Goal:** Hiện QR payOS và kết quả thanh toán ngay trong app Flutter Android, không đổi database.

**Architecture:** Backend chỉ trả QR payOS của đúng đơn được phép xem. Lưu phản hồi QR mới ở LocalApplicationData/PhotocopyChain/PayOSQr để tái sử dụng sau khi khởi động API; file này không phải bằng chứng đã nhận tiền. App vẽ QR tại chỗ, dùng trạng thái xác thực của API, SignalR, polling và kiểm tra khi resumed.

**Tech Stack:** ASP.NET Core 8, payOS 2.1.0, Flutter, qr_flutter, xUnit, flutter_test.

## Phạm vi đã chốt

- Người dùng xác nhận ngày 12/10/2026: QR trong app; chuyển tiền trong app ngân hàng; quay lại app xem kết quả.
- Chỉ backend xác nhận tiền thật; không tin callback URL/nút người dùng.
- Giữ navigation, màu và màn thành công hiện tại; không đổi schema, không tạo đơn khi kiểm tra.
- Mất mạng giữ thông tin đơn đã tải và hiện lỗi kiểm tra, không báo thành công giả.
- Đơn quá hạn/hủy không còn hiển thị QR dùng được. Vẫn cho đối soát thanh toán đến muộn.
- Quy mô đồ án, Android điện thoại; app không nhận tiền hay hoạt động thanh toán offline.

## Decision log

1. QR native được chọn theo xác nhận của người dùng. WebView checkout là phương án khác nhưng phụ thuộc trang ngoài và khó giữ bố cục; checkout ngoài app chỉ giữ làm dự phòng cho link cũ chưa lưu QR.
2. Dùng qr_flutter để vẽ đúng chuỗi QR payOS trả về, không tự viết thuật toán QR và không gửi dữ liệu sang dịch vụ vẽ ảnh bên thứ ba.
3. SDK GetAsync chỉ trả trạng thái/link id, không có QR. Cache JSON ngoài source giúp giữ QR từ CreateAsync qua restart mà không đổi DB. Kiểm tra id link + mã đơn + số tiền trước khi tái sử dụng. Mất/hỏng cache không tự dựng QR khác: dùng checkout chính thức dự phòng.
4. Không đổi logic webhook/xếp in/COD. API keys và checksum không đưa vào mobile/logs.

## Mobile checkpoint

Android, Flutter; tham chiếu: mobile-design-thinking, touch-psychology, mobile-performance, mobile-backend, mobile-testing, mobile-debugging, platform-android.

MFRI: platform 5 + accessibility readiness 5 - interaction 2 - performance 1 - offline dependence 3 = 4. Cần kiểm tra bố cục và vòng đời.

- QR đen-trắng có vùng trắng, không logo phủ; giới hạn 260dp và co theo bề ngang.
- Thao tác kiểm tra/copy là nút 48dp, có nhãn; không dựa vào gesture.
- SingleChildScrollView phù hợp một đơn, không danh sách dài; local state giữ architecture hiện tại.
- Dừng polling nền, không gửi chồng request; resumed kiểm tra ngay; timer/listener được dispose.
- Không xác nhận offline, không lưu API key, không tự đánh dấu đã trả khi app quay lại.
- Giữ Android back về danh sách đơn như hiện tại; iOS/tablet không nằm trong nghiệm thu thiết bị lượt này.

## Task 1: Backend QR và cache

Files: PhotocopyApi/ThanhToan/QuyTacPayOS.cs, DuLieuQrPayOSStore.cs (mới), Controllers/ThanhToanController.cs, Program.cs; PhotocopyShared/Dtos/DonHang/DonHangDtos.cs; PhotocopyApi.Tests/DuLieuQrPayOSTests.cs (mới).

1. Test phản hồi QR đúng order/amount, từ chối lệch dữ liệu; cache tồn tại qua instance mới, sai link không dùng, JSON hỏng không cấp QR.
2. Chạy `dotnet test PhotocopyApi.Tests --filter FullyQualifiedName~DuLieuQrPayOSTests --artifacts-path <thư mục tạm>`; xác nhận fail trước khi thêm code.
3. Thêm các trường QR, BIN, account, mô tả, order, amount vào DTO (giữ checkoutUrl/hạn cũ). Map CreateAsync bằng kiểm tra order/amount, ghi JSON atomic. Khi có link cũ hợp lệ lấy cache khớp id; thiếu thì trả checkout không QR.
4. Chạy lại test, kiểm tra Web Admin vẫn đọc DTO cũ.

## Task 2: Mobile và vòng đời

Files: photocopy_mobile/pubspec.yaml + lock, lib/models.dart, lib/screens/dat_in/thanh_toan_screen.dart; test/thanh_toan_qr_test.dart (mới).

1. Test QR ngay màn, không chỉ checkout; chỉ hiện thành công khi API trả đã trả; resumed kiểm tra; mất mạng giữ QR + retry; hết hạn/hủy không có QR; COD không tạo link; không polling nền/chồng request.
2. Chạy `flutter test test/thanh_toan_qr_test.dart --reporter expanded`; xác nhận fail đúng hành vi chưa có.
3. Render QrImageView từ QR trong DTO, thêm copy nội dung/tài khoản và hướng dẫn chụp QR để nhập từ ảnh trong app ngân hàng. Không tự mở ngân hàng/deep link không được kiểm chứng.
4. WidgetsBindingObserver: resumed đối soát, paused dừng polling, khóa request đang chờ; trạng thái thành công giữ màn kết quả và nút theo dõi.
5. Kiểm thử bề ngang 320/390dp, chữ phóng to; không giả lập tiền trong database thật.

## Task 3: Kiểm tra và bàn giao

- `flutter test`, `flutter analyze --no-fatal-infos`, `flutter build apk --debug --target-platform android-arm64`.
- .NET tests không DB với artifacts tạm; build API/Web/Simulator, không đè DLL đang chạy.
- Kiểm tra Swagger DTO trên API mới khi được restart. Không tự chuyển tiền hoặc đặt đơn thật chỉ để test.
- Ghi kết quả, file thay đổi, hướng dẫn chạy vào docs/QR_Trong_App_2026-10-12.md.
- Phân biệt kiểm thử phản hồi giả và chuyển khoản thật trên thiết bị; không tuyên bố nghiệm thu tiền thật khi chưa thực hiện.
