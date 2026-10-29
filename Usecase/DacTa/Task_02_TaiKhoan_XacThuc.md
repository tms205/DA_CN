# Task 2 Tài khoản và xác thực

Phạm vi: HeThong/TaiKhoan_XacThuc.png. Đối chiếu source ngày 26/10/2026. Gồm 0 đặc tả nghiệp vụ và 10 đặc tả hệ thống.

- Người dùng hệ thống là tác nhân khái quát; Khách hàng có thể kế thừa tác nhân này. Hướng tam giác rỗng từ Khách hàng đến Người dùng hệ thống là phù hợp nếu muốn chia sẻ các chức năng chung. Các vai trò nhân sự cũng thuộc khái quát đó, nhưng quyền App/Web vẫn khác nhau.
- Một số comment DTO/giao diện còn ghi nhân viên quên mật khẩu phải xin cấp trên. Action forgot-password hiện gửi OTP cho mọi tài khoản email hoạt động; tài liệu này theo action thực thi, không theo comment cũ.
- Các mã thời hạn JWT lấy từ cấu hình; không khẳng định giá trị mặc định là hằng số bắt buộc của hệ thống.

## HT-4.2-01 Đăng ký tài khoản bằng email

**Use case**
Đăng ký tài khoản bằng email

**Use case ID**
HT-4.2-01

**Tác nhân chính**
Khách hàng chưa có tài khoản

**Tổng quan**
Tạo tài khoản CUSTOMER trên App sau khi xác minh email; không có đăng ký nhân sự tự do trên Web.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Bao gồm HT-4.2-03 xác minh OTP trong ngữ cảnh đăng ký. SMTP hỗ trợ gửi thư. Không dùng Google trong luồng này.

**Dòng sự kiện chính**
1. Khách mở Đăng ký, nhập họ tên, Gmail, số điện thoại, mật khẩu và xác nhận mật khẩu trên giao diện.
2. App kiểm tra dữ liệu và gọi POST api/auth/register/otp.
3. API kiểm tra trùng, giữ tạm dữ liệu và mật khẩu đã băm trong bộ nhớ; gửi OTP sáu số hiệu lực 5 phút.
4. Khách nhập OTP; App gửi email và mã tới POST api/auth/register.
5. API kiểm tra mã và kiểm tra trùng lần nữa; tạo CUSTOMER, xóa dữ liệu OTP và trả JWT cùng hồ sơ.
6. App lưu phiên theo lựa chọn ghi nhớ và mở trang chính.

**Dòng sự kiện phụ**
Bước 1 thiếu dữ liệu, email không phải Gmail, điện thoại không đủ 10 số bắt đầu 0 hoặc mật khẩu không đạt: sửa và quay lại bước 1.
Bước 3 email/số điện thoại trùng: không tạo tài khoản; khách đổi thông tin hoặc đăng nhập tài khoản cũ.
Bước 3 SMTP thất bại: xóa dữ liệu đăng ký tạm, báo lỗi; không báo đã gửi thành công.
Bước 4 sai/hết hạn OTP: xử lý HT-4.2-03; chưa tạo tài khoản. Gửi lại phải đợi 60 giây.
Bước 5 dữ liệu đã trùng do yêu cầu khác: kết thúc bằng lỗi trùng; không tạo tài khoản thứ hai.

**Tiền điều kiện**
App kết nối API; Gmail gửi OTP đã cấu hình; email và số điện thoại chưa được dùng.

**Hậu điều kiện**
Thành công có một tài khoản khách và phiên App. Khi chưa xác minh hoặc lỗi, không có tài khoản đăng ký mới từ luồng này.

**Căn cứ** AUTH, AUTH_DTO, REGISTER_UI

## HT-4.2-02 Đăng ký khách hàng qua Google

**Use case**
Đăng ký khách hàng qua Google

**Use case ID**
HT-4.2-02

**Tác nhân chính**
Khách hàng chưa có tài khoản; Google là tác nhân hỗ trợ

**Tổng quan**
Tạo khách mới từ danh tính Gmail do Google xác thực và thông tin bổ sung trên App.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Dùng bước xác thực Google của HT-4.2-05. Không bao gồm OTP SMTP, vì danh tính Gmail đã được Google xác minh.

