# Thanh toán VietQR ngay trong app — 12/10/2026

## Đã làm

- Màn thanh toán Flutter hiện QR payOS trực tiếp, số tiền, người nhận, mã ngân hàng, tài khoản, nội dung và hạn thanh toán. Có nút sao chép tài khoản/nội dung.
- QR vẽ trên điện thoại bằng qr_flutter, giữ nguyên chuỗi payOS trả về; không gửi thông tin sang dịch vụ tạo ảnh QR bên thứ ba.
- Khi quay lại từ app ngân hàng, app kiểm tra ngay với API. Nếu API xác nhận đã nhận tiền, màn hiện “Thanh toán thành công”, cho xem tiến độ hoặc về danh sách đơn.
- Giữ SignalR và đối soát mỗi 5 giây khi app đang mở; ngừng polling trong nền/khi đã trả. Không gửi chồng request. Nếu resume trùng request cũ, kiểm tra tiếp khi request đó xong.
- Mất mạng giữ QR/thông tin đã tải và báo chưa kiểm tra được; nút “Kiểm tra thanh toán” thử lại. Không tự xác nhận đã trả bằng nút bấm hay URL.
- Đơn hủy/quá hạn không hiện QR dùng được. COD giữ màn đặt đơn thành công, không tạo link payOS.
- Lưu QR mới ở %LOCALAPPDATA%/PhotocopyChain/PayOSQr/don-<mã đơn>.json để mở lại sau khi API khởi động lại. Đây chỉ là dữ liệu hiển thị, không phải bằng chứng nhận tiền; file không chứa API key.
- Không thêm/sửa bảng, cột, trigger hoặc dữ liệu SQL; không thay luồng webhook, quyền truy cập, xếp in hay COD.

## File thay đổi trong lượt này

| File tính từ MaNguon | Nội dung |
| --- | --- |
| PhotocopyChain/PhotocopyShared/Dtos/DonHang/DonHangDtos.cs | Bổ sung trường QR/thông tin nhận tiền vào LienKetPayOSDto; giữ các trường cũ cho Web. |
| PhotocopyChain/PhotocopyApi/ThanhToan/QuyTacPayOS.cs | Đối chiếu orderCode/số tiền trước khi trả QR mới. |
| PhotocopyChain/PhotocopyApi/ThanhToan/DuLieuQrPayOSStore.cs | File mới: lưu JSON atomic, đọc lại khi khớp link/mã đơn/số tiền, bỏ qua cache thiếu/hỏng. |
| PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs | POST payos trả thêm QR, lấy QR cache cho link đã có; giữ kiểm tra quyền, hủy và hạn thanh toán. |
| PhotocopyChain/PhotocopyApi/Program.cs | Đăng ký nơi lưu QR ngoài source. |
| photocopy_mobile/lib/models.dart | Đọc các trường mới trong LienKetPayOS. |
| photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart | QR trong app, copy, kiểm tra thủ công, resume, giữ màn khi lỗi mạng, chặn QR sai đơn/tiền và QR hết hạn. |
| photocopy_mobile/pubspec.yaml + pubspec.lock | Thêm qr_flutter 4.1.0 và dependency qr; không nâng các thư viện khác. |
| PhotocopyChain/PhotocopyApi.Tests/DuLieuQrPayOSTests.cs | 8 kiểm thử mapping/cache. |
| PhotocopyChain/PhotocopyApi.Tests/QrPayOSControllerTests.cs | 2 kiểm thử SDK thật với HTTP giả: tạo/mở lại sau restart và link cũ thiếu cache. |
| photocopy_mobile/test/thanh_toan_qr_test.dart | 13 kiểm thử màn QR, trạng thái, vòng đời, lỗi mạng, COD, chữ lớn và request chậm. |
| PhotocopyChain/docs/plans/2026-10-12-qr-trong-app.md | Phạm vi đã chốt, quyết định và kế hoạch kiểm thử. |

## Cách chạy và thử trên điện thoại

