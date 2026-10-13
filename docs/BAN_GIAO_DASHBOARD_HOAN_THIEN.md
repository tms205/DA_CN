# Bàn giao dashboard hoàn thiện

Ngày: 13/10/2026. Các mục 1–10 ghi lịch sử Dashboard/Web và API báo cáo chỉ đọc. Cập nhật mới: bỏ hai khối Dashboard, sửa liên hệ và bổ sung danh mục quản lý; xem mục 11.

## 1. Kết quả chính

- Tách Tổng quan thành **Kinh doanh | Vận hành**. CEO mặc định Kinh doanh; quản lý chi nhánh mặc định Vận hành. Nhân viên không được cấp thêm quyền báo cáo.
- Kinh doanh dùng ba KPI theo **cùng kỳ đã chọn**: doanh thu, số đơn hoàn tất, giá trị trung bình/đơn (AOV), kèm đối chiếu kỳ trước. Không còn bốn thẻ cố định/tùy chọn dễ nhầm và lặp tiền.
- Preset hôm nay, tuần này, tháng này tự áp dụng. Ngày nhập **dd/MM/yyyy**, kiểm tra ngày thật và giới hạn kỳ; ngày tùy chọn vẫn có nút áp dụng để tránh gọi API khi đang gõ.
- Biểu đồ ngày là cột; tuần/tháng là đường. Trục tiền dùng bước tròn, số tiền đầy đủ với ₫. Kỳ trước toàn 0 không vẽ đường/cột đối chiếu; không hiện tăng trưởng vô hạn.
- Chỉ hiển thị so sánh chi nhánh khi có từ hai nhóm dữ liệu. Giữ một nút đến báo cáo chi nhánh; giải thích dài chuyển vào phần mở rộng bằng bàn phím.
- Có cơ cấu dịch vụ và phương thức thanh toán từ dữ liệu hệ thống. Khối giờ cao điểm/hiệu quả/đánh giá đã bỏ khỏi giao diện theo yêu cầu sau đó (mục 11).
- Vận hành có đơn cần tiếp nhận/hỏa tốc/quá hạn, kho theo ngưỡng, máy theo heartbeat, ca mở, ca chờ xác nhận, ca có chênh lệch và phiếu chờ duyệt.
- Cảnh báo cam chỉ bật khi có việc tương ứng; khi bằng 0 dùng trạng thái trung tính. Lỗi tải một nguồn không làm mất các nguồn còn lại và không giả số 0.
- Hàng đợi, heartbeat và chỉ số ca tự cập nhật mỗi **60 giây** khi mở Vận hành và tab trình duyệt hiển thị; chuyển sang Kinh doanh dừng timer. Không quét lịch sử kho/phiếu bằng timer. Có thời điểm cập nhật riêng.
- Sidebar tách **Dashboard / Vận hành / Ca & tiền / Quản lý / Cấu hình**; nhóm đóng/mở độc lập. CEO có badge tổng phiếu nhập + hoàn tiền chờ duyệt; lỗi tải hiện “?” thay vì 0.
- Giữ màu navy, font/cỡ chữ hiện có và kiến trúc Blazor/Dapper. Tăng tương phản chữ phụ trong dashboard; không thêm thư viện.

## 2. Nhận xét của Claude: áp dụng có chọn lọc

**Đúng và đã sửa:** thông tin lặp, cảnh báo giả, nhầm phạm vi KPI, thiếu AOV/số đơn rõ ràng, ưu tiên vận hành với quản lý, nhóm sidebar, trục biểu đồ, kỳ đối chiếu bằng 0, định dạng ngày, thời điểm cập nhật và báo cáo chuyên ngành.

**Cần điều chỉnh cách hiểu:**

- Bố cục phải theo vai trò, không ép tất cả người dùng cùng một thứ tự. Hai tab giải quyết việc CEO cần kinh doanh còn quản lý cần vận hành.
- Trước đây kho/máy đã có ở phần dưới; không thể kết luận “thiếu” chỉ từ ảnh bị cắt. Bản này giữ chúng trong Vận hành và bổ sung chỉ số ca/phê duyệt.
- Giữ phạm vi chi nhánh ở sidebar và bộ chọn là cần thiết, nhất là khi thu gọn sidebar; không xóa mọi lần nhắc chi nhánh.
- Line chart không mặc định sai: dùng cột cho ngày rời rạc, đường cho xu hướng tuần/tháng.
- Không dựng báo cáo scan, phương thức thanh toán hoặc cảm biến kẹt giấy nếu dữ liệu/connector chưa hỗ trợ.
- Không gọi thời gian từ đặt đến hoàn tất là “tốc độ in” hay SLA máy. Chỉ số đúng hẹn dựa giờ hẹn lưu trong đơn.
- Không có dữ liệu không đồng nghĩa 0 hoặc 100%. AOV/mốc thời gian/đánh giá thiếu mẫu hiển thị “Chưa có…” rõ ràng.

