# Báo cáo tích hợp thanh toán payOS

Ngày hoàn thiện: 06/10/2026 · cập nhật 08/10/2026

## 1. Kết quả

Hệ thống đã tích hợp thanh toán thật qua payOS theo phương án A:

- Mỗi đơn `VIETQR` có một link checkout payOS riêng.
- App mobile có nút mở trang thanh toán payOS chính thức bằng trình duyệt/ứng dụng bên ngoài.
- Web quản trị tại màn **Lập đơn tại quầy** cũng tạo và mở mã QR payOS chính thức; không còn dùng QR demo.
- CEO đang chọn chi nhánh, quản lý và nhân viên đều có thể đọc trạng thái và tạo link payOS cho đơn tại quầy.
- Tài khoản `STAFF` đăng nhập Web sẽ tự mở ca với tiền đầu ca `0`; đăng nhập lại tiếp tục dùng ca đang mở, không tạo trùng.
- payOS nhận thông báo từ ngân hàng và gửi webhook về API.
- API chỉ xác nhận đơn sau khi kiểm tra chữ ký, mã đơn, phương thức, tiền tệ, nội dung, mã giao dịch và số tiền.
- Khi hợp lệ, API gọi thủ tục cũ `sp_XacNhanChuyenKhoan`, sau đó báo real-time cho App và chi nhánh.
- Sau khi nhận đủ tiền, API tự chọn máy phù hợp trong đúng chi nhánh và chuyển đơn sang `DANG_IN`.
- Chương trình máy in ảo tải file, tính thời gian theo số trang/số bản/tốc độ máy và chỉ chuyển đơn sang `DA_IN` khi hết thời gian in.
- Nếu webhook bị lỡ, `GET /api/thanh-toan/{maDonHang}` đối soát trực tiếp với payOS; không tin query string của trang quay về.
- Đơn COD vẫn theo quy trình nhân viên xác nhận vì hệ thống chưa nhận được tiền ở thời điểm tạo đơn.
- Migration `003` tạo bảng giá riêng theo chi nhánh, đưa khuyến mãi về đúng chi nhánh và cập nhật thủ tục tính tiền/tạo đơn.
- Không lưu khóa payOS trong source code.

Webhook phải được đăng ký bằng URL Quick Tunnel đang chạy ở thời điểm kiểm tra:

```text
https://<URL-TUNNEL-HIEN-TAI>.trycloudflare.com/api/thanh-toan/webhook
```

Lưu ý: đây là Cloudflare Quick Tunnel nên URL sẽ đổi khi tắt và chạy lại `cloudflared`.

## 2. Luồng hoạt động

1. Khách tạo đơn `VIETQR` trên App, hoặc nhân viên tạo đơn `VIETQR` tại quầy.
2. App/Web gọi `GET /api/thanh-toan/{maDonHang}` để đọc trạng thái và hạn thanh toán.
3. App/Web gọi `POST /api/thanh-toan/{maDonHang}/payos`.
4. API kiểm tra người dùng có quyền xem đơn, đơn chưa trả, chưa hủy và chưa quá 15 phút.
5. API lấy link cũ theo `MaDonHang`, hoặc tạo link payOS mới với `orderCode = MaDonHang`.
6. App mở `checkoutUrl`; khách quét QR và chuyển khoản thật.
7. payOS gửi `POST /api/thanh-toan/webhook` sau khi ngân hàng ghi nhận tiền.
8. API xác minh webhook bằng `ChecksumKey`, đối chiếu với CSDL và gọi `sp_XacNhanChuyenKhoan`.
9. Trạng thái thanh toán thành `DA_THANH_TOAN`; API kiểm tra file, chọn máy cùng chi nhánh và chuyển đơn sang `DANG_IN`.
10. Máy in ảo tải file qua API bảo vệ bằng khóa connector, chờ theo tốc độ máy rồi báo hoàn thành để đơn sang `DA_IN`.
11. SignalR báo trạng thái mới cho Web/App; App vẫn hỏi lại API mỗi 5 giây để dự phòng mất kết nối real-time.

