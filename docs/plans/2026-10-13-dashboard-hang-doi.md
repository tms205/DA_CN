# Dashboard và hàng đợi nổi bật — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Thêm tổng quan doanh thu ngày/tuần/tháng và làm rõ đơn chờ tiếp nhận.

**Architecture:** Dashboard Blazor dùng API báo cáo hiện có (đơn HOAN_THANH, loại trừ in lại lỗi quầy). Hàm tính kỳ, nhóm dữ liệu và so sánh thuần C#; biểu đồ SVG không thêm thư viện. Hàng đợi chỉ đổi cách hiển thị, không đổi quy trình đơn, thanh toán hay database.

**Tech Stack:** .NET 8, Blazor WASM, xUnit, CSS, SVG.

## Quyết định đã duyệt

- Phương án A: CSS/SVG; giữ màu navy, font Be Vietnam Pro/IBM Plex Mono.
- `/tong-quan` cho CEO/quản lý; `/` vẫn là hàng đợi. API kiểm quyền và phạm vi chi nhánh như trước.
- Doanh thu là giá trị đơn hoàn tất theo ngày hoàn thành, không phải lợi nhuận hoặc tổng tiền ngân hàng nhận.
- Tuần bắt đầu thứ Hai; kỳ đối chiếu là cùng số ngày liền trước. Không gọi nhầm thành tháng trước/tuần trước.
- Ngày thiếu dữ liệu hiển thị 0; lỗi tải phải báo lỗi, không giả thành doanh thu 0.
- Đơn CHO_XAC_NHAN có dải thông báo, bộ lọc nổi bật, nhãn hành động; COD cần tiếp nhận, QR chưa trả chờ thanh toán, QR đã trả chờ hệ thống xử lý.
- Giữ sắp xếp mới nhất đầu, số dòng/trang và toàn bộ thao tác cũ; không nhấp nháy.
- Giới hạn tùy chọn 366 ngày; không sửa schema, không ghi dữ liệu, không thêm dependency.

## Thiết kế

Industrial/utilitarian administration: KPI ngày/tuần/tháng, bộ lọc, biểu đồ xu hướng so sánh, cột so sánh chi nhánh, bảng đối chiếu chi tiết bên dưới. Điểm nhấn là dải vàng chờ tiếp nhận và số liệu tiền rõ ràng, không trang trí động. Nhịp khoảng cách 8/16/24px, dùng biến màu hiện có. DFII chủ quan: 4 + 5 + 5 + 5 − 4 = 15. Responsive: không tràn ngang toàn trang; bảng riêng chuyển hàng thành thẻ ở màn nhỏ.

## Task 1 — Quy tắc và kiểm thử trước code

- Create: `MaNguon/PhotocopyChain/PhotocopyApi.Tests/TongQuanBaoCaoTests.cs`, `HangDoiNoiBatTests.cs`.
- Create: `MaNguon/PhotocopyChain/PhotocopyAdmin/TongQuanBaoCao.cs`, `QuyTacHangDoi.cs`.
- Test kỳ qua năm/tháng nhuận, thứ Hai, kỳ trước bằng số ngày, nhóm ngày/tuần/tháng và ngày trống, tổng nhiều chi nhánh, tỷ lệ khi kỳ trước bằng 0; phân loại COD/QR.
- RED: reflection `Assert.NotNull` khi lớp chưa tồn tại; GREEN: cài hàm thuần, chạy lại.
- Run từ `MaNguon/PhotocopyChain`: `dotnet test PhotocopyApi.Tests --filter "FullyQualifiedName~TongQuanBaoCaoTests|FullyQualifiedName~HangDoiNoiBatTests" --artifacts-path C:\Users\USER\AppData\Local\Temp\PhotocopyDashboardBuild-20261013 --verbosity minimal`.

## Task 2 — Dashboard và hàng đợi

- Create: `PhotocopyAdmin/Pages/TongQuan.razor`, `PhotocopyAdmin/Components/BieuDoDoanhThu.razor`.
- Modify: `PhotocopyAdmin/Layout/MainLayout.razor`, `Pages/HangDoiDon.razor`, `wwwroot/css/app-ui-v2.css`, `wwwroot/index.html`.
- Trước UI: test render dashboard loading/error/empty và tổng tiền; test render hàng đợi có nhãn chờ và bộ lọc không giữ từ khóa cũ.
- Load daily aggregate current, previous và phạm vi KPI ngày/tuần/tháng; chỉ cập nhật khi các request thành công. Khóa nút/bộ lọc trong lúc tải.
- SVG có tên mô tả, chú giải, không tọa độ NaN và bảng số liệu đối chiếu hỗ trợ đọc màn hình.

## Task 3 — Kiểm tra và bàn giao

- Run: `dotnet test PhotocopyChain.sln --artifacts-path C:\Users\USER\AppData\Local\Temp\PhotocopyDashboardBuild-20261013 --verbosity minimal`.
- Build bình thường để người dùng chạy launcher. Không tự commit.
- Kiểm tra trình duyệt dữ liệu fixture: dashboard, đổi nhóm, chi nhánh/role, loading/error/empty, pending filter, 390/768/1366/1920px, bàn phím. Ghi rõ fixture không phải kiểm thử ngân hàng thật.
- Ghi kết quả, file thay đổi và cách chạy vào `docs/BAN_GIAO_DASHBOARD_HANG_DOI.md`.

## Trạng thái

- Đã chốt phạm vi/phương án; đang thực hiện trong phiên hiện tại, tuần tự, không tạo agent.
- Chưa triển khai khi ghi kế hoạch; kết quả kiểm tra thực tế sẽ ghi trong file bàn giao.
- Hoàn tất: helper/trang/biểu đồ/đơn chờ, build thường và Release publish. 279 kiểm thử đạt, 15 DB skip; browser fixture qua 4 kích thước. Chi tiết và giới hạn kiểm chứng tại `docs/BAN_GIAO_DASHBOARD_HANG_DOI.md`.
