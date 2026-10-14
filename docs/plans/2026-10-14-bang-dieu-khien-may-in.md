# Bảng điều khiển máy in — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Điều khiển máy ảo trên web và gửi PDF tới máy thật USB/LAN đã cài driver Windows, không in lặp hoặc báo hoàn tất sai.

**Architecture:** Giữ API nhận lệnh/quyền connector hiện tại. Web nhân sự gọi API đã xác thực; connector tại chi nhánh nhận điều khiển, chạy mô phỏng hoặc gửi PDF qua SumatraPDF tới Windows Print Queue. Checkpoint ghi trước thao tác gửi; máy thật chờ kiểm tra bản giấy trước khi hoàn tất.

**Tech Stack:** .NET 8, Blazor WebAssembly, SQL Server hiện có, Winspool Windows, SumatraPDF tùy chọn. Không thêm package hoặc sửa schema.

## Phạm vi đã xác nhận

- Người dùng xác nhận ngày 14/10/2026: bảng điều khiển máy, tạm dừng/tiếp tục, mô phỏng hết giấy/kẹt giấy/offline/khôi phục, giữ thông số tốc độ.
- Máy thật đã cài driver và có tên queue Windows; USB và mạng dùng cùng pipeline. Không gửi PDF RAW trực tiếp ra cổng 9100 vì không phải máy nào cũng hiểu PDF.
- Phân quyền theo chi nhánh, không đưa khóa connector ra trình duyệt. Thanh toán và điều kiện nhận lệnh cũ giữ nguyên.
- Tiến độ máy ảo là mô phỏng. Máy thật chỉ hiển thị số liệu driver cung cấp, không dùng đồng hồ giả để kết luận in xong.
- Không có thiết bị thật được xác nhận; Spooler đang tắt. Không tự bật dịch vụ, cài driver hoặc gửi trang test.
- Hiệu năng: mỗi máy một worker, poll khoảng 1–2 giây, web 3 giây; xử lý lần lượt từng đơn/máy. Lỗi hoặc downtime không làm tăng tiến độ mô phỏng.
- Lệnh và điều khiển được lưu JSON riêng ngoài source; hỏng file phải dừng, không âm thầm tạo lệnh mới.

## Decision log

| Quyết định | Phương án khác | Lý do |
|---|---|---|
| Connector Windows tại chi nhánh | Backend ASP.NET trực tiếp in | USB thuộc máy tại tiệm; tách lỗi driver khỏi API |
| Queue Windows cho cả USB/LAN | IPP trực tiếp; RAW TCP/USB | Tận dụng driver đúng model, ít phụ thuộc giao thức riêng |
| SumatraPDF cấu hình tùy chọn | Tự viết bộ render PDF; gọi ứng dụng mặc định | Giữ dependency nhỏ, tham số in rõ ràng, không dùng shell interpolation |
| Xác nhận giấy cho máy thật | Tự hoàn tất khi process thoát/queue biến mất | Không chứng minh máy đã xuất đủ giấy; giữ an toàn khi driver thiếu phản hồi |
| Pause/Resume chỉ job tương ứng | Pause/Purge toàn queue | Không can thiệp lệnh ngoài hệ thống |
| Không có nút hủy/in lại tự động | Xóa rồi gửi lại job | Có thể in trùng, cần quy trình điều tra riêng |

## Thực hiện

### 1. Trạng thái và điều khiển

Files: Shared/Dtos/MayInAo/MayInAoDtos.cs; Api/MayInAo/TrangThaiMayInAoStore.cs; Api/Controllers/MayInAoController.cs; Api/Program.cs.

1. Viết test trạng thái tiến độ, điều khiển sai, xác nhận sai đơn/máy ảo, lưu điều khiển qua restart.
2. Chạy `dotnet test PhotocopyApi.Tests --artifacts-path <thư mục test riêng> --filter FullyQualifiedName~DieuKhienMayIn` và ghi nhận RED.
3. Thêm DTO, store, endpoints; kiểm quyền/token và chi nhánh trước ghi, không sửa SQL.
4. Chạy lại test và các test controller/claim cũ.

### 2. Worker và máy thật

Files: PrinterSimulator/MayInSimulator.cs; CauHinhSimulator.cs; appsettings.json; MayInWindows.cs; Shared/MayInAo/QuyTacBangDieuKhien.cs.

1. Test pause/offline không tăng tiến độ; downtime không tính là in; checkpoint gửi không được tự gửi lại; tham số giấy, màu, hai mặt, phạm vi, bản sao.
2. Chạy test RED rồi triển khai tối thiểu.
3. Tải PDF bằng token hiện có, ghi checkpoint trước gửi; khôi phục vào trạng thái cần kiểm tra nếu kết quả gửi không chắc chắn.
4. Query Winspool, chỉ pause/resume job khớp tên file định danh; không purge queue. Giữ default chế độ ảo.
5. Kiểm tra phần cấu hình thiếu/Spooler tắt mà không gửi giấy thật.

### 3. Giao diện

Files: Admin/Pages/MayIn.razor + CSS scoped; Layout/MainLayout.razor; Pages/CaLamViec.razor (liên kết).

1. Test hợp đồng route/nút/mô phỏng/xác nhận trước.
2. Giao diện công nghiệp thực dụng: màn hình LCD navy, trạng thái rõ, nút tác vụ có nhãn, tiến độ và hàng đợi; font/màu theo web cũ.
3. DFII: impact 4, fit 5, feasibility 5, performance 5, consistency risk 2 = 17 (thang điểm của skill có giới hạn mô tả 15; kết quả theo công thức là 17).
4. Kiểm chứng browser 1440/768/390px, keyboard, lỗi API; không dùng dữ liệu thật cho thao tác thử.

### 4. Nghiệm thu và bàn giao

1. Chạy toàn bộ .NET tests và build riêng API/Web/connector.
2. Lưu docs/BAO_CAO_BANG_DIEU_KHIEN_MAY_IN.md: files, pipeline, cấu hình .env không chứa khóa, cách chạy launcher hiện có, giới hạn và checklist thử máy thật.
3. Không tự commit. Không tự restart API/connector đang có giao dịch; xin người dùng trước khi nạp bản mới.

## Nguồn kỹ thuật

- https://learn.microsoft.com/en-us/dotnet/api/system.printing — không đưa System.Printing vào ASP.NET/service.
- https://learn.microsoft.com/en-us/windows/win32/printdocs/enumjobs — đọc trạng thái job.
- https://learn.microsoft.com/en-us/windows/win32/printdocs/setjob — pause/resume đúng job.
- https://www.sumatrapdfreader.org/docs/Command-line-arguments — tham số in; exit code không phản ánh lỗi spooler sau khi gửi.
