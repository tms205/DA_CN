# Đặc tả Use Case phần Mạnh của PhotocopyChain

Tài liệu đặc tả toàn bộ bảy sơ đồ trong thư mục Usecase thuộc phần Mạnh: nghiệp vụ 3.2 và 3.5; hệ thống 4.2, 4.3, 4.4, 4.5 và 4.11. Có 11 đặc tả nghiệp vụ, 38 đặc tả hệ thống hoặc hành vi kỹ thuật được diễn giải theo mục tiêu và 53 dòng đối chiếu, gồm 52 nhãn trên hình gốc và một chức năng nhận hoàn bổ sung từ code.
Mẫu nghiệp vụ có ba phần: giới thiệu, dòng cơ bản và dòng thay thế. Mẫu hệ thống có đủ mười trường theo tài liệu Word/ảnh tham khảo. Bước kiểm tra, nhập dữ liệu và hành vi nội bộ được ghi đúng vai trò; không coi mọi oval đều phải có một endpoint độc lập.
Căn cứ là mã nguồn đọc ngày 26/10/2026, không phải xác nhận mọi tích hợp ngoài đã thử giao dịch thật. Không sửa code, cấu hình, CSDL hoặc ảnh gốc để làm khớp mô tả. Những điểm ảnh vẽ khác code được chỉ ra trong đối chiếu từng nhóm.
ID trong tài liệu là mã đặc tả, không phải mã tính năng hoặc endpoint. Các chức năng được nhắc ở nhiều hình được dẫn chiếu để giữ cùng một điều kiện xử lý. Ưu tiên Cao là mức đề xuất phục vụ hoàn thiện báo cáo, không phải thuộc tính lưu trong CSDL.

## Quy tắc và thuật ngữ chung

**Tác nhân và quyền**
Khách hàng là một loại Người dùng hệ thống. App cho CUSTOMER/SHIPPER; Web cho STAFF/BRANCH_ADMIN/SUPER_ADMIN. Các hành vi khách trong bộ sơ đồ này chỉ dành CUSTOMER đúng chủ dữ liệu; không suy rộng quyền đặt in cho shipper.

**Vòng đời đơn**
CHO_XAC_NHAN → DA_XAC_NHAN → DANG_IN → DA_IN → HOAN_THANH khi nhận tại quầy; giao hàng đi qua DANG_GIAO và có thể GIAO_THAT_BAI trước khi thử lại hoặc HOAN_THANH. Hủy chỉ từ hai trạng thái đầu tới DA_HUY.

**Vòng đời tiền**
CHUA_THANH_TOAN → DA_THANH_TOAN → DA_HOAN_TIEN khi đủ điều kiện. Đơn App luôn VIETQR; COD thuộc quầy. Chờ hoàn là bộ lọc đơn DA_HUY trước in đã trả nhưng chưa hoàn, không phải trạng thái mới.

**Nguồn xác nhận tiền**
Webhook có chữ ký hoặc API payOS đối soát hợp lệ. URL quay về, thao tác mở QR và lời khách báo đã chuyển không đủ để cập nhật tiền.

**Xếp in**
Nhận tiền trước, xếp máy sau. Chưa đủ file/máy/vật tư có thể dừng chờ in; đã vào DANG_IN không hủy/hoàn theo luồng trước in. Bàn giao connector, hoàn tất giấy thực tế thuộc phần Tài.

**Hoàn tiền**
CEO chuyển tiền qua ngân hàng bên ngoài rồi xác nhận; khách nhập lý do/ngân hàng/tài khoản và chỉ xem kết quả. Không có tự động refund ngân hàng, chứng từ hoàn hoặc bước quản lý duyệt hủy riêng.

**Thông báo và thời gian**
SignalR khi còn kết nối, polling thanh toán khi màn hoạt động; không hứa push khi tắt App. Giờ hẹn là ước tính; chưa có timestamp cho mọi mốc hoặc mô hình SLA đầy đủ.

**Cách đọc luồng phụ**
Dòng bắt đầu Bước n chỉ điểm phân nhánh. Yêu cầu sửa/nhập lại quay về bước được chỉ; từ chối hoặc kết thúc giữ trạng thái cũ. Lỗi mất phản hồi cần đọc lại trạng thái, không suy rằng yêu cầu chưa được lưu.

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

# Task 3 Đặt in trực tuyến và thanh toán trước

Phạm vi: NghiepVu/DatIn_ThanhToan.png, HeThong/DatInTrenApp.png. Đối chiếu source ngày 26/10/2026. Gồm 6 đặc tả nghiệp vụ và 10 đặc tả hệ thống.

- Hình 3.2 mô tả công việc cửa hàng với khách và ngân hàng; không đưa API, SMTP hoặc Geoapify vào tác nhân nghiệp vụ.
- Các đường include từ Đặt đơn tới Tiếp nhận chỉ để diễn tả xảy ra sau nhau là chưa chặt. Nên diễn đạt quy trình nối tiếp bằng mô tả/Activity; include chỉ dùng cho hành vi bắt buộc được gọi trong phạm vi use case.
- Tên Ahamove còn trong comment/thông báo SQL cũ; code gọi bản đồ hiện tại là Geoapify và tự tính phí ship nội bộ. Không suy từ comment thành một tích hợp hãng giao hàng đang chạy.
- API nhận danh sách file nhưng giao diện App hiện gửi một file trong mỗi lượt. Không mô tả App có trình chọn nhiều file mà chưa triển khai.

## NV-3.2-01 Đặt đơn in trực tuyến

**Giới thiệu về use case nghiệp vụ**
Khách muốn cửa hàng in tài liệu, mẫu đơn trống hoặc ảnh thẻ. Mục tiêu là thống nhất nội dung, giá, nơi nhận và tiền thanh toán trước khi tổ chức in. Khách và ngân hàng là bên ngoài; nhân viên và shipper là bên thực hiện trong tổ chức.

**Các dòng cơ bản**
1. Khách cung cấp tài liệu hoặc chọn mẫu/ảnh và xác định số lượng cần in.
2. Cửa hàng cho khách lựa chọn quy cách có bán, chi nhánh đáp ứng và cách nhận.
3. Cửa hàng báo tiền in, gia công, phụ phí, giao hàng, giảm giá và thời điểm dự kiến.
4. Khách kiểm tra, đồng ý đặt và nhận mã đơn cùng hướng dẫn chuyển khoản.
5. Khách chuyển đủ tiền; cửa hàng xác minh khoản tiền thực nhận.
6. Cửa hàng tiếp nhận/xếp in khi đủ điều kiện, sau đó chuyển sang nhận tại quầy hoặc giao nội bộ.

**Các dòng thay thế**
- Tài liệu lỗi hoặc cấu hình không được bán: yêu cầu thay tài liệu/quy cách, quay lại bước 1–2.
- Không có chi nhánh đáp ứng: chưa nhận đơn theo lựa chọn đó.
- Chưa nhận đủ tiền: chưa coi đã thanh toán; đơn quá hạn được xử lý riêng.
- Khách chọn giao tận nơi: lấy điểm nhận và báo phí trước bước 4; không thu COD khi giao.
- Khách muốn hủy trước khi bắt đầu in: chuyển nghiệp vụ 3.5; đã bắt đầu in không hủy theo luồng thông thường.

**Căn cứ** FILE, ORDER, QUOTE, PAYMENT, AUTO_PRINT

## NV-3.2-02 Cung cấp tài liệu và lựa chọn quy cách in

**Giới thiệu về use case nghiệp vụ**
Khách giao nội dung cần in và thỏa thuận thành phẩm với cửa hàng. Nội dung có thể là tài liệu tải lên, biểu mẫu trắng có sẵn hoặc ảnh thẻ; không yêu cầu khách tự tạo biểu mẫu bằng tay trong phần mềm.

**Các dòng cơ bản**
1. Khách chọn nguồn tài liệu và kiểm tra đúng nội dung.
2. Cửa hàng tiếp nhận, chuyển về định dạng in được và xác định số trang hoặc số ảnh.
3. Khách chọn giấy/dịch vụ, một hoặc hai mặt, màu sắc, hướng giấy và số bản phù hợp.
4. Khách chọn các gia công được phép và tốc độ thường/hỏa tốc.
5. Cửa hàng kiểm tra các lựa chọn có thể thực hiện và chuyển sang báo giá.

**Các dòng thay thế**
- Không đọc được tài liệu: yêu cầu file thay thế; chưa báo giá thành đơn.
- Mẫu đã thay hoặc ngừng cung cấp: xem danh sách mới và chọn lại.
- Ảnh thẻ: chọn kích thước, số ảnh; không áp gia công tài liệu hoặc tùy ý thay bố trí PDF đã ghép.
- Bìa kiếng có thể đi với lò xo; lò xo không đi với kim góc; kim góc quá 50 tờ mỗi bản bị từ chối.
- Giấy tồn kho mới chưa có dịch vụ, giá hoặc máy phù hợp không tự trở thành lựa chọn in.

**Căn cứ** FILE, CATALOG, PRINT_RULE, PHOTO_RULE, SQL_PRICE

## NV-3.2-03 Thanh toán chuyển khoản trước

**Giới thiệu về use case nghiệp vụ**
Khách thanh toán toàn bộ đơn App qua ngân hàng theo hướng dẫn QR. Cửa hàng chỉ công nhận đã trả khi nhận được căn cứ xác thực từ cổng thanh toán; không dựa vào lời khách báo hoặc trang quay về.

**Các dòng cơ bản**
1. Cửa hàng cấp thông tin QR của đúng đơn, đúng số tiền và hạn trả.
2. Khách kiểm tra người nhận, số tiền, nội dung và chuyển khoản trên ngân hàng.
3. Ngân hàng/cổng thanh toán cung cấp kết quả giao dịch cho cửa hàng.
4. Cửa hàng đối chiếu đơn, tiền và thông tin giao dịch.
5. Khoản hợp lệ được ghi nhận đã trả và chuyển điều kiện cho xếp in.

**Các dòng thay thế**
- Khách chỉ mở QR hoặc đóng màn ngân hàng: chưa đủ căn cứ thanh toán.
- Không khớp tiền hoặc chưa nhận thông tin xác thực: chờ đối soát, không tự đánh dấu đã trả.
- Quá 15 phút khi còn chờ và chưa thanh toán: kết thúc đơn theo chính sách quá hạn.
- Tiền về sau khi đã hủy: giữ đơn hủy và xử lý hoàn, không đưa lại vào in.
- Ngân hàng báo lặp: không thu thêm hoặc tạo khoản thanh toán mới cho cùng đơn.

**Căn cứ** PAYMENT, SQL_PAY, TIMEOUT

## NV-3.2-04 Tiếp nhận đơn in đã thanh toán

**Giới thiệu về use case nghiệp vụ**
Cửa hàng chuyển đơn đã nhận đủ tiền sang quy trình sản xuất. Nhân viên theo dõi và xử lý ngoại lệ; các đơn VietQR đủ điều kiện có thể được tiếp nhận/xếp in tự động, không bắt buộc có thao tác nhân viên ở mọi đơn.

**Các dòng cơ bản**
1. Cửa hàng xác định đơn đã thanh toán còn chờ tiếp nhận/chờ in.
2. Kiểm tra tài liệu còn sẵn và chi nhánh có máy đáp ứng.
3. Tiếp nhận đơn; chọn máy đang kết nối phù hợp năng lực và hàng đợi.
4. Đưa lệnh sang quy trình in; nhân viên theo dõi tiến độ do connector báo.
5. Sau khi in xong, tổ chức gia công, bàn giao tại quầy hoặc chuyển giao hàng.

**Các dòng thay thế**
- Chưa có máy phù hợp kết nối hoặc file không còn sẵn: giữ đơn đã trả ở chờ in và báo cần xử lý.
- Không đủ vật tư hoặc bước xếp máy lỗi: chưa coi bắt đầu in thành công; giữ khoản đã trả.
- Nhân viên tiếp nhận thủ công phải đúng chi nhánh và có ca đang mở.
- Máy trục trặc khi đang in: thuộc xử lý máy/connector; không biến thành hủy đơn trước in.
- Khách nhận tại quầy hoặc shipper nhận hàng là nghiệp vụ tiếp sau, không dùng include để diễn tả đơn thuần thứ tự thời gian.

**Căn cứ** AUTO_PRINT, ORDER, SQL_ACCEPT

## NV-3.2-05 Áp dụng khuyến mãi cho đơn trực tuyến

**Giới thiệu về use case nghiệp vụ**
Khách cung cấp mã để được giảm phần tiền in và gia công nếu thỏa điều kiện của chi nhánh. Đây là lựa chọn tùy ý, không bắt buộc trong mọi lần đặt in.

**Các dòng cơ bản**
1. Khách nhập mã khuyến mãi khi xem tổng tiền.
2. Cửa hàng kiểm tra chi nhánh, thời hạn, trạng thái, số lượt và mức tối thiểu.
3. Nếu hợp lệ, tính giảm theo tỷ lệ và trần giảm.
4. Hiển thị mức giảm và tổng tiền mới để khách quyết định đặt.
5. Khi tạo đơn có mã hợp lệ, ghi nhận mã và tăng lượt sử dụng.

**Các dòng thay thế**
- Mã sai/hết hạn/hết lượt: thông báo lý do, không giảm và vẫn cho đặt giá thường.
- Chưa đủ tiền hàng tối thiểu: cho khách đổi nhu cầu hoặc tiếp tục không giảm.
- Đổi chi nhánh/quy cách: tính lại điều kiện và giá.
- Xem báo giá chưa chiếm một lượt mã; tạo đơn mới là thời điểm ghi lượt.
- Không mô tả tự hoàn lại lượt mã khi hủy; thủ tục hủy hiện không có bước đó.

**Căn cứ** SQL_PRICE, SQL_CREATE, BRANCH_UI

## NV-3.2-06 Giao tài liệu tận nơi bằng shipper nội bộ

**Giới thiệu về use case nghiệp vụ**
Khách chọn giao thay vì đến quầy; cửa hàng thu trước cả tiền hàng và phí ship, giao bằng shipper của chi nhánh. Mô tả ở mức nối tiếp đặt in, chi tiết nghiệp vụ giao hàng thuộc phần Tài 3.4 và 4.10.

**Các dòng cơ bản**
1. Khách xác định vị trí nhận, bổ sung số nhà/tầng/phòng, điện thoại và chỉ dẫn cần thiết.
2. Cửa hàng kiểm tra vùng phục vụ và báo phí trước khi đặt.
3. Khách thanh toán đủ tiền; cửa hàng in và hoàn thiện tài liệu.
4. Shipper nội bộ đúng chi nhánh nhận hoặc được phân công đơn đã in.
5. Shipper giao cho khách và ghi kết quả; thành công thì đơn hoàn tất, không thu thêm tiền.

**Các dòng thay thế**
- Không xác định được điểm giao hoặc ngoài bán kính: chọn lại phương án nhận; không báo phí giả.
- Khách từ chối quyền vị trí: giao tận nơi hiện chưa hoàn thành được trên App; có thể chọn nhận tại quầy.
- Giao thất bại: lưu lý do và lượt giao; còn dưới ba lượt thì cửa hàng có thể cho nhận lại.
- Đủ ba lượt thất bại: cần cửa hàng liên hệ khách; không tự hoàn tiền hoặc tự tiếp tục vô hạn.
- Không gọi hãng giao hàng ngoài để tạo vận đơn; Geoapify chỉ hỗ trợ địa chỉ và tuyến đường.

**Căn cứ** LOCATION_UI, GEO, SHIP, SHIP_RULE

## HT-4.3-01 Tải tài liệu in

**Use case**
Tải tài liệu in

