# Sidebar, header và footer — 13/10/2026

## Phạm vi đã chốt

Người dùng xác nhận sửa UI chung của trang quản trị: nhóm đầu Dashboard, icon đúng chức năng, nâng cấp header hiện có, footer cuối nội dung. Login/quên mật khẩu giữ layout riêng. Không sửa quyền, API, CSDL hoặc nghiệp vụ.

## Quyết định

- Chọn nâng cấp layout sẵn có, không thêm thanh header thứ hai hay thư viện icon. Header dùng nhãn nhóm + tên màn hình; cập nhật khi điều hướng.
- Footer theo luồng nội dung: trang ngắn nằm dưới cùng, trang dài cuộn tới cuối. Không dùng footer cố định vì sẽ chiếm chiều cao và che thao tác trên điện thoại.
- SVG stroke cùng hệ icon hiện có, viewBox 24×24; nhóm 20px, mục con 18px. Các mục sidebar không dùng lại cùng hình.
- Industrial/utilitarian, navy hiện có, Be Vietnam Pro/IBM Plex Mono, nhịp 8/16/24px. DFII chủ quan: 3+5+5+5−4=14. Dấu nhận diện là navigation rõ chức năng, không dùng hiệu ứng trang trí.
- Không đổi dữ liệu tài khoản, không thêm thông tin liên hệ giả hoặc trạng thái “kết nối tốt” chưa kiểm tra. Quy mô/performance: không có request/timer mới; header/footer là HTML/CSS.
- Giữ nhóm mở độc lập, sidebar thu gọn/drawer, bộ chọn chi nhánh, badge và bảo vệ vai trò.

## Kiểm chứng

1. RED: test SVG duy nhất, render layout tên Dashboard/header/footer/phạm vi theo CEO/quản lý/nhân viên trên bản cũ.
2. GREEN: chỉ sửa Svg.cs, MainLayout.razor, CSS và version CSS.
3. Build Web, chạy bộ .NET; publish và kiểm Chrome 390/768/1024/1366/1920px. Kiểm điều hướng, footer cuối nội dung, drawer/thu gọn, các nhóm mở độc lập và layout đăng nhập riêng.
4. Ghi kết quả vào tài liệu bàn giao, không commit tự động.

## Kết quả

- RED 15 bài mới lỗi đúng kỳ vọng trước sửa; sau triển khai đều đạt. ARIA có test tái hiện riêng rồi sửa tối thiểu sang chuỗi true/false.
- Full .NET cuối: 316 đạt, 0 lỗi, 16 skip / 332.
- Build Web 0 warning / 0 error; publish Release thành công.
- Chrome fixture 390/768/1024/1366/1920px đều đạt, gồm điều hướng CEO/quản lý, drawer/thu gọn, footer và auth layout riêng. Không có ghi backend thật.
- Web đang chạy với CSS mới, API không cần restart. Không đổi DB hoặc nghiệp vụ.
- Bàn giao thêm ở mục 8 của [dashboard hoàn thiện](../BAN_GIAO_DASHBOARD_HOAN_THIEN.md).

## Điều chỉnh footer theo yêu cầu mới

Người dùng yêu cầu footer không trượt theo trang, bỏ mã đồ án và dùng email/số điện thoại đã cung cấp. Quyết định mới thay footer theo luồng ở phần trên: đặt footer ngoài main cuộn, vẫn trong layout flex để dành chiều cao riêng, không che thao tác. Thêm slogan “In rõ nét — Nhận đúng hẹn.” và link mailto/tel, giữ navy/font/nhịp spacing utilitarian (DFII 14 như thiết kế trước).

Đã cập nhật test trước sửa và xác nhận RED; sau sửa full .NET 316 đạt / 16 skip / 332, Web build và publish đạt, browser 5 chiều rộng cùng 390×640px đạt. Bàn giao mới ở mục 9; backend/DB không đổi.

## Tinh chỉnh tiếp theo góp ý thẩm mỹ

Sidebar 361px xuống 325px, giữ chữ và cấu trúc. Footer utilitarian giữ navy/font, tách nhãn với giá trị, bỏ logo lặp, hạ mức nhấn copyright; hai cột liên hệ ở mobile. Dấu nhận diện là thanh thông tin liên hệ rõ chức năng, không dùng hiệu ứng trang trí; DFII tiếp tục 14. Kết quả kiểm tra và chi tiết ở mục 10 bàn giao.