## 3. Định nghĩa dữ liệu cần nắm khi bảo vệ

### Kinh doanh

- Doanh thu = tổng `DonHang.TongTien` của đơn `HOAN_THANH`, lọc theo `NgayHoanThanh`, loại `IN_LAI_LOI_QUAY`.
- **Không phải** tiền ngân hàng nhận, tiền mặt trong két, lợi nhuận hoặc doanh thu thuần sau hoàn tiền. Đơn đã trả QR nhưng chưa hoàn tất chưa nằm trong doanh thu này.
- AOV = doanh thu / số đơn hoàn tất. Đối chiếu cùng số ngày liền trước; tuần bắt đầu thứ Hai. Kỳ trước bằng 0 và kỳ này có tiền không tính %.
- Cơ cấu dịch vụ dùng số tiền chi tiết đã chốt. `ChiTietDonHang.ThanhTien` đã chứa gia công: tách gia công theo `GiaGiaCong × SoBan`, không cộng hai lần.
- Đối chiếu: tổng dịch vụ + phụ phí/điều chỉnh + phí vận chuyển − giảm giá = tổng đơn. Không tự phân bổ giảm giá vào từng dịch vụ.
- Phân loại ảnh thẻ theo quy cách snapshot; màu/đen trắng theo danh mục dịch vụ hiện tại. Đổi danh mục sau này có thể đổi nhãn phân loại lịch sử, **không đổi tiền đã chốt**.
- Phương thức thanh toán phân loại **doanh thu đơn hoàn tất**: VietQR, COD quầy/giao hàng, chưa xác định. Không phải thống kê giao dịch/sao kê.
- Giờ cao điểm theo `NgayDat`, gồm đơn đã hủy, loại in lại lỗi quầy. Ba khung 00–07/08–15/16–23 giúp xem đủ 24 giờ mà không trượt ngang; có bảng dữ liệu thay thế heatmap.
- Thời gian xử lý = đặt → hoàn tất, chỉ lấy mốc hợp lệ, gồm cả chờ/giao/khách xác nhận; hiển thị cỡ mẫu.
- Đúng hẹn = hoàn tất không muộn hơn giờ hẹn, chỉ lấy giờ hẹn và thời gian hợp lệ.
- Đánh giá = đánh giá công khai trong kỳ **ngày đánh giá**, không phải kỳ hoàn tất đơn.

### Vận hành

- Số ca mở/chờ xác nhận: toàn phạm vi được xem, không bị giới hạn kỳ doanh thu.
- Chênh lệch: ca đã đóng và có kiểm đếm trong **30 ngày lịch gần nhất tính đến lúc đọc**.
- Công thức: tiền kiểm đếm quản lý (nếu có, nếu không dùng kiểm đếm nhân viên) − (tiền đầu ca + doanh thu tiền mặt trong ca − tiền chi + tiền bù).
- Không giả ca đang mở đã cân tiền. Tổng chênh lệch có dấu có thể bù trừ giữa các ca; phải xem cả **số ca lệch**, không kết luận mọi ca cân chỉ vì tổng bằng 0.
- Kho/phiếu chi tiết là snapshot tải thủ công, có thời điểm tải; máy “HOAT_DONG” trong cấu hình không đồng nghĩa đang kết nối. Kết nối dựa heartbeat hiện có.

## 4. File và vùng liên quan

Các đường dẫn dưới đây tính từ `MaNguon/PhotocopyChain`; số dòng ứng với bản ngày 13/10/2026, có thể đổi sau commit khác.