1. Chạy lại MaNguon/scripts/chay_web.cmd để API/Web/máy in dùng bản code mới. API đang chạy trước đó không tự nhận DLL đã build ở thư mục kiểm thử tạm.
2. Android Studio: mở MaNguon/photocopy_mobile, Stop app cũ rồi Run lại lên điện thoại.
3. Nếu dùng API mặc định localhost:5268, bảo đảm thiết bị ADB đã kết nối và chạy adb reverse tcp:5268 tcp:5268. Nếu có nhiều thiết bị, dùng adb -s <serial> reverse tcp:5268 tcp:5268.
4. Đặt một đơn VietQR mới. App phải hiện QR và đúng số tiền/nội dung của đơn, chưa báo thành công.
5. Chụp màn hình QR; trong app ngân hàng chọn quét từ ảnh, kiểm tra người nhận và số tiền rồi thực hiện chuyển khoản. Nếu ngân hàng không hỗ trợ quét từ ảnh, có thể quét màn hình bằng thiết bị khác.
6. Quay lại PhotocopyChain. App đối soát với API; chỉ chuyển sang thành công khi máy chủ xác nhận. Nếu mạng/bank phản hồi chậm, giữ màn chờ và dùng “Kiểm tra thanh toán”.
7. Bấm “Theo dõi tiến độ đơn” để xem trạng thái xử lý/in do backend hiện có trả về.

APK debug ARM64 đã build: MaNguon/photocopy_mobile/build/app/outputs/flutter-apk/app-debug.apk. Không tự cài lên điện thoại hay chuyển tiền trong lượt này.

## Giới hạn cần biết

- Các link được tạo trước bản sửa này chưa lưu QR sẽ hiện thông báo và nút checkout payOS dự phòng. Không tự tạo QR chuyển khoản khác hoặc tạo link trùng. Hãy dùng đơn mới để thử QR trong app.
- SDK payOS 2.1.0 đang dùng có qrCode ở phản hồi tạo link, nhưng phản hồi lấy link cũ không có trường này; đã kiểm tra trực tiếp SDK và [API payOS](https://payos.vn/docs/api/).
- API vẫn cần cấu hình payOS và URL HTTPS công khai hợp lệ như trước. Native QR không thay thế cấu hình webhook/tunnel. Khi tunnel đổi, vẫn cần cập nhật cấu hình đang dùng.
- Không tự mở/chuyển tiền trong app ngân hàng và không tự đánh dấu đã trả nếu chỉ thấy người dùng quay lại app.
- Kiểm thử tiền thật trên thiết bị cần người dùng thực hiện; các kiểm thử tự động dùng HTTP/dữ liệu giả, không chứng minh tài khoản đã nhận một giao dịch thật.

## Kết quả kiểm tra

- TDD: test backend/mobile thất bại đúng phần thiếu trước khi triển khai, sau đó chạy lại thành công.
- Flutter: 29/29 test toàn app qua, gồm 13 test mới. Bố cục kiểm tra ở 320/390dp và chữ 150% tại 320dp; đã xem ảnh render QR từ dữ liệu giả.
- flutter analyze --no-fatal-infos: không lỗi/cảnh báo; còn 3 info thiếu dấu ngoặc ở cau_hinh_in_screen.dart đã có trước lượt này.
- flutter build apk --debug --target-platform android-arm64 --no-pub: thành công, dùng JBR của Android Studio.
- .NET: 116/116 test không phụ thuộc DB qua, gồm 10 test mới; đồng thời compile API, Admin, Shared, PrinterSimulator thành công. Loại 10 test phụ thuộc CSDL seed trong CaVaPhieuNhapCSDLTests/PhamViCSDLTests để không tác động dữ liệu thật.
- Build/test .NET dùng artifacts ở thư mục tạm riêng, không đè DLL của API đang chạy. Không thử trả tiền thật/đặt đơn thật.

### Lệnh kiểm thử lại

Tại MaNguon/photocopy_mobile:

```powershell
flutter test
flutter analyze --no-fatal-infos
$env:JAVA_HOME = 'C:\Program Files\Android\Android Studio\jbr'
flutter build apk --debug --target-platform android-arm64 --no-pub
```

Tại MaNguon/PhotocopyChain:

```powershell
dotnet test PhotocopyApi.Tests/PhotocopyApi.Tests.csproj --filter 'FullyQualifiedName!~CaVaPhieuNhapCSDLTests&FullyQualifiedName!~PhamViCSDLTests' --artifacts-path "$env:TEMP\PhotocopyQrBuild-20261012"
```

Xuất ảnh UI test (QR giả, không dùng thanh toán):

```powershell
$env:PHOTOCOPY_QR_PREVIEW = "$env:TEMP\PhotocopyQrPreview-20261012.png"
flutter test test/thanh_toan_qr_test.dart
```
