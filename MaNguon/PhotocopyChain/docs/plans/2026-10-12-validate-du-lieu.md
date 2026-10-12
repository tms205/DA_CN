# Kiểm tra dữ liệu nhập Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Chặn dữ liệu sai trên API, web và mobile, không đổi schema hoặc tài khoản cũ.

**Architecture:** Dùng DataAnnotations sẵn có trên DTO dùng chung cho API/Blazor. Quy tắc liên trường nằm trong IValidatableObject; Flutter kiểm tra tại lớp gọi API để các màn dùng cùng quy tắc, vẫn giữ API là hàng rào cuối. Không thêm thư viện.

**Tech Stack:** .NET 8, Blazor, Flutter, xUnit, flutter_test.

---

### Task 1: DTO và API
- Test: PhotocopyApi.Tests/KiemTraDuLieuTests.cs (định dạng, độ dài, số âm/phân số, null, enum, ngày/giờ/GPS, danh sách và tài khoản cũ).
- Modify: PhotocopyShared/Dtos/{Auth,DatIn,DonHang,Tuan3}; thêm PhotocopyShared/KiemTraDuLieu.
- Viết test dùng Validator.TryValidateObject, chạy và xác nhận thất bại trước khi thêm annotations.
- SĐT: 0 + 9 số ASCII. Email nhập mới: Gmail; đăng nhập/OTP khôi phục: email hợp lệ không giới hạn domain. Không sửa dữ liệu cũ.
- Giới hạn chuỗi/tiền theo Database/01_TaoBang.sql; giữ kiểm tra quyền, tồn kho và tài nguyên tại controller/SQL.
- Kiểm tra liên trường: giao hàng cần địa chỉ/SĐT, khoảng trang/ngày hợp lệ, GPS đủ cặp, giờ mở/đóng, file/dịch vụ trùng hoặc null.
- Run: dotnet test PhotocopyApi.Tests --artifacts-path <temp> --filter FullyQualifiedName~KiemTraDuLieuTests.

### Task 2: Web
- Modify: các form nhân viên, bảng giá, khuyến mãi, kho, quản trị, lập đơn và mật khẩu.
- Dùng helper KiemTraDuLieu.LayLoi trước gửi; trả lỗi tiếng Việt, không đổi dữ liệu/logic nghiệp vụ.

### Task 3: Mobile
- Create: lib/kiem_tra_du_lieu.dart, test/kiem_tra_du_lieu_test.dart.
- Modify: lib/api.dart và thông báo Gmail ở đăng ký.
- Test-first kiểm tra payload từng endpoint, tiền/số lượng, địa chỉ, ghi chú, OTP và mật khẩu; không tự xóa ký tự sai trong số điện thoại.
- Run: flutter test; flutter analyze.

### Task 4: Hồi quy và bàn giao
- Build solution vào thư mục artifacts riêng, build APK nếu thành công; không dừng app đang chạy tự động.
- Ghi báo cáo docs/ValidateDuLieu_2026-10-12.md: phạm vi đã rà, quy tắc, file, kết quả kiểm tra và giới hạn.
- Không tạo commit hoặc chạy migration trong nhiệm vụ này.