**Use case ID**
HT-4.3-01

**Tác nhân chính**
Khách hàng

**Tổng quan**
Đưa tài liệu từ điện thoại lên máy chủ để chuyển PDF và đếm trang; App hiện xử lý từng file trong một lượt đặt.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Là một cách cung cấp nội dung cho HT-4.3-10; hai cách thay thế là mẫu đơn HT-4.3-02 và ảnh thẻ HT-4.3-03.

**Dòng sự kiện chính**
1. Khách chọn In tài liệu và mở bộ chọn file.
2. Khách chọn file được hỗ trợ; App gửi multipart POST api/file-in.
3. API kiểm tra tên, rỗng, dung lượng cấu hình và đuôi; gắn file với khách lấy từ JWT.
4. API giữ PDF có sẵn hoặc đổi Office/ảnh sang PDF, đếm số trang; file gốc chuyển đổi được dọn.
5. API lưu FileIn và trả mã, tên, số trang, trạng thái; App mở cấu hình in.

**Dòng sự kiện phụ**
Bước 1 khách hủy chọn file: giữ màn hiện tại, không gửi lên.
Bước 3 file rỗng/quá dung lượng/đuôi không hỗ trợ: báo lỗi và chọn lại bước 1.
Bước 4 chuyển đổi hoặc đếm trang lỗi: dọn file của lượt thất bại; không trả file sẵn sàng giả.
Mất mạng khi tải: báo lỗi, chọn thử lại; không khẳng định đơn đã được tạo.
PDF, Word, Excel, PowerPoint, TXT và PNG/JPG là các nhóm hỗ trợ; mức dung lượng đọc từ cấu hình và chịu giới hạn request.

**Tiền điều kiện**
Khách đã đăng nhập; có file ở thiết bị và API truy cập được kho lưu trữ.

**Hậu điều kiện**
Thành công có FileIn của đúng khách cùng PDF và số trang; chưa có DonHang hoặc thanh toán.

**Căn cứ** FILE, FILE_UI

## HT-4.3-02 In mẫu đơn trống có sẵn

**Use case**
In mẫu đơn trống có sẵn

**Use case ID**
HT-4.3-02

**Tác nhân chính**
Khách hàng

**Tổng quan**
Chọn bản mẫu trắng đã được quản trị thư viện; xem trước đúng bản rồi đưa vào quy trình đặt in.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Thay thế tải tài liệu trong HT-4.3-01. Chọn mẫu chưa phải in vật lý; còn cấu hình, chi nhánh, tạo đơn và thanh toán.

**Dòng sự kiện chính**
1. Khách chọn In mẫu đơn trống; App gọi GET api/file-in/mau.
2. Khách tìm theo tên, lọc nhóm và mở xem trước mẫu/trang.
3. API trả ảnh xem trước hoặc PDF của mã và phiên bản mẫu.
4. Khách chọn dùng mẫu; App POST api/file-in/mau/{maMau} kèm phiên bản.
5. API đọc đúng PDF và dùng lại đường tải file để lưu bản FileIn cho khách.
6. App chuyển sang quy cách; nội dung PDF mẫu không được tự điền hộ khách.

**Dòng sự kiện phụ**
Bước 1 thư viện trống/lỗi: hiển thị trống/lỗi và cho làm mới.
Bước 3 mã hoặc trang đã bị gỡ/ẩn: không chọn được; tải danh sách lại.
Bước 4 phiên bản đã đổi: báo xung đột và yêu cầu xem bản mới trước khi chọn.
Khách đóng xem trước: trở về thư viện, không tạo file đặt in.
Mẫu đã thành bản file của đơn cũ không bị mô tả là tự thay theo mẫu mới.

**Tiền điều kiện**
Khách có phiên; thư viện có mẫu đang hiển thị và tài sản PDF/ảnh xem trước đọc được.

**Hậu điều kiện**
Có bản PDF mẫu thuộc khách để đặt in; thao tác xem trước đơn thuần không tạo đơn và không gửi máy in.

**Căn cứ** FILE, FILE_UI, TEMPLATE

## HT-4.3-03 In ảnh thẻ theo kích thước

**Use case**
In ảnh thẻ theo kích thước

**Use case ID**
HT-4.3-03

**Tác nhân chính**
Khách hàng

**Tổng quan**
Ghép JPG/PNG thành PDF ảnh thẻ với số ảnh và kích thước chính xác; tính giá theo từng ảnh.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Cách chọn nội dung thay thế HT-4.3-01. Dùng cấu hình ảnh thẻ riêng trong HT-4.3-04.

**Dòng sự kiện chính**
1. Khách chọn In ảnh thẻ; chọn 2×3, 3×4, 4×6, 3,5×4,5 hoặc 5×5 cm và số ảnh 1–100.
2. App gửi ảnh tới POST api/file-in với kichThuocAnh và soLuongAnh.
3. API kiểm tra ảnh/tham số, ghép PDF và lưu số trang, kích thước, số ảnh ở FileIn.
4. App chọn dòng dịch vụ đúng kích thước; giữ một bản PDF, hướng đứng và không gia công tài liệu.
5. Báo giá tính số ảnh nhân đơn giá; trang PDF vẫn dùng cho lượng giấy và quy trình máy in.

**Dòng sự kiện phụ**
Bước 1 số ảnh ngoài 1–100 hoặc mã kích thước không tồn tại: sửa trước khi tải.
Bước 2 chỉ gửi một trong hai tham số ảnh hoặc dùng file Office/PDF: từ chối.
Bước 4 cố đổi số bản, hướng ngang, kích thước dịch vụ sai hoặc thêm gia công: không cho tạo đơn không khớp.
Muốn đổi số ảnh/kích thước sau khi ghép: tải ảnh và ghép lại từ bước 1.
Không có bước AI sửa mặt/xóa phông hoặc cam kết ảnh đạt chuẩn giấy tờ trong luồng hiện tại.

**Tiền điều kiện**
Khách có phiên, có JPG/PNG và dịch vụ ảnh thẻ phù hợp trong bảng giá chi nhánh.

**Hậu điều kiện**
Có PDF ảnh thẻ và số ảnh nhất quán; chưa in thật cho tới khi đơn vào quy trình máy.

**Căn cứ** FILE, PHOTO_RULE, SQL_PRICE, SQL_CREATE, FILE_UI

## HT-4.3-04 Cấu hình quy cách in

**Use case**
Cấu hình quy cách in

**Use case ID**
HT-4.3-04

**Tác nhân chính**
Khách hàng

**Tổng quan**
Chọn các dịch vụ đang bán theo bảng giá chi nhánh, không lấy mọi vật tư tồn kho làm quy cách in.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Sau một trong HT-4.3-01/02/03; kết quả dùng cho chi nhánh HT-4.3-05, báo giá HT-4.3-09 và tạo đơn HT-4.3-10.

**Dòng sự kiện chính**
1. App tải chi nhánh hoạt động và GET api/danh-muc/bang-gia với mã chi nhánh cụ thể.
2. Khách chọn giấy/dịch vụ, mặt in, màu, hướng đứng/ngang, số bản và ghi chú.
3. Khách chọn gia công phù hợp và in thường/hỏa tốc; App kiểm tra các cặp loại trừ.
4. App gửi báo giá sơ bộ với mã file, mã dịch vụ, số bản, hướng và gia công.
5. API/SQL kiểm lại dịch vụ, file, số trang và ràng buộc trước khi trả tiền tạm tính.
6. Khách tiếp tục chọn chi nhánh và cách nhận; quy cách được giữ lại.

**Dòng sự kiện phụ**
Bước 1 chưa có bảng giá: hiển thị lỗi/thử lại; không gọi bang-gia với mã 0.
Bước 3 bìa kiếng + lò xo hợp lệ; plastic có thể đi lò xo; lò xo + kim góc bị loại trừ.
Bước 3 kim góc tối đa 50 tờ mỗi bản; hai mặt tính ceil(số trang/2), không nhân số bản để kiểm giới hạn.
Dịch vụ gia công cùng nhóm vẫn loại trừ theo cấu hình, ngoại lệ rõ là bìa kiếng đi lò xo.
Ảnh thẻ dùng ràng buộc riêng HT-4.3-03.
Giấy A0 mới trong kho chưa có dịch vụ hoặc máy phù hợp không tự xuất hiện/in được.

**Tiền điều kiện**
Có FileIn sẵn sàng; đọc được danh sách chi nhánh và bảng giá có hiệu lực.

**Hậu điều kiện**
Lựa chọn hợp lệ được chuyển bước; chưa lưu đơn. Số trang báo giá do máy chủ đọc từ FileIn, không tin số trang client.

**Căn cứ** CONFIG_UI, CATALOG, PRINT_RULE, SQL_PRICE, QUOTE

## HT-4.3-05 Chọn chi nhánh và hình thức nhận

**Use case**
Chọn chi nhánh và hình thức nhận

**Use case ID**
HT-4.3-05

**Tác nhân chính**
Khách hàng

**Tổng quan**
Chọn cửa hàng đáp ứng cấu hình và nhận tại quầy hoặc giao tận nơi.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Bao gồm chọn nơi xử lý cho HT-4.3-10. Chỉ nhánh giao hàng mới dùng HT-4.3-07 và HT-4.3-08.

**Dòng sự kiện chính**
1. App POST api/don-hang/chi-nhanh-phu-hop với các mã dịch vụ in.
2. API đọc chi nhánh hoạt động, bảng giá và năng lực máy; trả cờ đáp ứng cùng lý do khi không đáp ứng.
3. App gợi ý chi nhánh phù hợp; khách chọn chi nhánh có thể in.
4. Khách chọn TAI_QUAY hoặc GIAO_HANG; khi giao, cung cấp điểm nhận và điện thoại.
5. App xóa báo giá cũ và gọi lại báo giá theo chi nhánh/hình thức mới.

**Dòng sự kiện phụ**
Bước 2 không chi nhánh nào đáp ứng: chưa cho tạo đơn, quay lại quy cách.
Bước 3 chi nhánh không có giá đủ hoặc thiếu máy A3/màu/hai mặt: vô hiệu hóa kèm lý do.
Bước 4 chưa có tọa độ xác thực: chưa tính được phí, yêu cầu lấy vị trí hoặc chuyển nhận tại quầy.
API tạo đơn kiểm tra lại tình trạng chi nhánh/năng lực; kết quả bộ lọc trước đó không bảo đảm máy còn sẵn sau này.
Bộ lọc năng lực này không cam kết giấy luôn đủ, driver khỏe hoặc connector đang online.

**Tiền điều kiện**
Có quy cách và mã dịch vụ đã chọn.

**Hậu điều kiện**
Có chi nhánh và cách nhận hợp lệ cho bước báo giá; chưa tạo đơn.

**Căn cứ** ORDER, CATALOG, BRANCH_UI

## HT-4.3-06 Áp dụng mã khuyến mãi

**Use case**
Áp dụng mã khuyến mãi

**Use case ID**
HT-4.3-06

**Tác nhân chính**
Khách hàng

**Tổng quan**
Kiểm tra và tính giảm trên báo giá/đơn của chi nhánh đã chọn.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Lựa chọn tùy ý trong HT-4.3-09; không phải mọi đơn bắt buộc nhập mã. Chốt điều kiện lại trong HT-4.3-10.

**Dòng sự kiện chính**
1. Khách nhập mã ở bước chọn chi nhánh.
2. App gọi lại POST api/bao-gia; API trim và chuyển mã thành chữ hoa.
3. sp_TinhTien kiểm chi nhánh, trạng thái, thời gian, lượt và mức tiền tối thiểu.
4. Nếu hợp lệ, tính phần trăm tiền in cộng gia công và giới hạn giảm tối đa; trả mức giảm, tổng và kết quả mã.
5. App hiển thị kết quả; khi khách tạo đơn, SQL kiểm lại và tăng DaDung cho mã được áp dụng.

**Dòng sự kiện phụ**
Bước 3 mã không tồn tại/hết hạn/hết lượt: trả lý do, giảm 0; vẫn được đặt đơn giá thường.
Bước 3 chưa đạt tối thiểu: báo lý do; khách sửa nhu cầu hoặc tiếp tục không giảm.
Đổi chi nhánh hoặc quy cách: xóa kết quả cũ, tính lại bước 2.
Chỉ xem báo giá chưa tăng lượt; không mô tả giữ chỗ mã trước khi đặt.
Hủy đơn không có bước trả lại lượt mã hiện hành; đây là giới hạn cần biết khi trình bày.

**Tiền điều kiện**
Có file, quy cách và chi nhánh; khách có mã muốn dùng.

**Hậu điều kiện**
Báo giá có trạng thái mã và mức giảm chính xác tại thời điểm tính; báo giá không tạo đơn.

**Căn cứ** SQL_PRICE, SQL_CREATE, QUOTE, BRANCH_UI

## HT-4.3-07 Lấy địa chỉ giao hàng từ vị trí hiện tại

**Use case**
Lấy địa chỉ giao hàng từ vị trí hiện tại

**Use case ID**
HT-4.3-07

**Tác nhân chính**
Khách hàng; Geoapify hỗ trợ

**Tổng quan**
Xin quyền vị trí theo thao tác khách, chuyển GPS thành địa chỉ chính tự điền và cho bổ sung chi tiết.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Dùng khi GIAO_HANG trong HT-4.3-05. Không có nút tìm địa chỉ thủ công riêng trên App hiện tại.

**Dòng sự kiện chính**
1. Khách bấm Dùng vị trí hiện tại; App xóa điểm/báo giá cũ để tránh đặt sai điểm.
2. App xin quyền nếu cần, lấy GPS chính xác; kiểm tra số hữu hạn và sai số không quá 150m.
3. App POST api/giao-hang/dia-chi với tọa độ; API gọi reverse geocoding và cấp mã vị trí gắn khách.
4. App điền địa chỉ chính từ phản hồi, giữ tọa độ/mã; khách kiểm tra và nhập số nhà/tầng/phòng, ghi chú riêng.
5. Địa chỉ cùng mã vị trí được dùng để báo giá; chi tiết không thay đổi điểm tính tuyến.

**Dòng sự kiện phụ**
Bước 2 GPS tắt/từ chối quyền/hết thời gian hoặc sai số quá lớn: báo cách bật và thử lại; không dùng tọa độ cũ.
Bước 3 dịch vụ chưa cấu hình/lỗi/hết hạn mức: báo không lấy được địa chỉ; khách thử lại hoặc nhận tại quầy.
Bước 4 địa chỉ/tọa độ không hợp lệ: chưa chấp nhận làm điểm giao.
Mã vị trí hết hiệu lực hoặc địa chỉ chính bị thay không khớp mã: API báo giá từ chối; phải lấy vị trí lại.
Địa chỉ mặc định có sẵn chỉ là chuỗi, không thay thế được mã vị trí xác thực để giao.

**Tiền điều kiện**
Khách chọn giao tận nơi, thiết bị có dịch vụ vị trí; API đã cấu hình Geoapify.

**Hậu điều kiện**
Có địa chỉ chính và tọa độ xác thực với mã vị trí tạm 30 phút; lỗi thì chưa có điểm giao hợp lệ.

**Căn cứ** LOCATION_UI, GPS, SHIP, GEO

## HT-4.3-08 Ước tính phí giao hàng

**Use case**
Ước tính phí giao hàng

**Use case ID**
HT-4.3-08

**Tác nhân chính**
Khách hàng; Geoapify hỗ trợ tuyến đường

**Tổng quan**
Tính phí ship nội bộ từ quãng đường tuyến scooter, không gọi API báo cước hay tạo đơn của hãng ngoài.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Nhánh có điều kiện trong HT-4.3-09 khi GIAO_HANG; không chạy khi khách nhận tại quầy.

