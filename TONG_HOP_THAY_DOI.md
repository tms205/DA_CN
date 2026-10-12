# Tổng hợp thay đổi PhotocopyChain

> Cập nhật ngày 05/10/2026. Tài liệu này tổng hợp toàn bộ thay đổi đã thực hiện trong phiên làm việc, gồm phần đã hoàn tất và phần đang triển khai.

## 1. CEO chọn phạm vi chi nhánh

### Mục tiêu

- CEO có thể chọn một chi nhánh để xem và thao tác như quản lý chi nhánh.
- CEO có thể chọn `Tổng quan tất cả` để xem dữ liệu toàn chuỗi.
- Phạm vi đã chọn được ghi nhớ sau khi tải lại trang.
- Các thao tác trên từng bản ghi luôn bị giới hạn đúng chi nhánh đang chọn.

### Phần giao diện

- Thêm bộ chọn chi nhánh trên thanh bên trong `PhotocopyAdmin/Layout/MainLayout.razor`.
- Hiển thị tên và địa chỉ chi nhánh đang chọn.
- Lưu mã chi nhánh vào `localStorage` với lớp `PhotocopyAdmin/PhamViChiNhanh.cs`.
- Gắn header `X-Ma-Chi-Nhanh` vào các request API của CEO.
- Khi CEO chọn một chi nhánh, hiện thêm các mục nghiệp vụ như lập đơn tại quầy và bàn giao.
- Khi CEO chọn toàn chuỗi, ẩn các thao tác bắt buộc phải thuộc một chi nhánh.
- Đồng bộ mô tả phạm vi trên các trang hàng đợi đơn, ca làm việc, bàn giao và lịch sử đơn theo ca.

### Phần API và phân quyền

- Thêm `PhotocopyApi/PhamViChiNhanhMiddleware.cs`.
- Middleware chỉ chấp nhận phạm vi chi nhánh từ tài khoản `SUPER_ADMIN`.
- Khi CEO chọn chi nhánh, API giữ quyền `SUPER_ADMIN`, đồng thời cho phép xử lý nghiệp vụ như `BRANCH_ADMIN` trong chi nhánh đó.
- Header không hợp lệ trả về lỗi `400`.
- Người dùng không phải CEO không thể thay đổi phạm vi bằng header.
- Thêm tên header dùng chung tại `PhotocopyShared/Constants/TenHeader.cs`.

### Các nghiệp vụ đã áp dụng phạm vi

- Hàng đợi và chi tiết đơn hàng.
- Tiếp nhận đơn, phân công máy, in lại và tạo đơn bổ sung.
- Lập đơn và bàn giao tại quầy.
- Ca làm việc và két tiền.
- Kho vật tư và phiếu nhập.
- Danh sách nhân viên.
- Đánh giá khách hàng.
- Báo cáo doanh thu.
- Danh sách khách hàng và hoàn tiền.
- Chi nhánh và máy in.

### Stored procedure CEO theo chi nhánh

Đã cập nhật bốn thủ tục:

- `sp_TiepNhanDon`
- `sp_GiaoTaiQuay`
- `sp_MoCa`
- `sp_DongCa`

Các thay đổi chính:

- CEO được tiếp nhận và bàn giao đơn khi có ca đang mở tại đúng chi nhánh.
- CEO được mở ca tại chi nhánh đang chọn qua tham số `@MaChiNhanhThayThe`.
- Ca của CEO được xử lý như ca quản lý khi đóng.
- Không cho một người mở ca mới khi còn ca chưa kết thúc.
- Giữ khóa `UPDLOCK, HOLDLOCK` để tránh mở trùng ca.

Nội dung đã được đồng bộ vào:

- `Database/04_StoredProcedure.sql`
- `Database/CapNhat_CSDL_HienTai_Tuan3.sql`
- `Database/CapNhat_CSDL_CEO_ChiNhanh.sql`

File `CapNhat_CSDL_CEO_ChiNhanh.sql` đã được chạy thành công trên database `PhotocopyChain` tại `localhost`.

## 2. Kiểm thử cho phạm vi CEO

