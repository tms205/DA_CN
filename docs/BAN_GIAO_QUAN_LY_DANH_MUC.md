# Bàn giao Khách hàng, Dịch vụ, Chi nhánh và Máy in

Ngày 13/10/2026. Giữ Blazor/Dapper, màu navy và các bảng hiện có. Không sửa schema, không xóa dữ liệu, không thêm thư viện, không tự commit.

## 1. Đã triển khai

- **Quản lý → Khách hàng**: tách khỏi Chi nhánh & máy in; tìm tên/email/điện thoại, lọc trạng thái, phân trang 10 dòng, xem thông tin và xác nhận khóa/mở khóa. Tổng đơn/chi tiêu theo phạm vi đang chọn; khóa tài khoản có hiệu lực toàn hệ thống. Không tạo/xóa khách hàng trong màn này.
- **Quản lý → Dịch vụ**: danh mục dùng chung, không lặp một dịch vụ theo mỗi chi nhánh; tìm kiếm/lọc/phân trang, thêm/sửa quy cách tài liệu, ảnh thẻ và gia công. Chọn vật tư bằng tên thay vì nhập mã. Ảnh thẻ tự chọn vật tư Giấy ảnh A4.
- **Bảng giá in ấn**: chỉ sửa giá và trạng thái bán của chi nhánh đang chọn. Không còn form sửa định nghĩa dịch vụ tại đây. Giá/đơn đã chốt và giá chi nhánh khác không bị cập nhật theo thao tác này.
- **CEO thêm chi nhánh**: tên, địa chỉ, hotline, giờ mở/đóng, bán kính và tọa độ tùy chọn (phải đủ cặp). Lưu trong transaction cùng giá mặc định từ danh mục và các dòng kho số lượng 0. Không sao chép khuyến mãi, nhân sự hay số tồn. Sau lưu nạp lại toàn chuỗi để bộ chọn chi nhánh có cơ sở mới.
- **CEO thêm máy in**: chọn chi nhánh, model, khổ giấy lớn nhất, in màu/hai mặt, tốc độ, counter và trạng thái. API kiểm quyền CEO và phạm vi chi nhánh; đăng ký danh mục không tự kết nối thiết bị vật lý. Cần cấu hình gateway/máy in ảo và heartbeat trước khi máy nhận job.
- Hai form thêm mới nằm trên đầu danh sách. Các thao tác cập nhật/bảo trì máy cũ vẫn giữ.

## 2. Quyền và bảo vệ dữ liệu

| Chức năng | CEO | Quản lý chi nhánh | Nhân viên |
|---|---|---|---|
| Khách hàng | Xem, chi tiết, khóa/mở khóa | Không mở thêm quyền so với API cũ | Không |
| Dịch vụ | Xem, thêm/sửa danh mục chung | Xem | Không |
| Giá chi nhánh | Xem tất cả; chọn cơ sở để sửa | Xem cơ sở mình | Xem giá như trước |
| Chi nhánh/máy in | Xem, thêm; sửa theo phạm vi | Xem cơ sở mình, cập nhật máy như trước | Không |

- Giá/trạng thái mặc định của danh mục chỉ dùng để khởi tạo dịch vụ/chi nhánh mới; không tự áp lên mọi bảng giá đã có.
- Quy cách/vật tư của dịch vụ đã có chi tiết đơn không được thay đổi. Cần thêm quy cách mới để không làm sai đơn cũ.
- Giữ tên **Bấm kim góc / Đóng lò xo** vì quy tắc C# và thủ tục SQL hiện nhận diện chúng theo tên. Không đổi schema để thêm loại gia công trong đợt này.
- Input được kiểm tại UI và API: hotline 10 số bắt đầu 0; tọa độ/bán kính đúng giới hạn; giờ đóng sau mở; tốc độ 1–1.000 trang/phút; counter nguyên không âm; giá nguyên không âm, trong giới hạn DECIMAL(12,0).
- Sửa lỗi RangeAttribute đọc 999.99 theo culture tiếng Việt gây crash form chi nhánh. Giới hạn được đọc bằng invariant culture.
- Chặn bấm lưu lặp khi request đang chạy; lỗi kết nối yêu cầu làm mới kiểm tra kết quả trước khi thử tạo lại. Chưa có khóa idempotency bền vững cho request tạo danh mục.