**Dòng sự kiện chính**
1. App gửi báo giá gồm chi nhánh, địa chỉ, tọa độ và mã vị trí.
2. API kiểm tra mã điểm nhận; GeoapifyService yêu cầu tuyến scooter từ chi nhánh tới điểm giao.
3. Dịch vụ đọc khoảng cách và thời gian, kiểm tra bán kính phục vụ.
4. QuyTacGiaoHang tính phí từ cấu hình cửa hàng và làm tròn.
5. API trả khoảng cách/phút di chuyển/phí và giữ snapshot báo phí 15 phút với mã báo giá giao.
6. Khi đặt đơn, API đọc snapshot gắn đúng khách, chi nhánh, địa chỉ, tọa độ; không nhận số tiền ship tùy ý từ client.

**Dòng sự kiện phụ**
Bước 2 không có tuyến hoặc dịch vụ lỗi: báo lỗi, không tính khoảng cách đường chim bay thay thế âm thầm.
Bước 3 vượt bán kính: từ chối giao, cho chọn lại điểm hoặc nhận tại quầy.
Mặc định code: 15.000đ trong 2km, thêm 4.000đ/km vượt, làm tròn lên 1.000đ; giá thực tế lấy cấu hình, không gọi đây là bảng giá hãng vận chuyển.
Bước 6 snapshot hết hạn hoặc dữ liệu đổi: yêu cầu báo giá lại.
Không có API đặt shipper ngoài; shipper nội bộ thực hiện giao sau in.

**Tiền điều kiện**
Có chi nhánh tọa độ/bán kính hợp lệ và mã vị trí giao thuộc khách.

**Hậu điều kiện**
Có phí và vé báo giá giao tạm nếu hợp lệ; chưa tạo đơn hoặc thu tiền.

**Căn cứ** GEO, SHIP_RULE, QUOTE, ORDER

## HT-4.3-09 Xem báo giá và giờ nhận dự kiến

**Use case**
Xem báo giá và giờ nhận dự kiến

**Use case ID**
HT-4.3-09

**Tác nhân chính**
Khách hàng

**Tổng quan**
Tính tiền và ước tính thời gian trước khi đặt; kết quả là dự kiến theo trạng thái hiện tại.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Dùng file/quy cách/chi nhánh; mã giảm tùy chọn HT-4.3-06 và phí giao có điều kiện HT-4.3-08.

**Dòng sự kiện chính**
1. Khách chọn hoặc thay quy cách, chi nhánh, cách nhận, tốc độ và mã giảm.
2. App chờ thay đổi ngắn rồi POST api/bao-gia; chỉ nhận phản hồi của lựa chọn mới nhất.
3. API đọc số trang thật, lập danh sách in/gia công, tính tuyến giao nếu cần.
4. sp_BaoGia dùng sp_TinhTien trả tiền in, gia công, phụ phí, ship, giảm, tổng và NgayHenTra.
5. App hiển thị số tiền, kết quả mã, thời điểm dự kiến và thời gian di chuyển khi có.
6. Khách kiểm tra và chuyển HT-4.3-10 nếu đồng ý.

**Dòng sự kiện phụ**
Bước 3 file không thuộc khách/chưa sẵn sàng hoặc giá không có: từ chối, yêu cầu chọn lại.
Phụ phí hỏa tốc hiện 10.000đ/đơn; ước tính dùng tốc độ máy nhanh nhất hoạt động, số trang hàng đợi; in gấp bỏ phần trang chờ trong công thức.
Giờ ước tính không phải cam kết SLA và chưa mô hình đầy đủ thời gian gia công, giờ đóng cửa hay lỗi máy; không trình bày như lịch hẹn chắc chắn.
Đổi địa chỉ/chi nhánh làm vô hiệu báo giá cũ; phải đợi phản hồi mới.
Báo giá không lưu DonHang; lúc đặt tính lại nên không bảo đảm mọi mức giá đã được khóa.

**Tiền điều kiện**
File của khách sẵn sàng, dịch vụ có giá tại chi nhánh; giao hàng có điểm nhận hợp lệ.

**Hậu điều kiện**
Có bảng tiền và thời gian dự kiến hiện hành; lỗi thì nút đặt không dựa vào báo giá cũ.

**Căn cứ** QUOTE, SQL_PRICE, SQL_QUOTE, SQL_ETA, BRANCH_UI

## HT-4.3-10 Xác nhận đặt in trên App

**Use case**
Xác nhận đặt in trên App

**Use case ID**
HT-4.3-10

**Tác nhân chính**
Khách hàng

**Tổng quan**
Lưu đơn thật từ dữ liệu đã chọn và chuyển sang thanh toán trước bằng VietQR.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Kết thúc đặt in, mở thanh toán 4.4; tạo đơn không đồng nghĩa đã trả tiền hoặc đã in.

**Dòng sự kiện chính**
1. Khách kiểm tra tổng tiền và bấm Đặt in; App khóa bấm lặp trong khi gửi.
2. App POST api/don-hang với mã file/dịch vụ, số bản, hướng, gia công, chi nhánh, cách nhận và ghi chú.
3. API lấy khách từ JWT, ép phương thức VIETQR; kiểm chi nhánh, file thuộc khách, trang thật và năng lực máy.
4. Nếu giao, API xác thực snapshot phí theo đúng dữ liệu; SQL tính lại tiền và điều kiện dịch vụ/khuyến mãi.
5. sp_TaoDonHang lưu đơn, chi tiết in/gia công và thanh toán chưa trả trong giao dịch; lưu hướng giấy và giá của đơn.
6. API trả mã DH, tổng và yêu cầu trả trong 15 phút; thông báo đơn mới cho chi nhánh.
7. App thay màn sang Thanh toán để lấy QR; chưa gửi lệnh in nếu chưa nhận tiền.

**Dòng sự kiện phụ**
Bước 3 giả chủ file, chi nhánh dừng hoạt động hoặc cấu hình không đáp ứng: từ chối, không tạo đơn.
Bước 4 giá/khuyến mãi đã đổi: dùng kiểm tra và giá lúc tạo; báo giá sơ bộ không phải hợp đồng khóa mọi giá.
Bước 4 snapshot giao hết hạn/đổi điểm: yêu cầu tính lại trước khi đặt.
Bước 5 SQL thất bại: không báo tạo thành công; giao dịch đơn được rollback.
Mất phản hồi sau khi gửi có thể chưa biết đơn đã lưu; kiểm tra danh sách trước khi đặt lại. API này chưa thể hiện khóa idempotency cho mọi lần retry.
Đơn App không có lựa chọn COD, dù payload client cố gửi phương thức khác.

**Tiền điều kiện**
Có phiên CUSTOMER, FileIn sẵn sàng, lựa chọn hợp lệ và báo giá mới; giao hàng có snapshot phí.

**Hậu điều kiện**
Thành công có mã đơn CHO_XAC_NHAN và thanh toán CHUA_THANH_TOAN; chưa nhận tiền hoặc in. Lỗi không báo thành công.

**Căn cứ** ORDER, SQL_CREATE, BRANCH_UI, PAYMENT

## Đối chiếu nhãn trên hình gốc

| Nhãn | Đặc tả | Kết luận | Cách hiểu và điều chỉnh |
| --- | --- | --- | --- |
| NV: Đặt đơn in trực tuyến | NV-3.2-01 | Đúng | Kịch bản tổng hợp; các bước kỹ thuật được tách ở hình hệ thống. |
| NV: Tải tài liệu và lựa chọn quy cách in | NV-3.2-02 | Đúng | Có cả mẫu đơn và ảnh thẻ thay thế tải tài liệu thường. |
| NV: Chuyển khoản trước | NV-3.2-03 | Đúng | Đơn App trả QR trước cả tiền ship; không COD giao hàng. |
| NV: Tiếp nhận đơn in | NV-3.2-04 | Chỉnh diễn giải | Có xếp in tự động; không mọi đơn đều cần nhân viên bấm tiếp nhận. |
| NV: Áp dụng khuyến mại | NV-3.2-05 | Tùy chọn | Nhánh bổ sung khi khách có mã; sai mã vẫn đặt giá thường. |
| NV: Giao hàng tận nơi | NV-3.2-06 | Tùy chọn | Liên kết sang 3.4/4.10; không thu tiền lúc giao. |
| HT: Tải tài liệu in | HT-4.3-01 | Đúng | Tạo FileIn trước, chưa tạo đơn. |
| HT: In mẫu đơn trống | HT-4.3-02 | Đúng | Thư viện có phiên bản, xem trước và chọn bản PDF. |
| HT: In ảnh thẻ theo kích thước | HT-4.3-03 | Đúng | Ghép PDF và tính theo số ảnh, không số trang. |
| HT: Cấu hình quy cách | HT-4.3-04 | Đúng | Theo dịch vụ/giá bán, có kiểm gia công và hướng giấy. |
| HT: Chọn chi nhánh và hình thức nhận | HT-4.3-05 | Đúng | Năng lực và giá được kiểm, không bảo đảm driver/giấy luôn sẵn. |
| HT: Áp dụng mã khuyến mãi | HT-4.3-06 | Đúng | Kiểm trong báo giá và tạo đơn, không có endpoint áp mã độc lập. |
| HT: Lấy địa chỉ giao hàng | HT-4.3-07 | Đúng theo UI hiện tại | GPS chủ động, reverse địa chỉ tự điền; không nút tìm địa chỉ riêng. |
| HT: Ước tính phí giao hàng | HT-4.3-08 | Chỉnh vai trò dịch vụ | Geoapify trả tuyến, cửa hàng tự tính phí; chỉ chạy nhánh giao hàng. |
| HT: Xem báo giá và giờ nhận dự kiến | HT-4.3-09 | Đúng với giới hạn | Thời điểm dự kiến, không SLA; tính lại lúc đặt. |
| HT: Xác nhận đặt in | HT-4.3-10 | Đúng | Lưu đơn, bắt đầu thanh toán; chưa bắt đầu in chỉ vì tạo đơn. |

## Căn cứ mã nguồn

- **AUTO_PRINT** [MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs:17) — Xếp máy sau khi trả tiền; giữ chờ nếu chưa đủ điều kiện.
- **BRANCH_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/chon_chi_nhanh_screen.dart:19](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/chon_chi_nhanh_screen.dart:19) — Chọn chi nhánh/nhận, tính lại giá và tạo đơn.
- **CATALOG** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DanhMucController.cs:13](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DanhMucController.cs:13) — Chi nhánh hoạt động và dịch vụ có giá hiển thị.
- **CONFIG_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/cau_hinh_in_screen.dart:27](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/cau_hinh_in_screen.dart:27) — Bảng giá theo chi nhánh, giấy/dịch vụ và các quy cách.
- **FILE** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/FileInController.cs:28](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/FileInController.cs:28) — Quyền tải, chuyển PDF/đếm trang, ảnh thẻ và chọn mẫu có phiên bản.
- **FILE_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/chon_file_screen.dart:25](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/chon_file_screen.dart:25) — Ba nguồn file, chọn mẫu/xem trước, mỗi lượt một file.
- **GEO** [MaNguon/PhotocopyChain/PhotocopyApi/GiaoHang/GeoapifyService.cs:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/GiaoHang/GeoapifyService.cs:11) — Reverse GPS, routing scooter, vé vị trí và snapshot phí giao.
- **GPS** [MaNguon/photocopy_mobile/lib/dich_vu_vi_tri.dart:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/dich_vu_vi_tri.dart:11) — Xin quyền và đọc GPS khi người dùng chủ động yêu cầu.
- **LOCATION_UI** [MaNguon/photocopy_mobile/lib/widgets/dia_chi_giao.dart:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/widgets/dia_chi_giao.dart:11) — Tự điền địa chỉ GPS, sai số, chi tiết và vô hiệu điểm cũ.
- **ORDER** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32) — Đặt App, lọc chi nhánh, chi tiết, tiếp nhận, hủy và đánh giá.
- **PAYMENT** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30) — QR, chữ ký webhook, đối soát, tiền đến muộn và điều hướng.
- **PHOTO_RULE** [MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyCachAnhThe.cs:5](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyCachAnhThe.cs:5) — Năm kích thước ảnh thẻ.
- **PRINT_RULE** [MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyTacIn.cs:3](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/QuyCachIn/QuyTacIn.cs:3) — Gia công tương thích và số tờ kim góc.
- **QUOTE** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/BaoGiaController.cs:33](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/BaoGiaController.cs:33) — Báo giá từ file thật, lấy tuyến và giữ phí giao.
- **SHIP** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/GiaoHangController.cs:18](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/GiaoHangController.cs:18) — Điểm nhận, phân công/nhận và kết quả giao không COD.
- **SHIP_RULE** [MaNguon/PhotocopyChain/PhotocopyApi/GiaoHang/QuyTacGiaoHang.cs:3](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/GiaoHang/QuyTacGiaoHang.cs:3) — Phí nội bộ, bán kính, điều kiện nhận và ba lượt giao.
- **SQL_ACCEPT** [Database/04_StoredProcedure.sql:351](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:351) — Ca, nhân viên, trạng thái và tiền khi tiếp nhận thủ công.
- **SQL_CREATE** [Database/04_StoredProcedure.sql:226](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:226) — Giao dịch đơn, chi tiết, hướng giấy, giá, thanh toán và lượt mã.
- **SQL_ETA** [Database/03_View_Function.sql:41](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/03_View_Function.sql:41) — Công thức ước tính dùng tốc độ/hàng đợi.
- **SQL_PAY** [Database/04_StoredProcedure.sql:320](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:320) — Ghi nhận thanh toán chuyển khoản và thông báo.
- **SQL_PRICE** [Database/04_StoredProcedure.sql:71](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:71) — Tiền in/gia công/ảnh, phụ phí, giảm giá và ràng buộc.
- **SQL_QUOTE** [Database/04_StoredProcedure.sql:203](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:203) — Báo giá, tổng tiền và thời gian dự kiến.
- **TEMPLATE** [MaNguon/PhotocopyChain/PhotocopyApi/DichVu/ThuVienMauDon.cs:9](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DichVu/ThuVienMauDon.cs:9) — Thư viện động, phiên bản và tài sản PDF/xem trước.
- **TIMEOUT** [MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:12](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:12) — Chạy hủy quá hạn khoảng mỗi phút, dọn file là tác vụ riêng.

# Task 4 Thanh toán VietQR

Phạm vi: HeThong/ThanhToanVietQR.png. Đối chiếu source ngày 26/10/2026. Gồm 0 đặc tả nghiệp vụ và 6 đặc tả hệ thống.

- Ranh giới nên là Hệ thống PhotocopyChain hoặc phân hệ thanh toán của PhotocopyChain. payOS nằm ngoài ranh giới; ngân hàng thực hiện chuyển tiền ở nghiệp vụ.
- Bộ hẹn giờ là cơ chế bên trong ứng dụng. Nếu biểu diễn actor đồng hồ để chỉ kích hoạt thời gian thì chú thích đây là mô hình hóa khái niệm, không phải một dịch vụ ngoài thực sự.
- Không dùng include/extend chỉ để nối trước–sau. Xếp in sau webhook là phản ứng nghiệp vụ có điều kiện; sơ đồ Activity/Sequence phù hợp hơn để thể hiện giao dịch tiền và ngoại lệ.
- Return/cancel URL chỉ điều hướng/hiển thị. Web có thể quay về hàng đợi; App hiển thị kết quả khi API xác nhận, không cam kết một deep link tự mở App từ mọi trình duyệt.

## HT-4.4-01 Hiển thị và sử dụng mã VietQR thanh toán

