# Rà soát dữ liệu nhập — 12/10/2026

## Kết quả chính

Đã bổ sung kiểm tra tại DTO/API, form web và lớp gọi API mobile. Không đổi schema, không chạy migration, không sửa tài khoản/dữ liệu cũ, không thêm thư viện.

- Số điện thoại: đúng 10 chữ số ASCII, bắt đầu bằng `0`; không nhận chữ, số âm, ký tự phân cách hoặc chữ số Unicode. Trường không bắt buộc vẫn được để trống. Màn đăng ký mobile giữ cách nhập đầu `+84`, chuyển 9 số còn lại sang dạng `0xxxxxxxxx` trước khi kiểm tra.
- Email tạo tài khoản mới: địa chỉ hợp lệ thuộc `@gmail.com`, tối đa 100 ký tự; không phân biệt hoa/thường, hỗ trợ dạng `ten+nhan@gmail.com`. Không giới hạn domain khi đăng nhập/khôi phục mật khẩu, nên tài khoản cũ như `sv1@huit.edu.vn` vẫn sử dụng được.
- Mật khẩu mới: ít nhất 8 ký tự, có chữ hoa và chữ số, tối đa 72 byte UTF-8 theo giới hạn BCrypt, không có ký tự điều khiển. Không tự cắt/trim mật khẩu; không ép mật khẩu cũ theo quy tắc mới khi đăng nhập. Giữ kiểm tra nhập lại mật khẩu trên giao diện.
- OTP: đúng 6 chữ số ASCII. Kiểm tra định dạng không thay thế bước xác minh OTP thật tại API.
- Họ tên/tên dịch vụ/chi nhánh: tối đa 100 ký tự; tên khuyến mãi/nhà cung cấp tối đa 150; ghi chú, địa chỉ và lý do tối đa 255; nội dung đánh giá tối đa 500.
- Mã khuyến mãi: tối đa 30 ký tự, chỉ gồm chữ, số, `_`, `-`. Phần trăm giảm từ 1 đến 100; giới hạn lượt dùng không âm; ngày kết thúc không trước ngày bắt đầu và nằm trong miền SQL Server hỗ trợ.
- Tiền: số nguyên đồng, từ 0 đến 999.999.999.999, không nhận phần thập phân/âm/vượt cột tiền hiện có.
- Số bản, số lượng nhập/nhận hàng, mã bản ghi/dịch vụ/file/máy: số nguyên dương theo từng trường; counter máy không âm. Danh sách mã không null, không trùng, không có mã âm/0; danh sách cần chọn phải có ít nhất một phần tử.
- Khoảng trang in lại/phát sinh: trang bắt đầu dương, trang kết thúc không nhỏ hơn trang bắt đầu. Giữ kiểm tra số trang thật và quy tắc gia công ở luồng nghiệp vụ hiện có.
- Quy cách/trạng thái/vai trò/phương thức nhận và thanh toán: chỉ nhận các giá trị được hệ thống hỗ trợ. Dịch vụ in cần đủ khổ giấy, loại giấy, mặt in và màu in; dịch vụ gia công không bị ép nhập các trường này.
- Giao hàng: cần địa chỉ và số người nhận khi tạo đơn; báo giá cần địa chỉ. GPS phải đủ cặp vĩ độ/kinh độ, đúng phạm vi. Chi nhánh có kiểm tra độ chính xác tọa độ, bán kính và giờ đóng sau giờ mở.
- File: tên không rỗng, tên file tối đa 255 ký tự; giữ kiểm tra dung lượng, đuôi và nội dung tài liệu hiện có. Các dòng file in được kiểm tra riêng, không nhận dòng null hoặc file trùng.
- Báo cáo/lịch sử ca: khoảng ngày đúng thứ tự và không gây tràn giới hạn ngày SQL khi cộng ngày kết thúc.

## Cách hoạt động

1. Form web dùng `required`, `maxlength`, `min/max/step`, `pattern` và kiểm tra DTO dùng chung để báo lỗi trước khi gửi. Các form số được đưa vào luồng submit có kiểm tra HTML, tránh xóa ô mà vẫn gửi giá trị đã bind trước đó.
2. Mobile kiểm tra payload trong `Api.post`/`Api.put`; dữ liệu sai trả về lỗi để màn hình hiện thông báo và không gửi HTTP. Đăng ký có thông báo Gmail phù hợp quy tắc mới.
3. API kiểm tra lại bằng DataAnnotations/IValidatableObject và trả HTTP 400 cho dữ liệu không hợp lệ. Không tin việc client đã kiểm tra.
4. Quyền truy cập, chi nhánh, tồn kho, thanh toán, giới hạn bấm kim 50 tờ/bản và tương thích gia công vẫn do nghiệp vụ hiện có kiểm tra. Validation định dạng không thay thế các kiểm tra này.

## Các file thay đổi

Các đường dẫn sau tính từ `MaNguon/PhotocopyChain`, trừ mobile đã ghi riêng.

### Dùng chung/API