**Dòng sự kiện chính**
1. Khách chọn tiếp tục bằng Google trên App và chọn tài khoản.
2. App nhận ID token rồi gọi POST api/auth/google.
3. API xác minh token, tìm liên kết theo Google subject hoặc email; nếu chưa có tài khoản, cấp vé đăng ký tạm hiệu lực 10 phút.
4. Khách bổ sung họ tên và số điện thoại; App gọi POST api/auth/google/register với vé.
5. API kiểm tra vé, số điện thoại và dữ liệu đồng thời; trong giao dịch tạo CUSTOMER cùng liên kết Google.
6. API tiêu thụ vé, trả JWT và hồ sơ; App bắt đầu phiên.

**Dòng sự kiện phụ**
Bước 1 khách hủy chọn Google: trở về màn đăng nhập, không tạo dữ liệu.
Bước 2 Google chưa cấu hình, token sai chữ ký/hết hạn/sai audience hoặc email chưa xác minh: từ chối, cho dùng mật khẩu.
Bước 3 đã có tài khoản phù hợp: đi theo đăng nhập Google, không tạo khách trùng.
Bước 4 số điện thoại trùng: nhập số khác hoặc dùng tài khoản cũ; quay lại bước 4.
Bước 4 vé đã dùng/hết hạn hoặc API khởi động lại: chọn Google lại từ bước 1.
Không có luồng đăng ký Google nhân sự trên Web; email chưa được cấp tài khoản Web bị từ chối.

**Tiền điều kiện**
GoogleAuth đã bật, OAuth đúng môi trường; khách cho phép chọn tài khoản Google.

**Hậu điều kiện**
Có CUSTOMER và liên kết subject sau khi commit; tài khoản Google mới không tự có mật khẩu nội bộ và không được cấp quyền quản lý.

**Căn cứ** GOOGLE, GOOGLE_VERIFY, GOOGLE_DTO, GOOGLE_UI

## HT-4.2-03 Xác minh email bằng OTP

**Use case**
Xác minh email bằng OTP

**Use case ID**
HT-4.2-03

**Tác nhân chính**
Người đăng ký hoặc người quên mật khẩu; SMTP hỗ trợ gửi thư

**Tổng quan**
Kiểm tra mã sáu chữ số đúng email và đúng mục đích trước khi tạo tài khoản hoặc cho nhập mật khẩu mới.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Được dùng trong HT-4.2-01 và HT-4.2-07. Hai ngữ cảnh có hạn mã và điểm tiêu thụ khác nhau, không dùng chéo mã.

**Dòng sự kiện chính**
1. Giao diện mở màn OTP, hiển thị email, sáu vị trí nhập số và thời gian chờ gửi lại.
2. Người dùng nhập hoặc dán đủ sáu số; giao diện kiểm tra định dạng.
3. Đăng ký: gửi tới api/auth/register; API kiểm tra mã trong dữ liệu đăng ký tạm.
4. Quên mật khẩu: gửi tới api/auth/forgot-password/verify-otp; API kiểm tra mã trong OtpDatLaiStore.
5. Đúng mã đăng ký thì tạo tài khoản; đúng mã quên mật khẩu thì cho mở màn mật khẩu mới.
6. Khi lưu mật khẩu mới, API kiểm tra OTP lại và tiêu thụ mã; xác minh trước đó chưa làm mã có hiệu lực vĩnh viễn.

**Dòng sự kiện phụ**
Bước 2 thiếu/không đủ sáu chữ số: ở lại màn OTP để nhập lại.
Bước 3 mã đăng ký hết hạn sau 5 phút; bước 4 mã quên mật khẩu hết hạn sau 10 phút: yêu cầu gửi mã mới.
Nhập sai đạt 5 lần: khóa luồng tương ứng 15 phút; không tiếp tục thử vô hạn.
Gửi lại trước 60 giây bị từ chối; gửi lại hợp lệ làm mã cũ không còn dùng được.
API khởi động lại làm mất mã đang giữ trong bộ nhớ; người dùng yêu cầu gửi lại.
Sau khi mã đã dùng để đăng ký/đặt lại mật khẩu, dùng lại bị từ chối.

**Tiền điều kiện**
Người dùng đã yêu cầu gửi mã trong đúng luồng; mã chưa bị hết hạn hoặc khóa.

**Hậu điều kiện**
Xác minh đúng chuyển được bước tiếp theo của đúng mục đích. Sai/hết hạn/khóa không thay đổi tài khoản hoặc mật khẩu.