**Use case**
Hiển thị và sử dụng mã VietQR thanh toán

**Use case ID**
HT-4.4-01

**Tác nhân chính**
Khách hàng

**Tổng quan**
Xem QR đúng đơn trong App và chuyển khoản bằng ứng dụng ngân hàng; phần mềm theo dõi kết quả xác thực.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Dùng HT-4.4-02 để tạo/lấy dữ liệu QR. Nhận tiền là HT-4.4-03 hoặc HT-4.4-05; không do thao tác quét tự xác nhận.

**Dòng sự kiện chính**
1. App mở Thanh toán, GET api/thanh-toan/{id} để lấy trạng thái và hạn.
2. App POST api/thanh-toan/{id}/payos; kiểm mã đơn, số tiền và dữ liệu QR trả về.
3. App vẽ QR trong màn hình, hiển thị số tiền, ngân hàng/nội dung và đếm ngược; khách dùng ngân hàng quét/chuyển.
4. App nghe sự kiện SignalR và hỏi lại API khoảng 5 giây khi màn đang hoạt động; khi trở lại App hỏi ngay.
5. Chỉ khi API cho biết đã trả, App hiển thị kết quả và cho xem tiến độ đơn.

**Dòng sự kiện phụ**
Bước 2 dữ liệu QR không khớp hoặc không hợp lệ: không vẽ QR đó, báo lỗi và thử lại.
Link cũ không còn cache QR: có thể dùng checkout payOS hợp lệ; không tự dựng số tiền/nội dung khác.
Khách chọn mở checkout ngoài: chỉ chấp nhận HTTPS pay.payos.vn; quay lại App phải đối soát.
Đóng/chuyển nền màn hình: không tiếp tục polling như lúc hoạt động; resume kiểm tra lại.
Hết hạn/đã hủy: không cho tạo hoặc tiếp tục trả trên UI; nếu tiền vẫn tới sau hủy thì xử lý 4.11.
Thông báo trên trang quay về hoặc ảnh chụp ngân hàng không phải căn cứ API tự đổi trạng thái.

**Tiền điều kiện**
Đơn VietQR thuộc khách, chưa hủy, chưa trả và còn hạn thanh toán.

**Hậu điều kiện**
Giao diện phản ánh trạng thái trả về máy chủ; chưa xác thực tiền thì vẫn chưa trả. Quét QR đơn thuần không đổi CSDL.

**Căn cứ** PAYMENT, PAY_UI

## HT-4.4-02 Tạo hoặc lấy lại liên kết thanh toán payOS

**Use case**
Tạo hoặc lấy lại liên kết thanh toán payOS

**Use case ID**
HT-4.4-02

**Tác nhân chính**
Khách hàng hoặc nhân sự có quyền với đơn; payOS hỗ trợ

**Tổng quan**
Cấp QR/checkout cho đúng orderCode, tổng tiền và thời hạn của đơn.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Được gọi từ HT-4.4-01; không tạo lại DonHang và không tự xác nhận nhận tiền.

**Dòng sự kiện chính**
1. Giao diện POST api/thanh-toan/{id}/payos.
2. API kiểm quyền chủ đơn/phạm vi và điều kiện thanh toán.
3. API hỏi payOS bằng mã đơn; nếu có link còn dùng và tiền khớp thì lấy lại link và QR cache nếu còn.
4. Nếu chưa có link, API tạo orderCode từ mã đơn, amount từ CSDL, mô tả chuẩn, return/cancel URL theo kênh và ExpiredAt theo hạn.
5. API trả QR cùng metadata, checkout URL và hạn; cố lưu cache QR cho lần mở lại.

**Dòng sự kiện phụ**
Bước 2 sai quyền, COD, đã trả, đã hủy hoặc hết hạn: từ chối cấp link.
Bước 3 link có amount khác CSDL hoặc trạng thái cancelled/expired/failed: báo lỗi, không thay bằng QR tùy ý.
Bước 4 thiếu địa chỉ HTTPS công khai hoặc WebAdmin URL hợp lệ: báo cấu hình chưa sẵn sàng.
payOS lỗi: báo chưa kết nối được, cho thử lại.
Ghi cache QR lỗi: log cảnh báo, vẫn trả QR vừa nhận; không giả báo thanh toán thành công.

**Tiền điều kiện**
Phiên hợp lệ, đơn trong phạm vi, phương thức VietQR, chưa trả, chưa hủy và còn hạn; có cấu hình payOS/HTTPS công khai.

**Hậu điều kiện**
Có dữ liệu QR/link đúng đơn nếu thành công; tạo link không làm ThanhToan thành DA_THANH_TOAN.

**Căn cứ** PAYMENT, PAY_RULE, QR_STORE

## HT-4.4-03 Nhận webhook xác nhận thanh toán

**Use case**
Nhận webhook xác nhận thanh toán

**Use case ID**
HT-4.4-03

**Tác nhân chính**
Cổng payOS

**Tổng quan**
Tiếp nhận thông báo có chữ ký và chỉ ghi nhận khoản tiền đáp ứng đủ đối chiếu.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Sau ghi nhận có thể gọi HT-4.4-04 cho đơn chưa hủy; đơn đã hủy chuyển xử lý 4.11. Không coi trình duyệt quay về là webhook.

**Dòng sự kiện chính**
1. payOS POST api/thanh-toan/webhook kèm dữ liệu và chữ ký.
2. API dùng SDK VerifyAsync; kiểm sự kiện thành công và mã 00.
3. API tìm đơn, kiểm phương thức VietQR, currency VND, mô tả đúng mã đơn, reference và số tiền khớp.
4. API khóa đơn và thanh toán, gọi sp_XacNhanChuyenKhoan khi chưa ghi nhận trả; không ghi lùi khoản đã hoàn.
5. Commit tiền trước; nếu đơn chưa hủy thì thử xếp in, nếu đã hủy giữ nguyên để hoàn.
6. API gửi thông tin cập nhật tới chủ đơn/chi nhánh và phản hồi webhook.

**Dòng sự kiện phụ**
Bước 2 chữ ký sai: trả lỗi, không cập nhật.
Mẫu thử, sự kiện thất bại, orderCode không hợp lệ hoặc đơn không tồn tại: nhận phản hồi nhưng không sửa đơn.
Bước 3 sai tiền/nội dung/tiền tệ/thiếu reference: không xác nhận đã trả; không in.
Webhook lặp: không thu tiền hoặc tạo thanh toán thứ hai; có thể thử lại xếp in cho đơn đã trả còn chờ.
Đơn DA_HUY: nhận tiền hợp lệ vẫn ghi để hoàn, tuyệt đối không phục hồi đơn.
Đã DA_HOAN_TIEN: không chuyển trở lại DA_THANH_TOAN.

**Tiền điều kiện**
API webhook có thể được payOS truy cập; khóa xác minh được cấu hình. Endpoint không dùng JWT người dùng nhưng bắt buộc kiểm chữ ký.

**Hậu điều kiện**
Khoản hợp lệ được lưu một lần theo trạng thái hiện hành. Webhook không hợp lệ không tạo quyền in; lỗi xếp máy không làm mất khoản tiền đã commit.

**Căn cứ** PAYMENT, SQL_PAY, AUTO_PRINT

## HT-4.4-04 Tự động tiếp nhận và xếp đơn đã thanh toán vào quy trình in

**Use case**
Tự động tiếp nhận và xếp đơn đã thanh toán vào quy trình in

**Use case ID**
HT-4.4-04

**Tác nhân chính**
Hệ thống; kích hoạt bởi kết quả nhận tiền hoặc bộ thử xếp lại

**Tổng quan**
Hành vi nội bộ chuyển đơn VietQR đã trả sang chờ in hoặc đang in tùy điều kiện; không phải thao tác độc lập của khách.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Được kích hoạt sau HT-4.4-03/05 cho đơn chưa hủy; bàn giao cho use case máy in/connector 4.7–4.8 của Tài.

**Dòng sự kiện chính**
1. TuDongInService khóa đơn, đọc trạng thái và điều kiện nhận tiền.
2. Nếu CHO_XAC_NHAN thì tiếp nhận thành DA_XAC_NHAN.
3. Kiểm tra mọi PDF còn sẵn trên máy chủ và máy phù hợp đang có heartbeat kết nối.
4. Chọn máy cùng chi nhánh, đủ khổ/màu/hai mặt và ràng buộc connector; ưu tiên máy đã gán, rồi số đơn đang in ít hơn, rồi mã máy.
5. Gọi sp_PhanCongMayIn và sp_CapNhatTrangThaiIn sang DANG_IN trong giao dịch.
6. Gửi cập nhật; connector tiếp tục lấy và thực thi lệnh theo cơ chế của máy in.

**Dòng sự kiện phụ**
Bước 1 chưa trả hoặc đã hủy: không xếp in.
Đơn đã in/đang in/đã hoàn tất: không tạo thêm lượt in chỉ vì webhook lặp.
Bước 3 file mất: giữ DA_XAC_NHAN, ghi cảnh báo khi còn chỗ và yêu cầu xử lý file.
Bước 3–4 chưa có máy phù hợp kết nối: giữ chờ in; hệ thống có thể thử lại sau.
Bước 5 lỗi SQL/thiếu kho: rollback phần xếp in, giữ khoản tiền đã nhận; chưa coi đã bắt đầu in thành công.
DANG_IN là trạng thái quy trình, không chứng minh giấy đã ra; hoàn tất vật lý/queue thuộc connector.

**Tiền điều kiện**
ThanhToan DA_THANH_TOAN, phương thức VIETQR và đơn còn ở giai đoạn được phép xếp in.

**Hậu điều kiện**
Đủ điều kiện thì đơn vào DANG_IN có máy; chưa đủ thì ở DA_XAC_NHAN và vẫn đã thanh toán. Không tự phục hồi đơn hủy.

**Căn cứ** AUTO_PRINT, ORDER, PRINT_JOB

## HT-4.4-05 Đối soát trạng thái thanh toán với payOS

**Use case**
Đối soát trạng thái thanh toán với payOS

**Use case ID**
HT-4.4-05

**Tác nhân chính**
Khách hoặc nhân sự đúng phạm vi; payOS hỗ trợ

**Tổng quan**
Khôi phục kết quả khi webhook bị lỡ bằng đọc trực tiếp payOS; App cũng gọi khi theo dõi thanh toán.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Có thể chạy từ GET thanh toán của HT-4.4-01 hoặc nút Kiểm tra thanh toán trên Web. Dùng chung ghi nhận và xếp in HT-4.4-03/04.

**Dòng sự kiện chính**
1. Giao diện gọi GET api/thanh-toan/{id}.
2. API kiểm chủ đơn/phạm vi và đọc ThanhToan.
3. Nếu VietQR chưa trả, API GET payment request từ payOS theo mã đơn.
4. Chỉ khi status Paid, orderCode, Amount và AmountPaid đều khớp thì lấy reference để gọi ghi nhận tiền chung.
5. API đọc lại trạng thái sau ghi nhận và trả cho giao diện.
6. Giao diện hiện kết quả mới, không chỉ đổi nhãn cục bộ.

**Dòng sự kiện phụ**
Bước 2 không đúng quyền: không lộ trạng thái đơn khác.
Bước 3 chưa có link/chưa Paid hoặc tiền không khớp: giữ chưa thanh toán.
payOS báo lỗi truy vấn: ghi cảnh báo, chưa xác nhận tiền; người dùng có thể thử lại.
Đơn đã hủy mà tiền đúng: giữ hủy để hoàn, không in.
Đơn đã hoàn tiền: không ghi lùi sang đã trả.
Không dùng status=PAID trong URL hoặc dữ liệu client làm chứng cứ đối soát.

**Tiền điều kiện**
Người gọi có quyền với đơn; đơn VietQR hiện chưa được ghi nhận thanh toán.

**Hậu điều kiện**
Trạng thái được đồng bộ khi payOS cung cấp bằng chứng đúng; chưa có bằng chứng thì giữ nguyên. Đối soát không thu thêm tiền khách.

**Căn cứ** PAYMENT, PAY_UI

## HT-4.4-06 Tự động hủy đơn VietQR quá hạn chưa thanh toán

**Use case**
Tự động hủy đơn VietQR quá hạn chưa thanh toán

**Use case ID**
HT-4.4-06

**Tác nhân chính**
Bộ hẹn giờ nội bộ DonQuaHanJob

**Tổng quan**
Thực thi chính sách kết thúc đơn đang chờ sau hạn trả; không phải extension của hành vi truy vấn payOS.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Cùng hành vi đã đặc tả NV-3.5-03; tham chiếu hủy và tiền đến muộn trong Task 1. Không bắt buộc gọi đối soát mỗi lần job chạy.

**Dòng sự kiện chính**
1. Job chạy lượt kiểm tra định kỳ khoảng mỗi phút.
2. Job gọi sp_HuyDonQuaHan.
3. SQL chọn đúng đơn CHO_XAC_NHAN, VIETQR, CHUA_THANH_TOAN có NgayDat cộng 15 phút nhỏ hơn hiện tại.
4. SQL đổi DA_HUY và lưu ghi chú Quá 15 phút chưa thanh toán.
5. Lượt tiếp theo tiếp tục kiểm tra các đơn còn phù hợp; khách đọc lại sẽ thấy đã hủy.

**Dòng sự kiện phụ**
Đơn chưa quá hạn/đã trả/đã tiếp nhận: không bị hủy bởi điều kiện này.
API tắt hoặc CSDL lỗi: không bảo đảm hủy đúng giây; log và thử lượt sau khi hoạt động.
Tiền về sau khi hủy: xử lý như HT-4.11-05; không đưa lại vào in.
Không giả có actor quản lý bấm hủy quá hạn hoặc có timer của điện thoại điều khiển CSDL.
Dọn file của job là việc bảo trì riêng, không dùng làm điều kiện công nhận thanh toán.

**Tiền điều kiện**
API đang chạy background job và truy cập được CSDL.

**Hậu điều kiện**
Chỉ đơn đúng điều kiện được kết thúc; không tự hoàn khoản tiền chưa nhận và không xóa lịch sử đơn.

**Căn cứ** TIMEOUT, SQL_TIMEOUT

## Đối chiếu nhãn trên hình gốc

| Nhãn | Đặc tả | Kết luận | Cách hiểu và điều chỉnh |
| --- | --- | --- | --- |
| Quét mã VietQR thanh toán | HT-4.4-01 | Chỉnh tên cho sát ranh giới | App hiển thị QR, ngân hàng quét/chuyển; App không tự quét để chuyển tiền. |
| Tạo liên kết thanh toán PayOS | HT-4.4-02 | Đúng | Tạo hoặc lấy lại link/QR của đúng đơn. |
| Nhận Webhook xác nhận thanh toán | HT-4.4-03 | Đúng | Bắt buộc chữ ký và đối chiếu dữ liệu. |
| Tự động xếp đơn vào hàng đợi | HT-4.4-04 | Chỉnh include | Là xử lý có điều kiện sau nhận tiền; không luôn thành công/không chạy lại cho đơn hủy. |
| Đối soát trạng thái với PayOS | HT-4.4-05 | Bổ sung actor | Cả khách App và nhân sự đúng quyền có thể kích hoạt; không riêng nhân viên. |
| Tự động hủy đơn quá hạn sau 15p | HT-4.4-06 | Chỉnh quan hệ | Job độc lập; bỏ extend hướng vào đối soát nếu muốn mô tả đúng code. |

## Căn cứ mã nguồn