Link tạo từ Web Admin dùng `returnUrl` và `cancelUrl` là `http://localhost:5064/`, vì vậy trình duyệt quay thẳng về trang chủ và không phụ thuộc Quick Tunnel. Link tạo từ mobile vẫn dùng trang `payos/ket-qua` công khai để hiển thị kết quả. Query string không được phép đổi trạng thái CSDL; chỉ webhook có chữ ký hoặc kết quả đối soát từ API chính thức của payOS mới được xác nhận thanh toán.

## 3. Các file và dòng đã thay đổi

Số dòng bên dưới tính theo phiên bản hiện tại sau khi hoàn thiện.

### Backend ASP.NET Core

- `MaNguon/PhotocopyChain/PhotocopyApi/PhotocopyApi.csproj`: dòng 17 thêm SDK `payOS` 2.1.0.
- `MaNguon/PhotocopyChain/PhotocopyApi/Program.cs`: dòng 1-26 đọc ba khóa từ User Secrets và đăng ký `PayOSClient`; dòng 167-181 thêm lệnh đăng ký webhook một lần.
- `MaNguon/PhotocopyChain/PhotocopyApi/appsettings.json`: dòng 35-40 tắt giả lập thanh toán và khai báo `PayOS:PublicBaseUrl` rỗng, không chứa bí mật.
- `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs`: dòng 1-50 thêm SDK/client; dòng 52-112 đọc thanh toán và tạo/lấy link, đồng thời cho phép CEO đang chọn chi nhánh sử dụng; dòng 114-139 trả trang kết quả; dòng 141-220 xác minh webhook; dòng 223-279 kiểm tra phạm vi đơn và làm việc với payment link; dòng 281-344 giữ luồng xác nhận CSDL/SignalR cũ.
- `MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/QuyTacPayOS.cs`: dòng 1-46 chứa các quy tắc ngắn và dễ kiểm thử; dòng 40-45 nhận diện đúng phản hồi payOS `HTTP 200 / ErrorCode 101` là chưa có link để tiếp tục tạo mới.
- `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/AuthController.cs`: dòng 99-121 tự mở ca khi tài khoản `STAFF` đăng nhập Web; lỗi mở ca được trả rõ ràng thay vì đăng nhập thành công nhưng không có ca.
- `MaNguon/PhotocopyChain/PhotocopyApi/CaLamViec/QuyTacCaDangNhap.cs`: dòng 1-11 quy định chỉ `STAFF` đăng nhập Web mới tự mở ca.
- `MaNguon/PhotocopyChain/PhotocopyShared/Dtos/DonHang/DonHangDtos.cs`: dòng 267-273 thêm DTO trả link checkout và hạn thanh toán.
- `MaNguon/PhotocopyChain/PhotocopyApi.Tests/QuyTacPayOSTests.cs`: kiểm thử mô tả đơn, URL HTTPS, checkout URL, đối chiếu tiền và các mã phản hồi chưa có link của payOS.
- `MaNguon/PhotocopyChain/PhotocopyApi.Tests/QuyTacCaDangNhapTests.cs`: dòng 1-24 kiểm thử tài khoản nào được tự mở ca.

### Flutter mobile

- `MaNguon/photocopy_mobile/pubspec.yaml`: dòng 44 thêm `url_launcher` 6.3.3.
- `MaNguon/photocopy_mobile/pubspec.lock`: dòng 616-679 khóa các gói `url_launcher` theo từng nền tảng.
- `MaNguon/photocopy_mobile/lib/models.dart`: dòng 478-489 thêm model `LienKetPayOS`.
- `MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart`: dòng 1-20 thêm thư viện/mô tả luồng; dòng 30-36 lưu trạng thái link; dòng 57-77 tải thanh toán; dòng 108-152 tạo và mở link; dòng 203-205 nhận diện COD; dòng 288-380 thay QR giả lập bằng nút payOS; dòng 387-425 hiển thị hoàn tất; dòng 448-490 giữ thẻ đơn và xử lý cả `COD`/`TIEN_MAT` cũ.
- `MaNguon/photocopy_mobile/test/lien_ket_payos_test.dart`: dòng 1-14, test parse JSON link payOS.
- `MaNguon/photocopy_mobile/android/gradlew.bat`: dòng 72-82 dùng classpath tương đối để wrapper không hỏng khi đường dẫn có tiếng Việt.
- `MaNguon/photocopy_mobile/android/gradle.properties`: dòng 1-4 dùng UTF-8 và tắt incremental Kotlin khi build qua ổ đĩa ánh xạ.

