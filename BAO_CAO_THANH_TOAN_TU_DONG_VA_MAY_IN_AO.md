# Báo cáo thanh toán tự động và máy in ảo

Ngày hoàn thiện: 06/10/2026

## 1. Kết quả

- Thanh toán VietQR từ Web tại quầy và App mobile đều dùng payOS thật.
- Webhook hợp lệ hoặc đối soát trực tiếp với API payOS sẽ ghi `DA_THANH_TOAN`.
- Đơn đã trả tiền được kiểm tra file, tự chọn máy phù hợp trong đúng chi nhánh và chuyển sang `DANG_IN`.
- Máy in ảo là chương trình .NET chạy riêng, tải file qua API, tính thời gian theo số trang, số bản, khổ giấy, in hai mặt và tốc độ từng mẫu máy.
- Chỉ callback hoàn tất hợp lệ từ máy đã được phân công mới chuyển `DANG_IN -> DA_IN`.
- COD không tự đưa vào máy in ngay vì chưa có tín hiệu xác nhận đã thu tiền. Nhân viên tiếp tục xử lý theo quy trình tại quầy/giao hàng.
- Không thêm bảng hoặc cột database. Hệ thống tái sử dụng `DonHang.MaMayIn`, trạng thái đơn, `MayIn`, `FileIn`, `ChiTietDonHang` và các stored procedure cũ.

## 2. Luồng hoạt động

```text
Ngân hàng -> payOS -> webhook/đối soát API
                         |
                         v
                 DA_THANH_TOAN
                         |
             kiểm tra file + chọn máy
                         |
                         v
                      DANG_IN
                         |
              Printer Simulator/Connector
        tải file -> tính thời gian -> in xong
                         |
                         v
                       DA_IN
```

Nếu không có máy phù hợp hoặc file không còn tồn tại, tiền vẫn được ghi nhận nhưng đơn dừng ở `DA_XAC_NHAN` để nhân viên xử lý. Hệ thống không giả lập rằng tài liệu đã được in.

## 3. Máy in và thời gian mô phỏng

Thông số hiện tại:

| Mã | Mẫu máy | Khả năng | Tốc độ chính |
|---:|---|---|---:|
| 1 | Ricoh MP 2001L | A3, đen trắng, hai mặt | 20 trang/phút A4 |
| 2 | Canon imageRUNNER ADVANCE C3525i | A3, màu, hai mặt | 25 trang/phút A4 |
| 3 | Xerox WorkCentre 3345 | A4, đen trắng, hai mặt | 40 trang/phút A4 |
| 4 | HP LaserJet Pro M404dn | A4, đen trắng, hai mặt | 38 trang/phút A4 |

Công thức mô phỏng: `thời gian trang đầu + tổng(số trang × số bản × 60 / tốc độ phù hợp)`. Tiến trình đang in được lưu tại `%LOCALAPPDATA%\PhotocopyChain\PrinterSimulator`, nên khởi động lại simulator vẫn tiếp tục công việc chưa xong.

## 4. Khả năng thay máy thật

API giữa hệ thống và máy in đã được tách khỏi simulator. Khi dùng máy vật lý, giữ nguyên các endpoint nhận lệnh, tải file, heartbeat, báo lỗi và hoàn tất; chỉ thay phần `Task.Delay` trong connector bằng Windows Print Queue hoặc IPP và chỉ callback khi hàng đợi máy thật báo hoàn thành.

## 5. Bảo mật

- Khóa payOS và khóa `PrinterConnector:ApiKey` chỉ lưu trong .NET User Secrets.
- Connector gửi khóa qua header `X-Printer-Api-Key`; API so sánh theo thời gian cố định.
- File in chỉ tải được khi đơn đang `DANG_IN` và request có khóa connector hợp lệ.
- Query string `status=PAID` trên URL quay về không xác nhận tiền.
- API đối chiếu mã đơn, số tiền, tiền đã nhận và trạng thái `Paid` trực tiếp với payOS.

## 6. Cách chạy

Cách nhanh nhất: nhấp đúp `MaNguon\scripts\chay_web.cmd`. Script mở API, Web Admin và Printer Simulator.

Để nhận webhook payOS từ Internet, mở thêm PowerShell:

```powershell
& "$env:USERPROFILE\Desktop\cloudflared.exe" tunnel --url http://localhost:5268 --no-autoupdate
```

Sau mỗi lần Quick Tunnel đổi URL, cập nhật User Secrets trong `MaNguon\PhotocopyChain\PhotocopyApi`, khởi động lại API rồi đăng ký lại webhook:

```powershell
dotnet user-secrets set "PayOS:PublicBaseUrl" "https://<URL-MOI>.trycloudflare.com"
dotnet run --no-build -- --dang-ky-webhook-payos
```

## 7. File và dòng thay đổi