- Tạo project `PhotocopyApi.Tests` và thêm vào solution.
- Thêm test cho middleware phạm vi chi nhánh.
- Thêm test cho việc ghi nhớ chi nhánh trong trình duyệt.
- Đã kiểm tra giao diện thực tế:
  - Chọn Cơ sở 2 chỉ thấy dữ liệu Cơ sở 2.
  - Tải lại trang vẫn nhớ Cơ sở 2.
  - Chuyển về `Tổng quan tất cả` hiển thị dữ liệu toàn chuỗi.
  - Các mục thao tác riêng chi nhánh ẩn/hiện đúng.
- Trước khi bắt đầu tính năng bảng giá riêng, solution đã build thành công với `0 lỗi`, `0 cảnh báo` và `5/5` test đạt.

## 3. Thiết kế bảng giá riêng theo chi nhánh

### Quyết định đã thống nhất

- Giữ `DichVu` làm danh mục dịch vụ dùng chung toàn chuỗi.
- Tạo bảng mới `BangGiaChiNhanh`.
- Khóa chính của bảng mới là `(MaChiNhanh, MaDichVu)`.
- Mỗi chi nhánh có `DonGia` và `TrangThai` riêng cho từng dịch vụ.
- Giá thực tế của đơn được lấy theo chi nhánh.
- Đơn giá vẫn được lưu vào chi tiết đơn để lịch sử không đổi khi CEO sửa giá sau này.

### Cấu trúc dự kiến

```text
BangGiaChiNhanh
├── MaChiNhanh
├── MaDichVu
├── DonGia
└── TrangThai
```

### Thay đổi source SQL đã thực hiện

- Thêm định nghĩa `BangGiaChiNhanh` vào `Database/01_TaoBang.sql`.
- Cập nhật `Database/02_DuLieuMau.sql` để tạo giá cho mọi cặp chi nhánh/dịch vụ.
- Đổi `sp_TinhTien` trong `Database/04_StoredProcedure.sql` sang lấy giá từ `BangGiaChiNhanh`.
- Đổi `sp_TaoDonHang` để lưu đúng giá in và giá gia công của chi nhánh.
- Tạo script chuyển đổi `Database/CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql`.

## 4. Khuyến mãi riêng theo chi nhánh

### Quyết định đã thống nhất

- Thêm `MaChiNhanh` trực tiếp vào bảng `KhuyenMai`.
- Đổi ràng buộc duy nhất từ `MaCode` sang `(MaChiNhanh, MaCode)`.
- Cùng một mã code được phép xuất hiện ở nhiều chi nhánh.
- Mỗi chi nhánh có phần trăm giảm, hạn dùng, số lượng và số lượt đã dùng riêng.
- Khi báo giá, mã chỉ hợp lệ khi đồng thời khớp `MaCode` và `MaChiNhanh`.
- Không xóa cứng khuyến mãi đã được đơn hàng sử dụng.

### Chuyển đổi dữ liệu cũ

Script cập nhật được thiết kế để:

1. Giữ bản sao khuyến mãi cũ trong lúc chuyển đổi.
2. Bỏ ràng buộc `UNIQUE` toàn cục của `MaCode`.
3. Tạo bản khuyến mãi cho từng chi nhánh.
4. Cập nhật `DonHang.MaKhuyenMai` về đúng bản khuyến mãi của chi nhánh đặt đơn.
5. Thêm khóa ngoại tới `ChiNhanh`.
6. Thêm ràng buộc duy nhất `(MaChiNhanh, MaCode)`.

## 5. API bảng giá và khuyến mãi đang triển khai

### Quy tắc nghiệp vụ

- Thêm `PhotocopyApi/QuyTacQuanTriChiNhanh.cs`.
- Bắt buộc CEO chọn chi nhánh trước khi ghi bảng giá hoặc khuyến mãi.
- Không chấp nhận giá âm.
- Phần trăm giảm phải từ 1 đến 100.
- Ngày kết thúc không được trước ngày bắt đầu.
- Chỉ chấp nhận trạng thái bảng giá và khuyến mãi hợp lệ.

