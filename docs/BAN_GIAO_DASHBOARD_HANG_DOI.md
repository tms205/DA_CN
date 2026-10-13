# Bàn giao dashboard và hàng đợi nổi bật

> Tài liệu này ghi đợt triển khai ban đầu. Dashboard đã được cập nhật tiếp: xem [bàn giao mới ngày 13/10/2026](BAN_GIAO_DASHBOARD_HOAN_THIEN.md). Các mô tả KPI/bố cục cũ bên dưới không còn là bố cục hiện tại.

Ngày cập nhật: 13/10/2026. Phạm vi: Web quản trị PhotocopyChain, phương án A đã duyệt.

## 1. Đã bổ sung

- Mục **Tổng quan** ở đầu sidebar, đường dẫn `/tong-quan`, dành cho CEO và quản lý chi nhánh.
- KPI doanh thu hôm nay, tuần này, tháng này và kỳ tùy chọn; số đơn hoàn tất và bình quân mỗi đơn.
- Bộ lọc hôm nay/tuần này/tháng này/tùy chọn; nhóm báo cáo theo ngày, tuần, tháng; tối đa 366 ngày/lần.
- Biểu đồ đường so sánh kỳ chọn với cùng số ngày liền trước; biểu đồ thanh so sánh chi nhánh trong phạm vi được xem.
- Bảng doanh thu chi tiết: kỳ, số đơn, doanh thu, kỳ đối chiếu và tỷ lệ thay đổi; phân trang 10/20/50 dòng.
- Bố cục tham khảo Gentelella: KPI → biểu đồ chính/đối chiếu 2:1 → so sánh chi nhánh → vận hành → báo cáo chi tiết; giữ nguyên hệ màu/cỡ chữ.
- Vận hành hiện tại: đơn chờ, đang in, chờ bàn giao, hỏa tốc, quá hạn; phân biệt COD và QR; phân bố trạng thái và 5 đơn cần chú ý.
- Cảnh báo kho theo ngưỡng thật, 5 phiếu nhập chờ duyệt đầu tiên; danh sách máy kèm cấu hình, kết nối connector và lệnh báo gần nhất.
- Hàng đợi có dải vàng **Đơn đang chờ tiếp nhận**, số lượng, phân loại việc cần làm và nút **Xem đơn chờ**.
- Bộ lọc chờ tiếp nhận nổi bật; dòng COD cần tiếp nhận tô vàng; QR đang chờ tiền và QR đã trả chờ hệ thống có nhãn riêng. Không dùng hiệu ứng nhấp nháy.
- Nút Xem đơn chờ xóa từ khóa cũ, về trang 1 và lọc trạng thái CHO_XAC_NHAN; không sửa dữ liệu đơn.
- Giữ màu navy, font/cỡ chữ và AppShell hiện tại; bảng chuyển thành thẻ khi hẹp. Không thêm thư viện.

## 2. Cách tính và giới hạn quan trọng

1. Dùng API sẵn có `GET /api/quan-tri/bao-cao/doanh-thu`, lấy tổng theo ngày rồi nhóm lại ở Web.
2. Doanh thu = tổng `TongTien` của đơn `HOAN_THANH`, lọc theo `NgayHoanThanh`; loại trừ `IN_LAI_LOI_QUAY` theo API hiện có. **Không phải lợi nhuận, doanh thu thuần sau hoàn tiền hoặc tổng tiền ngân hàng nhận.**
3. Đơn QR đã thanh toán nhưng còn đang in/chờ bàn giao chưa được tính trong báo cáo này.
4. Tuần bắt đầu thứ Hai. KPI tuần/tháng tính đến hôm nay, theo ngày của trình duyệt, độc lập với khoảng tùy chọn phía dưới.
5. Ví dụ chọn 01–13/10: đối chiếu 18–30/09 (13 ngày liền trước), không đối chiếu toàn tháng 09. Khi nhóm tuần/tháng, kỳ trước được căn theo vị trí ngày của kỳ chọn; bảng ghi rõ ngày đối chiếu thực tế.
6. Tuần/tháng đầu và cuối chỉ cộng ngày thuộc khoảng đã chọn. Ngày không có đơn tính 0.
7. Kỳ trước bằng 0 và kỳ này có doanh thu: ghi “Chưa có cơ sở so sánh %”, không chia cho 0. Hai kỳ cùng 0: 0%.
8. Chi nhánh không có đơn hoàn tất trong kỳ không xuất hiện trong biểu đồ chi nhánh; giao diện ghi rõ điều này.
9. Lỗi API hiển thị thông báo lỗi, không giả thành báo cáo doanh thu 0. Bộ lọc bị khóa khi đang tải; dữ liệu chỉ cập nhật sau khi cả ba yêu cầu thành công.
10. Doanh thu cập nhật khi mở trang/bấm Xem tổng quan; vận hành cập nhật khi mở trang/bấm Làm mới vận hành. Không đăng ký realtime cho dashboard. Hàng đợi vẫn dùng SignalR và nút Làm mới như trước.
11. CEO dùng lựa chọn phạm vi hiện có trên thanh đầu trang; quản lý dùng phạm vi do API xác định từ tài khoản. Web không quyết định quyền dữ liệu thay API.

