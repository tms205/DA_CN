# Popup thao tác Web Admin Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Mọi thao tác thêm/sửa bản ghi quản trị mở form popup; xóa/gỡ và thay đổi quan trọng có popup xác nhận, không thay backend/SQL/quyền.

**Architecture:** Tái dùng native dialog đã được duyệt ở Mẫu đơn. Một component HopThoaiQuanTri nhận trạng thái mở, title, busy, error và hai vùng nội dung/nút; một XacNhanThaoTac cung cấp popup xác nhận có kết quả bool. Các trang giữ nguyên handler/endpoint/model, chỉ chuyển vùng nhập sang popup. JS quản lý Esc, vòng Tab, focus; CSS scoped giữ theme navy/Be Vietnam Pro hiện tại.

**Tech Stack:** Blazor WASM .NET 8, native HTML dialog, CSS hiện có, xUnit/HtmlRenderer và Playwright. Không dependency mới.

## Chốt yêu cầu / quyết định

- Người dùng yêu cầu áp dụng cùng kiểu popup đã duyệt cho toàn bộ Thêm/Xóa/Sửa trên Web Admin. Không thiết kế lại API/quy tắc nghiệp vụ.
- Popup mặc định form 760px, form dài 1040px; header/footer cố định, thân cuộn, không tràn ngang. Lỗi nằm trong popup; không đóng khi đang lưu. Esc/Tab/focus như Mẫu đơn.
- Không tự tạo thao tác Xóa ở trang vốn chỉ cho khóa/ẩn; giữ đúng quyền và ý nghĩa cũ. Không gọi API khi chỉ mở/đóng popup.
- Người dùng đã chọn đưa cả quy trình lập đơn tại quầy vào popup. Popup này gần toàn màn hình, mở sẵn khi vào trang đã chọn chi nhánh; tạm đóng giữ khách/file trong phiên trang. Tự mở ca không thêm bước xác nhận.
- Cân nhắc inline (ít quản lý focus nhưng phải cuộn), drawer (khác mẫu đã duyệt), popup dùng chung (đúng yêu cầu, giảm lặp). Chọn popup. DFII 15; utilitarian, giữ token/font và nhịp 8/16/24px, không thêm hiệu ứng trang trí.
- Quy mô đồ án hiện tại, không thêm request/polling, không logging secret; schema và dữ liệu thật không đụng. Giữ checkout đang có thay đổi và không tự commit/restart.

## Task 1 — Popup dùng chung (root)

1. Thêm `PhotocopyApi.Tests/PopupQuanTriTests.cs`; chạy `dotnet test PhotocopyApi.Tests --filter FullyQualifiedName~PopupQuanTriTests --artifacts-path <temp>` và quan sát RED do component chưa tồn tại.
2. Tạo `PhotocopyAdmin/Components/HopThoaiQuanTri.razor` + CSS: Mo, Dong, TieuDe, MoTa, DangLuu, Loi, Rong, NoiDung, Chan. Bổ sung `XacNhanThaoTac.razor`: HoiAsync thông báo/title/nút/nguy hiểm trả bool; điều hướng đi thì trả false.
3. Dùng helper JS popup trong `wwwroot/index.html`, giữ alias cũ cho Mẫu đơn. Kiểm thử GREEN và browser mở/đóng/focus/lỗi/busy.

## Task 2 — Danh mục dịch vụ/giá/khuyến mãi

File `Pages/DichVu.razor`, `BangGia.razor`, `KhuyenMai.razor`: kiểm thử trước khi đổi; form popup theo đúng vai trò/phạm vi cũ; nút Thêm cho form hiện sẵn, lỗi trong popup, không đóng khi lỗi. Xác nhận thay đổi trạng thái khuyến mãi.

## Task 3 — Nhân sự, khách hàng, phê duyệt, đánh giá

File `Pages/NhanVien.razor`, `KhachHang.razor`, `PheDuyet.razor`, `DanhGia.razor`: popup tạo nhân viên, cấp lại MK và kết quả tạm, chi tiết khách/khóa mở, duyệt/từ chối/hoàn tiền/ẩn. Không log hoặc đưa mật khẩu tạm vào DOM nền. Test quyền và không gửi khi Hủy.

## Task 4 — Chi nhánh/máy in/kho

File `Pages/QuanTri.razor`, `KhoVatTu.razor`: popup thêm/sửa CN, thêm máy, sửa chỉ số trang; xác nhận bảo trì/nhận kho. Không sửa/đổi giá, tồn kho hoặc thủ tục SQL. Test gửi đúng payload và phạm vi.

## Task 5 — Tác vụ bổ sung và nghiệm thu

Root rà ThongTinCaNhan, MauDon(gỡ/ẩn), BanGiao, GiaoHang, HangDoiDon(hủy/phục hồi/phân máy), CaLamViec và xóa file POS. Giữ bảng điều khiển thiết bị; bản xem trước máy quét được mở trên popup lập đơn và xử lý bàn phím riêng. Các form ghi bản ghi/đối soát dùng popup; màn xác thực giữ trang riêng.

Browser fixture chặn toàn bộ DB/PayOS/in thật: từng nút mở đúng popup; Hủy không gửi request; lỗi giữ form; gửi thành công đóng đúng; 320/390/768/1440px, bàn phím/busy. Chạy full .NET regression, build Web trong project; docs bàn giao ghi chính xác trang đã đổi và phần chưa nghiệm thu. Không restart API.

## Hoàn thành

- Task 1–5 đã triển khai; toàn bộ lập đơn theo lựa chọn bổ sung của người dùng cũng dùng popup lớn.
- Bộ .NET đạt 474/474 bài được bật, 22 bài bỏ qua theo cấu hình; chạy riêng hai browser fixture mẫu đơn/máy quét đều đạt.
- Browser CRUD/workflow giả lập đạt trên bốn kích thước, kiểm tra quyền tại 1440px. Build Web trong project 0 lỗi/0 cảnh báo.
- Phạm vi, file và cách chạy: `docs/POPUP_WEB_ADMIN.md`. Không sửa backend/schema, không thao tác đơn thật, không restart các tiến trình API/Web/connector của người dùng.
