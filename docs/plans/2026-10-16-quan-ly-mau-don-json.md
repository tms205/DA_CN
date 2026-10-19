# Quản lý mẫu đơn bằng JSON Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** CEO tải PDF kèm tên/loại, sửa, thay file, ẩn/hiện và gỡ mẫu trên web; Web/mobile dùng cùng thư viện, không sửa database.

**Architecture:** Giữ MauDonPdf làm nguồn khởi tạo 18 mẫu cũ. ThuVienMauDon lưu catalog.json và các phiên bản PDF/PNG bất biến ngoài mã nguồn; ghi JSON nguyên tử dưới khóa file. File đã chọn cho đơn tiếp tục đi qua TaiLen và được sao chép độc lập. API quản trị chỉ CEO; API chọn mẫu chỉ trả mẫu đang hiện. Giữ style navy và bố cục quản trị hiện có (industrial/utilitarian, ưu tiên thao tác rõ và responsive).

**Tech Stack:** ASP.NET Core 8, Blazor WASM, Flutter; PdfPig hiện có để kiểm tra PDF, PDFtoImage để tạo PNG lúc tải lên. Không thêm bảng, migration hay dịch vụ trả phí.

## Quyết định đã được duyệt

- Người dùng chọn cách 2: file + JSON, không database. Làm ngay trong checkout hiện có để giữ các thay đổi trước đó.
- PDF trước; Word cần xuất PDF. Tối đa 20 MB, 50 trang; từ chối PDF lỗi/khóa/đính kèm mã thực thi.
- Thư viện dùng chung toàn chuỗi, CEO quản lý. Tên và loại bắt buộc; loại cho phép nhập mới.
- Gỡ mẫu chỉ gỡ khỏi danh mục; giữ phiên bản file để bảo toàn và phục hồi. Không xóa tài liệu của đơn.
- Mỗi lần sửa phải có phiên bản đang đọc; xung đột trả 409. JSON hỏng trả lỗi, không âm thầm ghi đè bằng danh mục gốc.
- Chỉ khởi tạo dữ liệu thật khi API mới được chạy; không restart dịch vụ đang dùng hay sửa database trong kiểm thử.

## Task 1 — Kiểm thử HTTP trước (RED)

Tạo `MaNguon/PhotocopyChain/PhotocopyApi.Tests/QuanLyMauDonTests.cs` với Kestrel/fixture xác thực riêng, thư mục tạm riêng, không SQL. Kiểm tra CEO CRUD, tên/loại/file bắt buộc, phân quyền, preview, thay file, ẩn/gỡ, phiên bản và khởi động lại. Chạy `dotnet test ... --filter FullyQualifiedName~QuanLyMauDonTests --artifacts-path <temp>`; xác nhận lỗi do endpoint chưa có.

## Task 2 — JSON store, PDF và API (GREEN)

Thêm `DichVu/ThuVienMauDon.cs`, `DichVu/XemTruocMauDon.cs`, `Controllers/MauDonController.cs`. Mở rộng DTO không phá hợp đồng mobile; đăng ký singleton ở Program, chuyển các endpoint mẫu của FileInController sang thư viện động. Kiểm tra file, đường dẫn, giới hạn trang/kích thước; lưu PDF/PNG trước rồi publish JSON. Giữ bản cũ; dùng lock giữa tiến trình và atomic replace. Chạy lại test Task 1 cùng MauDonHttpTests/MauDonTaiSanTests.

## Task 3 — Trang quản trị và thư viện chọn mẫu

Thêm `PhotocopyAdmin/Pages/MauDon.razor` và CSS riêng; gắn menu Quản lý, icon riêng. Có lọc, phân trang, form upload/sửa/thay file, xác nhận gỡ, xem trước toàn màn hình. Giữ các luồng lập đơn cũ; thêm làm mới thư viện Web/mobile. Kiểm thử source regression và trình duyệt trên host QA không DB.

## Task 4 — Nghiệm thu và bàn giao

Chạy full .NET tests, build API/Web bằng output riêng khi API cũ đang chạy; kiểm tra mobile khi có chỉnh sửa. Kiểm chứng dữ liệu qua restart store, concurrent writes, catalog hỏng không mất dữ liệu, PNG thật, quyền các vai trò. Tạo `docs/QUAN_LY_MAU_DON_JSON.md`, cập nhật phần thư viện trong pipeline. Báo rõ kết quả thực chạy và phần chưa nghiệm thu trên API thật; không tự commit/restart.

## Mobile checkpoint / đánh giá UI

- Platform: Android hiện có; Flutter, Navigator stack, khách hàng trên điện thoại. Giữ nguyên navigation/theme, không thiết kế lại app hay thêm dependency.
- MFRI: (platform 5 + accessibility 5) − (interaction 1 + performance 1 + offline 2) = 6. DFII Web: giao diện quản trị tiện dụng, tái dùng tokens/nav/table sẵn có; không thêm hiệu ứng trang trí.
- Đã đọc mobile-design-thinking, touch-psychology, mobile-performance, mobile-backend, mobile-testing, mobile-debugging, platform-android.
- Áp dụng: nút Material cao ≥48dp, có loading/retry, gửi phiên bản để không chọn nhầm PDF đã thay. Không polling nền, không lưu lệnh in offline, không tự thử lại POST khi mạng lỗi.
- Chỉnh nhỏ màn chọn mẫu: làm mới theo yêu cầu, xóa nhóm lọc không còn tồn tại, ẩn liên kết nguồn trống. Tránh đổi cấu trúc screen/global state hoặc thêm thư viện xem PDF lên app.

## Tiến độ chốt

- Task 1–4 đã thực hiện: kiểm thử RED/GREEN, JSON/PDF/API, trang quản lý + Web/mobile, tài liệu và regression.
- Full .NET gồm browser mới + scan: 462 đạt / 20 bỏ qua; full Flutter: 118 đạt; analyze phần app sửa không issue; API/Web build không warning/error.
- Không sửa SQL, không khởi động lại dịch vụ đang dùng; output/build và API test cô lập. Người dùng chạy launcher lúc không có giao dịch để nạp phiên bản mới.