**Không sửa backend, schema/database, quy trình payOS, máy in, tự tiếp nhận, thu COD hoặc chức năng Mobile. Không đọc/sửa cấu hình mật khẩu. Không tự commit.**

## 3. Các file thay đổi

Đường dẫn gốc mã bên dưới là `MaNguon/PhotocopyChain/`. Số dòng ở bản bàn giao này; có thể thay đổi sau các commit tiếp theo.

| File | Vùng thay đổi | Nội dung |
|---|---|---|
| `PhotocopyAdmin/Pages/TongQuan.razor` | Mới, dòng 1–281 | Dashboard, bộ lọc, tải API, panel vận hành và bảng đối chiếu/phân trang |
| `PhotocopyAdmin/Components/TongQuanVanHanh.razor` | Mới, dòng 1–182 | Tải độc lập nguồn vận hành, bảng/cảnh báo và trạng thái lỗi |
| `PhotocopyAdmin/Components/BieuDoDoanhThu.razor` | Mới, dòng 1–61 | SVG, trục, hai đường/dấu chấm, chú giải và mô tả hỗ trợ truy cập |
| `PhotocopyAdmin/TongQuanBaoCao.cs` | Mới, dòng 1–56 | Kỳ ngày/tuần/tháng, kỳ trước, giới hạn ngày, nhóm dữ liệu và tỷ lệ |
| `PhotocopyAdmin/QuyTacHangDoi.cs` | Mới, dòng 1–23 | Nhãn/CSS phân biệt COD, QR chờ tiền và QR đã trả |
| `PhotocopyAdmin/Layout/MainLayout.razor` | Dòng 32–35 | Mục Tổng quan chỉ hiện cho CEO/quản lý |
| `PhotocopyAdmin/Pages/HangDoiDon.razor` | Dòng 29–39, 59–62, 91–92, 128–134, 455–459 | Dải chờ, chip, dòng/nhãn chờ, nút lọc nhanh |
| `PhotocopyAdmin/wwwroot/css/app-ui-v2.css` | Dòng 1837–1994 | CSS có phạm vi dashboard/hàng đợi, responsive, lưới biểu đồ/panel; không giới hạn khung bộ lọc ở 760px |
| `PhotocopyAdmin/wwwroot/index.html` | Dòng 13 | Đổi phiên bản CSS tránh cache giao diện cũ |
| `PhotocopyApi.Tests/TongQuanBaoCaoTests.cs` | File mới | Kiểm thử ngày/nhóm/tổng/tỷ lệ và biên DateTime |
| `PhotocopyApi.Tests/TongQuanWebTests.cs` | File mới | Render trang, lỗi/rỗng, policy quyền và biểu đồ một ngày/culture |
| `PhotocopyApi.Tests/TongQuanVanHanhTests.cs` | Mới, dòng 1–138 | Đơn đang xử lý, QR/COD, máy thiếu heartbeat, kho/phiếu và lỗi từng nguồn |
| `PhotocopyApi.Tests/HangDoiNoiBatTests.cs` | File mới | Phân loại đơn chờ và lọc nhanh không thay đổi đơn |
| `verification/dashboard-hang-doi.cjs` | File mới | Kiểm thử trình duyệt bằng API fixture, chặn truy cập backend thật |

Kế hoạch và quyết định thiết kế: `docs/plans/2026-10-13-dashboard-hang-doi.md`, `docs/plans/2026-10-13-dashboard-gentelella.md` (từ gốc dự án). Vùng dashboard mới nhất: `TongQuan.razor` dòng 1–281, CSS dashboard/hàng đợi dòng 1837–1994; script kiểm chứng dòng 1–199.