- **AUTO_PRINT** [MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/TuDongInService.cs:17) — Xếp máy sau khi trả tiền; giữ chờ nếu chưa đủ điều kiện.
- **ORDER** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32) — Đặt App, lọc chi nhánh, chi tiết, tiếp nhận, hủy và đánh giá.
- **PAYMENT** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30) — QR, chữ ký webhook, đối soát, tiền đến muộn và điều hướng.
- **PAY_RULE** [MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/QuyTacPayOS.cs:8](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/QuyTacPayOS.cs:8) — Chuẩn hóa URL, mô tả đơn và so tiền.
- **PAY_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart:24](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart:24) — Vẽ QR, polling 5 giây khi hoạt động, đối soát khi resume.
- **PRINT_JOB** [MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/XepLaiDonChoInJob.cs:7](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/MayInAo/XepLaiDonChoInJob.cs:7) — Bộ thử xếp lại đơn đã thanh toán đang chờ.
- **QR_STORE** [MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/DuLieuQrPayOSStore.cs:8](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/ThanhToan/DuLieuQrPayOSStore.cs:8) — Cache dữ liệu QR theo đơn/link/tiền.
- **SQL_PAY** [Database/04_StoredProcedure.sql:320](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:320) — Ghi nhận thanh toán chuyển khoản và thông báo.
- **SQL_TIMEOUT** [Database/04_StoredProcedure.sql:339](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:339) — Điều kiện VietQR chưa trả, chờ tiếp nhận và quá 15 phút.
- **TIMEOUT** [MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:12](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DonQuaHanJob.cs:12) — Chạy hủy quá hạn khoảng mỗi phút, dọn file là tác vụ riêng.

# Task 5 Theo dõi đơn cá nhân

Phạm vi: HeThong/TheoDoiDon.png. Đối chiếu source ngày 26/10/2026. Gồm 0 đặc tả nghiệp vụ và 7 đặc tả hệ thống.

- Những thao tác thanh toán/hủy trùng giữa các hình dùng một quy tắc nghiệp vụ và dẫn chiếu ID, không tạo điều kiện khác nhau cho cùng chức năng.
- Xem tiến độ hiện dựa trạng thái và mốc API dựng, không giả mọi mốc có thời điểm riêng hoặc màn khách luôn hiển thị phần trăm giấy đã in của driver.
- Hủy, đánh giá và đánh dấu đọc là hành động tùy chọn có điều kiện; các đường extend chỉ hợp lý khi ghi rõ điểm mở rộng tương ứng trong mô tả.

## HT-4.5-01 Tra cứu danh sách đơn cá nhân

**Use case**
Tra cứu danh sách đơn cá nhân

**Use case ID**
HT-4.5-01

**Tác nhân chính**
Khách hàng

**Tổng quan**
Xem và lọc các đơn của chính khách trên tab Đơn hàng.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Mở sang chi tiết HT-4.5-02 hoặc thanh toán HT-4.5-03 theo trạng thái. Không phải màn quản trị tra đơn toàn chuỗi.

**Dòng sự kiện chính**
1. Khách mở tab Đơn hàng.
2. App GET api/don-hang/cua-toi; API lấy mã khách từ JWT và truy vấn đơn mới nhất trước.
3. App hiển thị mã, chi nhánh, trạng thái, số tiền và thông tin tóm tắt.
4. Khách lọc Tất cả, Đang xử lý, Đang giao, Hoàn tất hoặc Đã hủy; App lọc danh sách đã nhận.
5. Khách chọn đơn; đơn VietQR còn nợ chưa hủy mở thanh toán, các đơn khác mở tiến độ.
6. Khi có sự kiện hoặc quay về danh sách, App tải lại trạng thái.

**Dòng sự kiện phụ**
Bước 2 không có đơn: hiển thị trạng thái trống, cho sang đặt in.
Lỗi mạng/token hết hạn: báo lỗi/thử lại hoặc đăng nhập, không hiện đơn người khác.
GIAO_THAT_BAI thuộc nhóm Đang giao; DA_IN vẫn thuộc Đang xử lý cho đến bàn giao.
Các nhóm là nhóm UI, không phải trạng thái mới trong CSDL.
App không có API tìm toàn hệ thống bằng mã tùy ý cho khách; chỉ trả đơn của chính khách.

**Tiền điều kiện**
Có phiên CUSTOMER hợp lệ.

**Hậu điều kiện**
Danh sách/lọc được hiển thị; thao tác đọc không đổi trạng thái đơn hoặc tiền.

**Căn cứ** ORDER, ORDER_SQL, ORDERS_UI, MOBILE_MODELS

## HT-4.5-02 Xem chi tiết đơn và thông tin thanh toán

**Use case**
Xem chi tiết đơn và thông tin thanh toán

**Use case ID**
HT-4.5-02

**Tác nhân chính**
Khách hàng

**Tổng quan**
Xem dòng in, gia công, tiền, ghi chú, nơi nhận, máy/nhân sự và các mốc tiến độ của đơn.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Từ HT-4.5-01; hành vi hủy, đánh giá và thông tin nhận hoàn là lựa chọn theo điều kiện. Nhãn hóa đơn trên hình cần phân biệt với hóa đơn điện tử thuế.

**Dòng sự kiện chính**
1. Khách mở một đơn; App GET api/don-hang/{id}.
2. API đọc đơn rồi kiểm MaKhachHang từ JWT; đọc các dòng in/gia công và thông tin giao liên quan.
3. API dựng các mốc theo trạng thái; đơn hủy chỉ hiển thị mốc đã đặt và đã hủy.
4. App hiển thị tổng tiền/các khoản, quy cách và mốc tiến độ; nút thao tác theo điều kiện.
5. App nghe sự kiện đúng mã đơn để đọc lại; kéo xuống cho phép làm mới.
6. Khách rời màn; App bỏ đăng ký lắng nghe của màn.

**Dòng sự kiện phụ**
Bước 2 không tồn tại/không phải chủ: trả không tìm thấy, không lộ nội dung.
Mất SignalR: có thể kéo làm mới; không hứa có push khi App đã tắt.
Nhiều sự kiện đồng thời: màn gom lượt tải để không chồng truy vấn vô hạn.
CSDL không có timestamp riêng cho mọi mốc; không giả giờ tiếp nhận/bắt đầu in/in xong nếu không được lưu.
Thông tin tiền là chi tiết đơn; chưa có chức năng xuất hóa đơn điện tử VAT/XML hoặc số hóa đơn thuế trong luồng này.

**Tiền điều kiện**
Khách có phiên và đơn thuộc chính khách.

**Hậu điều kiện**
Đọc được chi tiết hợp lệ và phản ánh trạng thái; xem đơn không tự in, hủy hay thanh toán.

**Căn cứ** ORDER, DETAIL_UI, HUB, MOBILE_MODELS

## HT-4.5-03 Tiếp tục thanh toán đơn VietQR còn hạn

**Use case**
Tiếp tục thanh toán đơn VietQR còn hạn

**Use case ID**
HT-4.5-03

**Tác nhân chính**
Khách hàng

**Tổng quan**
Mở lại QR của đơn cá nhân chưa trả để khách hoàn tất khoản chuyển.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Tái sử dụng HT-4.4-01/02/05, không tạo đơn mới. Có thể mở trực tiếp từ danh sách.

**Dòng sự kiện chính**
1. Khách chọn đơn đang chờ chuyển khoản từ danh sách.
2. App mở Thanh toán, đọc trạng thái/hạn từ API.
3. Nếu còn đủ điều kiện, App lấy lại link/QR theo HT-4.4-02.
4. Khách chuyển khoản; App nghe sự kiện, polling khi hoạt động và đối soát lúc resume.
5. API xác nhận đúng tiền thì hiển thị đã trả và cho chuyển xem tiến độ.

**Dòng sự kiện phụ**
UI danh sách chỉ biết chưa trả/chưa hủy; hạn cuối cùng được màn thanh toán/API kiểm tra, không tin ảnh danh sách cũ.
Đã trả trong lúc mở: hiển thị kết quả mới, không yêu cầu trả lần nữa.
Đơn đã hủy/hết hạn: dừng thanh toán trên giao diện; hướng dẫn đặt lại nếu cần.
Link lỗi hoặc payOS chưa phản hồi: cho kiểm tra/thử lại, không gán đã thanh toán cục bộ.
Tiền vào sau hủy: chuyển luồng hoàn 4.11, không khôi phục.

**Tiền điều kiện**
Đơn thuộc khách, VIETQR, chưa trả, chưa hủy và còn hạn do máy chủ xác nhận.

**Hậu điều kiện**
Khoản trả được phản ánh theo máy chủ; không nhân bản đơn vì mở lại thanh toán.

**Căn cứ** ORDERS_UI, PAY_UI, PAYMENT

## HT-4.5-04 Hủy đơn khi chưa bắt đầu in

**Use case**
Hủy đơn khi chưa bắt đầu in

**Use case ID**
HT-4.5-04

**Tác nhân chính**
Khách hàng

**Tổng quan**
Hủy đơn cá nhân từ QR/tiến độ khi còn chờ tiếp nhận hoặc đã tiếp nhận nhưng chưa in.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Tái sử dụng HT-4.11-01; nhãn Hủy đơn chờ xác nhận trên hình 4.5 hẹp hơn điều kiện thực tế. Bổ sung tài khoản nhận hoàn theo HT-4.11-03.

**Dòng sự kiện chính**
1. App kiểm trạng thái và hiện nút Hủy đơn.
2. Khách nhập lý do; nếu đã trả thì nhập ngân hàng và số tài khoản cùng lý do hoàn.
3. App xác nhận và POST api/don-hang/{id}/huy.
4. API khóa, kiểm lại chủ đơn/trạng thái/dữ liệu nhận hoàn rồi gọi thủ tục hủy.
5. App tải lại; đơn DA_HUY, có tiền thì chờ CEO chuyển hoàn, chưa trả thì không có khoản hoàn.

**Dòng sự kiện phụ**
Đơn vừa chuyển DANG_IN trước lúc gửi: API từ chối dù nút cũ từng hiện.
Lý do thiếu, tài khoản nhận sai hoặc thiếu trường khi đã trả: sửa trong popup rồi gửi lại.
Khách hủy popup: giữ đơn hiện tại.
Đơn đã hủy/đã hoàn: không hủy lại.
Khách chưa có thông tin nhận hoàn ở đơn hủy đã trả có thể bổ sung khi còn được phép; không tự xác nhận CEO đã hoàn.

**Tiền điều kiện**
Đơn thuộc khách, trạng thái CHO_XAC_NHAN hoặc DA_XAC_NHAN.

**Hậu điều kiện**
Hủy thành công không tự chuyển tiền; trạng thái nhận tiền và hoàn được theo dõi riêng. Từ DANG_IN không hủy/hoàn qua chức năng này.

**Căn cứ** ORDER, CANCEL_UI, MOBILE_MODELS, REFUND_DTO

## HT-4.5-05 Đánh giá chất lượng dịch vụ

**Use case**
Đánh giá chất lượng dịch vụ

**Use case ID**
HT-4.5-05

**Tác nhân chính**
Khách hàng

**Tổng quan**
Gửi một đánh giá cho đơn của mình đã hoàn tất.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Lựa chọn tùy ý từ chi tiết HT-4.5-02 khi HOAN_THANH và chưa có đánh giá; không bắt buộc để hoàn tất đơn.

**Dòng sự kiện chính**
1. App hiển thị Đánh giá trên đơn đủ điều kiện.
2. Khách chọn 1–5 sao và nhập nhận xét nếu muốn.
3. App POST api/don-hang/{id}/danh-gia.
4. API kiểm sao; sp_DanhGia kiểm chủ đơn, HOAN_THANH và chưa có đánh giá.
5. SQL lưu đánh giá, API cảm ơn; App tải lại và không hiện lời mời đánh giá mới cho cùng đơn.

**Dòng sự kiện phụ**
Bước 2 số sao ngoài 1–5/nhận xét quá giới hạn 500 ký tự: yêu cầu sửa.
Bước 4 đơn chưa hoàn tất hoặc không thuộc khách: từ chối.
Bước 4 đơn đã có đánh giá: từ chối gửi thêm; không mô tả có sửa/xóa đánh giá cho khách khi chưa có API.
Quản lý có quyền ẩn đánh giá ở phân hệ riêng; không có nghĩa khách tự sửa trạng thái hiển thị.
Mất phản hồi: tải lại chi tiết kiểm đã đánh giá trước khi gửi lại.

**Tiền điều kiện**
Phiên CUSTOMER; đơn thuộc khách, HOAN_THANH, chưa đánh giá.

**Hậu điều kiện**
Có một đánh giá nếu hợp lệ; đơn và tiền giữ nguyên. Không tự đổi đánh giá thành khiếu nại/hoàn tiền.

**Căn cứ** ORDER, SQL_REVIEW, DETAIL_UI, ORDER_DTO

## HT-4.5-06 Xem thông báo cá nhân

**Use case**
Xem thông báo cá nhân

**Use case ID**
HT-4.5-06

**Tác nhân chính**
Khách hàng

**Tổng quan**
Đọc các thông báo nghiệp vụ trong App, gồm thông báo đơn và thông báo hệ thống.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Có thể gọi đánh dấu đã đọc HT-4.5-07. SignalR hỗ trợ cập nhật khi còn kết nối, không phải dịch vụ push lúc tắt App.

**Dòng sự kiện chính**
1. Khách mở tab Thông báo.
2. App GET api/thong-bao, API lấy người nhận từ JWT.
3. API trả thông báo mới nhất và tổng số chưa đọc; mặc định 50, giới hạn yêu cầu trong 1–200.
4. App hiển thị tiêu đề, nội dung, thời điểm và dấu đọc/chưa đọc.
5. Khi có tin realtime hoặc kéo làm mới, App lấy danh sách lại.
6. Khách chạm thông báo đơn: đánh dấu đọc rồi mở tab Đơn hàng theo xử lý giao diện hiện tại.

**Dòng sự kiện phụ**
Danh sách trống: hiển thị chưa có thông báo, không báo lỗi.
Mất mạng: báo lỗi/thử lại; không giả có tin mới.
Số chưa đọc là toàn bộ thông báo của người dùng, có thể lớn hơn số item đang tải.
Chạm thông báo đơn hiện mở tab Đơn hàng, không khẳng định deep link luôn mở thẳng đúng màn chi tiết.
Không có cam kết nhận thông báo đẩy khi ứng dụng đã tắt; người dùng đã yêu cầu bỏ lựa chọn này.

**Tiền điều kiện**
Có phiên; thông báo nếu có được lưu cho đúng người dùng.

**Hậu điều kiện**
Thông báo của chính khách được trình bày; chỉ xem danh sách chưa mặc định đánh dấu toàn bộ đã đọc.

**Căn cứ** NOTICE, NOTICE_UI, HUB

## HT-4.5-07 Đánh dấu thông báo đã đọc

**Use case**
Đánh dấu thông báo đã đọc

**Use case ID**
HT-4.5-07

**Tác nhân chính**
Khách hàng

**Tổng quan**
Ghi nhận khách đã đọc một hoặc toàn bộ thông báo cá nhân.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Thao tác tùy chọn từ HT-4.5-06. Có thể mô hình extend với điểm mở rộng chọn đọc, không dùng để khẳng định phải đọc hết khi xem.

**Dòng sự kiện chính**
1. Khách chạm một thông báo chưa đọc hoặc chọn Đọc tất cả.
2. App PUT api/thong-bao/{id}/da-doc hoặc api/thong-bao/da-doc-tat-ca.
3. API lấy mã người dùng từ JWT, cập nhật chỉ bản ghi cùng chủ sở hữu.
4. API trả kết quả; App tải lại danh sách và số chưa đọc.
5. Nếu chạm thông báo gắn đơn, App điều hướng sang tab đơn sau khi ghi đọc thành công.