- Thêm `PhotocopyShared/KiemTraDuLieu/QuyTacNhap.cs`: helper và các attribute kiểm tra Gmail, tài khoản, mật khẩu, tiền, danh sách mã/file.
- Sửa `PhotocopyShared/Dtos/Auth/AuthDtos.cs`, `Dtos/DatIn/DatInDtos.cs`, `Dtos/DonHang/DonHangDtos.cs`, `Dtos/Tuan3/Tuan3Dtos.cs`: annotations theo từng dữ liệu nhập.
- Thêm `Dtos/DatIn/KiemTraBaoGia.cs`, `Dtos/DonHang/KiemTraDatDon.cs`, `Dtos/Tuan3/KiemTraQuanTri.cs`: quy tắc liên trường, giữ các DTO dễ đọc.
- Sửa các controller trong `PhotocopyApi/Controllers`: `BaoGia`, `Ca`, `DanhMuc`, `DonHang`, `FileIn`, `MayInAo`, `NhanVien`, `QuanTri`, `ThanhToan`, `ThongBao` — kiểm tra mã số nguyên dương trên route/query; bổ sung chặn tên file, khoảng ngày, chuẩn hóa kiểm tra điện thoại khách tại quầy. Không thay đổi mã đơn `long` hoặc xác thực callback payOS.

### Web

- `PhotocopyAdmin/Pages/_Imports.razor`: dùng helper chung.
- `NhanVien.razor`, `BangGia.razor`, `KhuyenMai.razor`, `KhoVatTu.razor`, `QuanTri.razor`, `LapDonTaiQuay.razor`, `CaLamViec.razor`, `BanGiao.razor`: ràng buộc form và kiểm tra trước gửi ở các thao tác tương ứng.
- `ThongTinCaNhan.razor`, `PhotocopyAdmin/DatLaiMatKhauForm.cs`: kiểm tra mật khẩu mới cùng quy tắc API.
- Đăng nhập/quên mật khẩu dùng DTO chung nên nhận thêm validation, không đổi bố cục hoặc luồng OTP hiện có.

### Mobile

- Thêm `MaNguon/photocopy_mobile/lib/kiem_tra_du_lieu.dart`: kiểm tra các payload xác thực, hồ sơ, báo giá, đơn hàng, tìm chi nhánh, hủy và đánh giá.
- Sửa `lib/api.dart`: kiểm tra trước POST/PUT và tên file trước upload.
- Sửa `lib/screens/xac_thuc/dang_ky_screen.dart`: quy tắc Gmail/điện thoại, không âm thầm xóa ký tự sai trong điện thoại.

### Kiểm thử/tài liệu

- Thêm `PhotocopyApi.Tests/KiemTraDuLieuTests.cs`, `KiemTraHttpNhapTests.cs`, `KiemTraFileVaNgayTests.cs`.
- Thêm `MaNguon/photocopy_mobile/test/kiem_tra_du_lieu_test.dart`.
- Kế hoạch: `docs/plans/2026-10-12-validate-du-lieu.md`.

## Kiểm chứng

- Viết và chạy các kiểm thử mới trước khi triển khai; đã quan sát lỗi, bổ sung validation rồi chạy lại.
- Build toàn solution .NET: thành công, 0 warning, 0 error. Artifacts kiểm thử ở thư mục tạm riêng, không ghi đè binary của dịch vụ đang chạy.
- `dotnet test PhotocopyApi.Tests`: **193 đạt, 13 bỏ qua, 0 lỗi**. 13 test tích hợp DB cũ bị bỏ qua vì không cấu hình `PHOTOCOPY_TEST_DB`; không coi đây là kiểm chứng nghiệp vụ trên database thật.
- Test HTTP chạy qua MVC/controller thực, dùng xác thực giả chỉ trong test; payload sai bị chặn trước khi truy cập DB/Gmail/payOS. Có trường hợp số nguyên dạng phân số, ID âm, danh sách null/trùng và dòng file sai.
- `flutter test`: **76 đạt, 0 lỗi**.
- `flutter analyze`: **No issues found**.
- APK debug Android arm64 build thành công tại `MaNguon/photocopy_mobile/build/app/outputs/flutter-apk/app-debug.apk`.
- Trình duyệt bản web thử riêng cổng 5065: form đăng nhập báo trường bắt buộc; quên mật khẩu báo email sai định dạng và không chuyển sang bước OTP. Không gửi OTP thật, không tạo đơn, không đổi mật khẩu hoặc ghi dữ liệu thử vào DB.
- Chưa kiểm thử tương tác từng form quản trị có đăng nhập trên trình duyệt và chưa cài APK mới lên điện thoại trong lượt này. Không khẳng định đã nghiệm thu toàn bộ tình huống dữ liệu/nghiệp vụ ngoài các kiểm thử trên.

## Chạy để thử

1. Chạy lại `MaNguon/scripts/chay_web.cmd` để launcher build và nạp API/web mới; F5 trang web.
2. Android Studio: Stop app cũ rồi Run lại dự án `MaNguon/photocopy_mobile`. Hot reload không thay API đang chạy.
3. Thử tạo nhân viên với email không phải Gmail, điện thoại thiếu/thừa số, mật khẩu yếu, tiền âm/lẻ, khuyến mãi ngược ngày và số bản 0; hệ thống phải báo lỗi.
4. Thử đăng nhập/khôi phục bằng email tài khoản cũ không phải Gmail; không được bị chặn chỉ vì domain. Gửi OTP thật cần cấu hình Gmail hiện có hoạt động.