### API bảng giá

- `api/danh-muc/bang-gia` nhận `maChiNhanh` và trả giá đúng chi nhánh.
- `api/quan-tri/dich-vu` trả thêm mã và tên chi nhánh.
- Khi CEO đang ở tổng quan, API trả giá của mọi chi nhánh để so sánh.
- Khi CEO chọn chi nhánh, API chỉ trả giá của chi nhánh đó.
- Thêm dịch vụ mới sẽ tạo dòng giá cho các chi nhánh.
- Sửa dịch vụ cập nhật thông tin chung và chỉ sửa giá tại chi nhánh đang chọn.

### API khuyến mãi

- Danh sách khuyến mãi được lọc theo phạm vi chi nhánh.
- Thêm khuyến mãi bắt buộc có chi nhánh đang chọn.
- Sửa khuyến mãi chỉ được tác động tới bản ghi thuộc chi nhánh đang chọn.
- Trùng mã trong cùng chi nhánh trả về thông báo dễ hiểu.

### API tạo đơn

- Các truy vấn báo giá và tạo đơn phát sinh đã được chuyển sang đọc `BangGiaChiNhanh`.
- Danh sách chi nhánh phù hợp kiểm tra cả dịch vụ đang bán tại chi nhánh và khả năng máy in.
- Trang lập đơn và bàn giao gửi mã chi nhánh khi tải bảng giá.

## 6. Giao diện quản lý đang triển khai

### Trang bảng giá

- Hiển thị thêm cột chi nhánh.
- Ở `Tổng quan tất cả`, CEO xem và so sánh nhưng không được ghi vì chưa có chi nhánh đích.
- Khi chọn một chi nhánh, CEO được thêm hoặc sửa từng bản ghi.

### Trang khuyến mãi mới

- Tạo `PhotocopyAdmin/Pages/KhuyenMai.razor` với đường dẫn `/khuyen-mai`.
- Thêm mục `Quản lý khuyến mãi` vào menu CEO.
- Hỗ trợ:
  - Thêm khuyến mãi.
  - Sửa từng bản ghi.
  - Tạm dừng hoặc mở lại từng bản ghi.
  - Xem tất cả chi nhánh ở chế độ tổng quan.
- Đã bỏ khối khuyến mãi cũ khỏi trang `Chi nhánh & máy in` để tránh trùng giao diện.

## 7. Test mới cho bảng giá và khuyến mãi

- Tạo `PhotocopyApi.Tests/QuyTacQuanTriChiNhanhTests.cs`.
- Đã thực hiện đúng chu trình TDD:
  - Test thất bại vì lớp quy tắc chưa tồn tại.
  - Thêm phần triển khai tối thiểu.
  - Chạy lại và đạt `12/12` test ở cấu hình Release.
- Sau khi chuẩn hóa lại giao diện, solution đã được build lại ở cấu hình Release với `0 lỗi`, `0 cảnh báo` và toàn bộ `12/12` test đều đạt.

## 8. Trạng thái database hiện tại

### Đã áp dụng

- Quyền CEO thao tác theo chi nhánh.
- Bốn stored procedure CEO: tiếp nhận, bàn giao, mở ca và đóng ca.

### Chưa áp dụng

- Bảng `BangGiaChiNhanh` chưa tồn tại trong database hiện tại.
- Cột `KhuyenMai.MaChiNhanh` chưa tồn tại trong database hiện tại.
- Script `CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql` chưa được chạy.

Không nên chạy ứng dụng bằng phần code bảng giá mới cho tới khi migration được build, kiểm tra và áp dụng thành công.

## 9. Công việc còn lại

1. Kiểm tra lại toàn bộ truy vấn còn đọc `DichVu.DonGia` hoặc trạng thái giá toàn cục.
2. Kiểm tra cú pháp migration trên SQL Server.
3. Chạy migration bảng giá/khuyến mãi lên database hiện tại.
4. Xác minh số dòng `BangGiaChiNhanh` bằng số chi nhánh nhân số dịch vụ.
5. Xác minh mọi khuyến mãi và đơn cũ thuộc đúng chi nhánh.
6. Kiểm tra giao diện CEO với `Tổng quan tất cả`, Cơ sở 1 và Cơ sở 2.
7. Thử đặt đơn với hai mức giá khác nhau và cùng mã khuyến mãi ở hai chi nhánh.