**Căn cứ** AUTH, OTP_STORE, OTP_UI

## HT-4.2-04 Đăng nhập bằng tài khoản và mật khẩu

**Use case**
Đăng nhập bằng tài khoản và mật khẩu

**Use case ID**
HT-4.2-04

**Tác nhân chính**
Người dùng hệ thống gồm khách hàng, shipper, nhân viên, quản lý và CEO

**Tổng quan**
Xác thực danh tính và cấp phiên đúng kênh: App cho CUSTOMER/SHIPPER, Web cho STAFF/BRANCH_ADMIN/SUPER_ADMIN.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Khách hàng là một loại người dùng hệ thống, cùng với các vai trò khác; không phải mọi người dùng đều là khách. Google là phương thức thay thế tại HT-4.2-05.

**Dòng sự kiện chính**
1. Người dùng nhập email hoặc số điện thoại, mật khẩu và lựa chọn ghi nhớ nếu có.
2. Giao diện gửi POST api/auth/login cho App hoặc api/auth/admin/login cho Web.
3. API kiểm tra khóa tạm, tìm tài khoản và xác minh BCrypt.
4. API kiểm tra vai trò và trạng thái tài khoản có được vào kênh đã chọn không.
5. Với nhân viên hoặc quản lý đăng nhập Web, hệ thống mở/lấy lại ca riêng có tiền đầu ca mặc định 200.000đ; CEO mở ca thủ công sau khi chọn chi nhánh.
6. API cấp JWT với thời hạn theo cấu hình và hồ sơ; giao diện lưu phiên, mở trang theo vai trò.

**Dòng sự kiện phụ**
Bước 3 sai danh tính/mật khẩu: báo thông tin không chính xác; đủ 5 lần sai khóa đăng nhập 15 phút.
Bước 3 một số điện thoại gắn nhiều tài khoản cũ: yêu cầu đăng nhập bằng email.
Bước 4 tài khoản bị khóa hoặc sai kênh: từ chối, không cấp phiên cho kênh đó.
Bước 5 không mở được ca: trả lỗi, yêu cầu xử lý ca rồi đăng nhập lại.
Email cũ ngoài Gmail vẫn có thể đăng nhập; quy tắc Gmail áp dụng tạo mới, không chặn tài khoản cũ.
Mất mạng/hết hạn phiên: hiển thị lỗi hoặc yêu cầu đăng nhập lại, không giả thành công.

**Tiền điều kiện**
Tài khoản tồn tại và hoạt động; người dùng chọn đúng App hoặc Web.

**Hậu điều kiện**
Thành công có JWT và hồ sơ đúng quyền. Đăng nhập thất bại không có phiên mới; không mặc định mở ca cho CEO.

**Căn cứ** AUTH, AUTH_DTO, SESSION, ROLE_LOGIN

## HT-4.2-05 Đăng nhập bằng Google

**Use case**
Đăng nhập bằng Google

**Use case ID**
HT-4.2-05

**Tác nhân chính**
Người dùng đã có tài khoản đúng kênh; Google hỗ trợ xác thực

**Tổng quan**
Dùng danh tính Google để đăng nhập tài khoản hiện có và giữ nguyên vai trò được cấp.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Là phương thức xác thực thay thế cho HT-4.2-04, không phải lần nào đăng nhập thường cũng chạy Google. Có thể dẫn sang HT-4.2-02 khi khách App chưa có tài khoản.

**Dòng sự kiện chính**
1. Người dùng chọn Google và tài khoản trên giao diện.
2. App/Web lấy ID token, gửi api/auth/google hoặc api/auth/admin/google.
3. API kiểm chữ ký RS256, issuer, audience, hạn token, subject và email_verified; chỉ chấp nhận Gmail hợp lệ.
4. API tìm liên kết subject trước, sau đó email; kiểm tra tài khoản hoạt động và đúng kênh.
5. Nếu chưa liên kết và không xung đột, API lưu subject gắn với tài khoản, giữ nguyên quyền; hoàn tất đăng nhập chung.
6. Giao diện nhận JWT và mở chức năng theo vai trò; ca Web theo quy tắc HT-4.2-04.

