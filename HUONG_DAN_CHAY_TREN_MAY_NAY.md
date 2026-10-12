# HƯỚNG DẪN CHẠY PHOTOCOPYCHAIN TRÊN MÁY NÀY

## Chạy Web Admin

Nhấp đúp file:

```text
MaNguon\scripts\chay_web.cmd
```

Script kiểm tra dịch vụ SQL Server, chạy API trước và đợi cổng `5268` sẵn sàng, sau đó mở ba cửa sổ terminal:

- API: `http://localhost:5268/swagger`
- Web Admin: `http://localhost:5064`
- Máy in ảo: kết nối API và mô phỏng bốn máy in theo tốc độ thực tế

Web Admin sẽ tự mở khi cổng `5064` đã sẵn sàng. Không đóng ba cửa sổ terminal trong lúc sử dụng, đặc biệt là cửa sổ máy in ảo nếu muốn đơn tự chuyển sang `DANG_IN` và `DA_IN`. Muốn dừng hệ thống thì đóng cả ba cửa sổ.

Tài khoản dữ liệu mẫu:

```text
Email nhân viên: thoin1@photocopy.vn
Mật khẩu:        Demo@123
```

Có thể dùng tài khoản quản lý `qlcn1@photocopy.vn` hoặc Super Admin `ceo@photocopy.vn` với cùng mật khẩu mẫu.

### Cập nhật CSDL

Trước lần chạy đầu tiên của phiên bản này, mở SQL Server Management Studio và chạy một lần file:

```text
Database\003-CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql
Database\004-TuDongMoCaKhiNhanVienDangNhap.sql
Database\005-CapNhat_ThongSoMayIn.sql
Database\006-SuaMaHoaThongBaoKhuyenMai.sql
```

Bốn script này đã được chạy trên database hiện tại. Script `005` chỉ sửa dữ liệu thông số máy in. Script `006` chỉ nạp lại thủ tục `sp_TinhTien` bằng UTF-8 để sửa lỗi tiếng Việt, không tạo bảng/cột và không sửa dữ liệu đơn hàng.

## Chạy thủ công khi cần xem log rõ hơn

Mở ba terminal tại thư mục `MaNguon\PhotocopyChain`.

Terminal 1:

```powershell
dotnet run --project PhotocopyApi\PhotocopyApi.csproj --launch-profile http
```

Terminal 2:

```powershell
dotnet run --project PhotocopyAdmin\PhotocopyAdmin.csproj --launch-profile http
```

Terminal 3:

```powershell
dotnet run --project PhotocopyPrinterSimulator\PhotocopyPrinterSimulator.csproj
```

Máy in ảo và API dùng chung một khóa trong .NET User Secrets. Không ghi khóa thật vào source:

```powershell
cd MaNguon\PhotocopyChain\PhotocopyApi
dotnet user-secrets set "PrinterConnector:ApiKey" "<KHOA-NGAU-NHIEN-GIONG-NHAU>"

cd ..\PhotocopyPrinterSimulator
dotnet user-secrets set "PrinterConnector:ApiKey" "<KHOA-NGAU-NHIEN-GIONG-NHAU>"
```

Thanh toán payOS qua Internet vẫn cần mở `cloudflared` và đăng ký lại webhook theo file `HUONG_DAN_TICH_HOP_PAYOS.md`. Quick Tunnel đổi URL sau mỗi lần tắt.

## Chạy App Flutter trên điện thoại Android

1. Bật Developer options và USB debugging trên điện thoại.
2. Cắm cáp USB và chấp nhận hộp thoại cho phép gỡ lỗi.
3. Tại thư mục `MaNguon`, chạy:

```powershell
scripts\chay_app.cmd
```

Script tự dùng ADB tại Android SDK của tài khoản Windows và nối cổng `5268` từ điện thoại về API. API phải đang chạy trước.

Hiện tại chưa có điện thoại USB kết nối; có thể kiểm tra bằng:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" devices
```

## Cấu hình đã khớp với máy

- .NET SDK: dùng SDK 9.0.305 để build dự án target .NET 8.
- SQL Server: `localhost\MSSQLSERVER01`, database `PhotocopyChain`.
- Thư mục file in: `D:\PhotocopyData`.
- Chuyển Word/Excel/PowerPoint: dùng Microsoft Office đã cài trên máy.
- API và Web dùng HTTP local nên không cần chứng chỉ HTTPS development.

## Nếu không chạy được

Kiểm tra theo thứ tự:

```powershell
Get-Service 'MSSQL$MSSQLSERVER01'
dotnet --version
flutter doctor -v
```

SQL service `MSSQL$MSSQLSERVER01` phải ở trạng thái `Running`. Nếu script không tự khởi động được dịch vụ do thiếu quyền, hãy mở **SQL Server (MSSQLSERVER01)** trong Windows Services hoặc chạy `chay_web.cmd` bằng quyền Administrator. API phải chạy ở cổng `5268` trước khi mở Web hoặc App.

Nếu API báo `Cannot open database "PhotocopyChain"` hoặc mã SQL `4060`, API đang chạy với sai instance hoặc instance `MSSQLSERVER01` đang tắt. Hãy đóng ba cửa sổ cũ rồi chạy lại `chay_web.cmd` bằng quyền Administrator; không tạo database mới trên instance mặc định `MSSQLSERVER`.