## 10. Tài liệu thiết kế và kế hoạch

- `docs/plans/2026-10-04-ceo-pham-vi-chi-nhanh-design.md`
- `docs/plans/2026-10-04-ceo-pham-vi-chi-nhanh.md`
- `docs/plans/2026-10-04-bang-gia-khuyen-mai-theo-chi-nhanh-design.md`
- `docs/plans/2026-10-04-bang-gia-khuyen-mai-theo-chi-nhanh.md`

## 11. Các file chính đã thay đổi hoặc tạo mới

### Database

- `Database/01_TaoBang.sql`
- `Database/02_DuLieuMau.sql`
- `Database/04_StoredProcedure.sql`
- `Database/CapNhat_CSDL_HienTai_Tuan3.sql`
- `Database/CapNhat_CSDL_CEO_ChiNhanh.sql`
- `Database/CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql`

### API

- `PhotocopyApi/Program.cs`
- `PhotocopyApi/PhamViChiNhanhMiddleware.cs`
- `PhotocopyApi/QuyTacQuanTriChiNhanh.cs`
- `PhotocopyApi/DuLieu/TruyVanSql.cs`
- `PhotocopyApi/Controllers/CaController.cs`
- `PhotocopyApi/Controllers/DanhMucController.cs`
- `PhotocopyApi/Controllers/DonHangController.cs`
- `PhotocopyApi/Controllers/QuanTriController.cs`

### Shared

- `PhotocopyShared/Constants/TenHeader.cs`
- `PhotocopyShared/Dtos/Tuan3/Tuan3Dtos.cs`

### Giao diện quản trị

- `PhotocopyAdmin/Program.cs`
- `PhotocopyAdmin/PhamViChiNhanh.cs`
- `PhotocopyAdmin/PhienDangNhap.cs`
- `PhotocopyAdmin/Layout/MainLayout.razor`
- `PhotocopyAdmin/Pages/BangGia.razor`
- `PhotocopyAdmin/Pages/KhuyenMai.razor`
- `PhotocopyAdmin/Pages/QuanTri.razor`
- `PhotocopyAdmin/Pages/LapDonTaiQuay.razor`
- `PhotocopyAdmin/Pages/BanGiao.razor`
- `PhotocopyAdmin/Pages/CaLamViec.razor`
- `PhotocopyAdmin/Pages/HangDoiDon.razor`
- `PhotocopyAdmin/Pages/DonHangTrongCa.razor`
- `PhotocopyAdmin/Pages/KhoVatTu.razor`
- `PhotocopyAdmin/Pages/NhanVien.razor`
- `PhotocopyAdmin/wwwroot/css/app.css`

### Kiểm thử

- `PhotocopyApi.Tests/PhotocopyApi.Tests.csproj`
- `PhotocopyApi.Tests/PhamViChiNhanhMiddlewareTests.cs`
- `PhotocopyApi.Tests/PhamViChiNhanhTests.cs`
- `PhotocopyApi.Tests/QuyTacQuanTriChiNhanhTests.cs`

## 12. Phụ lục vị trí thay đổi theo số dòng hiện tại

> Các số dòng dưới đây được chốt theo nội dung file ngày 05/10/2026. Thư mục hiện tại không có lịch sử Git, vì vậy đây là vị trí của **khối code đang chứa thay đổi**, không phải kết quả so sánh tự động với một commit cũ. Nếu tiếp tục thêm hoặc xóa code phía trên thì số dòng sẽ dịch chuyển.

Quy ước đường dẫn:

- File SQL và tài liệu tính từ thư mục gốc dự án.
- File C#, Razor và CSS tính từ `MaNguon/PhotocopyChain/`.
- Ghi `toàn file` nghĩa là file được tạo mới cho phần việc này hoặc toàn bộ nội dung file thuộc phần thay đổi.
- Phần code đã bị xóa không còn số dòng trong file hiện tại; trường hợp đó được ghi chú riêng.