Số dòng tính theo source tại ngày hoàn thiện. File ghi “toàn file” là file mới của tính năng.

| File | Dòng | Nội dung |
|---|---:|---|
| `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs` | 31–50, 57–157, 169–286, 312–410 | Tạo link theo kênh, điều hướng Web, webhook, đối soát dự phòng và gọi tự động in. |
| `MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs` | 1–194, toàn file | Kiểm tra file, chọn máy cùng chi nhánh, phân công và chuyển `DANG_IN`. |
| `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/MayInAoController.cs` | 1–239, toàn file | API heartbeat, nhận lệnh, tải file, báo hoàn tất/lỗi và trạng thái connector. |
| `MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TrangThaiMayInAoStore.cs` | 1–38, toàn file | Bộ nhớ heartbeat và trạng thái máy. |
| `MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs` | 6–8, 80–101 | Giữ file đơn VietQR tự hủy thêm 24 giờ để đối soát webhook đến muộn. |
| `MaNguon/PhotocopyChain/PhotocopyApi/Program.cs` | 8, 56–58 | Đăng ký dịch vụ máy in tự động. |
| `MaNguon/PhotocopyChain/PhotocopyApi/appsettings.json` | 35–44 | URL Web Admin, placeholder khóa connector và URL công khai payOS. |
| `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/HangDoiDon.razor` | 349–381 | Nhận mã đơn từ URL quay về và tự mở đúng chi tiết đơn. |
| `MaNguon/PhotocopyChain/PhotocopyShared/Dtos/MayInAo/MayInAoDtos.cs` | 1–53, toàn file | DTO lệnh in, cấu hình tốc độ, heartbeat và callback. |
| `MaNguon/PhotocopyChain/PhotocopyShared/MayInAo/QuyTacMayInAo.cs` | 1–56, toàn file | Kiểm tra file/quy cách và tính thời gian in. |
| `MaNguon/PhotocopyChain/PhotocopyPrinterSimulator/Program.cs` | 1–29, toàn file | Khởi động simulator từ mọi thư mục và kết nối API. |
| `MaNguon/PhotocopyChain/PhotocopyPrinterSimulator/CauHinhSimulator.cs` | 1–16, toàn file | Cấu hình simulator. |
| `MaNguon/PhotocopyChain/PhotocopyPrinterSimulator/MayInSimulator.cs` | 1–190, toàn file | Bốn vòng lặp máy, tải file, mô phỏng thời gian, lưu tiến trình và callback. |
| `MaNguon/PhotocopyChain/PhotocopyPrinterSimulator/appsettings.json` | 1–50, toàn file | Tốc độ và thời gian trang đầu của bốn mẫu máy. |
| `MaNguon/PhotocopyChain/PhotocopyPrinterSimulator/PhotocopyPrinterSimulator.csproj` | 1–22, toàn file | Project console .NET 8. |
| `MaNguon/PhotocopyChain/PhotocopyApi.Tests/QuyTacMayInAoTests.cs` | 1–86, toàn file | Test thời gian, khả năng máy và file sẵn sàng. |
| `MaNguon/PhotocopyChain/PhotocopyChain.sln` | 14–15, 39–42, 72–75 | Thêm simulator vào solution/build. |
| `MaNguon/scripts/chay_web.cmd` | 1–20, toàn file | Chạy API, Admin và Printer Simulator. |
| `Database/02_DuLieuMau.sql` | 29–33 | Đồng bộ hỗ trợ in hai mặt và tốc độ máy mẫu. |
| `Database/005-CapNhat_ThongSoMayIn.sql` | 1–26, toàn file | Cập nhật thông số máy trên database hiện tại, không đổi schema. |
| `docs/2026-10-06-payos-auto-print-design.md` | toàn file | Thiết kế đã được duyệt trước khi triển khai. |

## 8. Kiểm thử và trạng thái dữ liệu thật

- `dotnet build PhotocopyChain.sln --no-restore`: thành công, 0 lỗi, 0 cảnh báo.
- `dotnet test PhotocopyApi.Tests/PhotocopyApi.Tests.csproj --no-restore`: 41/41 test đạt.
- Simulator khởi động từ đúng câu lệnh trong `chay_web.cmd` và bốn máy đều kết nối API.
- URL kết quả payOS kênh Admin trả `302` về `http://localhost:5064/?maDonHang=6&thanhToan=thanh-cong`.
- Quick Tunnel mới trả HTTP 200 và webhook đã được payOS xác nhận.
- Đơn 6 đã được payOS đối soát thật là `DA_THANH_TOAN`. File của đơn đã bị job phiên bản cũ xóa trước khi bản sửa được cài, nên dữ liệu đã được đưa về `DA_XAC_NHAN`, bỏ phân máy và hoàn vật tư; cần tải lại file để in. Không tạo file giả và không đánh dấu `DA_IN` giả.