**Dòng sự kiện phụ**
Bước 1 người dùng đóng cửa sổ Google: ở lại màn đăng nhập.
Bước 3 token không hợp lệ: từ chối; không tin email hoặc vai trò do giao diện tự gửi.
Bước 4 Web không có nhân sự được cấp: từ chối; không tự tạo tài khoản nhân sự.
Bước 4 App chưa có tài khoản: chuyển HT-4.2-02 bằng vé đăng ký tạm.
Bước 5 đã liên kết Google khác: báo xung đột và liên hệ quản trị; không ghi đè liên kết.
Google hoặc mạng lỗi: báo lỗi và cho dùng mật khẩu nếu tài khoản có mật khẩu nội bộ.

**Tiền điều kiện**
OAuth được cấu hình; có Gmail đã xác minh và tài khoản nội bộ phù hợp hoặc khách muốn đăng ký App.

**Hậu điều kiện**
Đăng nhập đúng tài khoản nội bộ; không lưu mật khẩu Google. Token sai hoặc xung đột không cấp phiên.

**Căn cứ** GOOGLE, GOOGLE_VERIFY, GOOGLE_UI, AUTH

## HT-4.2-06 Xem hồ sơ cá nhân

**Use case**
Xem hồ sơ cá nhân

**Use case ID**
HT-4.2-06

**Tác nhân chính**
Người dùng đã đăng nhập

**Tổng quan**
Đọc thông tin tài khoản của chính người gọi trên App hoặc Web.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Độc lập với cập nhật HT-4.2-08 và đổi mật khẩu HT-4.2-09; có thể mở các chức năng đó từ hồ sơ.

**Dòng sự kiện chính**
1. Người dùng mở hồ sơ cá nhân.
2. Giao diện gọi GET api/auth/me kèm JWT.
3. API lấy mã người dùng từ claim, đọc hồ sơ tương ứng.
4. API trả họ tên, email, điện thoại, vai trò và thông tin chi nhánh/địa chỉ mặc định nếu có.
5. Giao diện trình bày dữ liệu và các thao tác được phép.

**Dòng sự kiện phụ**
Bước 2 token thiếu/hết hạn: yêu cầu đăng nhập; không xem được hồ sơ.
Bước 3 không tìm thấy tài khoản: báo không tồn tại, không hiển thị dữ liệu người khác.
Bước 2 mất mạng: báo lỗi và cho thử lại từ bước 2.
Không có tham số cho người dùng tự chọn mã tài khoản khác trong chức năng hồ sơ cá nhân.

**Tiền điều kiện**
Có phiên hợp lệ.

**Hậu điều kiện**
Hồ sơ được hiển thị; không thay đổi dữ liệu và không trả mật khẩu/hash.

**Căn cứ** AUTH, AUTH_DTO, PROFILE_UI

## HT-4.2-07 Đặt lại mật khẩu khi quên

**Use case**
Đặt lại mật khẩu khi quên

**Use case ID**
HT-4.2-07

**Tác nhân chính**
Người có tài khoản email đang hoạt động; SMTP hỗ trợ

**Tổng quan**
Khôi phục quyền đăng nhập bằng OTP email trước, sau đó nhập mật khẩu mới hai lần trên giao diện.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Bao gồm HT-4.2-03 trong ngữ cảnh quên mật khẩu; khác đổi mật khẩu khi đang đăng nhập.

**Dòng sự kiện chính**
1. Người dùng chọn Quên mật khẩu, nhập email và gửi tới POST api/auth/forgot-password.
2. API giới hạn gửi lại; nếu email thuộc tài khoản hoạt động thì gửi OTP 10 phút, phản hồi chung tránh khẳng định email tồn tại.
3. Người dùng nhập mã; API verify-otp kiểm tra đúng trước khi giao diện mở màn mật khẩu mới.
4. Người dùng nhập mật khẩu mới và nhập lại; giao diện kiểm tra hai ô khớp và độ mạnh.
5. Giao diện gửi email, OTP và mật khẩu mới tới POST api/auth/reset-password.
6. API kiểm OTP lại, tài khoản hoạt động và mật khẩu không trùng hiện tại; tiêu thụ mã, lưu BCrypt mới, xóa bộ đếm khóa đăng nhập liên quan.
7. Thông báo thành công, yêu cầu đăng nhập bằng mật khẩu mới.