## 3. Các việc đang dở trước đó đã hoàn tất

- Footer email mở Gmail soạn thư qua HTTPS ở tab mới, không phụ thuộc trình chọn ứng dụng mail của Windows; có thể cần đăng nhập Gmail. Không tự gửi thư.
- Điện thoại mở hộp liên hệ có số rõ ràng, nút gọi bằng ứng dụng và sao chép. Có thông báo sao chép thành công/lỗi, Escape/đóng và trả focus về nút mở. Gọi thật trên PC vẫn cần ứng dụng gọi được cấu hình; không tự đổi ứng dụng mặc định của Windows.
- Bỏ **Hiệu quả & trải nghiệm khách hàng** và **Giờ cao điểm** khỏi Dashboard; giữ cơ cấu dịch vụ, phương thức thanh toán và **Chi tiết doanh thu**. Không bỏ các trường API để tránh phá client khác.
- Footer vẫn ngoài vùng main cuộn, không che nội dung. Menu hai trang mới có icon riêng, header đúng nhãn và nhóm Quản lý tự mở khi truy cập trực tiếp.

## 4. File/vùng thay đổi

Đường dẫn tính từ MaNguon/PhotocopyChain; số dòng của bản bàn giao này, có thể thay đổi khi sửa tiếp.

| File | Vùng dòng | Thay đổi |
|---|---:|---|
| PhotocopyAdmin/Pages/KhachHang.razor | 1–96 | Trang quản lý khách hàng mới |
| PhotocopyAdmin/Pages/DichVu.razor | 1–333 | Danh mục chung, form dịch vụ/vật tư, lọc và phân trang |
| PhotocopyAdmin/Pages/BangGia.razor | 1–204 | Bỏ form định nghĩa; chỉ sửa giá và trạng thái bán |
| PhotocopyAdmin/Pages/QuanTri.razor | 1–362 | Bỏ khách hàng; thêm form cơ sở/máy; giữ cập nhật máy |
| PhotocopyAdmin/Layout/MainLayout.razor | 96–103, 181–206, 240–241, 340, 366–386 | Menu/header, Gmail, dialog điện thoại |
| PhotocopyAdmin/Components/Svg.cs | 31 | Icon dịch vụ cùng hệ SVG |
| PhotocopyAdmin/DocLoiApi.cs | 1–24 | Đọc thông báo API/validation, không hiện nguyên phản hồi lỗi |
| PhotocopyAdmin/Components/TongQuanPhanTich.razor | 1–71 | Bỏ hai khối phân tích không cần |
| PhotocopyAdmin/wwwroot/index.html | 13, 32–33 | Version CSS và helper mở dialog |
| PhotocopyAdmin/wwwroot/css/app-ui-v2.css | 694–712, 2910–2920 | Footer anchor/button, dialog, bộ lọc và cột khách hàng; xóa CSS heatmap không dùng |
| PhotocopyApi/Controllers/QuanTriDanhMucController.cs | 1–93 | GET danh mục, PUT giá/dịch vụ, POST chi nhánh/máy |
| PhotocopyApi/Controllers/QuanTriController.cs | 17, 49–127, 538–553 | Partial controller, giá khởi tạo, bảo vệ dịch vụ/vật tư |
| PhotocopyApi/QuyTacQuanTriChiNhanh.cs | 7–17 | Không đổi tên hai gia công có ràng buộc |
| PhotocopyShared/Dtos/Tuan3/QuanLyDanhMucDtos.cs | 1–41 | DTO/validation thêm máy và sửa giá |
| PhotocopyShared/Dtos/Tuan3/Tuan3Dtos.cs | 222–223 | Sửa giới hạn decimal theo culture |
| PhotocopyApi.Tests/QuanLyDanhMucHttpTests.cs | Toàn file | HTTP quyền/validation/phạm vi trước SQL |
| PhotocopyApi.Tests/QuanLyDanhMucValidationTests.cs | Toàn file | Giới hạn bán kính vi-VN/fr-FR/en-US |
| PhotocopyApi.Tests/QuanLyDanhMucDocCSDLTests.cs | Toàn file | Chỉ SELECT danh mục thật và kiểm phạm vi |
| PhotocopyApi.Tests/SidebarHeaderFooterTests.cs, TongQuanPhanTichTests.cs, QuyTacQuanTriChiNhanhTests.cs | Các test bổ sung | Menu/quyền/footer/bỏ khối/giữ ràng buộc |
| verification/dashboard-hang-doi.cjs, quan-ly-danh-muc.cjs | Các luồng mới | Browser fixture, không gọi API ghi thật |