**Dòng sự kiện phụ**
Bước 3 mã không tồn tại/không thuộc người dùng: báo không tìm thấy, không sửa thông báo người khác.
Thông báo đã đọc: không làm phát sinh thông báo mới hoặc nhân đôi bản ghi.
Đọc tất cả khi không còn tin chưa đọc: kết quả hợp lệ, số chưa đọc bằng 0.
Mất mạng hoặc cập nhật lỗi: không giả đã lưu; hiển thị lỗi, thử lại.
Không xóa thông báo hay hủy đơn khi đánh dấu đã đọc.

**Tiền điều kiện**
Có phiên, thông báo thuộc người gọi hoặc người dùng muốn đánh dấu toàn bộ.

**Hậu điều kiện**
Cờ DaDoc và số đếm được cập nhật đúng chủ; dữ liệu nội dung/đơn/tiền không đổi.

**Căn cứ** NOTICE, NOTICE_UI

## Đối chiếu nhãn trên hình gốc

| Nhãn | Đặc tả | Kết luận | Cách hiểu và điều chỉnh |
| --- | --- | --- | --- |
| Tra cứu đơn | HT-4.5-01 | Đúng | Danh sách cá nhân, lọc nhóm ở App. |
| Thanh toán đơn VietQR còn hạn | HT-4.5-03 | Đúng có kiểm máy chủ | Danh sách điều hướng theo chưa trả/chưa hủy; API QR kiểm hạn cuối. |
| Hủy đơn chờ xác nhận | HT-4.5-04 và HT-4.11-01 | Cần đổi tên | Hủy cả CHO_XAC_NHAN và DA_XAC_NHAN; từ DANG_IN không hủy. |
| Xem chi tiết và hóa đơn | HT-4.5-02 | Cần làm rõ tên | Có chi tiết tiền của đơn; chưa có hóa đơn điện tử thuế, nên dùng Xem chi tiết đơn và thanh toán. |
| Đánh giá chất lượng dịch vụ | HT-4.5-05 | Đúng có điều kiện | Chỉ HOAN_THANH, đúng khách, một lần. |
| Xem thông báo cá nhân | HT-4.5-06 | Đúng | Thông báo lưu CSDL và realtime khi kết nối; không push khi tắt App. |
| Đánh dấu thông báo đã đọc | HT-4.5-07 | Đúng | Có một hoặc tất cả, cập nhật theo chủ sở hữu. |

## Căn cứ mã nguồn

- **CANCEL_UI** [MaNguon/photocopy_mobile/lib/widgets/huy_don.dart:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/widgets/huy_don.dart:11) — Form hủy và cập nhật ba trường nhận hoàn trên App.
- **DETAIL_UI** [MaNguon/photocopy_mobile/lib/screens/don_hang/tien_do_screen.dart:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/don_hang/tien_do_screen.dart:17) — Mốc tiến độ, gom lượt đọc và tác vụ theo trạng thái.
- **HUB** [MaNguon/PhotocopyChain/PhotocopyApi/Hubs/CapNhatHub.cs:16](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Hubs/CapNhatHub.cs:16) — Nhóm SignalR theo người/chi nhánh/toàn chuỗi từ JWT.
- **MOBILE_MODELS** [MaNguon/photocopy_mobile/lib/models.dart:216](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/models.dart:216) — Các getter nhóm, chờ chuyển khoản và điều kiện hủy/hoàn.
- **NOTICE** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThongBaoController.cs:17](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThongBaoController.cs:17) — Danh sách/đếm và đánh dấu đọc đúng người nhận.
- **NOTICE_UI** [MaNguon/photocopy_mobile/lib/screens/thong_bao/thong_bao_screen.dart:15](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/thong_bao/thong_bao_screen.dart:15) — Đọc một/tất cả, thông báo đơn mở tab đơn hàng.
- **ORDER** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:32) — Đặt App, lọc chi nhánh, chi tiết, tiếp nhận, hủy và đánh giá.
- **ORDERS_UI** [MaNguon/photocopy_mobile/lib/screens/don_hang/don_hang_screen.dart:71](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/don_hang/don_hang_screen.dart:71) — Danh sách, lọc nhóm và điều hướng thanh toán/tiến độ.
- **ORDER_DTO** [MaNguon/PhotocopyChain/PhotocopyShared/Dtos/DonHang/DonHangDtos.cs:8](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/Dtos/DonHang/DonHangDtos.cs:8) — Hợp đồng danh sách/chi tiết đơn và đánh giá.
- **ORDER_SQL** [MaNguon/PhotocopyChain/PhotocopyApi/DuLieu/TruyVanSql.cs:6](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/DuLieu/TruyVanSql.cs:6) — Các truy vấn phục vụ danh sách/chi tiết đơn.
- **PAYMENT** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30) — QR, chữ ký webhook, đối soát, tiền đến muộn và điều hướng.
- **PAY_UI** [MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart:24](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/screens/dat_in/thanh_toan_screen.dart:24) — Vẽ QR, polling 5 giây khi hoạt động, đối soát khi resume.
- **REFUND_DTO** [MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Tuan3/ThongTinHoanTienRequest.cs:7](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Tuan3/ThongTinHoanTienRequest.cs:7) — Giới hạn lý do, ngân hàng, số tài khoản và ghi chú ba dòng.
- **SQL_REVIEW** [Database/04_StoredProcedure.sql:692](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:692) — Chỉ chủ đơn HOAN_THANH và một đánh giá mỗi đơn.

# Task 1 Hủy đơn và hoàn tiền

Phạm vi: NghiepVu/HuyDon_HoanTien.png, HeThong/HuyDon_HoanTien.png. Đối chiếu source ngày 26/10/2026. Gồm 5 đặc tả nghiệp vụ và 5 đặc tả hệ thống.

- Các nhãn Kiểm tra trạng thái và Nhập lý do được đặc tả đầy đủ như bước trong hủy đơn; không cố tạo endpoint độc lập không có trong code.
- Không có bước duyệt yêu cầu hủy riêng. Quản lý không xem danh sách hoàn chuyên biệt; khách không xác nhận đã chuyển hoàn.
- Thông tin nhận hoàn gồm ba dòng trong GhiChu hiện có: lý do, ngân hàng, số tài khoản. Không thêm bảng yêu cầu hoàn hoặc API chuyển tiền vào mô tả.

## NV-3.5-01 Giải quyết yêu cầu hủy đơn

**Giới thiệu về use case nghiệp vụ**
Khách hàng không còn nhu cầu in hoặc quản lý quyết định hủy đơn trong phạm vi phụ trách. Mục tiêu là kết thúc đúng đơn chưa bắt đầu in, giữ thông tin để đối soát và xác định khoản cần hoàn. Khách là tác nhân ngoài tổ chức; quản lý và CEO là người xử lý nội bộ. Việc chấp nhận hủy hiện được kiểm tra trực tiếp, không có bước quản lý duyệt một yêu cầu đang chờ.

**Các dòng cơ bản**
1. Khách hoặc người quản lý xác định đơn cần hủy.
2. Người yêu cầu cung cấp lý do hủy. Nếu khách đã trả tiền, khách cung cấp thêm lý do hoàn, ngân hàng và số tài khoản nhận tiền.
3. Cửa hàng kiểm tra đúng người, đúng chi nhánh và đơn vẫn ở giai đoạn chờ tiếp nhận hoặc đã tiếp nhận nhưng chưa in.
4. Khi hợp lệ, cửa hàng ghi nhận đơn đã hủy và giữ lý do cùng thông tin nhận hoàn nếu đã cung cấp.
5. Với đơn chưa nhận tiền, quy trình hủy kết thúc, không phát sinh khoản hoàn.
6. Với đơn đã nhận tiền, khoản phải trả lại được đưa vào danh sách để CEO xử lý. Khách nhận kết quả hủy; hủy đơn không đồng nghĩa đã chuyển hoàn tiền.

**Các dòng thay thế**
- Tại bước 3, đơn đã bắt đầu in, đã in, đang giao, giao thất bại, hoàn tất hoặc đã hủy: từ chối hủy; kết thúc mà không thay đổi đơn.
- Tại bước 2, lý do hoặc thông tin nhận hoàn bắt buộc còn thiếu: yêu cầu bổ sung rồi quay lại bước 2.
- Quản lý/CEO hủy đơn đã trả nhưng chưa có tài khoản nhận: vẫn ghi nhận hủy hợp lệ; chờ khách bổ sung trên App trước khi CEO xác nhận hoàn.
- Tiền được ngân hàng báo về sau lúc hủy: giữ đơn đã hủy và chuyển sang giải quyết khoản hoàn, không khởi động lại việc in.
- Không đúng chủ đơn hoặc ngoài phạm vi chi nhánh: không chấp nhận yêu cầu; không tiết lộ thông tin đơn khác.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, SQL_CANCEL

## NV-3.5-02 Rà soát danh sách đơn chờ hoàn tiền

**Giới thiệu về use case nghiệp vụ**
CEO xác định những đơn đã hủy trước khi in nhưng cửa hàng đã nhận tiền để tổ chức chuyển trả. Mục tiêu là tránh bỏ sót tiền của khách. Quản lý chi nhánh không có chức năng xem danh sách hoàn tiền chuyên biệt này trong phần mềm hiện tại.

**Các dòng cơ bản**
1. CEO xác định phạm vi cần rà soát: toàn chuỗi hoặc chi nhánh đang chọn.
2. Cửa hàng tập hợp các đơn đã hủy trước khi in, đã nhận tiền và chưa ghi nhận hoàn.
3. CEO xem mã đơn, khách hàng, số điện thoại, chi nhánh, số tiền và ghi chú.
4. CEO kiểm tra ghi chú gồm lý do hoàn tiền, ngân hàng và số tài khoản.
5. CEO phân biệt khoản đã đủ thông tin để chuyển trả và khoản phải chờ khách bổ sung.
6. Danh sách được làm mới sau khi khách cập nhật hoặc sau khi xác nhận hoàn; chưa có chuyển tiền trong bước rà soát.

**Các dòng thay thế**
- Bước 2 không có đơn phù hợp: thông báo danh sách trống, kết thúc.
- Bước 4 thiếu hoặc sai định dạng thông tin nhận hoàn: chờ khách cập nhật; không xác nhận đã hoàn.
- Khách vừa cập nhật: CEO làm mới danh sách rồi quay lại bước 3.
- CEO đang xem một chi nhánh: chỉ rà soát đơn thuộc phạm vi đó.
- Đơn chưa trả tiền hoặc đã xác nhận hoàn: không đưa vào danh sách cần hoàn. Đơn hủy cũ đã in còn mã máy không xử lý theo luồng hoàn thông thường.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, SQL_CANCEL

## NV-3.5-03 Kết thúc đơn quá hạn thanh toán

**Giới thiệu về use case nghiệp vụ**
Đơn chuyển khoản còn chờ tiếp nhận nhưng quá thời hạn thanh toán phải được kết thúc để tránh giữ yêu cầu không còn hiệu lực. Đây là chính sách nghiệp vụ được phần mềm thực hiện tự động; không yêu cầu quản lý bấm nút hủy quá hạn.

**Các dòng cơ bản**
1. Cửa hàng quy định thời hạn chuyển khoản là 15 phút tính từ thời điểm đặt đơn.
2. Trong các lượt kiểm tra định kỳ, phần mềm xác định đơn còn chờ tiếp nhận, chọn VietQR và chưa được ghi nhận thanh toán.
3. Khi thời điểm đặt cộng 15 phút đã nhỏ hơn thời điểm kiểm tra, đơn được chuyển sang đã hủy.
4. Lý do quá hạn được lưu trong ghi chú; thông tin đơn vẫn được giữ để tra cứu.
5. Khách xem lại đơn sẽ thấy trạng thái đã hủy; vì chưa nhận tiền nên chưa có khoản hoàn.

**Các dòng thay thế**
- Chưa vượt hạn hoặc đơn đã được nhận tiền/đã rời trạng thái chờ tiếp nhận: bỏ qua trong lượt kiểm tra, đơn giữ nguyên.
- Bộ kiểm tra chạy khoảng mỗi phút nên không cam kết đổi trạng thái đúng từng giây ở phút thứ 15.
- Tiền hợp lệ đến sau khi đơn bị hủy: ghi nhận khoản nhận tiền để hoàn, không tự khôi phục đơn; khách bổ sung tài khoản nếu còn thiếu.
- Khi bộ kiểm tra gặp lỗi kết nối: ghi nhận lỗi và thử ở lượt sau; không giả định đơn đã được xử lý thành công.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, SQL_CANCEL

## NV-3.5-04 Chuyển trả tiền cho khách

**Giới thiệu về use case nghiệp vụ**
CEO chuyển trả toàn bộ khoản tiền đã nhận của đơn hủy trước in sau khi đã có thông tin nhận hoàn. Ngân hàng là tổ chức ngoài tham gia chuyển khoản thực tế. Phần mềm PhotocopyChain không thực hiện chuyển khoản và không gọi API hoàn tiền tự động.

**Các dòng cơ bản**
1. CEO chọn một khoản chờ hoàn đã có đủ thông tin nhận tiền.
2. CEO đối chiếu đúng khách, mã đơn, số tiền, ngân hàng và số tài khoản.
3. CEO sử dụng kênh ngân hàng bên ngoài phần mềm để chuyển trả tiền.
4. CEO kiểm tra kết quả chuyển tiền trên kênh ngân hàng.
5. Chỉ khi chuyển trả thực tế thành công, CEO thực hiện nghiệp vụ xác nhận đã hoàn trên Web.
6. Cửa hàng giữ nguyên trạng thái đơn đã hủy; việc chuyển trả không làm đơn được in lại.

**Các dòng thay thế**
- Thiếu hoặc nghi ngờ sai tài khoản tại bước 2: liên hệ khách để làm rõ và chờ cập nhật; chưa chuyển tiền.
- Ngân hàng báo thất bại hoặc kết quả chưa chắc chắn: kiểm tra giao dịch ngân hàng trước khi thử lại; chưa xác nhận đã hoàn trên Web.
- CEO đã chuyển tiền nhưng Web mất kết nối: kiểm tra trạng thái ghi nhận trước khi xác nhận lại; không chuyển ngân hàng thêm lần nữa chỉ vì Web chưa phản hồi.
- Không có trường lưu chứng từ/mã giao dịch hoàn hoặc cơ chế xác minh tiền ra ngân hàng trong chức năng hiện tại; không mô tả phần mềm như đã tự đối soát được khoản chuyển trả.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, SQL_CANCEL

## NV-3.5-05 Xác nhận khoản tiền đã chuyển hoàn

**Giới thiệu về use case nghiệp vụ**
Sau khi chuyển trả thực tế thành công, CEO xác nhận kết quả để kết thúc khoản cần hoàn trong sổ theo dõi của phần mềm. Đây là xác nhận của CEO, không phải thao tác khách hàng tự đánh dấu đã nhận tiền.

**Các dòng cơ bản**
1. CEO mở lại đúng đơn trong danh sách chờ xác nhận hoàn tiền.
2. CEO kiểm tra lần cuối số tiền và ba dòng thông tin nhận hoàn.
3. CEO chọn Đã chuyển hoàn, đọc nội dung xác nhận và đồng ý.
4. Cửa hàng ghi nhận thanh toán đã hoàn tiền khi đơn vẫn đủ điều kiện và chưa được xác nhận trước đó.
5. Đơn vẫn đã hủy, được loại khỏi danh sách khoản cần hoàn và có cập nhật kết quả cho người liên quan.
6. Khách xem lại trạng thái đã hoàn tiền trên App; thông tin ghi chú vẫn gắn với đơn để tra cứu.

