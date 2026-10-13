# Dashboard Gentelella — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Hoàn thiện Tổng quan kinh doanh và vận hành bằng dữ liệu thật, giữ màu/cỡ chữ và quyền chi nhánh hiện tại.

**Architecture:** Blazor WASM, CSS scoped theo tiền tố tq, biểu đồ SVG hiện có. Component vận hành tải độc lập từng nguồn API; lỗi kho/máy không làm mất báo cáo doanh thu. Không sửa backend, SQL, quy trình hoặc thêm dependency.

**Tech Stack:** .NET 8, Blazor, CSS Grid, SVG, xUnit, Playwright fixture.

## Hiểu yêu cầu và giả định đã xác nhận

- CEO xem toàn chuỗi hoặc một chi nhánh; quản lý chỉ xem chi nhánh trong JWT.
- Doanh thu ngày/tuần/tháng, đối chiếu cùng số ngày liền trước và so sánh chi nhánh.
- Thông tin vận hành là ảnh chụp lúc tải, không chịu bộ lọc ngày doanh thu: chờ tiếp nhận, đang in, chờ bàn giao, hỏa tốc, quá hạn.
- Máy lấy danh sách cấu hình và heartbeat; không có phản hồi là chưa kết nối, không suy đoán từ trạng thái HOAT_DONG trong DB.
- Kho cảnh báo theo MucToiThieu/CanhBao của API; phiếu chờ duyệt không phải toàn bộ đề xuất mọi loại.
- Giả định quy mô đồ án, tải thủ công, không polling toàn bộ kho/phiếu/đơn mỗi giây. Lỗi từng nguồn phải có thông báo, không giả thành số 0.
- Không đổi palette/font, không tràn ngang, không thay quyền hoặc nghiệp vụ; code dễ đọc, không thêm framework.

## Decision log

- Người dùng xác nhận phạm vi ở tin nhắn “ok ní”.
- Chọn bố cục Gentelella với CSS/SVG hiện có; thay thế xem xét: nhập toàn bộ template (xung đột kiến trúc), chỉ đổi thẻ số liệu (không đủ thông tin).
- Biểu đồ + đối chiếu 2:1, KPI 4/2/1 theo chiều rộng, các panel và bảng chi tiết ở dưới. Điểm nhận diện: dải công việc cần xử lý màu cảnh báo, số tiền navy, font Be Vietnam Pro + IBM Plex Mono.
- Industrial/utilitarian, DFII chủ quan 4+5+5+5−4=15; nhịp 8/16/24px, không animation trang trí.
- Mỗi API vận hành có lỗi riêng; không hiển thị dữ liệu cũ khi làm mới nguồn đó thất bại. Thời điểm tải hiển thị rõ.
- Không tự commit; làm tuần tự trong checkout hiện tại, bảo toàn thay đổi đang có.
- Tham khảo: https://github.com/ColorlibHQ/gentelella/blob/master/production/index.html. Không dùng traffic/task/device số liệu demo.

## Task 1 — Tests RED

- Tạo `PhotocopyApi.Tests/TongQuanVanHanhTests.cs`: render component thật với HTTP fixture. Kiểm đếm chỉ đơn đang xử lý; QR chưa thanh toán không ghi cần tiếp nhận thủ công; máy thiếu heartbeat không ghi đang kết nối; lỗi từng nguồn không ghi số 0; kho/phiếu lọc đúng.
- Bổ sung fixture và kiểm tra mới vào `verification/dashboard-hang-doi.cjs` trước code.
- Run: `dotnet test PhotocopyApi.Tests --filter FullyQualifiedName~TongQuanVanHanhTests --artifacts-path C:\Users\USER\AppData\Local\Temp\PhotocopyDashboardBuild-20261013 --verbosity minimal`.
- Expected RED: component chưa tồn tại, Assert.NotNull thất bại.

## Task 2 — Component và layout GREEN

- Create `PhotocopyAdmin/Components/TongQuanVanHanh.razor`: GET hàng đợi, kho, phiếu nhập, máy cấu hình, heartbeat; trạng thái loading/error/empty riêng, button refresh.
- Modify `PhotocopyAdmin/Pages/TongQuan.razor`: chart/đối chiếu 2:1; branch share/comparison; bảng doanh thu phân trang 10/20/50 dòng; timestamp và nguồn số liệu.
- Modify `PhotocopyAdmin/wwwroot/css/app-ui-v2.css`, `wwwroot/index.html`: chỉ vùng tq, responsive 390/768/1024/1366/1920, focus/touch targets rõ, không co chữ nhỏ.
- Chạy tests targeted rồi full suite và build thường.

## Task 3 — Kiểm chứng và bàn giao

- Publish Release vào Temp, browser fixture chặn API thật: role/scope, partial failure, ngày/nhóm, phân trang, máy/kho, keyboard và 5 kích thước.
- Xem screenshot thực tế và sửa nếu chữ/chart/grid bị cắt.
- Cập nhật `docs/BAN_GIAO_DASHBOARD_HANG_DOI.md`, ghi rõ fixture vs dữ liệu/ngân hàng thật và cách chạy launcher.

## Trạng thái

Hoàn tất component/layout/phân trang. RED: 9 tests thiếu component và browser thiếu panel vận hành trên bản publish cũ. GREEN: 288 tests đạt, 15 DB skip; browser fixture qua 390/768/1024/1366/1920px, scoped branch, kho lỗi/thử lại, phân trang, quyền STAFF. Build thường 0 lỗi/0 cảnh báo. Không thay database; kết quả/giới hạn kiểm chứng tại file bàn giao.
