# Hoàn thiện dashboard — Implementation Plan

> Kế hoạch triển khai theo từng bước, có kiểm thử và đối chiếu trước bàn giao.

**Goal:** Giảm thông tin lặp, cảnh báo đúng dữ liệu và bổ sung các báo cáo chuyên ngành bằng API chỉ đọc.

**Architecture:** Giữ Blazor/Dapper/SQL Server. Hai phần Kinh doanh và Vận hành; CEO mặc định Kinh doanh, quản lý mặc định Vận hành, không cấp thêm quyền STAFF. API mới chỉ SELECT, giới hạn kỳ 366 ngày, lọc phạm vi từ JWT/middleware, từ chối quản lý thiếu claim chi nhánh. Không sửa schema hoặc quy trình đơn/in/thanh toán.

**Tech Stack:** .NET 8, Blazor WASM, CSS/SVG, MudBlazor đã có, xUnit, Playwright.

## Quyết định và giả định đã xác nhận

- Người dùng duyệt UI + API chỉ đọc, không đổi cấu trúc DB. Làm tuần tự, không commit hoặc thay thư viện.
- Giảm lặp: bỏ bộ KPI cố định chung bộ lọc; KPI theo kỳ gồm doanh thu, số đơn, AOV, có đối chiếu. Ngày/tuần/tháng chọn preset tự áp dụng; tùy chọn vẫn có nút áp dụng để không gửi request lúc đang nhập.
- Cảnh báo chỉ có màu cam khi >0; trạng thái bằng 0 trung tính. Chỉ so sánh chi nhánh khi có >=2 nhóm dữ liệu, không xóa ngữ cảnh phạm vi cần thiết.
- Biểu đồ ngày dùng cột, tuần/tháng đường; trục bước tròn, đơn vị đồng thống nhất; kỳ trước toàn 0 không vẽ đường. Bảng chi tiết vẫn có giá trị 0 của kỳ trước.
- Chi tiết định nghĩa dài thu vào details có thể mở bằng bàn phím; giữ nhãn phạm vi và định nghĩa doanh thu ngắn luôn thấy.
- Cơ cấu dịch vụ theo tiền lưu trong chi tiết, tách gia công tránh cộng hai lần. Không phân bổ giảm giá giả: bảng đối chiếu hiển thị tiền dịch vụ + điều chỉnh/phụ phí + vận chuyển − giảm giá = doanh thu; phân loại in màu hiện dựa danh mục dịch vụ, ảnh theo snapshot ct.KichThuocAnh.
- Thanh toán phân loại doanh thu đơn hoàn tất theo phương thức, không phải số tiền đã nhận tại ngân hàng. COD gồm quầy/giao hàng, không dựng phương thức chuyển khoản khác khi DB không có.
- Heatmap = đơn đặt theo ngày trong tuần/giờ, trong kỳ NgayDat; gồm đơn hủy, loại in lại lỗi quầy. Thời gian xử lý = NgayDat→NgayHoanThanh, gồm cả chờ/giao/khách xác nhận; đúng hẹn = hoàn tất <= NgayHenTra, không gọi là tốc độ máy in. Thiếu/sai mốc không tính trung bình, ghi số loại trừ.
- Ca mở toàn phạm vi, không chỉ ca riêng người dùng; chênh lệch chỉ ca đóng có kiểm đếm trong 30 ngày gần nhất. Không giả số chênh lệch ca đang mở.
- Dashboard dùng dữ liệu thật, lỗi riêng; snapshot thời điểm tải, không cam kết realtime. Tự làm mới hàng đợi/heartbeat/chỉ số vận hành mỗi 60 giây khi phần vận hành hiển thị; kho/phiếu chi tiết không quét lịch sử tự động. API badge nhẹ, lỗi không hiển thị 0.
- Industrial/utilitarian; navy hiện có, font Be Vietnam Pro/IBM Plex Mono, nhịp 8/16/24px, DFII chủ quan 15; chữ phụ dùng #475569 trong vùng dashboard, không trang trí động.

## Task 1 — RED

- `PhotocopyApi.Tests/DashboardHoanThienTests.cs`: biểu đồ tick tròn/ẩn kỳ 0, cảnh báo 0 trung tính, API range/scope/policy.
- Browser fixture kiểm tab, preset, ngày dd/MM, KPI theo kỳ, số đơn/AOV, 1 chi nhánh ẩn so sánh, API mới/cơ cấu/heatmap, lỗi độc lập, badge, auto refresh và responsive.
- Run: `dotnet test PhotocopyApi.Tests --filter FullyQualifiedName~DashboardHoanThienTests --artifacts-path C:\Users\USER\AppData\Local\Temp\PhotocopyDashboardBuild-20261013 --verbosity minimal`. Xác nhận RED trước code.

## Task 2 — API báo cáo GREEN

- Create `PhotocopyShared/Dtos/Tuan3/TongQuanDtos.cs`: dữ liệu cơ cấu, giờ cao điểm, hiệu quả/đánh giá và ca/phê duyệt.
- Create `PhotocopyApi/DuLieu/BaoCaoTongQuanSql.cs`, `Controllers/BaoCaoTongQuanController.cs`: báo cáo kỳ và chỉ số vận hành hiện tại, SQL tham số hóa, không CREATE/UPDATE/DELETE.
- Tests SQL read-only trên cấu hình máy nếu kết nối được; không kích hoạt các bài DB có ghi/rollback đang skip.

## Task 3 — UI GREEN

- Modify `TongQuan.razor`, `BieuDoDoanhThu.razor`, `TongQuanBaoCao.cs`, `TongQuanVanHanh.razor`: giảm lặp, bộ lọc, biểu đồ, cảnh báo, cập nhật, ca và báo cáo mới.
- Create `Components/TongQuanPhanTich.razor`: cơ cấu, heatmap có bảng, hiệu quả và điểm đánh giá theo kỳ.
- Modify `MainLayout.razor`: nhóm Báo cáo / Quản lý / Cấu hình, badge phiếu+hoàn tiền; giữ nhóm đóng/mở độc lập.
- Modify CSS chỉ phạm vi tq, CSS version, JS visibility nhỏ cho timer nếu cần. Không đổi màu thương hiệu hoặc cỡ chữ đã thống nhất.

## Task 4 — Kiểm chứng/bàn giao

- Full .NET suite, normal build API/Web, Release publish Web.
- Browser fixture bản cuối 390/768/1024/1366/1920px, scope/roles/error/preset/tab/keyboard/auto-refresh; xem ảnh.
- Cập nhật `docs/BAN_GIAO_DASHBOARD_HOAN_THIEN.md`: nguồn, định nghĩa, file, kết quả và hạn chế.

## Trạng thái

Đã hoàn tất RED → GREEN → kiểm chứng trên bản cuối. Không thay DB/schema.

- API/Web build 0 warning / 0 error; publish Release thành công.
- Full .NET: 301 đạt, 0 lỗi, 16 skip / 317. Chạy riêng SELECT báo cáo trên DB thật: 1 đạt (toàn chuỗi và chi nhánh 1/2).
- Chrome fixture: 390/768/1024/1366/1920px đều đạt; thử quyền, bộ lọc, lỗi độc lập, bàn phím, phân trang và timer 60 giây.
- API đã restart theo sự đồng ý của người dùng; Web 5064 phục vụ CSS mới.
- Bàn giao: [BAN_GIAO_DASHBOARD_HOAN_THIEN.md](../BAN_GIAO_DASHBOARD_HOAN_THIEN.md).
