# Quản lý khách hàng, dịch vụ và bổ sung cơ sở

> Thực hiện tuần tự trong checkout hiện tại để bảo toàn các thay đổi của người dùng; không tự commit, không đổi schema.

**Mục tiêu:** Tách trang Khách hàng và Dịch vụ, cho CEO tạo chi nhánh/máy in, hoàn tất liên hệ footer và thu gọn Dashboard.

**Kiến trúc:** Giữ Blazor WASM, controller Dapper và DTO DataAnnotations hiện có. Khách hàng giữ quyền CEO hiện hành. Dịch vụ là danh mục chung; giá/trạng thái bán riêng từng chi nhánh. Không thêm thư viện hay migration.

**Công nghệ:** .NET 8, SQL Server, Blazor, xUnit, Playwright.

## 1. Liên hệ và Dashboard

- Sửa MainLayout.razor, index.html, app-ui-v2.css: Gmail compose qua HTTPS; dialog điện thoại, tel và sao chép có báo kết quả.
- Bỏ hai khối trong TongQuanPhanTich.razor, giữ cơ cấu và bảng Chi tiết doanh thu.
- Test RED đã xác nhận: SidebarHeaderFooterTests và TongQuanPhanTichTests. Chạy lại sau sửa để GREEN.

## 2. Các danh mục quản lý

- Thêm Pages/KhachHang.razor: tìm kiếm, lọc, phân trang, chi tiết và khóa/mở khóa qua API cũ. Không xóa tài khoản.
- Thêm Pages/DichVu.razor: danh mục chung và thêm/sửa dịch vụ. Bảng giá chỉ sửa giá/trạng thái bán ở phạm vi cụ thể.
- Bổ sung menu, header và nhóm tự mở trong Layout/MainLayout.razor; không mở rộng quyền nhân viên.
- Bổ sung endpoint giá riêng để thao tác giá không đổi định nghĩa chung; bảo vệ quy cách đã có đơn.

## 3. CEO thêm chi nhánh / máy in

- Sửa QuanTriController.cs và DTO Tuan3: POST có quyền SUPER_ADMIN, kiểm tra input và phạm vi máy in.
- Chi nhánh mới: transaction chèn ChiNhanh + bảng giá mặc định + tồn kho bằng 0; không sao chép số tồn/khuyến mãi.
- Form QuanTri.razor đầy đủ địa chỉ, giờ, tọa độ, bán kính; máy in có chi nhánh, tốc độ, màu, hai mặt, khổ giấy, counter.
- Đăng ký máy không đồng nghĩa thiết bị đã kết nối; heartbeat/gateway vẫn quyết định khả năng nhận job.
- Kiểm thử request sai và quyền qua Kestrel không có database; thao tác browser qua fixture, không tạo dữ liệu thử trong database thật.

## 4. Xác minh / bàn giao

- Chạy dotnet test PhotocopyChain.sln --artifacts-path thư_mục_tạm --verbosity minimal.
- Build/publish frontend, kiểm browser desktop/mobile, menu/quyền, các form và dữ liệu sau lưu.
- API mới cần nạp lại tiến trình sau khi người dùng xác nhận không có giao dịch đang xử lý.
- Ghi rõ giới hạn kiểm thử và file đã sửa trong docs/BAN_GIAO_DASHBOARD_HOAN_THIEN.md.