### 12.1. Database

| File | Dòng hiện tại | Nội dung thay đổi |
|---|---:|---|
| `Database/01_TaoBang.sql` | 71–77 | Tạo bảng `BangGiaChiNhanh`. |
| `Database/01_TaoBang.sql` | 90–103 | Thêm `MaChiNhanh` và ràng buộc duy nhất theo chi nhánh cho `KhuyenMai`. |
| `Database/02_DuLieuMau.sql` | 66–68 | Tạo giá mẫu cho mọi cặp chi nhánh/dịch vụ. |
| `Database/02_DuLieuMau.sql` | 76–81 | Tạo khuyến mãi mẫu riêng cho từng chi nhánh. |
| `Database/04_StoredProcedure.sql` | 66–152 | `sp_TinhTien`: đọc giá và kiểm tra khuyến mãi theo chi nhánh. |
| `Database/04_StoredProcedure.sql` | 178–235 | `sp_TaoDonHang`: lưu giá in, giá gia công và khuyến mãi đúng chi nhánh. |
| `Database/04_StoredProcedure.sql` | 266–303 | `sp_TiepNhanDon`: cho CEO thao tác đúng chi nhánh và yêu cầu ca đang mở. |
| `Database/04_StoredProcedure.sql` | 380–440 | `sp_GiaoTaiQuay`: kiểm quyền, ca và thanh toán theo chi nhánh. |
| `Database/04_StoredProcedure.sql` | 617–701 | `sp_MoCa`, `sp_DongCa`: CEO mở/đóng ca tại chi nhánh được chọn. |
| `Database/CapNhat_CSDL_HienTai_Tuan3.sql` | 38–92 | Bản cập nhật `sp_MoCa`. |
| `Database/CapNhat_CSDL_HienTai_Tuan3.sql` | 94–122 | Bản cập nhật `sp_DongCa`. |
| `Database/CapNhat_CSDL_CEO_ChiNhanh.sql` | 1–201, toàn file | Script cập nhật quyền CEO theo chi nhánh; đã chạy trên database hiện tại. |
| `Database/CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql` | 1–242, toàn file | Migration bảng giá và khuyến mãi theo chi nhánh; chưa chạy trên database hiện tại. |

### 12.2. API