**Dòng sự kiện phụ**
Bước 2 email không tồn tại/không hoạt động: phản hồi chung nhưng không tạo mã dùng được; không tiết lộ tài khoản.
Bước 2 gửi thư lỗi: báo lỗi thật; không chuyển như đã gửi thành công.
Bước 3 sai/hết hạn/khóa: ở lại OTP hoặc gửi lại theo HT-4.2-03.
Bước 4 xác nhận không khớp: nhập lại; chưa gọi lưu mật khẩu.
Bước 6 mật khẩu trùng hiện tại: báo trùng, quay lại bước 4; không đổi mật khẩu.
Bước 6 mã đã dùng hoặc tài khoản bị khóa trong lúc chờ: từ chối; không tự đăng nhập.

**Tiền điều kiện**
Có email của tài khoản hoạt động; dịch vụ gửi thư đã cấu hình. Không bắt buộc còn phiên.

**Hậu điều kiện**
Thành công mật khẩu đã đổi và mã đã dùng một lần. Thất bại không báo đã đặt lại thành công; chưa có cơ chế thu hồi mọi JWT cũ được mô tả trong luồng này.

**Căn cứ** AUTH, OTP_STORE, RESET_UI

## HT-4.2-08 Cập nhật hồ sơ cá nhân

**Use case**
Cập nhật hồ sơ cá nhân

**Use case ID**
HT-4.2-08

**Tác nhân chính**
Người dùng đã đăng nhập

**Tổng quan**
Sửa họ tên, điện thoại của chính mình; khách có thể sửa thêm địa chỉ mặc định.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Thường mở từ HT-4.2-06. Không cho sửa email, vai trò hoặc chi nhánh bằng API hồ sơ cá nhân.

**Dòng sự kiện chính**
1. Người dùng mở sửa hồ sơ, chỉnh họ tên và điện thoại; khách có thể chỉnh địa chỉ mặc định.
2. Giao diện kiểm tra dữ liệu, gửi PUT api/auth/me.
3. API lấy mã người dùng từ JWT, chuẩn hóa chuỗi và kiểm tra điện thoại không trùng tài khoản khác.
4. API cập nhật họ tên, điện thoại; chỉ CUSTOMER được cập nhật cột địa chỉ theo yêu cầu này.
5. API đọc lại hồ sơ; giao diện lưu thông tin phiên mới và báo thành công.

**Dòng sự kiện phụ**
Bước 2 tên rỗng/quá 100 ký tự, điện thoại không hợp lệ hoặc địa chỉ quá 255 ký tự: sửa và quay lại bước 1.
Bước 3 điện thoại trùng: báo lỗi, giữ hồ sơ cũ và quay lại bước 1.
Nhân sự gửi địa chỉ mặc định: cột địa chỉ của nhân sự giữ nguyên.
Token hết hạn hoặc mất mạng: không khẳng định đã lưu; tải lại hồ sơ để kiểm tra trước khi sửa tiếp.

**Tiền điều kiện**
Có phiên hợp lệ và mở biểu mẫu hồ sơ.

**Hậu điều kiện**
Dữ liệu hồ sơ hợp lệ được lưu và hiển thị lại; quyền và email không đổi qua chức năng này.

**Căn cứ** AUTH, AUTH_DTO, PROFILE_UI, SESSION

## HT-4.2-09 Đổi mật khẩu

**Use case**
Đổi mật khẩu

**Use case ID**
HT-4.2-09

**Tác nhân chính**
Người dùng đã đăng nhập và có mật khẩu nội bộ

**Tổng quan**
Đổi mật khẩu sau khi xác minh mật khẩu hiện tại; giao diện yêu cầu xác nhận mật khẩu mới.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Mở từ hồ sơ/cài đặt. Không dùng OTP; người quên mật khẩu hoặc tài khoản Google chưa có mật khẩu dùng HT-4.2-07.

**Dòng sự kiện chính**
1. Người dùng mở Đổi mật khẩu và nhập mật khẩu hiện tại, mật khẩu mới, nhập lại mật khẩu mới.
2. Giao diện kiểm tra đủ ô, mật khẩu mới tối thiểu 8 ký tự có chữ hoa và số, hai ô mới khớp.
3. Giao diện gửi PUT api/auth/me/password với mật khẩu cũ và mới.
4. API đọc hash hiện tại, xác minh mật khẩu cũ bằng BCrypt và kiểm tra mật khẩu mới không trùng hiện tại.
5. API băm mật khẩu mới, cập nhật và trả thông báo; giao diện kết thúc biểu mẫu.

