# Bàn giao giao hàng nội bộ — PhotocopyChain

Cập nhật: **14/10/2026**. Phạm vi: giao hàng bằng nhân viên của cửa hàng, khách trả QR trước; không đặt tài xế bên ngoài.

## 1. Đã triển khai

- Bỏ Ahamove khỏi code/cấu hình runtime; giữ nguyên các migration cũ để không làm mất lịch sử.
- App cho chọn **Giao tận nơi**, bấm lấy GPS hoặc tìm địa chỉ thủ công. Khách phải chọn một kết quả địa chỉ, có ô số nhà/tầng/phòng và ghi chú giao riêng.
- Quyền vị trí chỉ xin khi bấm GPS; không theo dõi nền. Từ chối quyền, tắt GPS, vị trí không chính xác hoặc mất mạng đều có thông báo và đường nhập thủ công.
- API gọi Geoapify để tìm địa chỉ và tính **tuyến xe máy theo đường đi**, kiểm tra phạm vi giao của chi nhánh rồi tính phí cửa hàng. Không dùng khoảng cách đường chim bay hoặc giả phí 0 khi bản đồ lỗi.
- Điểm nhận được server cấp mã xác nhận 30 phút; báo giá phí được giữ 15 phút, gắn đúng khách, chi nhánh, địa chỉ đầy đủ và tọa độ. Sửa địa chỉ/GPS làm mất báo giá cũ ngay. Có nút **Tính lại phí**.
- Web có **Vận hành → Giao hàng nội bộ**: chờ giao, đang giao, lịch sử; tìm kiếm, xem địa chỉ/lượt/lý do và bàn giao cho shipper cùng chi nhánh.
- App shipper có nhận tài liệu, gọi khách, mở đường đi, ghi nhận giao thành công/thất bại và xem lịch sử của mình.
- Khóa đơn bằng giao dịch SQL khi nhận, kiểm tra quyền/phạm vi và trạng thái đã trả tiền; cùng đơn không được nhận bởi hai người. Gửi lại cùng kết quả không tạo thêm lượt/thông báo.
- Cập nhật trạng thái, thông báo cho khách/quản lý và SignalR sau khi database đã lưu. Realtime lỗi không biến một thao tác đã lưu thành thất bại; vẫn tải lại được.
- Giữ giao diện navy hiện tại, bổ sung CSS responsive, không đổi thiết kế các phân hệ khác.

**Không thêm bảng/cột, không chạy migration, không xóa/sửa đơn cũ hay mật khẩu.** Dùng bảng `GiaoHang`, `DonHang`, `ThanhToan`, `ThongBao` hiện có. Thủ tục giao hàng cũ vẫn giữ cho tương thích; API mới xử lý giao dịch/khóa đơn trực tiếp.

## 2. Cách hoạt động

1. Khách đặt in → chọn chi nhánh → Giao tận nơi → xác nhận địa chỉ, chi tiết và SĐT 10 số.
2. API tính tuyến/phí → app hiện phí giao, tổng tiền và số phút **di chuyển ước tính**. Giờ in xong khác giờ giao đến; chưa cam kết SLA giao hàng.
3. Khách thanh toán QR toàn bộ tiền in + giao. Thanh toán vẫn do payOS xác minh theo luồng hiện có.
4. Tài liệu in xong (`DA_IN`) mới vào Chờ giao.
5. Quầy chọn shipper và **xác nhận đã bàn giao tài liệu**, hoặc shipper tự xác nhận đã nhận tại chi nhánh → `DANG_GIAO`.
6. Shipper giao thành công → lượt `THANH_CONG`, đơn `HOAN_THANH`. Giao thất bại → lượt `THAT_BAI`, đơn `GIAO_THAT_BAI`; lý do bắt buộc từ 5–255 ký tự.
7. Giao lại tối đa 3 lượt. Sau lượt thứ ba, hiện cảnh báo để cửa hàng liên hệ khách; không tự tạo lượt thứ tư.

**Không thu COD:** mọi lượt giao mới ghi `TienThu=0`; không cộng lại số tiền khách đã trả vào tiền COD của shipper.

Việc đổi đơn giao thất bại thành nhận tại quầy, giữ/hoàn phí giao **chưa áp dụng tự động**, vì cần chốt chính sách với khách. Không tự hủy hay tự hoàn tiền.

## 3. Phí giao và căn cứ