| File | Vùng dòng | Vai trò |
|---|---:|---|
| PhotocopyAdmin/Pages/TongQuan.razor | 1–240 | Hai tab, bộ lọc, KPI, biểu đồ/bảng và tải báo cáo |
| PhotocopyAdmin/Components/BieuDoDoanhThu.razor | 1–64 | Cột/đường, tick tròn, ẩn kỳ đối chiếu 0 |
| PhotocopyAdmin/TongQuanBaoCao.cs | 1–69 | Nhóm kỳ, đối chiếu, bước trục và định dạng |
| PhotocopyAdmin/Components/TongQuanPhanTich.razor | 1–110 | Cơ cấu, hiệu quả/đánh giá, heatmap |
| PhotocopyAdmin/Components/TongQuanVanHanh.razor | 1–280 | Cảnh báo, kho/máy/ca, lỗi riêng và timer |
| PhotocopyAdmin/ThongBaoPheDuyet.cs | 1–23 | Trạng thái badge dùng chung |
| PhotocopyAdmin/Layout/MainLayout.razor | 6, 34–110, 195–208, 263–275, 298–305 | Nhóm sidebar, quyền, cập nhật/hủy đăng ký badge |
| PhotocopyAdmin/Pages/PheDuyet.razor | 4, 145 | Đồng bộ badge sau tải/duyệt; không thay hành vi POST |
| PhotocopyAdmin/Program.cs | 23 | Đăng ký service badge |
| PhotocopyAdmin/wwwroot/css/app-ui-v2.css | từ 1864 | Style dashboard, tabs, KPI, chart, heatmap, responsive |
| PhotocopyAdmin/wwwroot/index.html | 13, 33 | Version CSS và kiểm tra tab trình duyệt hiển thị |
| PhotocopyShared/Dtos/Tuan3/TongQuanDtos.cs | 1–56 | DTO báo cáo mới |
| PhotocopyApi/Controllers/BaoCaoTongQuanController.cs | 1–72 | Hai GET, kiểm tra quyền/phạm vi/kỳ |
| PhotocopyApi/DuLieu/BaoCaoTongQuanSql.cs | 1–94 | SELECT tham số hóa, không migration |
| PhotocopyApi.Tests/*TongQuan*Tests.cs, DashboardHoanThienTests.cs | các test liên quan | Số liệu, render, ngày/đối chiếu, phạm vi, dữ liệu lỗi/rỗng |
| PhotocopyApi.Tests/MaPhieuNhapWebTests.cs | DI fixture | Giữ kiểm thử mã phiếu sau thêm badge |
| verification/dashboard-hang-doi.cjs | toàn script | Kiểm tra Chrome với API fixture, chặn ghi backend thật |

Đợt trước đã làm nổi bật hàng đợi qua `HangDoiDon.razor` và `QuyTacHangDoi.cs`; đợt này giữ nguyên thay đổi đó. Các tài liệu/edits khác có sẵn được giữ lại, chưa tạo commit.

## 5. Kiểm chứng đã thực hiện

- API và Web **build thành công, 0 warning / 0 error**; Web đã publish Release.
- Bộ .NET cuối: **301 đạt, 0 thất bại, 16 bỏ qua / 317**. Có 15 test CSDL cũ cần bật riêng; test báo cáo chỉ đọc cũng bỏ qua mặc định để không phụ thuộc máy.
- Test báo cáo chỉ đọc đã chạy riêng trên CSDL cấu hình thật: **1 đạt**, đối chiếu toàn chuỗi và chi nhánh 1/2; tổng doanh thu khớp API cũ, tổng phương thức khớp doanh thu, tổng cơ cấu dịch vụ khớp tiền dòng chi tiết, mẫu hiệu quả và phạm vi dữ liệu hợp lệ. Chỉ SELECT, không bật nhóm test có tạo dữ liệu.
- Chrome trên **390/768/1024/1366/1920px**: đều đạt; không ghi nhận JS page error hoặc tràn ngang main/document.
- Đã thử tab mặc định theo vai trò, bàn phím, preset, ngày sai không gọi API, kỳ trước 0, bảng/phân trang, một chi nhánh, đổi chi nhánh, lỗi riêng/phục hồi, nguồn vận hành lỗi một phần và nhân viên bị chặn báo cáo.
- Timer được kiểm ở 1366px: cập nhật queue/heartbeat/chỉ số ca sau 60 giây; không quét kho; chuyển tab dừng quét. Ảnh chụp được xem lại; ảnh dùng **dữ liệu fixture**, không phải số liệu thật của cửa hàng.
- API đã restart với sự đồng ý của bạn; Swagger có hai route mới; GET chưa đăng nhập trả 401. Web cổng 5064 trả 200 và đã phục vụ version CSS mới.
- SDK báo `wasm-tools` chưa cài cho tối ưu publish; đây không phải lỗi build. Chưa đo tải lớn hoặc nghiệm thu tiền thật/máy in vật lý trong đợt dashboard này.

## 6. Cách chạy/test

1. Bình thường vẫn chạy `MaNguon/scripts/chay_web.cmd`. Không cần migrate hoặc nhập lại cấu hình.
2. Mở `http://localhost:5064/tong-quan`, nhấn **Ctrl+F5** để nạp bản mới.
3. CEO: xem Kinh doanh, chọn ngày/tuần/tháng hoặc kỳ tùy chọn, đổi chi nhánh; xem cơ cấu/hiệu quả/giờ cao điểm và bảng chi tiết.
4. Quản lý: vào Vận hành mặc định; xem việc ưu tiên, kho, máy, ca. Chờ 60 giây kiểm tra thời điểm cập nhật; bấm Làm mới vận hành khi cần tải lại kho/phiếu.
5. CEO: mở Phê duyệt để đối chiếu badge. Không thao tác duyệt/hoàn tiền thật chỉ để test giao diện.
6. API docs: `http://localhost:5268/swagger`.

Chạy lại .NET từ thư mục solution:

```powershell
dotnet test PhotocopyChain.sln
```

Browser harness cần thư mục `wwwroot` đã publish và Playwright/Chrome có sẵn; hướng dẫn đầu script. Mọi API được giả lập trong harness để tránh thanh toán/đơn hàng thật.

## 7. Giới hạn và việc có thể làm tiếp

- Không sửa schema, giá, thanh toán, in tự động, đăng nhập hay quyền nghiệp vụ.
- Ngày tùy chọn đang nhập văn bản dd/MM/yyyy có validation, chưa có lịch chọn ngày riêng.
- KPI hoàn tất đúng hẹn không thay báo cáo công suất/tốc độ máy; trạng thái kẹt giấy cần connector thật cung cấp.
- Tổng hợp SQL được đối chiếu dữ liệu thật, nhưng chưa benchmark quy mô lớn hay chạy stress test.
- Báo cáo là snapshot; Kinh doanh làm mới qua preset/nút áp dụng, không tự polling doanh thu. CSDL thay đổi đồng thời giữa các truy vấn có thể làm snapshot lệch thời điểm.
- Quyền UI được thử bằng fixture; phạm vi controller được kiểm và query CSDL được đối chiếu. Không coi fixture là kiểm thử xuyên suốt đăng nhập–thanh toán thật.

Kế hoạch: [2026-10-13-dashboard-hoan-thien.md](plans/2026-10-13-dashboard-hoan-thien.md).

## 8. Cập nhật sidebar, header/footer sau góp ý về icon

Phạm vi được xác nhận: chỉ giao diện trang quản trị, giữ layout riêng của đăng nhập/quên mật khẩu.

- Nhóm đầu đổi thành **Dashboard**, vẫn chứa Tổng quan và Báo cáo doanh thu. Giữ ID nội bộ để tránh đổi code không cần thiết.
- Các nhóm và mục menu có hình khác nhau: Dashboard lưới; Tổng quan đồng hồ đo; Vận hành quy trình; Ca & tiền tiền mặt; Ca làm việc ví; Lịch sử mũi tên đồng hồ; Quản lý sơ đồ; Cấu hình bánh răng; Đánh giá ngôi sao; Bảng giá thẻ giá; Khuyến mãi vé phần trăm. Mục khác giữ hình phù hợp sẵn có.
- SVG cùng hệ nét stroke/viewBox 24×24; nhóm 20px và mục con 18px. Không tải thư viện icon hoặc emoji.
- Header hiện nhãn nhóm và tên màn hình đang mở, cập nhật ngay khi điều hướng cho CEO/quản lý/nhân viên. Bộ chọn chi nhánh và các quyền cũ không đổi; màn hình rất nhỏ ưu tiên chỗ cho bộ chọn/menu.
- Footer dùng chung đã được chỉnh tiếp theo yêu cầu mới: bỏ mã đồ án, thêm liên hệ và slogan, nằm ngoài vùng cuộn để luôn thấy. Chi tiết ở mục 9.
- Sửa trạng thái ARIA của nút thu gọn/drawer: xuất rõ chuỗi true/false thay vì bool để Blazor không bỏ thuộc tính khi false. Đây là sửa accessibility UI, không đổi nghiệp vụ.
- Style giữ navy, font và cỡ chữ đã thống nhất; footer tự xuống dòng trên điện thoại. Không thêm timer/request API cho header/footer.

### File/vùng thay đổi của đợt này

| File (từ MaNguon/PhotocopyChain) | Vùng dòng | Thay đổi |
|---|---:|---|
| PhotocopyAdmin/Components/Svg.cs | 3, 31–40 | Mô tả đúng bộ icon và thêm 10 SVG phục vụ menu |
| PhotocopyAdmin/Layout/MainLayout.razor | 34–110 | Dashboard, icon riêng và kích thước mục con |
| PhotocopyAdmin/Layout/MainLayout.razor | 130–177 | Header/ARIA, wrapper nội dung và footer |
| PhotocopyAdmin/Layout/MainLayout.razor | 196–222, 241, 337–347 | Nhãn màn hình, cập nhật giao diện khi điều hướng và hủy đăng ký |
| PhotocopyAdmin/wwwroot/css/app-ui-v2.css | 534–585, 642–706 | Header co giãn, nội dung flex và footer responsive |
| PhotocopyAdmin/wwwroot/index.html | 13 | Version CSS 20261013-shell-icons |
| PhotocopyApi.Tests/SidebarHeaderFooterTests.cs | toàn file | SVG/uniqueness, render UI/quyền/header/footer và ARIA |
| verification/dashboard-hang-doi.cjs | các kiểm tra shell mới | Drawer/thu gọn, nhóm độc lập, icon, footer, điều hướng và auth layout riêng |

### Kiểm chứng bản cuối của đợt này

- RED đã xác nhận icon thiếu/trùng, nhãn cũ và header/footer chưa có; riêng lỗi ARIA cũng tái hiện trước sửa.
- **316 test đạt, 0 thất bại, 16 skip / 332**. Có 15 bài mới; các skip vẫn là nhóm CSDL cần bật riêng đã giải thích ở phần 5.
- Web build **0 warning / 0 error**, publish Release thành công. SDK vẫn nhắc wasm-tools cho tối ưu publish, không phải lỗi build.
- Chrome fixture **390/768/1024/1366/1920px đều đạt**: icon/menu, nhóm độc lập, drawer/thu gọn, header điều hướng không reload cho CEO và quản lý, footer đúng cuối nội dung, login/quên mật khẩu không nhận layout quản trị, cùng các hồi quy dashboard.
- Không ghi nhận JS page error hoặc tràn ngang main/document trong các luồng đã thử. Ảnh đã xem lại; số liệu trong ảnh là fixture.
- Web 5064 và Swagger API 5268 trả 200; Web đang phục vụ CSS mới. Đợt này không sửa backend/DB, không thử giao dịch tiền thật, không restart API và chưa commit.

**Cách xem:** Ctrl+F5 tại web đang mở; kiểm Dashboard và các nhóm menu, chuyển màn hình để xem header. Footer hiện cố định dưới vùng nội dung. Trang đăng nhập/quên mật khẩu giữ nguyên bố cục riêng.

Thiết kế và quyết định: [sidebar-header-footer](plans/2026-10-13-sidebar-header-footer.md).

## 9. Footer liên hệ, không cuộn theo trang

Người dùng yêu cầu thay thiết kế footer ở mục 8, vẫn chỉ sửa UI.

- Bỏ CNTT-KLCN031 khỏi footer.
- Email: manh27062005@gmail.com, link mailto để mở ứng dụng email.
- Điện thoại: 0769479813, link tel để mở ứng dụng gọi điện nếu thiết bị hỗ trợ.
- Slogan: **In rõ nét — Nhận đúng hẹn.** Giữ thương hiệu PhotocopyChain và năm bản quyền.
- Footer chuyển ra ngoài main đang cuộn, thành phần tử cùng cấp với header/main trong layout flex. Luôn thấy dưới vùng làm việc; có chiều cao riêng nên không đè bảng/nút. Không cần JavaScript hoặc position fixed.
- Liên hệ có icon nét, vùng bấm tối thiểu 44px và focus bàn phím. Màn hình nhỏ tự xuống dòng; giữ màu/font hiện tại, không thêm dữ liệu liên hệ giả.

File: MainLayout.razor (khối main/footer); app-ui-v2.css (chan-trang/ct-*); index.html (version 20261013-footer-contact); SidebarHeaderFooterTests.cs và verification/dashboard-hang-doi.cjs (kiểm tra liên hệ/footer ngoài vùng cuộn).

Kiểm chứng cuối: Web build 0 warning / 0 error, publish thành công; **316 test đạt, 0 lỗi, 16 skip / 332**. Chrome fixture 390/768/1024/1366/1920px đều đạt, thêm kiểm tra 390×640px: footer giữ nguyên vị trí khi cuộn main, không tràn ngang/che nội dung, còn đủ vùng làm việc và Tab chuyển từ email sang điện thoại. Không thực hiện gửi email/cuộc gọi thật; chỉ kiểm href và bàn phím.

Web đang phục vụ CSS mới; Ctrl+F5 để nạp. Không sửa backend/DB, không restart API, không tạo commit.

## 10. Sidebar hẹp hơn và footer có phân cấp UI/UX

- Sidebar mặc định **361px → 325px** (giảm khoảng 10%); giữ cỡ chữ, logo, icon, nhóm độc lập và chế độ thu gọn. Drawer điện thoại cũng dùng độ rộng mới khi đủ chỗ.
- Footer bỏ ô logo lặp; thương hiệu/slogan ở trái. Email và điện thoại có nhãn riêng phía trên, giá trị liên hệ phía dưới và đường ngăn nhẹ giữa các khối.
- Năm bản quyền được giảm nhấn mạnh. Font hiện có giữ nguyên; footer dùng 17px cho tên, 16px cho dữ liệu và 14px cho nhãn/slogan/bản quyền, màu chữ theo các biến theme hiện có.
- Mobile: hai cột email/điện thoại dưới thương hiệu; email cho phép xuống dòng sau @ để đọc đủ địa chỉ, không cắt hoặc trượt ngang. Footer tối đa 150px trong bài thử 390×640px và vẫn chừa đủ vùng làm việc; vùng bấm tối thiểu 44px.
- Footer vẫn ngoài main cuộn, cố định theo layout, không che nội dung. Mailto/tel, slogan, header và logic/quyền không đổi.
- Chỉ sửa MainLayout.razor (markup footer), app-ui-v2.css (token sidebar và style ct-*), index.html (version 20261013-shell-compact), test và browser harness.

Kiểm chứng: test nhãn liên hệ và width được xác nhận RED trước sửa; Web build 0 warning / 0 error, publish Release đạt; full .NET **316 đạt / 16 skip / 332, 0 lỗi**. Chrome fixture **390/768/1024/1366/1920px đều đạt**, thêm 390×640px, gồm vị trí/chiều cao footer, href/focus, đo sidebar 325px và các hồi quy dashboard. Ảnh footer/sidebar đã xem lại. CSS mới đang được Web 5064 phục vụ; không đổi backend/DB và chưa commit.

## 11. Liên hệ và các danh mục quản lý

- Email footer chuyển sang Gmail compose; điện thoại mở dialog có gọi/sao chép và phản hồi lỗi rõ ràng.
- Bỏ Hiệu quả & trải nghiệm khách hàng và Giờ cao điểm; giữ Chi tiết doanh thu cùng cơ cấu dịch vụ/thanh toán.
- Thêm Khách hàng và Dịch vụ trong Quản lý; CEO thêm được chi nhánh/máy in. Giữ quyền cũ, không sửa schema, không xóa dữ liệu.
- Bảng giá chỉ sửa giá/trạng thái riêng từng cơ sở; dịch vụ chung không lặp theo chi nhánh. Bảo vệ quy cách đã có đơn và tên gia công gắn ràng buộc.
- Full .NET: 325 đạt, 17 skip / 342, 0 lỗi; 2 bài SELECT CSDL thật đạt. Browser fixture qua 5 kích thước; không ghi dữ liệu thử vào database thật.
- Chi tiết file/vùng dòng, cách chạy và giới hạn: [BAN_GIAO_QUAN_LY_DANH_MUC.md](BAN_GIAO_QUAN_LY_DANH_MUC.md).

