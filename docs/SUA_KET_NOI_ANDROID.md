# Khôi phục kết nối Android – 13/10/2026

## Nguyên nhân đã xác nhận

API localhost:5268 đang chạy (Swagger HTTP 200), điện thoại V2352A đã kết nối ADB Wi-Fi nhưng danh sách `reverse --list` trống. TCP localhost:5268 trên điện thoại bị Connection refused. Sau khi tạo reverse đúng serial, người dùng xác nhận app kết nối lại được.

Launcher cũ chỉ giữ reverse trong chế độ `app`, bằng Start-Job tồn tại theo phiên chạy Flutter. Chạy `chay_web.cmd` mặc định rồi mở app từ Android Studio không có cơ chế đó. Lệnh reverse không chỉ định serial còn có thể lỗi khi nhiều thiết bị được kết nối.

## Đã sửa

- Vẫn chỉ có **một file chạy**: `MaNguon/scripts/chay_web.cmd`.
- Mặc định mở Web/API cũng bật monitor ADB ẩn, độc lập với cửa sổ launcher/Flutter. Không tự chạy/cài Flutter khi bấm đúp Web.
- Theo dõi từng serial ở trạng thái `device`, bỏ qua offline/unauthorized; chỉ khôi phục ánh xạ TCP5268 nếu thiếu/sai. Kiểm tra lại kết quả sau khi tạo; lỗi ADB được thử lại ở chu kỳ sau.
- Chu kỳ kiểm tra 3 giây. Thiết bị có thể kết nối sau khi đã mở Web; USB và Wi-Fi ADB đều được xử lý.
- Mutex chống monitor trùng; theo đúng PID + thời điểm khởi động API, tự thoát khi API đó dừng. Không tự khởi động lại API, không đổi firewall/địa chỉ LAN.
- Chế độ `app` chọn rõ serial khi nhiều thiết bị rồi chạy `flutter run -d`; không dùng Start-Job tạm nữa.
- Thêm chế độ `ket-noi`: chỉ khôi phục/bật theo dõi kết nối, không dừng/build/restart API/Web/máy in.
- File CMD được định dạng CRLF và `.gitattributes` giữ CRLF khi Git checkout. Không thay code app, schema/database, login, payOS hay logic nghiệp vụ. Không tự gửi lại POST tạo đơn/thanh toán.

## Cách sử dụng

1. Bấm đúp `MaNguon/scripts/chay_web.cmd` như trước để chạy hệ thống.
2. Kết nối điện thoại USB hoặc pair/connect Wi-Fi trong Android Studio; trạng thái `adb devices` phải là `device`.
3. Chạy app từ Android Studio. Không cần tự gõ reverse mỗi lần; monitor sẽ khôi phục khi thiết bị kết nối lại.

Nếu API đã chạy và chỉ muốn sửa kết nối, không restart các dịch vụ, mở terminal ở `MaNguon/scripts` và chạy:

```cmd
chay_web.cmd ket-noi
```

Mình đã bật monitor cho API hiện đang chạy và người dùng xác nhận app hoạt động trở lại, không cần cài APK mới cho sửa đổi này.

## Kiểm chứng

- Test-first tái hiện thiếu cơ chế khôi phục theo serial và kiểm CRLF của CMD.
- `verification/kiem_thu_ket_noi_android.ps1`: PASS – nhiều thiết bị, offline/unauthorized, mapping đúng không tạo lại, mapping mất/ADB lỗi rồi phục hồi, Web/app đều bật monitor, hidden/mutex/CRLF.
- `verification/kiem_thu_launcher.ps1`: PASS – vẫn một launcher, chỉ chọn tiến trình đúng dự án, đọc cấu hình, build/chạy dịch vụ và mở Web/Swagger.
- Điện thoại thật: được người dùng cho phép gỡ riêng reverse5268 khi không có giao dịch; monitor tự tạo lại và TCP5268 kết nối thành công. Không ngắt Wi-Fi/ADB, không tạo đơn hoặc thu tiền thử.
- Đã chạy thật `chay_web.cmd ket-noi`: không lỗi CMD, không restart API; launcher cha đã thoát nhưng monitor còn sống. Bật lại monitor vẫn chỉ còn một tiến trình sau khi mutex loại trùng.
- API vẫn trả HTTP 200; không sửa/công khai `.env` hoặc secret.

## Giới hạn cần hiểu

Đây là kết nối phát triển qua ADB, không phải triển khai Internet. Nếu API bị tắt, máy tính ngủ, điện thoại chưa được cấp quyền ADB hoặc Wi-Fi debugging chưa kết nối, app không thể truy cập API. Monitor sửa mapping **sau khi ADB kết nối lại**, không tự vượt quyền thiết bị hay làm việc khi máy tính tắt. Khởi động API lại thì dùng launcher để gắn monitor mới.

Tài liệu chính thức: [Android Debug Bridge](https://developer.android.com/tools/adb).