### Web quản trị

- `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/LapDonTaiQuay.razor`: dòng 8-37 thay QR demo bằng trạng thái và nút mở QR payOS; dòng 125 đổi tên phương thức hiển thị; dòng 195 lưu link payOS; dòng 393-438 tự tạo link sau khi lập đơn và cho phép kiểm tra lại trạng thái.
- `MaNguon/scripts/chay_web.cmd`: kiểm tra SQL Server, thử khởi động dịch vụ, chạy API trước và chờ cổng `5268` sẵn sàng rồi mới chạy Web Admin/máy in ảo; chỉ mở trình duyệt khi cổng `5064` đã sẵn sàng.

### Kế hoạch và CSDL

- `docs/plans/2026-10-06-tich-hop-payos.md`: dòng 1-59, kế hoạch triển khai và kiểm thử.
- `Database/003-CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql`: dòng 1-96 tạo/chuyển dữ liệu bảng giá và khuyến mãi theo chi nhánh; dòng 100-248 cập nhật `sp_TinhTien` và `sp_TaoDonHang`. Script đã được sửa lỗi chạy động ràng buộc tại dòng 50-59, chạy thử rollback rồi mới chạy thật.
- `Database/004-TuDongMoCaKhiNhanVienDangNhap.sql`: dòng 1-174 bật `QUOTED_IDENTIFIER ON`, sửa `sp_MoCa`, `sp_DongCa`, `sp_GiaoTaiQuay` và cho phép đăng nhập lại dùng đúng ca đang mở. Migration đã được chạy thật.
- `Database/005-CapNhat_ThongSoMayIn.sql`: cập nhật tốc độ, khổ giấy và khả năng in màu của các máy mẫu để thời gian máy in ảo sát dữ liệu cấu hình.
- `Database/006-SuaMaHoaThongBaoKhuyenMai.sql`: chỉ nạp lại `sp_TinhTien` bằng UTF-8 để sửa thông báo khuyến mãi bị lỗi dấu; không tạo bảng/cột và không sửa dữ liệu đơn hàng.
- `Database/04_StoredProcedure.sql`: phần đầu file đã bổ sung `ANSI_NULLS ON` và `QUOTED_IDENTIFIER ON`; `sp_MoCa` gốc cũng được đồng bộ quy tắc dùng lại ca để CSDL cài mới không gặp lỗi; webhook tiếp tục tái sử dụng `sp_XacNhanChuyenKhoan`.
- Hai file cập nhật rời cũ `CapNhat_CSDL_CEO_ChiNhanh.sql` và `CapNhat_CSDL_HienTai_Tuan3.sql` đã được bỏ vì nội dung đã nằm trong SQL gốc và CSDL hiện tại.

## 4. Cấu hình bí mật

Chỉ lưu giá trị thật trong .NET User Secrets. Không điền giá trị thật vào `appsettings.json`, file `.env`, source code, Git hoặc tài liệu này.

Chạy trong thư mục `MaNguon/PhotocopyChain/PhotocopyApi`:

```powershell
dotnet user-secrets set "PayOS:ClientId" "<CLIENT_ID_MOI>"
dotnet user-secrets set "PayOS:ApiKey" "<API_KEY_MOI>"
dotnet user-secrets set "PayOS:ChecksumKey" "<CHECKSUM_KEY_MOI>"
dotnet user-secrets set "PayOS:PublicBaseUrl" "https://<URL-TUNNEL-HIEN-TAI>"
```

Không chạy `dotnet user-secrets list` khi chia sẻ màn hình hoặc log vì lệnh đó in giá trị bí mật.

## 5. Cách chạy API, tunnel và webhook

### Terminal 1 - chạy API

```powershell
cd "C:\Users\USER\Desktop\Báo cáo tuần\CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD\MaNguon\PhotocopyChain\PhotocopyApi"
dotnet run
```

API phải hiện:

```text
Now listening on: http://localhost:5268
```

### Terminal 2 - mở HTTPS công khai

```powershell
& "$env:USERPROFILE\Desktop\cloudflared.exe" tunnel --url http://localhost:5268
```

Chép URL `https://....trycloudflare.com` mới, sau đó cập nhật User Secrets và khởi động lại API:

```powershell
dotnet user-secrets set "PayOS:PublicBaseUrl" "https://<URL-MOI>.trycloudflare.com"
```

### Terminal 3 - đăng ký lại webhook

Giữ Terminal 1 và Terminal 2 đang chạy. Trong thư mục API, chạy:

```powershell
dotnet run --no-build -- --dang-ky-webhook-payos
```

Kết quả đúng:

```text
Đã đăng ký webhook payOS: https://.../api/thanh-toan/webhook
```

payOS sẽ gửi một webhook mẫu. API có thể ghi log `mã đơn không tồn tại: 123`; đây là kết quả an toàn vì dữ liệu mẫu không được ghi vào CSDL.

## 6. Cách chạy App mobile

Thư mục cha có tên tiếng Việt. Android Gradle trên Windows không xử lý ổn định đường dẫn này, vì vậy hãy ánh xạ project sang ổ `P:`. Lệnh này không sao chép hay di chuyển source:

```powershell
subst P: "C:\Users\USER\Desktop\Báo cáo tuần\CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD\MaNguon\photocopy_mobile"
Set-Location P:\
$env:JAVA_HOME = "C:\Program Files\Java\jdk-17"
flutter pub get
flutter run
```

Nếu chạy trên điện thoại Android thật cắm USB, chạy thêm trước `flutter run`:

```powershell
adb reverse tcp:5268 tcp:5268
```

Khi không dùng nữa có thể gỡ ổ ánh xạ:

```powershell
Set-Location C:\
subst P: /d
```

APK debug đã build thành công tại:

```text
MaNguon/photocopy_mobile/build/app/outputs/flutter-apk/app-debug.apk
```

### Chạy Web quản trị

Nhấp đúp file sau:

```text
scripts/chay_web.cmd
```

File này kiểm tra SQL Server, mở API tại `http://localhost:5268`, đợi API sẵn sàng, sau đó mở Web quản trị tại `http://localhost:5064`, máy in ảo và trình duyệt. Nếu đã có một cửa sổ API/Web đang chạy, hãy đóng cửa sổ cũ trước để tránh báo trùng cổng. Không đóng máy in ảo nếu muốn đơn tự chuyển từ `DA_XAC_NHAN` sang `DANG_IN` rồi `DA_IN`.

## 7. Kiểm thử đã chạy

- `dotnet build`: thành công, 0 lỗi, 0 cảnh báo.
- `dotnet test` (08/10/2026): 64 test đạt, 6 test tích hợp CSDL được bỏ qua vì chưa cấp `PHOTOCOPY_TEST_DB`; không có test thất bại.
- Migration `003`: chạy thử trong transaction và rollback thành công, sau đó chạy thật thành công (`MIGRATION_EXIT=0`).
- CSDL sau migration: 32 dòng bảng giá chi nhánh, 2 dòng khuyến mãi và không có khuyến mãi thiếu mã chi nhánh.
- API bảng giá chi nhánh 1 sau migration: HTTP 200, trả 16 dịch vụ.
- Đơn tại quầy số 5: tạo link payOS thật thành công; gọi lại lấy đúng link cũ, cùng trả checkout trên `pay.payos.vn`.
- Migration `004`: `sp_MoCa`, `sp_DongCa`, `sp_GiaoTaiQuay` đều có `QUOTED_IDENTIFIER = ON`.
- Kiểm tra CSDL trong transaction: mở ca, mở lại nhận cùng mã ca, đóng ca thành `CHO_XAC_NHAN`, sau đó rollback sạch.
- `flutter test`: 13/13 test đạt.
- `flutter build apk --debug`: thành công.
- Endpoint kết quả qua localhost: HTTP 200.
- Endpoint kết quả qua Quick Tunnel: HTTP 200.
- SDK payOS đăng ký webhook thành công; API nhận và xác minh được webhook mẫu.
- `flutter analyze`: không có lỗi payOS; còn 3 thông báo `curly_braces_in_flow_control_structures` cũ tại `cau_hinh_in_screen.dart` dòng 144, 730 và 881. Không sửa vì ngoài phạm vi thanh toán.

## 8. Cách thử giao dịch thật