| File | Dòng hiện tại | Nội dung thay đổi |
|---|---:|---|
| `PhotocopyApi/Program.cs` | 144–145 | Đăng ký middleware phạm vi chi nhánh sau xác thực JWT. |
| `PhotocopyApi/PhamViChiNhanhMiddleware.cs` | 1–47, toàn file | Đọc header, kiểm tra CEO và thay claim chi nhánh cho request hiện tại. |
| `PhotocopyApi/QuyTacQuanTriChiNhanh.cs` | 1–27, toàn file | Quy tắc kiểm tra dữ liệu bảng giá và khuyến mãi. |
| `PhotocopyApi/DuLieu/TruyVanSql.cs` | 20–173 | Các truy vấn đơn hàng có lọc phạm vi chi nhánh. |
| `PhotocopyApi/DuLieu/TruyVanSql.cs` | 176–294 | Các truy vấn ca, đơn trong ca và lịch sử theo chi nhánh. |
| `PhotocopyApi/DuLieu/TruyVanSql.cs` | 297–323 | Danh sách nhân viên theo quyền và chi nhánh. |
| `PhotocopyApi/Controllers/CaController.cs` | 31–213 | Xem danh sách, lịch sử và mở ca theo phạm vi CEO đã chọn. |
| `PhotocopyApi/Controllers/CaController.cs` | 216–304 | Đóng ca, xem và xác nhận ca đúng phạm vi chi nhánh. |
| `PhotocopyApi/Controllers/DanhMucController.cs` | 32–49 | API bảng giá nhận `maChiNhanh` và đọc `BangGiaChiNhanh`. |
| `PhotocopyApi/Controllers/DonHangController.cs` | 65–98 | Chỉ đề xuất chi nhánh có đủ bảng giá dịch vụ. |
| `PhotocopyApi/Controllers/DonHangController.cs` | 318–426 | Lập đơn tại quầy theo chi nhánh và ca đang mở. |
| `PhotocopyApi/Controllers/DonHangController.cs` | 443–535 | Kiểm tra phạm vi khi xem đơn và lấy máy in. |
| `PhotocopyApi/Controllers/DonHangController.cs` | 567–930 | Kiểm tra chi nhánh trên tiếp nhận, in, tải file, bàn giao, in lại, bổ sung, hủy và hoàn tiền. |
| `PhotocopyApi/Controllers/DonHangController.cs` | 959–994 | Lọc và ẩn đánh giá theo chi nhánh. |
| `PhotocopyApi/Controllers/DonHangController.cs` | 996–1190 | Tạo đơn phát sinh và lấy giá từ `BangGiaChiNhanh`. |
| `PhotocopyApi/Controllers/DonHangController.cs` | 1317–1330 | Hàm kiểm tra quyền theo chi nhánh và tìm ca đang mở đúng chi nhánh. |
| `PhotocopyApi/Controllers/QuanTriController.cs` | 25–101 | Xem, thêm và sửa bảng giá theo chi nhánh. |
| `PhotocopyApi/Controllers/QuanTriController.cs` | 103–209 | Lọc chi nhánh, máy in và khách hàng theo phạm vi CEO. |
| `PhotocopyApi/Controllers/QuanTriController.cs` | 211–283 | CRUD khuyến mãi theo từng chi nhánh. |
| `PhotocopyApi/Controllers/QuanTriController.cs` | 286–488 | Lọc kho, phiếu nhập, báo cáo, file in và hoàn tiền theo phạm vi. |

### 12.3. Shared

| File | Dòng hiện tại | Nội dung thay đổi |
|---|---:|---|
| `PhotocopyShared/Constants/TenHeader.cs` | 1–7, toàn file | Khai báo header `X-Ma-Chi-Nhanh`. |
| `PhotocopyShared/Dtos/Tuan3/Tuan3Dtos.cs` | 111–140 | DTO bảng giá có mã/tên chi nhánh và request lưu giá. |
| `PhotocopyShared/Dtos/Tuan3/Tuan3Dtos.cs` | 190–218 | DTO và request quản lý khuyến mãi theo chi nhánh. |

### 12.4. Giao diện quản trị

