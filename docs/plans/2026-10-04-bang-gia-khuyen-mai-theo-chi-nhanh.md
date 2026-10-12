# Bảng giá và khuyến mãi theo chi nhánh Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Cho phép mỗi chi nhánh có bảng giá và khuyến mãi riêng, đồng thời cung cấp giao diện CEO quản lý từng bản ghi theo phạm vi chi nhánh đã chọn.

**Architecture:** Giữ `DichVu` làm danh mục chung và thêm `BangGiaChiNhanh` làm nguồn giá thực tế. Gắn `MaChiNhanh` trực tiếp vào `KhuyenMai`; mọi truy vấn báo giá và quản trị lấy chi nhánh từ phạm vi đã xác thực.

**Tech Stack:** SQL Server, ASP.NET Core 8, Dapper, Blazor WebAssembly, xUnit.

---

### Task 1: Quy tắc nghiệp vụ có thể kiểm thử

**Files:**
- Create: `MaNguon/PhotocopyChain/PhotocopyApi.Tests/QuyTacQuanTriChiNhanhTests.cs`
- Create: `MaNguon/PhotocopyChain/PhotocopyApi/QuyTacQuanTriChiNhanh.cs`

**Steps:**

1. Viết test thất bại cho giá âm, khuyến mãi sai khoảng ngày, phần trăm ngoài 1–100 và yêu cầu bắt buộc chọn chi nhánh.
2. Chạy `dotnet test PhotocopyApi.Tests/PhotocopyApi.Tests.csproj --no-restore` và xác nhận test thất bại vì lớp chưa tồn tại.
3. Viết lớp quy tắc nhỏ, trả về thông báo lỗi tiếng Việt hoặc `null`.
4. Chạy lại test và xác nhận tất cả đạt.

### Task 2: Cấu trúc database và script chuyển đổi

**Files:**
- Modify: `Database/01_TaoBang.sql`
- Modify: `Database/02_DuLieuMau.sql`
- Create: `Database/CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql`

**Steps:**

1. Thêm `BangGiaChiNhanh` với khóa chính `(MaChiNhanh, MaDichVu)` và kiểm tra giá/trạng thái.
2. Thêm `MaChiNhanh` vào định nghĩa gốc của `KhuyenMai`; đổi khóa duy nhất thành `(MaChiNhanh, MaCode)`.
3. Cập nhật dữ liệu mẫu để tạo giá cho mọi chi nhánh và gắn khuyến mãi mẫu với chi nhánh.
4. Viết script cập nhật chạy lại an toàn: sao chép giá, tách khuyến mãi cũ theo chi nhánh và cập nhật khóa ngoại đơn hàng.
5. Không chạy script cho tới khi stored procedure và API đã sẵn sàng.

### Task 3: Tính giá và tạo đơn theo chi nhánh

**Files:**
- Modify: `Database/04_StoredProcedure.sql`
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DanhMucController.cs`
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs`

**Steps:**

1. Đổi `sp_TinhTien` để xác thực và lấy giá từ `BangGiaChiNhanh` theo `@MaChiNhanh`.
2. Đổi `sp_TaoDonHang` để lưu đúng đơn giá chi nhánh vào chi tiết đơn và gia công.
3. Lọc khuyến mãi bằng cả `MaCode` và `MaChiNhanh`.
4. Đổi endpoint danh mục bảng giá nhận `maChiNhanh` và trả đúng giá/trạng thái chi nhánh.
5. Đổi các truy vấn tạo đơn phát sinh sang `BangGiaChiNhanh`.

### Task 4: API quản trị và DTO

**Files:**
- Modify: `MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Tuan3/Tuan3Dtos.cs`
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/QuanTriController.cs`

**Steps:**

1. Bổ sung mã/tên chi nhánh cho DTO giá và khuyến mãi.
2. Danh sách giá ở tổng quan trả mọi chi nhánh; phạm vi chi nhánh chỉ trả chi nhánh đã chọn.
3. Thêm/sửa dịch vụ tạo hoặc cập nhật dòng `BangGiaChiNhanh`; thao tác ghi bắt buộc có chi nhánh.
4. Danh sách, thêm và sửa khuyến mãi lọc theo phạm vi; thao tác ghi bắt buộc có chi nhánh.
5. Bắt lỗi trùng mã trong cùng chi nhánh với thông báo dễ hiểu.

### Task 5: Giao diện CEO

**Files:**
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/BangGia.razor`
- Create: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/KhuyenMai.razor`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/QuanTri.razor`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Layout/MainLayout.razor`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/LapDonTaiQuay.razor`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/BanGiao.razor`

**Steps:**

1. Trang bảng giá hiển thị tên chi nhánh và chỉ mở form ghi khi CEO đã chọn một chi nhánh.
2. Thêm trang khuyến mãi riêng, hỗ trợ thêm/sửa/bật-tắt trên từng dòng.
3. Bỏ khối khuyến mãi cũ khỏi trang quản trị tổng hợp.
4. Thêm mục `Quản lý khuyến mãi` vào menu CEO.
5. Truyền mã chi nhánh khi tải danh mục bảng giá trong các màn hình lập đơn/bàn giao.

### Task 6: Áp dụng và xác minh

**Files:**
- Modify: `HUONG_DAN_CHAY_TREN_MAY_NAY.md`

**Steps:**

1. Chạy `dotnet test PhotocopyChain.sln --no-restore`.
2. Chạy `dotnet build PhotocopyChain.sln --no-restore` và yêu cầu 0 lỗi, 0 cảnh báo.
3. Chạy `Database/CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql` bằng `sqlcmd -b` trên database đã cấu hình.
4. Xác minh số dòng giá bằng `số chi nhánh × số dịch vụ`, mọi khuyến mãi có chi nhánh và không có đơn trỏ sai chi nhánh.
5. Chạy API/Web và kiểm tra CEO ở chế độ tổng quan, Cơ sở 1 và Cơ sở 2.
6. Kiểm tra hai chi nhánh có thể dùng giá khác nhau và cùng mã khuyến mãi mà không ảnh hưởng nhau.

> Workspace hiện không phải Git repository nên các bước commit trong quy trình chuẩn được bỏ qua.