**Các dòng thay thế**
- CEO chọn hủy hộp xác nhận: quay về danh sách, không đổi trạng thái.
- Chưa đủ lý do/ngân hàng/số tài khoản hoặc đơn không còn đủ điều kiện: từ chối xác nhận và yêu cầu kiểm tra lại.
- Khoản đã được xác nhận hoàn: không ghi nhận hoàn thêm lần nữa.
- Khách cố sửa tài khoản sau khi đã xác nhận hoàn: không cho cập nhật qua chức năng bổ sung thông tin.
- Việc xác nhận không tự chuyển tiền và không tạo bằng chứng ngân hàng; chỉ được thực hiện sau bước chuyển trả thực tế.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, SQL_CANCEL

## HT-4.11-01 Hủy đơn hàng trước khi bắt đầu in

**Use case**
Hủy đơn hàng trước khi bắt đầu in

**Use case ID**
HT-4.11-01

**Tác nhân chính**
Khách hàng; Quản lý chi nhánh; CEO

**Tổng quan**
Kết thúc đúng đơn còn CHO_XAC_NHAN hoặc DA_XAC_NHAN. Kích hoạt khi người dùng chọn Hủy đơn trên màn QR/tiến độ của App hoặc Hàng đợi đơn hàng trên Web.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Tác nhân trực tiếp: khách đúng chủ; quản lý/CEO đúng phạm vi. Kiểm tra trạng thái và nhập lý do là các bước bắt buộc của use case này. Hoàn tiền là quy trình riêng, chỉ phát sinh khi thực sự đã nhận tiền; không coi việc hủy luôn include việc hoàn.

**Dòng sự kiện chính**
1. Tác nhân mở đúng đơn và chọn Hủy đơn.
2. Giao diện mở hộp nhập lý do; với khách đã trả tiền, hiển thị thêm ngân hàng và số tài khoản nhận hoàn.
3. Tác nhân nhập dữ liệu, đọc cảnh báo chỉ hủy trước in và xác nhận.
4. API kiểm quyền, mở transaction và khóa đọc đơn để tránh đồng thời bắt đầu in.
5. API kiểm chủ đơn hoặc phạm vi chi nhánh và trạng thái chỉ thuộc hai bước trước in; kiểm dữ liệu bắt buộc.
6. SQL kiểm lại điều kiện, đổi đơn sang DA_HUY, lưu ghi chú và xóa MaMayIn đã phân công ở giai đoạn trước in.
7. Hệ thống commit, thực hiện chính sách dọn tệp kết thúc, phát cập nhật và trả kết quả có hay không khoản cần hoàn.
8. Giao diện tải lại đơn đã hủy; nếu đã nhận tiền thì khách chờ CEO xử lý hoàn, không hiển thị như đã chuyển tiền hoàn.

**Dòng sự kiện phụ**
Biến thể 2a — Đơn chưa trả: chỉ nhập lý do hủy, tối đa 255 ký tự; tiếp tục bước 3.
Biến thể 2b — Khách đã trả: lý do hoàn tối đa 100 ký tự, ngân hàng 60 ký tự, tài khoản 6–30 chữ số; ghi chú gồm ba dòng. Tiếp tục bước 3.
Biến thể 5a — Quản lý/CEO hủy đơn đã trả nhưng chưa có tài khoản: ghi nhận hủy trước in; khách bổ sung theo HT-4.11-03.
Ngoại lệ 5b — Đơn đã sang DANG_IN hoặc bước sau in/giao, hoàn tất hay đã hủy: từ chối, không thay đổi đơn; kết thúc.
Ngoại lệ 5c — Không đúng chủ hoặc sai phạm vi: từ chối; không thay đổi dữ liệu; kết thúc.
Ngoại lệ 5d — Dữ liệu thiếu/sai: hiển thị lỗi, người dùng sửa và quay lại bước 2; chưa hủy đơn.
Ngoại lệ 7a — Mất phản hồi: người dùng tải lại để xác định trạng thái đã commit; không coi thao tác chắc chắn thất bại và không chuyển tiền thủ công lặp.

**Tiền điều kiện**
Đã đăng nhập với vai trò cho phép. Đơn tồn tại, thuộc chủ/phạm vi hợp lệ, đang CHO_XAC_NHAN hoặc DA_XAC_NHAN. Thợ in và shipper không được gọi chức năng này.

**Hậu điều kiện**
Thành công: đơn DA_HUY, có lý do và MaMayIn trống; thanh toán vẫn CHUA_THANH_TOAN hoặc DA_THANH_TOAN. Thất bại trước commit: không hủy hoặc hoàn tiền. Không sinh trạng thái CHO_HOAN_TIEN mới.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, PAYMENT, SQL_CANCEL, SQL_REFUND, CANCEL_UI

## HT-4.11-02 Xem danh sách đơn cần hoàn tiền

**Use case**
Xem danh sách đơn cần hoàn tiền

**Use case ID**
HT-4.11-02

**Tác nhân chính**
CEO

**Tổng quan**
Cung cấp các khoản đã nhận tiền của đơn hủy trước in để CEO chuyển trả. Kích hoạt khi CEO mở Phê duyệt hoặc bấm Làm mới thông tin.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Chỉ CEO là tác nhân. HT-4.11-04 sử dụng một đơn từ danh sách nhưng không vì vậy mặc nhiên có quan hệ include/extend. Việc khách bổ sung thông tin được mô tả riêng ở HT-4.11-03.

**Dòng sự kiện chính**
1. CEO mở Phê duyệt trong phạm vi toàn chuỗi hoặc chi nhánh đang chọn.
2. Web yêu cầu danh sách đơn cần hoàn; API kiểm vai trò SUPER_ADMIN và phạm vi.
3. API lọc đơn DA_HUY, MaMayIn trống, thanh toán DA_THANH_TOAN và thuộc phạm vi hiện tại.
4. API trả mã đơn, khách, số điện thoại, chi nhánh, số tiền và ghi chú.
5. Web hiển thị ghi chú giữ xuống dòng, gồm lý do hoàn, ngân hàng và số tài khoản khi đã có đủ dữ liệu hợp lệ.
6. Web khóa nút Đã chuyển hoàn nếu chưa đủ thông tin và thông báo chờ khách bổ sung.
7. CEO làm mới thông tin khi cần rồi chọn xử lý khoản đủ điều kiện; use case này chưa đổi thanh toán.

**Dòng sự kiện phụ**
Biến thể 3a — Chỉ chọn một chi nhánh: trả danh sách đúng chi nhánh đó; tiếp tục bước 4.
Biến thể 5a — Ghi chú cũ chưa có đủ ba dòng hợp lệ: hiển thị ghi chú hiện có và cảnh báo chờ khách; quay lại danh sách.
Biến thể 7a — Khách đã cập nhật nhưng trang còn dữ liệu cũ: bấm Làm mới thông tin rồi lặp từ bước 2.
Ngoại lệ 2a — Quản lý/thợ in/shipper/khách gọi danh sách chuyên biệt: từ chối truy cập; kết thúc.
Ngoại lệ 3b — Không có khoản phù hợp: hiển thị danh sách trống; kết thúc thành công không có dữ liệu.
Ngoại lệ 2b — Lỗi tải API: hiển thị lỗi tải danh sách; không trình bày dữ liệu như khoản đã được xử lý.

**Tiền điều kiện**
CEO có phiên đăng nhập hợp lệ; phạm vi chi nhánh được xác định theo phiên và lựa chọn hiện tại.

**Hậu điều kiện**
Hiển thị khoản đủ điều kiện đúng phạm vi hoặc báo trống/lỗi; không chuyển tiền hay sửa trạng thái.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, PAYMENT, SQL_CANCEL, SQL_REFUND, CANCEL_UI

## HT-4.11-03 Cung cấp hoặc sửa thông tin nhận hoàn tiền

**Use case**
Cung cấp hoặc sửa thông tin nhận hoàn tiền

**Use case ID**
HT-4.11-03

**Tác nhân chính**
Khách hàng sở hữu đơn

**Tổng quan**
Bổ sung tài khoản nhận cho đơn hủy trước in đã nhận tiền nhưng chưa hoàn. Kích hoạt ở Theo dõi tiến độ bằng nút Bổ sung thông tin hoàn tiền hoặc Sửa thông tin hoàn tiền.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Hỗ trợ HT-4.11-04 khi đơn còn thiếu thông tin; là nhánh cần thiết cho đơn cũ hoặc tiền đến muộn. Không phải CEO phê duyệt yêu cầu hủy và không phải khách tự xác nhận đã hoàn.

**Dòng sự kiện chính**
1. Khách mở tiến độ đơn đã hủy đang chờ hoàn và chọn bổ sung hoặc sửa thông tin.
2. App mở popup ba trường: lý do hoàn tiền, ngân hàng, số tài khoản; nếu đã lưu thì điền lại đúng dữ liệu cũ.
3. Khách kiểm tra dữ liệu và chọn Lưu thông tin hoàn tiền.
4. App kiểm bắt buộc/định dạng; API kiểm lại lý do tối đa 100 ký tự, ngân hàng 60 ký tự, số tài khoản 6–30 chữ số.
5. API khóa đọc đơn và thanh toán, kiểm đúng chủ, DA_HUY, MaMayIn trống và thanh toán DA_THANH_TOAN.
6. API lưu GhiChu thành ba dòng Lý do hoàn tiền, Ngân hàng, Số tài khoản bằng câu lệnh có điều kiện chủ đơn và trạng thái.
7. API commit và phát cập nhật; App tải lại ghi chú. Số tài khoản được giữ dưới dạng chuỗi, không mất số 0 đầu.
8. Khách có thể sửa lại khi còn chờ hoàn; CEO làm mới trang để đọc thông tin mới trước khi chuyển tiền.

**Dòng sự kiện phụ**
Biến thể 2a — Chọn Sửa: dữ liệu đã lưu được điền sẵn; quay lại bước 3 sau khi điều chỉnh.
Biến thể 3a — Chọn Để sau: đóng popup, không gửi cập nhật; kết thúc giữ nguyên thông tin.
Ngoại lệ 4a — Thiếu/sai/qua giới hạn: báo lỗi và quay lại bước 2; không lưu một phần hoặc cắt bỏ thông tin.
Ngoại lệ 5a — Đơn không thuộc khách: từ chối và không tiết lộ dữ liệu; kết thúc.
Ngoại lệ 5b — Đã hoàn tiền, chưa nhận tiền, đơn chưa hủy hoặc là đơn hủy cũ đã in: không cho cập nhật; kết thúc.
Ngoại lệ 6a — Trạng thái đã thay đổi: yêu cầu tải lại; không sửa ghi chú của đơn khác và không đổi trạng thái thanh toán.
Ngoại lệ 7a — Mất phản hồi: tải lại đơn để kiểm tra thông tin đã lưu trước khi gửi lại.

**Tiền điều kiện**
CUSTOMER đăng nhập, đúng chủ đơn. Đơn đã hủy trước in, đã nhận tiền và chưa hoàn. App nhận cờ cho phép bổ sung từ API.

**Hậu điều kiện**
Thành công: GhiChu chứa đủ ba dòng trong giới hạn 255 ký tự; đơn vẫn DA_HUY, thanh toán vẫn DA_THANH_TOAN. Nếu bị từ chối thì ghi chú và thanh toán không bị cập nhật bởi yêu cầu đó.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, PAYMENT, SQL_CANCEL, SQL_REFUND, CANCEL_UI

## HT-4.11-04 Xác nhận đã chuyển hoàn tiền

**Use case**
Xác nhận đã chuyển hoàn tiền

**Use case ID**
HT-4.11-04

**Tác nhân chính**
CEO

**Tổng quan**
Ghi nhận việc CEO đã chuyển trả thực tế, không thực hiện giao dịch ngân hàng. Kích hoạt khi CEO chọn Đã chuyển hoàn trên Web và đồng ý popup.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Tác nhân duy nhất là CEO. Điều kiện đầu vào là tiền đã nhận hợp lệ và thông tin nhận hoàn đầy đủ. Chuyển tiền thực tế thuộc NV-3.5-04, nằm ngoài phần mềm; không include một API chuyển tiền chưa được triển khai.

**Dòng sự kiện chính**
1. CEO chọn khoản đủ thông tin trong danh sách cần hoàn.
2. CEO đã thực hiện chuyển trả qua ngân hàng bên ngoài và kiểm tra kết quả thành công.
3. CEO chọn Đã chuyển hoàn; popup nhắc lại mã đơn, khách, số tiền và ghi chú ngân hàng/tài khoản.
4. CEO đồng ý xác nhận sau khi đối chiếu.
5. API kiểm SUPER_ADMIN, khóa đọc đơn trong transaction và kiểm phạm vi, DA_HUY, MaMayIn trống, đủ thông tin nhận hoàn hợp lệ.
6. Thủ tục SQL kiểm CEO và chỉ cập nhật thanh toán đang DA_THANH_TOAN của đúng đơn sang DA_HOAN_TIEN.
7. API commit và phát cập nhật; Web tải lại danh sách, khoản đó không còn nằm trong danh sách cần hoàn.
8. App tải lại sẽ hiện Đã hoàn tiền; đơn vẫn DA_HUY và không còn cho sửa thông tin tài khoản.

**Dòng sự kiện phụ**
Biến thể 4a — CEO không đồng ý popup: đóng xác nhận và quay về danh sách; không gọi thao tác hoàn.
Ngoại lệ 5a — Vai trò khác hoặc sai phạm vi: từ chối; kết thúc không đổi thanh toán.
Ngoại lệ 5b — Chưa có đủ lý do/ngân hàng/tài khoản: từ chối; chờ khách bổ sung theo HT-4.11-03.
Ngoại lệ 6a — Đơn không còn đủ điều kiện hoặc đã hoàn: SQL không cập nhật và trả lỗi; không ghi nhận hoàn thêm lần nữa.
Ngoại lệ 7a — Web không nhận được phản hồi: CEO kiểm tra lại trạng thái trước khi xác nhận lại; không thực hiện thêm chuyển khoản chỉ vì lỗi mạng.
Giới hạn — Hệ thống không tự kiểm chứng giao dịch ngân hàng; người dùng phải chịu trách nhiệm xác nhận sau khi đã thực sự chuyển trả.

**Tiền điều kiện**
CEO đăng nhập đúng phạm vi. Đơn hủy trước in đã nhận tiền, chưa hoàn; ghi chú có đủ ba trường hợp lệ. CEO đã chuyển trả tiền thật bên ngoài phần mềm.

**Hậu điều kiện**
Thành công: thanh toán DA_HOAN_TIEN, đơn vẫn DA_HUY, ghi chú được giữ. Lỗi trước commit: không ghi nhận hoàn; hệ thống không tự làm phát sinh chuyển tiền bên ngoài.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, PAYMENT, SQL_CANCEL, SQL_REFUND, CANCEL_UI

## HT-4.11-05 Ghi nhận tiền hợp lệ đến sau khi hủy đơn

**Use case**
Ghi nhận tiền hợp lệ đến sau khi hủy đơn

**Use case ID**
HT-4.11-05

**Tác nhân chính**
payOS là hệ thống ngoài cung cấp kết quả thanh toán

**Tổng quan**
Không bỏ sót tiền ngân hàng nhận được sau lúc đơn bị hủy thủ công hoặc tự hủy quá hạn. Kích hoạt bởi webhook hợp lệ hoặc thao tác đối soát đọc trực tiếp kết quả payOS.

**Độ ưu tiên**
Cao

**Mối quan hệ**
Đây là nhánh của use case ghi nhận thanh toán VietQR thuộc Hình 4.4; Task 1 mô tả riêng phần đơn đã hủy. Khách/quầy có thể kích hoạt đối soát nhưng không được tự khai thanh toán thành công. Không biến tác vụ kiểm tra nội bộ thành tác nhân ngoài.