## 5. Kiểm chứng và giới hạn

- Build Web bình thường: 0 warning / 0 error; API đã build thành công. Bản cuối API được biên dịch trong thư mục artifacts riêng vì tiến trình thật giữ DLL.
- Full .NET: **325 đạt, 0 lỗi, 17 skip / 342 bài**. Các bài CSDL opt-in không tự bật hàng loạt.
- Chạy riêng hai bài chỉ đọc CSDL bằng PHOTOCOPY_REPORT_ENV_FILE: **2/2 đạt** (danh mục mới + đối chiếu báo cáo).
- Chrome fixture đã qua 390/768/1024/1366/1920px: khóa/mở khách, thêm/sửa dịch vụ, thêm chi nhánh và máy, scope dropdown sau reload, sửa giá A không đổi B/danh mục, phân trang, menu/quyền, footer, Gmail popup, copy success/failure, bàn phím, hồi quy doanh thu.
- Request ghi trong browser được xử lý bằng dữ liệu giả trong bộ nhớ. **Chưa kiểm thử ghi end-to-end vào database thật** để không tạo dữ liệu thử, làm thay đổi identity/tồn kho hay tài khoản thực.
- Gmail được kiểm URL/new-tab bằng fixture, không gửi thư hay gọi điện; clipboard được giả lập ở biên trình duyệt, không ghi clipboard máy người dùng.
- Live Web 5064 trả 200, CSS mới; Swagger 5268 có POST chi nhánh/máy, GET danh mục, PUT giá; API danh mục không có token trả 401.
- Publish Release thành công; còn thông báo môi trường chưa có workload wasm-tools, không phải lỗi build. Chưa benchmark danh mục lớn hoặc kiểm máy vật lý mới.
- Công cụ bị chặn thao tác tự đóng process, nên người dùng chạy CMD để nạp API. Lớp bảo vệ đổi tên gia công bổ sung cuối cần chạy lại CMD sau khi nhận bàn giao.

## 6. Cách chạy/thử

1. Chạy MaNguon/scripts/chay_web.cmd sau khi không còn giao dịch/in đang thực hiện. Script build và nạp API/Web/máy in như trước.
2. Ctrl+F5 web. CEO: Quản lý → Khách hàng / Dịch vụ; Cấu hình → Chi nhánh & máy in.
3. Thêm cơ sở: điền thông tin, giờ hợp lệ, tọa độ đủ cặp nếu dùng; lưu; chọn cơ sở mới ở header.
4. Thêm máy: chọn đúng cơ sở/model/tốc độ; lưu. Sau đó cấu hình connector bằng mã máy được cấp; chưa cấu hình thì không coi là máy đã online.
5. Thêm/sửa dịch vụ tại danh mục; chỉnh giá riêng qua Bảng giá in ấn sau khi chọn chi nhánh. Để ngừng bán tại cơ sở hiện có, dùng trạng thái bán trong Bảng giá.
6. Khi test trên dữ liệu thật, dùng thông tin cơ sở/máy dự định sử dụng, không tạo hàng loạt bản ghi thử.