**Dòng sự kiện phụ**
Bước 2 xác nhận không khớp: ở lại biểu mẫu; chưa cập nhật.
Bước 4 mật khẩu cũ sai: báo lỗi, cho nhập lại bước 1.
Bước 4 mật khẩu mới trùng mật khẩu hiện tại: báo trùng, cho chọn mật khẩu khác.
Tài khoản Google chưa có hash mật khẩu: không thể vượt qua bước mật khẩu cũ; dùng quên mật khẩu nếu cần tạo mật khẩu nội bộ.
Không khẳng định đổi mật khẩu tự thu hồi mọi JWT đã cấp; code hiện tại cập nhật hash mật khẩu.

**Tiền điều kiện**
Có phiên hợp lệ và biết mật khẩu hiện tại.

**Hậu điều kiện**
Mật khẩu nội bộ đổi khi toàn bộ kiểm tra đúng; nếu lỗi thì giữ hash cũ.

**Căn cứ** AUTH, AUTH_DTO, CHANGE_UI

## HT-4.2-10 Đăng xuất

**Use case**
Đăng xuất

**Use case ID**
HT-4.2-10

**Tác nhân chính**
Người dùng đang có phiên trên thiết bị

**Tổng quan**
Kết thúc phiên giao diện trên App hoặc Web và trở về màn đăng nhập.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Thao tác độc lập; không đồng nghĩa đóng ca tiền mặt hoặc hủy đơn.

**Dòng sự kiện chính**
1. Người dùng chọn Đăng xuất.
2. Giao diện ngừng sử dụng phiên và ngắt luồng realtime liên quan theo vòng đời ứng dụng.
3. App xóa token, hồ sơ và lựa chọn ghi nhớ trong bộ nhớ, xóa phiên ở secure storage; Web xóa phiên được lưu.
4. Giao diện chuyển về đăng nhập; các thao tác cần quyền đòi hỏi đăng nhập lại.

**Dòng sự kiện phụ**
Thiết bị không có mạng: vẫn có thể xóa phiên cục bộ, không cần API logout.
Kho bảo mật App không xóa được: code bắt lỗi; phiên trong bộ nhớ vẫn được xóa. Không coi đây là thu hồi token tại máy chủ.
JWT đã sao chép ra ngoài có thể còn hiệu lực đến hạn; hệ thống không có endpoint blacklist/logout để hủy toàn bộ JWT.
Ca đang mở và đơn đang chạy không tự đóng/hủy khi bấm Đăng xuất.

**Tiền điều kiện**
Giao diện đang giữ phiên đăng nhập.

**Hậu điều kiện**
Giao diện không còn phiên sử dụng; không ghi nhận là đã đóng ca và không sửa trạng thái đơn.

**Căn cứ** SESSION, WEB_SESSION, MOBILE_MAIN

## Đối chiếu nhãn trên hình gốc

| Nhãn | Đặc tả | Kết luận | Cách hiểu và điều chỉnh |
| --- | --- | --- | --- |
| Đăng ký khách hàng qua Google | HT-4.2-02 | Đúng có điều kiện | Chỉ khách App mới được tạo tự phục vụ; Web phải có nhân sự được cấp. |
| Đăng ký tài khoản bằng Email | HT-4.2-01 | Đúng | Tạo sau OTP; email mới phải Gmail. |
| Xác minh email bằng OTP | HT-4.2-03 | Đúng có hai ngữ cảnh | Đăng ký 5 phút; quên mật khẩu 10 phút. SMTP là tác nhân hỗ trợ. |
| Đăng nhập bằng Google | HT-4.2-05 | Chỉnh quan hệ | Phương thức đăng nhập thay thế; không bắt buộc suy diễn extend nếu chưa xác định điểm mở rộng. |
| Đăng nhập | HT-4.2-04 | Đúng | Tách kênh App và Web, kiểm quyền vai trò. |
| Xem hồ sơ cá nhân | HT-4.2-06 | Đúng | Chỉ hồ sơ chính người gọi. |
| Cập nhật hồ sơ cá nhân | HT-4.2-08 | Đúng | Không đổi email/quyền/chi nhánh qua me. |
| Đổi mật khẩu | HT-4.2-09 | Đúng | Có mật khẩu hiện tại và kiểm tra trùng mật khẩu mới. |
| Đặt lại mật khẩu | HT-4.2-07 | Đúng | Xác minh OTP trước, API vẫn kiểm tra mã khi lưu mật khẩu mới. |
| Đăng xuất | HT-4.2-10 | Đúng với giới hạn | Xóa phiên cục bộ; không thu hồi mọi JWT hoặc đóng ca. |