| File | Dòng hiện tại | Nội dung thay đổi |
|---|---:|---|
| `PhotocopyAdmin/Program.cs` | 21 | Đăng ký dịch vụ `PhamViChiNhanh`. |
| `PhotocopyAdmin/PhamViChiNhanh.cs` | 1–53, toàn file | Ghi nhớ chi nhánh và gắn header vào `HttpClient`. |
| `PhotocopyAdmin/PhienDangNhap.cs` | 25–33, 61 | Nhận dịch vụ phạm vi và khởi tạo phạm vi khi khôi phục phiên. |
| `PhotocopyAdmin/Layout/MainLayout.razor` | 3–4 | Inject dịch vụ phạm vi và HTTP client. |
| `PhotocopyAdmin/Layout/MainLayout.razor` | 18–38 | Bộ chọn `Tổng quan tất cả` hoặc từng chi nhánh. |
| `PhotocopyAdmin/Layout/MainLayout.razor` | 45–49 | Hiện thao tác tại quầy khi CEO đã chọn chi nhánh. |
| `PhotocopyAdmin/Layout/MainLayout.razor` | 63–67 | Thêm menu quản lý khuyến mãi. |
| `PhotocopyAdmin/Layout/MainLayout.razor` | 103–155 | Tải danh sách, khôi phục, kiểm tra và đổi phạm vi chi nhánh. |
| `PhotocopyAdmin/Pages/BangGia.razor` | 3–26 | Nhận phạm vi và cảnh báo khi CEO đang xem toàn chuỗi. |
| `PhotocopyAdmin/Pages/BangGia.razor` | 28–140 | Form và bảng dữ liệu giá theo chi nhánh, đã chuẩn hóa trình bày. |
| `PhotocopyAdmin/Pages/BangGia.razor` | 142–248 | Tải, sửa và lưu từng bản ghi bảng giá, đã tách rõ từng hàm và câu lệnh. |
| `PhotocopyAdmin/Pages/KhuyenMai.razor` | 1–249, toàn file | Trang quản lý khuyến mãi: xem, thêm, sửa và đổi trạng thái từng bản ghi; đã chuẩn hóa trình bày. |
| `PhotocopyAdmin/Pages/QuanTri.razor` | 1–291, toàn file hiện tại | Bỏ giao diện khuyến mãi cũ và chuẩn hóa toàn bộ cách trình bày trang quản trị. |
| `PhotocopyAdmin/Pages/LapDonTaiQuay.razor` | 2–5 | Cho CEO vào trang và nhận phạm vi chi nhánh. |
| `PhotocopyAdmin/Pages/LapDonTaiQuay.razor` | 226–232 | Tải bảng giá của chi nhánh đang chọn. |
| `PhotocopyAdmin/Pages/LapDonTaiQuay.razor` | 399–406 | Gửi mã chi nhánh khi báo giá. |
| `PhotocopyAdmin/Pages/BanGiao.razor` | 2–7 | Cho CEO vào trang và nhận phạm vi chi nhánh. |
| `PhotocopyAdmin/Pages/BanGiao.razor` | 244–260 | Tải bảng giá đúng chi nhánh. |
| `PhotocopyAdmin/Pages/BanGiao.razor` | 591–596 | Hiển thị mô tả phạm vi hiện tại. |
| `PhotocopyAdmin/Pages/CaLamViec.razor` | 2–6, 26–32 | Cho CEO vào trang và chặn thao tác ca ở chế độ toàn chuỗi. |
| `PhotocopyAdmin/Pages/CaLamViec.razor` | 340–382 | Tải dữ liệu ca theo phạm vi và nhận biết CEO đang xem toàn chuỗi. |
| `PhotocopyAdmin/Pages/CaLamViec.razor` | 455–540 | Xem, mở và đóng ca trong chi nhánh đang chọn. |
| `PhotocopyAdmin/Pages/CaLamViec.razor` | 573–578 | Hiển thị mô tả phạm vi. |
| `PhotocopyAdmin/Pages/HangDoiDon.razor` | 6 | Inject phạm vi chi nhánh. |
| `PhotocopyAdmin/Pages/HangDoiDon.razor` | 371–490 | Tải hàng đợi, máy in và trạng thái ca theo phạm vi. |
| `PhotocopyAdmin/Pages/HangDoiDon.razor` | 613–620 | Hiển thị mô tả phạm vi. |
| `PhotocopyAdmin/Pages/DonHangTrongCa.razor` | 2–7 | Cho CEO vào trang và nhận phạm vi chi nhánh. |
| `PhotocopyAdmin/Pages/DonHangTrongCa.razor` | 188–230 | Tải dữ liệu đơn trong ca/lịch sử theo phạm vi. |
| `PhotocopyAdmin/Pages/DonHangTrongCa.razor` | 346–354 | Hiển thị mô tả phạm vi. |
| `PhotocopyAdmin/Pages/KhoVatTu.razor` | 2–5 | Cho CEO vào trang và nhận phạm vi chi nhánh. |
| `PhotocopyAdmin/Pages/KhoVatTu.razor` | 10–143 | Form, bảng tồn kho và phiếu nhập, đã chuẩn hóa trình bày. |
| `PhotocopyAdmin/Pages/KhoVatTu.razor` | 145–221 | Tải dữ liệu và thao tác kho, đã tách rõ từng hàm và câu lệnh. |
| `PhotocopyAdmin/Pages/NhanVien.razor` | 2–5, 49 | Cho CEO vào trang, nhận phạm vi và khóa lựa chọn chi nhánh khi cần. |
| `PhotocopyAdmin/Pages/NhanVien.razor` | 159–205 | Khởi tạo và tải nhân viên theo chi nhánh đang chọn. |
| `PhotocopyAdmin/Pages/NhanVien.razor` | 281–286 | Hiển thị mô tả phạm vi. |
| `PhotocopyAdmin/wwwroot/css/app.css` | 342–347 | Kiểu hiển thị bộ chọn chi nhánh trên sidebar. |