## 4. Kiểm tra đã thực hiện

- TDD: chạy kiểm thử trước khi có helper/trang/nút lọc; xác nhận thất bại do thiếu chức năng, sau đó cài đặt và chạy lại.
- `dotnet build PhotocopyAdmin`: thành công, **0 lỗi, 0 cảnh báo**; đã build vào thư mục bin thông thường để launcher dùng được.
- Toàn solution mới nhất: **288 đạt, 0 thất bại, 15 bỏ qua**, tổng 303. Các bài database tích hợp chưa bật điều kiện chạy; không coi chúng là đã nghiệm thu.
- Bản Release publish thành công. SDK nhắc workload `wasm-tools` chưa cài nên chưa có tối ưu native Wasm tùy chọn; không tự cài thêm dependency/workload.
- Chrome trên bản publish: **390, 768, 1024, 1366, 1920px** đều qua các luồng: dashboard, nhóm ngày/tuần/tháng, kỳ một ngày, phân trang/đổi số dòng, vận hành, lỗi kho riêng và thử lại bằng bàn phím, dữ liệu rỗng/lỗi, cuộn đến cuối báo cáo, nhãn trục không bị cắt, bộ lọc đủ rộng, không tràn ngang trang/vùng main.
- Đổi CEO sang chi nhánh 2 ở desktop: request dùng header phạm vi hiện có và UI chỉ hiển thị số liệu fixture của chi nhánh 2.
- Hàng đợi: cả ba nhãn COD/QR đúng; tìm kiếm không ra đơn rồi dùng nút Xem đơn chờ bằng bàn phím vẫn lọc đúng ba đơn; `aria-pressed` đúng.
- STAFF vào `/tong-quan` bị chặn và không gọi API báo cáo; kiểm thử policy .NET xác nhận CEO/quản lý được vào, STAFF/khách không được vào.
- Không có lỗi JavaScript (`pageerror`) trong các luồng fixture. SignalR bị chặn có chủ đích, nên không dùng phép thử này để chứng minh realtime hoạt động.
- Kiểm tra dịch vụ đang chạy: Web cổng 5064 trả HTTP 200 và index dùng CSS mới; API báo cáo không có token trả HTTP 401.
- `git diff --check`: không có lỗi khoảng trắng (Git chỉ nhắc chuyển LF→CRLF trên Windows).

**Giới hạn bằng chứng:** UI/renderer dùng số liệu fixture. Chưa đối chiếu dashboard với dữ liệu tài chính thật trong SQL, chưa thực hiện giao dịch ngân hàng thật hay kiểm thử thay đổi trạng thái đơn trên DB trong lượt này. Dữ liệu hiển thị thực tế do API hiện có cung cấp, không nhúng fixture vào ứng dụng.

## 5. Cách chạy và kiểm tra nhanh

1. Đã build vào bin thông thường. Chạy lại `MaNguon/scripts/chay_web.cmd` để nạp assembly Web mới, rồi **Ctrl+F5** tránh cache giao diện cũ.
2. Không cần script mới hoặc migrate SQL. Lần kiểm tra cổng gần nhất trong lượt này không có Web lắng nghe 5064; không khẳng định dịch vụ đang chạy nếu chưa bật launcher.
3. Đăng nhập CEO/quản lý → chọn **Tổng quan** ở sidebar, hoặc mở `http://localhost:5064/tong-quan`.
4. CEO chọn toàn chuỗi/chi nhánh trên thanh đầu. Thử chọn Tuần này hoặc Tháng này; chọn nhóm Ngày/Tuần/Tháng rồi bấm **Xem tổng quan**.
5. Xem số tiền/ngày đối chiếu trong bảng cuối trang, chọn 10/20/50 dòng. Đối chiếu lại trang Báo cáo doanh thu cũ với cùng khoảng và phạm vi.
6. Sang Hàng đợi đơn. Nếu có đơn CHO_XAC_NHAN sẽ có dải vàng. Bấm **Xem đơn chờ** để thấy việc cần thu xếp. Dải không hiện khi không còn đơn chờ.

Chạy lại kiểm thử .NET từ `MaNguon/PhotocopyChain`:

```powershell
dotnet test PhotocopyChain.sln --artifacts-path "$env:TEMP\PhotocopyDashboardBuild-20261013" --verbosity minimal
```

Kiểm thử trình duyệt cần Node + Playwright/Chrome có sẵn, **không phải dependency mới của ứng dụng**:

```powershell
dotnet publish PhotocopyAdmin -c Release -o "$env:TEMP\PhotocopyDashboardPreview-20261013"
node verification/dashboard-hang-doi.cjs "$env:TEMP\PhotocopyDashboardPreview-20261013\wwwroot"
```

Nếu dùng Node/Playwright được đóng gói trong Codex, đặt `PLAYWRIGHT_MODULE` trỏ đến thư mục package Playwright của runtime trước khi chạy script. Ảnh kiểm thử trên máy này lưu tại `C:\Users\USER\AppData\Local\Temp\PhotocopyDashboardScreenshots`; đây là dữ liệu thử, không phải doanh thu thật.

## 6. Hướng thiết kế đã áp dụng

Industrial/utilitarian: giữ hệ màu navy, font Be Vietnam Pro/IBM Plex Mono và nhịp 8/16/24px. Điểm nhấn thực dụng là dải vàng báo công việc cần tiếp nhận, tiền và kỳ đối chiếu rõ ràng. Không đổi thương hiệu, không thêm animation hoặc thư viện biểu đồ. Định hướng, DFII và lý do chọn SVG được lưu trong kế hoạch.

## 7. Bổ sung Gentelella — cách đọc và nguồn dữ liệu

Tham khảo cấu trúc KPI, biểu đồ + panel bên cạnh, bảng và widget của [dashboard Gentelella](https://github.com/ColorlibHQ/gentelella/blob/master/production/index.html). Không copy số liệu traffic, visitor, device usage, task demo; không đưa Vite/framework vào Blazor.

- Bộ lọc ngày chỉ áp dụng doanh thu. Vận hành là ảnh chụp lúc tải, có timestamp riêng.
- `api/don-hang/hang-doi`: API có cả đơn kết thúc gần đây; dashboard loại HOAN_THANH và DA_HUY khỏi việc đang xử lý. Quá hạn = chưa kết thúc và giờ hẹn < thời điểm tải; hỏa tốc có thể trùng nhóm chờ/đang in, không cộng các thẻ thành tổng.
- Chờ tiếp nhận gồm COD cần tiếp nhận, QR chưa trả và QR đã trả đang chờ tự tiếp nhận; không gợi ý nhân viên nhận thủ công tất cả QR.
- Bảng đơn cần chú ý: ưu tiên quá hạn, hỏa tốc, rồi ngày đặt tăng dần; tối đa 5 đơn, liên kết mở hàng đợi. Không giả thành bảng lịch sử toàn bộ kỳ.
- `api/quan-tri/kho`: chỉ dùng CanhBao từ API (SoLuong <= MucToiThieu); tối đa 5 mục, kèm tổng mục thiếu và ngưỡng.
- `api/quan-tri/phieu-nhap`: CHO_DUYET, ưu tiên phiếu lập trước, tối đa 5. Đây chỉ là phiếu nhập, không bao gồm đề xuất giá/khuyến mãi.
- `api/quan-tri/may-in` + `api/may-in-ao/trang-thai`: đối chiếu mã máy trong phạm vi. Không có heartbeat hiển thị Chưa có phản hồi; API báo MAT_KET_NOI sau 30 giây. HOAT_DONG trong DB không đồng nghĩa connector kết nối. Bộ đếm kết nối không bao gồm mã máy không nằm trong danh sách; có phản hồi không đồng nghĩa máy đủ quy cách/sẵn sàng in.
- Lỗi từng API vận hành không làm mất các nguồn khác; kết nối lỗi hiển thị Chưa xác định, không giả thành tất cả máy offline. Dữ liệu chỉ đọc, không tự duyệt/nhận kho/chuyển trạng thái đơn.
- Làm mới vận hành gửi 5 GET song song, không polling liên tục. Các API danh sách hiện có chưa phân trang server-side; nếu triển khai dữ liệu lớn cần nâng cấp API riêng, ngoài phạm vi lượt giao diện này.
- Tỷ trọng doanh thu chi nhánh là phần trong tổng phạm vi đang xem; thanh so sánh chuẩn hóa theo chi nhánh doanh thu cao nhất, không phải % KPI mục tiêu.

9 tests vận hành chạy RED trước khi component tồn tại, GREEN sau triển khai. Browser test mới cũng xác nhận thất bại trên bản publish cũ rồi thành công trên bản mới. Các ảnh UI đã được xem trực tiếp; không khẳng định đây là nghiệm thu ngân hàng/database hoặc khả năng tải lớn.