1. Giữ API và `cloudflared` đang chạy.
2. Tạo một đơn VietQR có số tiền nhỏ trên App hoặc tại Web **Lập đơn tại quầy**.
3. Trên App bấm `Mở trang thanh toán payOS`; trên Web bấm `Mở mã QR payOS`.
4. Dùng App MBBank quét QR trên trang payOS và xác nhận giao dịch.
5. Không sửa số tiền hoặc nội dung chuyển khoản.
6. Sau khi payOS xác nhận thành công, trình duyệt mở từ Web Admin phải quay thẳng về trang chủ `http://localhost:5064/`. Trạng thái thanh toán được cập nhật riêng qua webhook/SignalR, không lấy query string làm bằng chứng nhận tiền.
7. Với đơn mới, giữ API, Web và máy in ảo đang chạy. Đơn chỉ chuyển sang `DANG_IN` khi file PDF ở trạng thái sẵn sàng và có máy phù hợp đang kết nối; in xong mới chuyển sang `DA_IN`.
8. Kiểm tra `ThanhToan.TrangThai = 'DA_THANH_TOAN'`, `MaGiaoDich` có giá trị và `NgayThanhToan` đã được ghi.

Bước xác nhận chuyển tiền thật phải do chủ tài khoản ngân hàng thực hiện. Không dùng endpoint giả lập để nghiệm thu luồng thanh toán này.

## 9. Lỗi thường gặp

- `Thiếu cấu hình PayOS...`: kiểm tra lại ba User Secrets, không in giá trị ra màn hình.
- `Máy chủ chưa cấu hình địa chỉ HTTPS`: cập nhật `PayOS:PublicBaseUrl` bằng URL Quick Tunnel mới và khởi động lại API.
- payOS không gọi webhook: bảo đảm tunnel còn chạy và chạy lại lệnh `--dang-ky-webhook-payos`.
- Thanh toán thành công nhưng mở trang lỗi Cloudflare: QR/link cũ đã được tạo trước bản sửa bằng Quick Tunnel đã hết hiệu lực. URL của link payOS đã tạo không thể đổi hồi tố; hãy tự mở `http://localhost:5064/`, sau đó kiểm tra đơn đã nhận tiền. Các **đơn mới** tạo từ Web Admin sẽ quay thẳng về trang chủ và không còn phụ thuộc tunnel cho việc điều hướng.
- Trang quay về chứa `localhost:5064` chỉ mở được trên máy đang chạy Web Admin. Khi khách quét bằng điện thoại, điện thoại không thể mở `localhost` của máy tính; Web Admin vẫn nhận trạng thái riêng qua webhook/SignalR.
- Web báo `Chưa kết nối được payOS` nhưng log ghi `Mã thanh toán không tồn tại`: bản sửa hiện tại đã xử lý mã nghiệp vụ `101` để tự tạo link mới, không còn hiểu nhầm là mất kết nối.
- App không gọi được API: chạy `adb reverse tcp:5268 tcp:5268` hoặc truyền `--dart-define=API_BASE_URL=http://<IP-LAN>:5268`.
- Web báo `Không tải được bảng giá` kèm lỗi SQL `Invalid object name 'BangGiaChiNhanh'`: chạy `Database/003-CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql` trên CSDL cũ. CSDL hiện tại đã được chạy migration này.
- Mở ca báo `SET options have incorrect settings: 'QUOTED_IDENTIFIER'`: chạy `Database/004-TuDongMoCaKhiNhanVienDangNhap.sql`. CSDL hiện tại đã được chạy migration này.
- Thông báo mã giảm giá hiện ký tự như `MÃ£...`: chạy `sqlcmd` với tùy chọn UTF-8: `sqlcmd -S localhost\MSSQLSERVER01 -d PhotocopyChain -E -C -b -f 65001 -i "Database\006-SuaMaHoaThongBaoKhuyenMai.sql"`.
- Đơn đã thanh toán nhưng còn `Đã tiếp nhận`: mở chi tiết đơn ở **Hàng đợi đơn hàng** và xem khối **Điều kiện bắt đầu in tự động**. Cần đủ file PDF sẵn sàng và máy đúng loại đang kết nối; giữ `PhotocopyPrinterSimulator` chạy, hệ thống sẽ thử lại theo chu kỳ khoảng 10 giây.
- Gradle báo lỗi đường dẫn/XML dù XML hợp lệ: build từ `P:\` theo mục 6, không build trực tiếp từ thư mục có dấu.
