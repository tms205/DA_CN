# Đăng nhập và khôi phục mật khẩu — 09/10/2026

## Phạm vi đã chốt

- Khung xác thực tối đa 400 → 480px (+20%), chữ tăng 1px; điện thoại không tràn ngang.
- Giữ màu, font, kiến trúc và cách viết code hiện có; chỉ sửa các màn xác thực.
- Email → xác minh OTP → mật khẩu mới + xác nhận → hoàn tất.
- OTP gồm sáu vị trí gạch chân, nhập/xóa/dán bằng bàn phím, có nhãn cho trình đọc màn hình.
- Không thêm thư viện, không thay đổi schema database, không thay mật khẩu thật để kiểm thử.

## Quyết định và phương án

1. Chọn API kiểm tra OTP chưa tiêu thụ mã, sau đó API lưu mật khẩu kiểm tra và tiêu thụ mã như cũ. Phương án cấp thêm token đặt lại mật khẩu cũng khả thi nhưng cần thêm vòng đời token; không cần cho phạm vi này. Không chọn chỉ kiểm tra sáu chữ số ở frontend vì không xác thực được mã thực sự.
2. Dùng một ô nhập thật phủ sáu vị trí hiển thị. So với sáu ô input rời, cách này giữ được dán mã, chọn/xóa, tự điền OTP và điều khiển bàn phím chuẩn mà không thêm JavaScript.
3. Xác nhận mật khẩu ở form web; giữ nguyên hợp đồng reset-password của app mobile. API vẫn yêu cầu OTP hợp lệ và mật khẩu đúng quy tắc.

## Giả định và giới hạn

- Tăng một đơn vị = 1px, màn hình nhỏ co theo bề rộng khả dụng.
- Quy mô, hiệu năng và khả dụng giữ như hệ thống cá nhân hiện tại; không thêm truy vấn SQL ở bước kiểm tra OTP.
- OTP vẫn sống 10 phút, gửi lại sau 60 giây, sai năm lần khóa 15 phút; xác minh và lưu dùng chung bộ đếm sai. Khởi động lại API làm mã cũ hết hiệu lực.
- Xác minh không gia hạn mã. Nếu mã hết hạn/được gửi lại/đã dùng trước khi lưu, người dùng phải quay lại lấy và xác minh mã hợp lệ.
- Code nhỏ, dùng DataAnnotations và bộ nhớ OTP sẵn có; không thay đổi đăng nhập, Gmail hay chức năng nghiệp vụ khác.

## Kế hoạch thực hiện

1. Viết kiểm thử kiểm tra OTP không tiêu thụ mã, khóa/hết hạn/gửi lại và xác nhận mật khẩu; chứng minh thất bại trước khi sửa.
2. Thêm phương thức kiểm tra OTP và endpoint có giới hạn tần suất.
3. Tách form web thành ba bước, bổ sung OTP sáu gạch chân và hai ô mật khẩu.
4. Điều chỉnh CSS xác thực; build và chạy lại các kiểm thử hồi quy.
5. Kiểm tra trình duyệt ở desktop/mobile và bàn giao cách chạy.

## Kết quả kiểm thử

- Đã chứng minh 10 ca kiểm thử mới thất bại do chưa có phương thức xác minh/form xác nhận mật khẩu trước khi triển khai; sau triển khai bổ sung thêm hai ca kiểm thử tích hợp form web với controller xác minh thật.
- Bản mã cuối: **106 kiểm thử đạt**, không lỗi. Bao gồm 12 ca của phần này: mã đúng/sai, chỉ chuyển bước khi API xác nhận, mã chỉ tiêu thụ khi lưu, dùng chung giới hạn sai, không gia hạn OTP, gửi lại vô hiệu mã cũ, email khác và mật khẩu xác nhận.
- API và Admin build ở đường dẫn chạy thông thường: **0 warning, 0 error**.
- Trình duyệt: khung desktop 480px (trước là 400px), tiêu đề 23px (trước 22px), chữ ô đăng nhập 16px (trước 15px).
- Kiểm tra bề rộng CSS 1366px và 390px: không tràn ngang; đăng nhập và bước OTP co theo màn hình.
- Gõ `123` hiện ba vị trí, Backspace còn `12`, nhập đủ sáu số hiển thị sáu vị trí; bỏ ký tự không phải số và khóa nút khi chưa đủ mã.
- API đang chạy từ chối mã không được cấp; web vẫn ở bước OTP và không hiện mật khẩu. Nút đổi email và quay lại đăng nhập hoạt động, không có lỗi JavaScript trong tab kiểm thử.
- Trình duyệt dùng email `otp-ui-check@example.invalid` không có tài khoản: không gửi Gmail thật, không sửa mật khẩu hay dữ liệu người dùng. Luồng mã đúng và hai mật khẩu được kiểm tra bằng code với OTP store/controller thật, không nghiệm thu bằng cách đổi mật khẩu thật của bạn.
- Không chạy lại 10 kiểm thử phụ thuộc dữ liệu SQL (`PhamViCSDLTests`, `CaVaPhieuNhapCSDLTests`) trong lần này. Không thay schema, trigger hay thủ tục SQL.

