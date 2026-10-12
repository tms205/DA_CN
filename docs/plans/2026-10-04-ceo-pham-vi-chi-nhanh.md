# CEO chọn phạm vi chi nhánh Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Cho phép CEO chọn một chi nhánh để dùng đầy đủ chức năng quản lý, hoặc chọn toàn chuỗi để xem và thao tác trên từng bản ghi của mọi chi nhánh.

**Architecture:** Web Admin lưu mã chi nhánh đã chọn trong `localStorage` và gửi mã này bằng header dùng chung. Middleware API chỉ chuyển request của `SUPER_ADMIN` có header hợp lệ sang phạm vi hiệu lực `BRANCH_ADMIN` của chi nhánh đã chọn, đồng thời vẫn giữ quyền `SUPER_ADMIN` cho các chức năng cấp chuỗi.

**Tech Stack:** .NET 8, Blazor WebAssembly, ASP.NET Core Web API, Dapper, SQL Server, xUnit.

---

### Task 1: Hợp đồng header và middleware bảo mật

**Files:**
- Create: `MaNguon/PhotocopyChain/PhotocopyApi.Tests/PhamViChiNhanhMiddlewareTests.cs`
- Create: `MaNguon/PhotocopyChain/PhotocopyApi.Tests/PhotocopyApi.Tests.csproj`
- Create: `MaNguon/PhotocopyChain/PhotocopyShared/Constants/TenHeader.cs`
- Create: `MaNguon/PhotocopyChain/PhotocopyApi/PhamViChiNhanhMiddleware.cs`
- Modify: `MaNguon/PhotocopyChain/PhotocopyChain.sln`
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/Program.cs`

1. Viết test thất bại cho ba trường hợp: CEO có header được thêm vai trò quản lý và claim chi nhánh; quản lý thường không đổi được claim bằng header; header sai trả lỗi 400.
2. Chạy riêng test middleware và xác nhận thất bại vì chưa có implementation.
3. Thêm hằng tên header và middleware tối thiểu để các test qua.
4. Đặt middleware sau xác thực và trước phân quyền.
5. Chạy lại test.

### Task 2: Trạng thái phạm vi dùng chung trên Web Admin

**Files:**
- Create: `MaNguon/PhotocopyChain/PhotocopyAdmin/PhamViChiNhanh.cs`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Program.cs`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/PhienDangNhap.cs`

1. Tạo lớp nhỏ giữ `MaChiNhanh`, đọc/ghi `localStorage`, và cập nhật header mặc định của `HttpClient`.
2. Đăng ký lớp theo phạm vi phiên Blazor.
3. Khởi tạo phạm vi trước khi công bố trạng thái đăng nhập để request đầu tiên cũng có đúng header.
4. Giữ nguyên lựa chọn khi tải lại trang và không gửi header nếu đang ở toàn chuỗi.

### Task 3: Bộ chọn chi nhánh và menu theo phạm vi

**Files:**
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Layout/MainLayout.razor`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/wwwroot/css/app.css`

1. Tải danh sách chi nhánh hoạt động cho CEO.
2. Thay thẻ “Toàn chuỗi” bằng `select` có lựa chọn toàn chuỗi và từng chi nhánh.
3. Hiển thị đúng tên/địa chỉ phạm vi hiện tại.
4. Khi lựa chọn không còn hợp lệ, trở về toàn chuỗi.
5. Khi đổi phạm vi, lưu lựa chọn và tải lại trang hiện tại để xóa dữ liệu cũ.
6. Chỉ hiện các chức năng tại quầy/ca làm việc của quản lý cho CEO khi đã chọn chi nhánh.

### Task 4: Cho CEO dùng các màn hình quản lý theo chi nhánh

**Files:**
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/CaLamViec.razor`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/LapDonTaiQuay.razor`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/BanGiao.razor`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/KhoVatTu.razor`
- Modify: `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/NhanVien.razor`

1. Cho phép `SUPER_ADMIN` mở các trang vốn chỉ dành cho quản lý/nhân viên khi có phạm vi chi nhánh.
2. Dùng chi nhánh đang chọn khi lập đơn tại quầy.
3. Hiện thao tác quản lý kho và xác nhận ca cho CEO trong phạm vi chi nhánh.
4. Mặc định form tạo nhân viên theo chi nhánh đang chọn.
5. Giữ chế độ toàn chuỗi không thực hiện thao tác tạo mới cần một chi nhánh ngầm định.

### Task 5: Lọc đầy đủ dữ liệu toàn hệ thống

**Files:**
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/QuanTriController.cs`
- Modify: các controller chỉ khi một endpoint chưa đọc claim chi nhánh hiệu lực.

1. Kiểm tra các endpoint danh sách hiện có; middleware phải làm cho các helper đọc claim cũ tự lọc đúng.
2. Bổ sung bộ lọc cho danh sách hoàn tiền và các danh sách còn đang trả toàn chuỗi ngoài ý muốn.
3. Với thao tác trên bản ghi ở chế độ toàn chuỗi, tiếp tục lấy chi nhánh từ bản ghi và giữ quyền CEO.
4. Với thao tác tạo mới, yêu cầu phải có chi nhánh hiệu lực.

### Task 6: Cho CEO thực hiện nghiệp vụ tại quầy có ca làm việc

**Files:**
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/CaController.cs`
- Modify: `Database/04_StoredProcedure.sql`
- Create: `Database/CapNhat_CSDL_CEO_ChiNhanh.sql`

1. Cập nhật `sp_MoCa` nhận chi nhánh thay thế cho `SUPER_ADMIN`, còn người dùng khác vẫn lấy chi nhánh từ tài khoản.
2. Cập nhật `sp_DongCa` để ca của CEO được đóng như ca quản lý.
3. Cập nhật kiểm tra người thao tác trong `sp_TiepNhanDon` và `sp_GiaoTaiQuay` để `fn_LaQuanLy` chấp nhận CEO mà vẫn khóa sai chi nhánh cho nhân viên thường.
4. Truyền claim chi nhánh hiệu lực vào `sp_MoCa` từ API.
5. Tạo script `CREATE OR ALTER` riêng để người dùng chủ động áp dụng cho CSDL hiện tại; không tự chạy migration.

### Task 7: Xác minh và bàn giao

**Files:**
- Modify only if verification finds a scoped regression.

1. Chạy `dotnet test MaNguon/PhotocopyChain/PhotocopyChain.sln`.
2. Chạy `dotnet build MaNguon/PhotocopyChain/PhotocopyChain.sln`.
3. Kiểm tra tĩnh toàn bộ API cho các chỗ đọc `MaChiNhanh`/`SUPER_ADMIN` để phát hiện endpoint bỏ lọc.
4. Báo rõ script SQL cần chạy thủ công và không tự thay đổi CSDL.
