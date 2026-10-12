# Tích hợp thanh toán payOS Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Tạo link thanh toán payOS thật cho từng đơn VietQR, mở trang checkout chính thức trên mobile và tự động xác nhận đơn bằng webhook đã kiểm tra chữ ký.

**Architecture:** Giữ nguyên bảng `ThanhToan` và dùng `MaDonHang` làm `orderCode` duy nhất của payOS. API tạo mới hoặc lấy lại payment link bằng SDK, dựng lại checkout URL từ `paymentLinkId`, còn webhook là nơi duy nhất cập nhật trạng thái thanh toán và phát SignalR.

**Tech Stack:** ASP.NET Core 8, Dapper, SQL Server, payOS .NET SDK 2.1.0, Flutter, `url_launcher`, xUnit.

---

### Task 1: Quy tắc payOS có thể kiểm thử độc lập

**Files:**
- Create: `MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/QuyTacPayOS.cs`
- Create: `MaNguon/PhotocopyChain/PhotocopyApi.Tests/QuyTacPayOSTests.cs`

**Steps:** Viết test thất bại cho mô tả đơn, PublicBaseUrl, checkout URL và số tiền; triển khai tối thiểu; chạy lại toàn bộ test API.

### Task 2: Đăng ký SDK và cấu hình an toàn

**Files:**
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/Program.cs`
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/appsettings.json`
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/PhotocopyApi.csproj`

**Steps:** Đọc khóa từ User Secrets, đăng ký `PayOSClient`, thêm `PayOS:PublicBaseUrl` rỗng trong source và tắt giả lập mặc định.

### Task 3: API tạo/lấy link và callback

**Files:**
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs`
- Modify: `MaNguon/PhotocopyChain/PhotocopyShared/Dtos/DonHang/DonHangDtos.cs`

**Steps:** Thêm endpoint có xác thực; kiểm tra quyền, phương thức, trạng thái, hạn và số tiền từ CSDL; lấy link cũ hoặc tạo link mới hết hạn sau 15 phút; thêm return/cancel page chỉ để hiển thị.

### Task 4: Hoàn thiện webhook

**Files:**
- Modify: `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs`

**Steps:** Xác minh HMAC, VND, mã đơn, VietQR, số tiền và trạng thái; gọi thủ tục theo hướng idempotent; phát SignalR; chấp nhận webhook mẫu hợp lệ.

### Task 5: Mobile mở checkout chính thức

**Files:**
- Modify: `MaNguon/photocopy_mobile/pubspec.yaml`
- Modify: `MaNguon/photocopy_mobile/pubspec.lock`
- Modify: `MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart`

**Steps:** Thêm `url_launcher`; gọi API link một lần; hiện nút mở payOS; giữ polling và SignalR; có nút thử lại khi lỗi.

### Task 6: Kiểm thử và tài liệu bàn giao

**Files:**
- Create: `HUONG_DAN_TICH_HOP_PAYOS.md`

**Steps:** Chạy test/build/analyze; cấu hình URL tunnel; xác nhận webhook khi có thể; ghi file/dòng sửa, luồng hoạt động, cách chạy và giới hạn Quick Tunnel.