### 12.5. Kiểm thử

| File | Dòng hiện tại | Nội dung thay đổi |
|---|---:|---|
| `PhotocopyApi.Tests/PhotocopyApi.Tests.csproj` | 1–26, toàn file | Project test mới và các tham chiếu cần thiết. |
| `PhotocopyApi.Tests/PhamViChiNhanhMiddlewareTests.cs` | 1–83, toàn file | Test middleware phạm vi CEO. |
| `PhotocopyApi.Tests/PhamViChiNhanhTests.cs` | 1–56, toàn file | Test ghi nhớ và xóa phạm vi chi nhánh. |
| `PhotocopyApi.Tests/QuyTacQuanTriChiNhanhTests.cs` | 1–82, toàn file | Test quy tắc bảng giá và khuyến mãi. |

### 12.6. Tài liệu thiết kế và kế hoạch

| File | Dòng hiện tại | Nội dung thay đổi |
|---|---:|---|
| `docs/plans/2026-10-04-ceo-pham-vi-chi-nhanh-design.md` | 1–59, toàn file | Thiết kế phạm vi CEO. |
| `docs/plans/2026-10-04-ceo-pham-vi-chi-nhanh.md` | 1–101, toàn file | Kế hoạch triển khai phạm vi CEO. |
| `docs/plans/2026-10-04-bang-gia-khuyen-mai-theo-chi-nhanh-design.md` | 1–80, toàn file | Thiết kế bảng giá và khuyến mãi theo chi nhánh. |
| `docs/plans/2026-10-04-bang-gia-khuyen-mai-theo-chi-nhanh.md` | 1–102, toàn file | Kế hoạch triển khai bảng giá và khuyến mãi theo chi nhánh. |

## 13. Chuẩn hóa cách trình bày code

Đã chuẩn hóa bốn trang có nhiều code viết dồn:

- `PhotocopyAdmin/Pages/QuanTri.razor`
- `PhotocopyAdmin/Pages/KhoVatTu.razor`
- `PhotocopyAdmin/Pages/BangGia.razor`
- `PhotocopyAdmin/Pages/KhuyenMai.razor`

Nguyên tắc áp dụng:

- Mỗi thẻ Razor, điều kiện và vòng lặp được tách thành khối rõ ràng.
- Mỗi field, câu lệnh và nhánh xử lý được đặt trên dòng riêng.
- Object initializer được trình bày mỗi thuộc tính một dòng.
- Chuẩn hóa khoảng trắng quanh toán tử, lambda và biểu thức điều kiện.
- Giữ nguyên endpoint, request, thông báo và logic nghiệp vụ.
- Không áp dụng các tối ưu làm thay đổi thứ tự gọi API trong lượt chuẩn hóa này.

Kết quả kiểm tra sau khi chuẩn hóa:

- `dotnet build PhotocopyChain.sln -c Release --no-restore`: thành công, `0 lỗi`, `0 cảnh báo`.
- `dotnet test PhotocopyChain.sln -c Release --no-build`: thành công, `12/12` test đạt.

## 14. Thanh toán tự động và máy in ảo

Phần hoàn thiện ngày 06/10/2026 đã bổ sung đối soát payOS, tự động chuyển đơn VietQR đã trả tiền sang máy in, chương trình máy in ảo chạy riêng và callback `DA_IN` khi in xong. Không thay đổi schema database; COD vẫn do nhân viên xử lý.

Báo cáo chi tiết, cách chạy, luồng hoạt động và toàn bộ vị trí thay đổi theo số dòng nằm tại:

- `BAO_CAO_THANH_TOAN_TU_DONG_VA_MAY_IN_AO.md`
- `HUONG_DAN_TICH_HOP_PAYOS.md`
- `HUONG_DAN_CHAY_TREN_MAY_NAY.md`