Đây là **giá đề xuất của cửa hàng**, không phải báo giá trực tiếp của hãng, giá trung bình toàn Việt Nam hay thu nhập thực nhận của tài xế.

```text
Phí = làm tròn lên 1.000đ của:
      15.000đ + max(0, quãng đường − 2 km) × 4.000đ/km
```

| Tuyến | Phí |
|---|---:|
| 0–2 km | 15.000đ |
| 2,01 km | 16.000đ |
| 3 km | 19.000đ |
| 5 km | 27.000đ |
| 10 km | 47.000đ |

Chỉ giao trong phạm vi chi nhánh; bán kính kiểm tra trước khi làm tròn khoảng cách. Không tự cộng phụ phí mưa, tối, giờ cao điểm. Giá đề xuất ngày 13/10, nguồn được kiểm tra lại ngày **14/10/2026**:

- [Ahamove – dịch vụ xe máy](https://ahamove.com/service/aha-delivery): trang HCM công bố Nhanh 13.745đ/2km đầu, Siêu Tốc 15.709đ/2km đầu; chỉ dùng tham khảo giá, **không còn gọi Ahamove API**.
- [Lalamove – bảng giá](https://www.lalamove.com/vi-vn/bang-gia-giao-hang): dùng đối chiếu cấu trúc giá theo phương tiện/khu vực/quãng đường; giá thực tế còn tùy tuyến và phụ phí.
- [Geoapify Routing](https://apidocs.geoapify.com/docs/routing/): chế độ `scooter`; trả khoảng cách và thời gian tuyến. Thời gian không phải GPS trực tiếp của tài xế, không chứng minh đã giao hàng.

## 4. Cấu hình Geoapify

File riêng:

[PhotocopyChain/.env](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/.env)

Các dòng liên quan; **không copy key thật vào báo cáo/chat/Git**:

```dotenv
GiaoHang__GeoapifyApiKey=DIEN_KEY_RIENG_TREN_MAY
GiaoHang__PhiCoBan=15000
GiaoHang__KmCoBan=2
GiaoHang__PhiMoiKm=4000
GiaoHang__LamTronDen=1000
GiaoHang__GioiHanCreditsNgay=2800
```

Key trên máy đã được điền và đã gọi thử tìm địa chỉ, reverse geocoding, routing thành công. Key chỉ đọc trong API, không đưa vào app/Web.

Nếu cần tạo key mới:

1. Vào [Geoapify MyProjects](https://myprojects.geoapify.com), đăng ký/đăng nhập tài khoản.
2. Tạo project PhotocopyChain → API Keys → lấy key.
3. Điền kín vào dòng tương ứng của `.env`; không tạo dòng trùng.
4. Chạy lại API để nạp cấu hình. Nếu giới hạn key, nhớ request xuất phát từ backend; giới hạn chỉ cho website/referrer có thể chặn backend.

[Gói Geoapify Free](https://www.geoapify.com/pricing/) hiện có **3.000 credits/ngày, tối đa 5 request/giây**, không cần thẻ; có yêu cầu attribution. App đã có liên kết Geoapify/OpenStreetMap cạnh thông tin địa chỉ.

Backend cache kết quả 10 phút, giãn lượt gọi khoảng 250ms, tự chặn ở ngưỡng cấu hình mặc định 2.800. **Bộ đếm trong RAM, khởi động API lại sẽ mất; không thay thế thống kê tài khoản Geoapify** và không tính được request của ứng dụng khác dùng chung key. Không bảo đảm miễn phí vô hạn. Khi vượt hạn mức/lỗi dịch vụ, chọn nhận tại quầy hoặc thử lại; không tự chuyển sang gói trả phí.

## 5. Chạy và thử trên điện thoại

1. Chạy [chay_web.cmd](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/scripts/chay_web.cmd) để build/chạy backend, Web và các thành phần hiện có.
2. Web vào [Giao hàng nội bộ](http://localhost:5064/giao-hang) bằng tài khoản CEO/quản lý/nhân viên đúng quyền.
3. Android cần **build/cài lại app** vì thêm quyền GPS và plugin; hot reload không cập nhật AndroidManifest.
4. Trong Android Studio mở project `photocopy_mobile`, chọn điện thoại rồi Run. Hoặc dùng PowerShell:

```powershell
Set-Location -LiteralPath 'C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile'
C:/SDK/flutter/bin/flutter.bat pub get
C:/SDK/flutter/bin/flutter.bat run
```

API mặc định `http://localhost:5268`; điện thoại USB/wireless ADB cần reverse cổng 5268. Launcher hiện có cơ chế giữ kết nối; xem [hướng dẫn Android](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/docs/SUA_KET_NOI_ANDROID.md) nếu máy mất kết nối. Launcher **không tự cài bản APK mới**.

Điều kiện trước khi thử: chi nhánh hoạt động có tọa độ/bán kính giao và máy phù hợp; shipper đang hoạt động thuộc chi nhánh; file sẵn sàng. Quản lý chỉ được giao cho shipper của chi nhánh giữ tài liệu.

## 6. File đã thêm/sửa

Đường dẫn dưới đây tính từ thư mục dự án `CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD`; dấu `…/` thay cho `MaNguon/PhotocopyChain/` ở nhóm API/DTO/Web, và `MaNguon/photocopy_mobile/` ở nhóm Mobile/Android.

| Nhóm | File | Nội dung |
|---|---|---|
| API mới | `MaNguon/PhotocopyChain/PhotocopyApi/GiaoHang/GeoapifyService.cs` | Địa chỉ, tuyến, cache, hạn mức, mã điểm nhận/báo giá |
| API mới | `…/PhotocopyApi/GiaoHang/QuyTacGiaoHang.cs` | Phí, điều kiện nhận, ghép địa chỉ |
| API mới | `…/PhotocopyApi/Controllers/GiaoHangController.cs` | Danh sách, shipper, nhận/phân công, kết quả, giao dịch SQL/SignalR |
| API sửa | `…/PhotocopyApi/Controllers/BaoGiaController.cs`, `DonHangController.cs` | Bỏ helper Ahamove, dùng dịch vụ chung và snapshot phí |
| API sửa | `…/PhotocopyApi/Program.cs`, `appsettings.json`, `Models/KetQuaPhiGiaoHang.cs`, `.env` | Đăng ký dịch vụ/cấu hình/tên mô tả |
| DTO | `…/PhotocopyShared/Dtos/GiaoHang/GiaoHangDtos.cs` | Request/response giao hàng và validation |
| DTO sửa | `…/PhotocopyShared/Dtos/DatIn/DatInDtos.cs`, `DonHang/DonHangDtos.cs` | Mã điểm nhận/báo giá, tọa độ, chi tiết/ghi chú |
| Web mới | `…/PhotocopyAdmin/Pages/GiaoHang.razor`, `GiaoHang.razor.css` | Trang điều phối; bảng thành card trên màn nhỏ |
| Web sửa | `…/PhotocopyAdmin/Layout/MainLayout.razor`, `wwwroot/index.html` | Menu/header route và tải scoped CSS |
| Mobile mới | `MaNguon/photocopy_mobile/lib/giao_hang.dart`, `dich_vu_vi_tri.dart`, `widgets/dia_chi_giao.dart` | Model giao hàng, GPS và chọn địa chỉ |
| Mobile sửa | `…/lib/screens/dat_in/chon_chi_nhanh_screen.dart`, `lib/models.dart` | Chọn giao hàng, phí và đặt đơn; tổng tiền không che nội dung |
| Mobile sửa | `…/lib/screens/shipper/shipper_screen.dart` | Thay placeholder bằng thao tác thật; dialog có lifecycle đúng |
| Mobile sửa | `…/lib/screens/toi/ho_tro_screen.dart` | Bỏ chữ Ahamove cũ |
| Android | `…/android/app/src/main/AndroidManifest.xml`, `pubspec.yaml`, `pubspec.lock` | Quyền vị trí foreground, geolocator 14.1.1 |
| Kiểm thử | `MaNguon/PhotocopyChain/PhotocopyApi.Tests/GiaoHang*Tests.cs`, `GeoapifyServiceTests.cs` | Phí, snapshot, vị trí, role/HTTP, render, SELECT DB và bản đồ thật |
| Kiểm thử | `MaNguon/photocopy_mobile/test/dia_chi_giao_test.dart`, `giao_hang_screen_test.dart`, `giao_hang_checkout_test.dart` | Quyền/lỗi/stale GPS, báo giá, shipper, responsive |
| Kiểm thử | `MaNguon/PhotocopyChain/verification/kiem_thu_giao_hang_web.cjs` | Chrome headless với API fixture, không ghi DB |
| Tài liệu | `docs/plans/2026-10-13-giao-hang-noi-bo.md`, file này | Quyết định, kế hoạch, cấu hình và nghiệm thu |

Các thay đổi launcher/kiểm thử kết nối Android đã có trước lượt này được giữ nguyên; không tự commit.

## 7. Kết quả kiểm thử 14/10/2026

| Kiểm tra | Kết quả / giới hạn |
|---|---|
| .NET toàn bộ suite | **392 passed, 20 skipped, 0 failed**; các bài cần dữ liệu/config riêng không tự bật |
| .NET opt-in giao hàng đọc CSDL + Geoapify thật | **2 passed**; SELECT theo tất cả/từng chi nhánh; tìm, reverse và tuyến thật; không ghi đơn |
| .NET opt-in giao hàng ghi CSDL, được người dùng cho phép | **1 passed**; tạo 4 đơn test có dấu GUID riêng, chặn đơn chưa trả/chưa in/sai chi nhánh, nhận đồng thời không trùng, thành công/lặp, 3 thất bại, không thu COD; đã dọn đúng bản ghi test |
| Flutter analyze | Không có lỗi/cảnh báo |
| Flutter toàn bộ suite | **107 passed** |
| Build .NET solution | Thành công, 0 warning / 0 error; đã cập nhật output build thông thường để launcher dùng |
| Android debug APK | Build thành công; plugin geolocator có cảnh báo Java 8/deprecated API, không chặn build |
| Web publish + Chrome | PASS ở 1366/1024/768/390/320px, không tràn ngang; xác nhận 2 bước, nhóm/tìm kiếm, lỗi/thử lại, HTML escape; không lỗi JavaScript |
| Phân quyền HTTP | Chặn anonymous, sai role, nhân viên không có chi nhánh; validation chạy trước database |
| Lỗi đã phát hiện và sửa | CSS scoped chưa tải; giữ điểm nhận cũ khi GPS mới; tràn tổng tiền/tên chi nhánh; dialog dùng controller đã dispose; dữ liệu bản đồ sai gây lỗi không thân thiện |

Quy trình test-first đã giúp sửa các lỗi trên trước khi bàn giao. Các test HTTP/fixture không phải bằng chứng đã giao hàng ngoài đời.

Kiểm thử ghi gọi controller thật và transaction SQL thật, không gọi payOS/máy in và không phát SignalR tới người dùng thật. Các dòng thanh toán test chỉ là fixture trong database, không phải xác nhận tiền ngân hàng. Đã xóa 4 đơn test và các lượt giao, thanh toán giả, thông báo đi kèm; giữ nguyên đơn/tài khoản/tồn kho cũ. Không reset identity, nên mã đơn có thể có khoảng trống sau kiểm thử — đây là hành vi bình thường.

## 8. Cần nghiệm thu thêm, không đánh đồng với build thành công

- Cài APK mới trên điện thoại thật, thử bật/tắt GPS, từ chối quyền, vị trí trong/ngoài phạm vi, font lớn và mất mạng. Lúc kiểm tra ADB chưa thấy thiết bị.
- Luồng ghi nhận/phân công/kết quả và nhận đồng thời đã chạy trên đơn test riêng có phép. Cần thử thêm thao tác qua Web/app đang chạy cùng lúc, realtime và khôi phục kết nối trên điện thoại thật; không thay thế kiểm thử nghiệp vụ bằng việc chỉ build thành công.
- Thử QR thật trong app mới → in xong → shipper nhận → giao → khách nhận realtime. Không phát sinh thanh toán thật trong bộ test này.
- Không có tracking vị trí shipper nền, tối ưu nhiều điểm, ảnh/chữ ký chứng minh bàn giao, hay xác thực người nhận bằng OTP. Nút thành công là xác nhận của shipper, không phải chứng cứ độc lập.
- Sau 3 thất bại: chưa tự đổi sang nhận tại quầy/hoàn phí; chưa có thao tác thu hồi một lượt đang giao để phân cho người khác. Cửa hàng cần thống nhất chính sách trước khi bổ sung.
- Danh sách giới hạn 100 đơn/lượt, chưa có phân trang lịch sử dài; cache/mã báo giá không dùng chung nhiều API instance. Phù hợp đồ án một backend, không coi là kiến trúc vận tải quy mô lớn.