## Các file đã thay đổi

Các đường dẫn dưới tính từ thư mục `MaNguon/PhotocopyChain` của dự án:

| File | Vị trí chính | Nội dung |
| --- | --- | --- |
| `PhotocopyAdmin/wwwroot/css/app-ui-v2.css` | từ dòng 1507, phần xác thực | Khung 480px, chữ/spacing, OTP sáu gạch chân, chỉ báo ba bước, responsive và focus. |
| `PhotocopyAdmin/wwwroot/index.html` | dòng 13 | Đổi phiên bản URL CSS để tránh tải lại giao diện cũ từ cache. |
| `PhotocopyAdmin/Pages/QuenMatKhau.razor` | toàn bộ màn | Tách ba bước; gửi lại có đếm ngược, OTP xác minh qua API, quay lại, hai ô mật khẩu và thông báo lỗi/thành công. Email cập nhật ngay qua `oninput` để gửi đúng giá trị đang hiển thị. |
| `PhotocopyAdmin/DatLaiMatKhauForm.cs` | file mới | Required, quy tắc mật khẩu và Compare hai lần nhập; chỉ dùng trên web. |
| `PhotocopyShared/Dtos/Auth/AuthDtos.cs` | từ dòng 123 | Thêm DTO email + OTP cho endpoint xác minh; DTO reset-password cũ giữ nguyên. |
| `PhotocopyApi/Controllers/AuthController.cs` | từ dòng 279 | Thêm `POST api/auth/forgot-password/verify-otp`, dùng chính sách giới hạn tần suất hiện có. |
| `PhotocopyApi/Auth/OtpDatLaiStore.cs` | từ dòng 66 | Dùng chung kiểm tra/đếm sai trong khóa; xác minh chưa tiêu thụ, lưu tiêu thụ mã. |
| `PhotocopyApi.Tests/XacMinhOtpDatLaiTests.cs` | file mới | 12 ca kiểm thử mới; không gọi database hay SMTP. |
| `docs/XacMinhOTP_2026-10-09.jpg` | ảnh mới | Ảnh kiểm thử thực tế vùng sáu gạch chân; mã trong ảnh là dữ liệu thử không hợp lệ, không phải OTP tài khoản thật. |

Trang `DangNhap.razor` dùng các class CSS chung nên được tăng kích thước mà **không sửa logic đăng nhập**. Các trang nghiệp vụ và app mobile không đổi.

## Cách chạy và thử

1. Web/API đã được build, dịch vụ đang chạy. Nhấn **Ctrl+F5** tại `http://localhost:5064/dang-nhap` để tải bản mới.
2. Khi cần khởi động lại toàn bộ, dùng `MaNguon/scripts/chay_web.cmd` như trước; không cần chạy SQL.
3. Chọn Quên mật khẩu, nhập email đã đăng ký, mở Gmail lấy OTP. Không gửi OTP hoặc mật khẩu vào chat.
4. Nhập sáu số và bấm **Xác minh mã OTP**. Mã sai không cho chuyển bước; có thể gửi lại sau 60 giây.
5. Mã đúng mới hiện hai ô mật khẩu. Tự nhập mật khẩu mới và nhập lại đúng, rồi bấm **Đặt lại mật khẩu**. Quy tắc hiện tại: tối thiểu tám ký tự, có chữ hoa và số.
6. Khi báo thành công, quay về đăng nhập bằng mật khẩu mới. Nếu khởi động lại API giữa các bước, phải lấy mã mới.

## Skill đã áp dụng và hướng thiết kế

- [brainstorming](C:/Users/USER/.agents/skills/brainstorming/SKILL.md): chốt phạm vi/giả định và ghi các lựa chọn trước khi sửa.
- [frontend-design](C:/Users/USER/.agents/skills/frontend-design/SKILL.md): hướng **quản trị thực dụng**, DFII 14; giữ Be Vietnam Pro/IBM Plex Mono và màu xanh hiện có theo yêu cầu, khoảng cách theo nhịp 4/8px, không thêm hiệu ứng trang trí. Điểm nhấn phục vụ thao tác là sáu gạch chân và chỉ báo ba bước.
- [dotnet-backend](C:/Users/USER/.agents/skills/dotnet-backend/SKILL.md): giữ controller/DTO/store sẵn có, giới hạn tần suất và không phát sinh migration.
- [test-driven-development](C:/Users/USER/.agents/skills/test-driven-development/SKILL.md): kiểm thử đỏ trước, triển khai rồi chạy xanh và hồi quy.
- [browser-automation](C:/Users/USER/.agents/skills/browser-automation/SKILL.md): kiểm tra bằng thao tác thật, bề rộng CSS, focus, OTP và lỗi JavaScript; không coi build là bằng chứng đủ cho UI.
