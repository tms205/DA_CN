# Thiết kế giao diện quản trị V2

## Mục tiêu

Chuyển phong cách sáng xanh - trắng từ bản mẫu React/Figma Make sang Web quản trị Blazor hiện tại, đồng thời giữ nguyên API, dữ liệu, phân quyền và các luồng nghiệp vụ đang hoạt động.

## Quyết định thiết kế

- Dùng bản mẫu React/Vite trong `Web quản trị PhotocopyChain.zip` làm tài liệu tham chiếu giao diện, không dùng nó làm ứng dụng chạy thật.
- Giữ nguyên Blazor WebAssembly, các trang Razor, DTO, API và cơ sở dữ liệu.
- Dùng màu chính `#0F4C81`, nền `#F5F6F8`, bề mặt trắng và chữ `#18212F`.
- Sidebar rộng 240 px trên desktop, thu gọn còn 76 px trên màn hình trung bình và chuyển thành drawer trên màn hình nhỏ.
- Topbar cao 64 px, luôn hiển thị phạm vi chi nhánh và nút mở menu trên thiết bị nhỏ.
- Bảng rộng cuộn trong vùng bảng, không làm toàn trang tràn ngang.
- Các nút, trường nhập, thẻ, bộ lọc, trạng thái và focus dùng cùng một hệ thống token.
- Không đưa dữ liệu giả `localStorage`, QR giả, OTP giả, máy in giả hoặc panel điều khiển demo vào ứng dụng thật.

## Phạm vi

- `PhotocopyAdmin/Layout/MainLayout.razor`: bổ sung AppShell, topbar và drawer responsive.
- `PhotocopyAdmin/wwwroot/css/app-ui-v2.css`: lớp giao diện V2 ghi đè có tổ chức.
- `PhotocopyAdmin/wwwroot/index.html`: nạp stylesheet V2 sau stylesheet cũ.

## Ngoài phạm vi

- Không đổi database hoặc migration.
- Không sửa API và logic nghiệp vụ.
- Không thay React cho Blazor.
- Không thêm thư viện giao diện mới.

## Yêu cầu phi chức năng

- Hoạt động tốt tại 1440, 1024, 768 và 390 px.
- Nút điều khiển chính trên thiết bị cảm ứng có kích thước tối thiểu 44 px.
- Có focus rõ ràng khi dùng bàn phím và tôn trọng `prefers-reduced-motion`.
- Giữ code tiếng Việt, dễ đọc và bám cấu trúc hiện tại.

## Nhật ký quyết định

| Ngày | Quyết định | Lý do |
|---|---|---|
| 2026-10-07 | Chọn giao diện sáng xanh - trắng (phương án A) | Người dùng xác nhận muốn đồ án giống bản Figma Make |
| 2026-10-07 | Dùng stylesheet ghi đè V2 | Hạn chế sửa CSS nghiệp vụ cũ, dễ kiểm tra và hoàn tác |
| 2026-10-07 | Không chuyển mã React vào Blazor | Bản React dùng dữ liệu mô phỏng và không có backend thật |