## Căn cứ mã nguồn

- **AUTH** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/AuthController.cs:27](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/AuthController.cs:27) — Đăng ký OTP, đăng nhập, hồ sơ, quên/đổi mật khẩu; đọc action thực thi thay vì comment cũ.
- **AUTH_DTO** [MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Auth/AuthDtos.cs:50](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Auth/AuthDtos.cs:50) — Validation Gmail mới, điện thoại, mật khẩu và hồ sơ.
- **CHANGE_UI** [MaNguon/photocopy_mobile/lib/screens/toi/doi_mat_khau_screen.dart:9](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/toi/doi_mat_khau_screen.dart:9) — Nhập mật khẩu cũ, mới và nhập lại, kiểm khớp phía UI.
- **GOOGLE** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/GoogleAuthController.cs:16](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/GoogleAuthController.cs:16) — Google trên App/Web, vé bổ sung, liên kết và tạo CUSTOMER.
- **GOOGLE_DTO** [MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Auth/GoogleAuthDtos.cs:6](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Auth/GoogleAuthDtos.cs:6) — Dữ liệu xác thực Google và đăng ký bổ sung.
- **GOOGLE_UI** [MaNguon/photocopy_mobile/lib/screens/xac_thuc/bo_sung_google_screen.dart:27](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/xac_thuc/bo_sung_google_screen.dart:27) — Bổ sung thông tin khách và mở phiên Google.
- **GOOGLE_VERIFY** [MaNguon/PhotocopyChain/PhotocopyApi/Auth/GoogleTokenVerifier.cs:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Auth/GoogleTokenVerifier.cs:17) — Xác minh chữ ký, issuer, audience, thời hạn, subject và Gmail verified.
- **MOBILE_MAIN** [MaNguon/photocopy_mobile/lib/main.dart:10](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/main.dart:10) — Khởi tạo ứng dụng và phiên.
- **OTP_STORE** [MaNguon/PhotocopyChain/PhotocopyApi/Auth/OtpDatLaiStore.cs:9](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Auth/OtpDatLaiStore.cs:9) — Hạn OTP quên mật khẩu, giới hạn gửi/sai, băm và tiêu thụ mã.
- **OTP_UI** [MaNguon/photocopy_mobile/lib/screens/xac_thuc/xac_thuc_otp_screen.dart:20](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/xac_thuc/xac_thuc_otp_screen.dart:20) — Sáu vị trí OTP, xác minh trước form mật khẩu mới.
- **PROFILE_UI** [MaNguon/photocopy_mobile/lib/screens/toi/ho_so_screen.dart:12](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/toi/ho_so_screen.dart:12) — Hồ sơ và biểu mẫu cá nhân.
- **REGISTER_UI** [MaNguon/photocopy_mobile/lib/screens/xac_thuc/dang_ky_screen.dart:14](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/xac_thuc/dang_ky_screen.dart:14) — Biểu mẫu đăng ký và xác nhận mật khẩu phía App.
- **RESET_UI** [MaNguon/photocopy_mobile/lib/screens/xac_thuc/quen_mat_khau_screen.dart:10](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/xac_thuc/quen_mat_khau_screen.dart:10) — Yêu cầu OTP trước khi đặt lại mật khẩu.
- **ROLE_LOGIN** [MaNguon/PhotocopyChain/PhotocopyApi/Auth/QuyTacDangNhap.cs:3](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Auth/QuyTacDangNhap.cs:3) — Phân quyền tài khoản theo App và Web.
- **SESSION** [MaNguon/photocopy_mobile/lib/phien.dart:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/phien.dart:11) — Lưu/xóa phiên App, secure storage khi ghi nhớ.
- **WEB_SESSION** [MaNguon/PhotocopyChain/PhotocopyAdmin/PhienDangNhap.cs:130](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyAdmin/PhienDangNhap.cs:130) — Xóa token và dữ liệu phiên trên Web.