**Dòng sự kiện chính**
1. API nhận webhook payOS hoặc đọc kết quả đối soát đáng tin cậy của đúng đơn.
2. Với webhook, API xác minh chữ ký, kết quả thành công, mã đơn, mô tả, tiền tệ, mã giao dịch và số tiền; đối soát trực tiếp kiểm Paid, mã đơn, số tiền và tiền đã trả.
3. API xác định đơn đã DA_HUY, khóa đọc đơn và thanh toán để xử lý nhất quán với yêu cầu hủy.
4. Nếu thanh toán còn CHUA_THANH_TOAN, API ghi nhận khoản tiền và thông tin giao dịch bằng thủ tục thanh toán.
5. API giữ nguyên DA_HUY, không khôi phục đơn và không gọi xếp in cho đơn đã hủy.
6. API phát cập nhật. Đơn hủy trước in đã nhận tiền xuất hiện trong danh sách cần hoàn của CEO.
7. Nếu thiếu ngân hàng/tài khoản, khách cung cấp theo HT-4.11-03; CEO xử lý chuyển trả và xác nhận theo các use case tương ứng.

**Dòng sự kiện phụ**
Ngoại lệ 2a — Chữ ký hoặc dữ liệu đối chiếu không hợp lệ, sai số tiền hay đơn không thuộc hệ thống: không tự ghi nhận đã thanh toán cho đơn; kết thúc theo kết quả kiểm tra của API.
Biến thể 4a — Webhook lặp khi đã DA_THANH_TOAN: không ghi nhận lại khoản tiền; vẫn không khôi phục đơn.
Biến thể 4b — Khoản đã DA_HOAN_TIEN: giữ nguyên đã hoàn, không đổi ngược về đã thanh toán và không gửi in.
Biến thể 6a — Đơn hủy cũ đã in còn mã máy: không đưa vào danh sách hoàn thông thường; cần xử lý ngoại lệ ngoài phạm vi này.
Ngoại lệ 1a — Không gọi được payOS: không suy diễn đã nhận tiền; cho đối soát lại khi kết nối phục hồi.
Query return/cancel của trang thanh toán chỉ điều hướng/hiển thị, không thay thế bằng chứng giao dịch.

**Tiền điều kiện**
Có thanh toán VietQR của đơn trong hệ thống và kết quả ngân hàng/payOS đã được xác minh theo kênh gọi. Đây là phần nhận tiền thật, không phải thao tác giả lập thanh toán.

**Hậu điều kiện**
Khoản hợp lệ chưa ghi nhận được cập nhật, đơn vẫn DA_HUY; khoản đã hoàn không bị mở lại. Giao dịch không hợp lệ không tạo quyền hoàn tiền bằng dữ liệu do khách tự khai.

**Căn cứ** CANCEL, REFUND_DTO, REFUND_LIST, PAYMENT, SQL_CANCEL, SQL_REFUND, CANCEL_UI

## Đối chiếu nhãn trên hình gốc

| Nhãn | Đặc tả | Kết luận | Cách hiểu và điều chỉnh |
| --- | --- | --- | --- |
| Nghiệp vụ: Gửi yêu cầu hủy đơn | NV-3.5-01 | Có triển khai | Chấp nhận hủy trực tiếp khi hợp lệ; không tạo yêu cầu chờ quản lý duyệt. |
| Nghiệp vụ: Xem danh sách chờ hoàn tiền | NV-3.5-02 | Có, cần sửa tác nhân | Chức năng danh sách chuyên biệt chỉ cho CEO; hình gốc đang nối cả quản lý chi nhánh. |
| Nghiệp vụ: Duyệt yêu cầu hủy | NV-3.5-01 bước 3 | Không có bước duyệt riêng | Thể hiện thành kiểm tra quyền/trạng thái trong luồng hủy, không mô tả nút Duyệt chưa có. |
| Nghiệp vụ: Hủy đơn quá hạn thanh toán | NV-3.5-03 | Có, tự động | Job nội bộ thực hiện theo hạn 15 phút và nhịp kiểm tra; quản lý không bấm tác vụ này. |
| Nghiệp vụ: Hoàn trả tiền cho khách | NV-3.5-04 | Có, ngoài phần mềm | CEO chuyển trả bằng kênh ngân hàng; không phải API refund tự động. |
| Nghiệp vụ: Xác nhận hoàn tiền | NV-3.5-05 | Có, CEO thực hiện | Ghi nhận sau khi đã chuyển trả thật; không dùng extend/include để diễn tả thứ tự. |
| Hệ thống: Hủy đơn hàng | HT-4.11-01 | Có triển khai | Cho cả khách đúng chủ và quản lý/CEO đúng phạm vi, chỉ hai trạng thái trước in. |
| Hệ thống: Kiểm tra trạng thái đơn | HT-4.11-01 bước 4–6 | Bước kiểm tra bắt buộc | Không phải tác nhân ngoài; hướng include trên ảnh gốc không được xem là căn cứ triển khai. |
| Hệ thống: Nhập lý do hủy đơn | HT-4.11-01 bước 2–3 | Bước nhập dữ liệu | Khách đã trả phải nhập cả thông tin nhận hoàn; không coi đây là xác nhận thanh toán. |
| Hệ thống: Xác nhận đã hoàn tiền | HT-4.11-04 | Có, cần sửa tác nhân | Chỉ CEO được xác nhận; khách chỉ xem kết quả và cung cấp thông tin nhận tiền. |
| Hệ thống: Thực hiện hoàn tiền | NV-3.5-04 và HT-4.11-04 | Cần tách ranh giới | Chuyển ngân hàng ở ngoài phần mềm; endpoint trong hệ thống chỉ ghi nhận đã chuyển hoàn. |
| Hệ thống: Xác nhận đã thanh toán | HT-4.11-05; Task 4 | Có, thuộc nhận tiền | Nhận tiền từ payOS/đối soát hợp lệ; không phải bước CEO tự đánh dấu để được hoàn. |
| Hệ thống: Xem đơn cần hoàn tiền | HT-4.11-02 | Có, CEO thực hiện | Lọc đúng phạm vi, đơn hủy trước in và thanh toán đã trả. |
| Bổ sung hiện hành: Cung cấp hoặc sửa thông tin nhận hoàn | HT-4.11-03 | Có trong code mới | Ảnh gốc chưa thể hiện rõ lý do hoàn, ngân hàng, số tài khoản và khóa sửa sau hoàn. |

## Căn cứ mã nguồn

- **CANCEL** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:937](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/DonHangController.cs:937) — Hủy trước in, cập nhật thông tin nhận hoàn và xác nhận CEO.
- **CANCEL_UI** [MaNguon/photocopy_mobile/lib/widgets/huy_don.dart:11](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/photocopy_mobile/lib/widgets/huy_don.dart:11) — Form hủy và cập nhật ba trường nhận hoàn trên App.
- **PAYMENT** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs:30) — QR, chữ ký webhook, đối soát, tiền đến muộn và điều hướng.
- **REFUND_DTO** [MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Tuan3/ThongTinHoanTienRequest.cs:7](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyShared/Dtos/Tuan3/ThongTinHoanTienRequest.cs:7) — Giới hạn lý do, ngân hàng, số tài khoản và ghi chú ba dòng.
- **REFUND_LIST** [MaNguon/PhotocopyChain/PhotocopyApi/Controllers/QuanTriController.cs:565](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/MaNguon/PhotocopyChain/PhotocopyApi/Controllers/QuanTriController.cs:565) — Danh sách hoàn chỉ CEO, lọc hủy trước in/đã trả/phạm vi.
- **SQL_CANCEL** [Database/04_StoredProcedure.sql:645](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:645) — Hủy hai trạng thái trước in và giữ lịch sử.
- **SQL_REFUND** [Database/04_StoredProcedure.sql:675](C:/Users/USER/Desktop/DA_CN/CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD/Database/04_StoredProcedure.sql:675) — Ghi nhận DA_HOAN_TIEN chỉ khoản hợp lệ.


# Task 6 Rà soát độ phủ và tính nhất quán

Đã đối chiếu cả 7 hình gốc, 52 nhãn và một nhánh nhận hoàn bổ sung. Có 49 đặc tả, gồm 11 nghiệp vụ và 38 hệ thống/hành vi được mô tả. Các bước con được gộp đúng chức năng, không buộc số đặc tả bằng số oval.

| Hình | File gốc | Nhãn gốc | Đặc tả | Điểm cần sửa trên hình |
| --- | --- | --- | --- | --- |
| 3.2 | NghiepVu/DatIn_ThanhToan.png | 6 | NV-3.2-01 đến 06 | Khuyến mãi/giao là tùy chọn; tiếp nhận có tự động; không diễn tả trước–sau bằng include. |
| 3.5 | NghiepVu/HuyDon_HoanTien.png | 6 | NV-3.5-01 đến 05 | Không có duyệt hủy riêng; quá hạn tự động; danh sách hoàn chỉ CEO. |
| 4.2 | HeThong/TaiKhoan_XacThuc.png | 10 | HT-4.2-01 đến 10 | Khách kế thừa Người dùng; phân biệt Google, đăng ký OTP và đặt lại mật khẩu. |
| 4.3 | HeThong/DatInTrenApp.png | 10 | HT-4.3-01 đến 10 | Địa chỉ từ GPS; tuyến Geoapify, cước nội bộ; tạo đơn luôn VIETQR. |
| 4.4 | HeThong/ThanhToanVietQR.png | 6 | HT-4.4-01 đến 06 | QR hiển thị trong App; job độc lập; xếp in có điều kiện; tiền đến muộn không khôi phục đơn. |
| 4.5 | HeThong/TheoDoiDon.png | 7 | HT-4.5-01 đến 07 | Hủy cả hai trạng thái trước in; đổi tên hóa đơn để tránh hiểu là hóa đơn thuế. |
| 4.11 | HeThong/HuyDon_HoanTien.png | 7 | HT-4.11-01 đến 05 | Khách không xác nhận hoàn; chuyển ngân hàng ngoài hệ thống; bổ sung ba trường nhận hoàn. |

## Quan hệ UML và ranh giới cần thống nhất

1. Khái quát hóa: Khách hàng là loại Người dùng hệ thống, mũi tên tam giác rỗng hướng về tác nhân khái quát. Quyền cụ thể vẫn kiểm theo vai trò/kênh.
2. include: từ use case gọi tới hành vi dùng chung bắt buộc. Ví dụ Đăng ký email bao gồm xác minh OTP; không đảo chiều vì hành vi xảy ra trước/sau.
3. extend: từ hành vi bổ sung tới use case nền, có điều kiện và điểm mở rộng. Có thể dùng cho khuyến mãi, giao tùy chọn, đánh giá khi hoàn tất; không nối mọi use case để biến thành flowchart.
4. Thanh toán thành công gọi xử lý xếp in có điều kiện; đơn đã hủy, đã vào quy trình hoặc thiếu tài nguyên có nhánh khác. Không vẽ xếp in như luôn bắt buộc thành công.
5. Hủy quá hạn là job độc lập, không phải quản lý duyệt hoặc extension của nút kiểm tra payOS.
6. Chuyển hoàn thực tế nằm ngoài ranh giới phần mềm. Trong hệ thống chỉ có nhập tài khoản, xem khoản cần hoàn và CEO xác nhận đã chuyển.
7. Phân biệt ba mức: nghiệp vụ cửa hàng, chức năng người dùng và bước kỹ thuật bên trong. Dùng Activity/Sequence cho thứ tự gọi API/SQL, không cố biểu diễn mọi chi tiết bằng quan hệ Use Case.

## Kiểm tra điều kiện dùng chung

- **Tác nhân và quyền:** Khách hàng là một loại Người dùng hệ thống. App cho CUSTOMER/SHIPPER; Web cho STAFF/BRANCH_ADMIN/SUPER_ADMIN. Các hành vi khách trong bộ sơ đồ này chỉ dành CUSTOMER đúng chủ dữ liệu; không suy rộng quyền đặt in cho shipper.
- **Vòng đời đơn:** CHO_XAC_NHAN → DA_XAC_NHAN → DANG_IN → DA_IN → HOAN_THANH khi nhận tại quầy; giao hàng đi qua DANG_GIAO và có thể GIAO_THAT_BAI trước khi thử lại hoặc HOAN_THANH. Hủy chỉ từ hai trạng thái đầu tới DA_HUY.
- **Vòng đời tiền:** CHUA_THANH_TOAN → DA_THANH_TOAN → DA_HOAN_TIEN khi đủ điều kiện. Đơn App luôn VIETQR; COD thuộc quầy. Chờ hoàn là bộ lọc đơn DA_HUY trước in đã trả nhưng chưa hoàn, không phải trạng thái mới.
- **Nguồn xác nhận tiền:** Webhook có chữ ký hoặc API payOS đối soát hợp lệ. URL quay về, thao tác mở QR và lời khách báo đã chuyển không đủ để cập nhật tiền.
- **Xếp in:** Nhận tiền trước, xếp máy sau. Chưa đủ file/máy/vật tư có thể dừng chờ in; đã vào DANG_IN không hủy/hoàn theo luồng trước in. Bàn giao connector, hoàn tất giấy thực tế thuộc phần Tài.
- **Hoàn tiền:** CEO chuyển tiền qua ngân hàng bên ngoài rồi xác nhận; khách nhập lý do/ngân hàng/tài khoản và chỉ xem kết quả. Không có tự động refund ngân hàng, chứng từ hoàn hoặc bước quản lý duyệt hủy riêng.
- **Thông báo và thời gian:** SignalR khi còn kết nối, polling thanh toán khi màn hoạt động; không hứa push khi tắt App. Giờ hẹn là ước tính; chưa có timestamp cho mọi mốc hoặc mô hình SLA đầy đủ.
- **Cách đọc luồng phụ:** Dòng bắt đầu Bước n chỉ điểm phân nhánh. Yêu cầu sửa/nhập lại quay về bước được chỉ; từ chối hoặc kết thúc giữ trạng thái cũ. Lỗi mất phản hồi cần đọc lại trạng thái, không suy rằng yêu cầu chưa được lưu.

## Điểm bàn giao với thành viên khác

- Tài: sau thanh toán/tiếp nhận sang 4.7–4.8; giấy ra và hoàn tất máy không do màn thanh toán quyết định. Sau in sang 4.9 nhận quầy hoặc 3.4/4.10 giao nội bộ.
- Mai: đăng nhập STAFF/BRANCH_ADMIN tự mở/lấy ca riêng mặc định 200.000đ; CEO mở thủ công. Quyền và ca tuân theo phân hệ ca, không tự đóng ca khi đăng xuất.
- Hoàn tiền và trạng thái hủy dùng cùng luật trong App, Web, SQL và các hình 4.4/4.5/4.11. Không có hoàn thường sau khi bắt đầu in.

## Giới hạn và bằng chứng

Đây là rà soát tĩnh tài liệu với source, không phải biên bản nghiệm thu giao dịch payOS, GPS, Gmail, in giấy hay ngân hàng hoàn tiền. Không tự xác nhận các dịch vụ ngoài đã chạy thành công trên mọi thiết bị. Các file nguồn và ảnh gốc được kiểm hash trong quá trình tạo; không đổi để che chỗ chưa khớp.

Bản Word giữ mẫu bảng ba phần nghiệp vụ và mười trường hệ thống; mục lục và số trang được cập nhật khi xuất kiểm tra. Nội dung đã gộp trong DAC_TA_USECASE_MANH_DAY_DU.docx và bản MD cùng tên.