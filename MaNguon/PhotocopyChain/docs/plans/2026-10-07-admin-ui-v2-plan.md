# Kế hoạch triển khai giao diện quản trị V2

## 1. Nâng cấp AppShell

- Chỉnh `PhotocopyAdmin/Layout/MainLayout.razor`.
- Thêm topbar dùng chung, phạm vi chi nhánh, nút mở/đóng menu và lớp phủ mobile.
- Giữ nguyên điều kiện phân quyền và đường dẫn của từng menu.

## 2. Tạo hệ thống giao diện V2

- Tạo `PhotocopyAdmin/wwwroot/css/app-ui-v2.css`.
- Ghi đè token màu, khoảng cách, bo góc, focus, card, form, bảng và trạng thái.
- Thiết kế sidebar desktop, compact và drawer mobile.

## 3. Hoàn thiện responsive

- Kiểm soát tràn ngang tại bảng và grid.
- Sắp xếp lại tiêu đề trang, bộ lọc và hành động ở 768 px và 390 px.
- Tôn trọng reduced motion và bổ sung kiểu in cơ bản.

## 4. Tích hợp và xác minh

- Nạp CSS V2 trong `PhotocopyAdmin/wwwroot/index.html`.
- Chạy `dotnet build PhotocopyChain.sln`.
- Mở ứng dụng và smoke test desktop/mobile nếu dịch vụ API hiện tại cho phép.
- Ghi lại những phần đã xác minh và giới hạn còn lại.

