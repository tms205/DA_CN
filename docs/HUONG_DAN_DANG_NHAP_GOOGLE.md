# Cấu hình đăng nhập Google – Web và Android

## 1. Phần đã triển khai

- Web quản trị: chỉ đăng nhập nhân sự đã được cấp tài khoản (CEO, quản lý chi nhánh, nhân viên). Không có tự đăng ký quản trị.
- App Android: đăng nhập khách hàng/shipper có sẵn. Gmail mới xác thực Google rồi bổ sung tên, số điện thoại 10 số và đồng ý điều khoản; tài khoản tạo mới luôn là CUSTOMER.
- Lần đầu: Gmail Google đã xác minh được ghép với email tài khoản hiện có. Các lần sau dùng `sub` bất biến, không lấy quyền từ email/client.
- Tài khoản bị khóa vẫn bị từ chối. Nhân viên đăng nhập Google trên Web vẫn tự mở/tái sử dụng ca như đăng nhập mật khẩu, tiền đầu ca mặc định 200.000đ.
- Không lưu mật khẩu Google, không xin quyền đọc Gmail, không cần Firebase hay Client Secret. Mật khẩu Gmail ứng dụng đang dùng gửi OTP không phải cấu hình Google Sign-In.
- Migration `Database/011-LienKetTaiKhoanGoogle.sql` đã áp dụng vào PhotocopyChain theo xác nhận; thêm một bảng liên kết, không sửa/xóa tài khoản cũ. SQL gốc `01_TaoBang.sql` cũng có bảng này cho cài đặt mới.

## 2. Tạo Google Cloud project và thông tin ứng dụng

Mở [Google Cloud Console](https://console.cloud.google.com/), tạo/chọn project riêng cho PhotocopyChain.

Trong **Google Auth Platform** (hoặc **APIs & Services → OAuth consent screen**, tùy giao diện):

1. **Branding**: tên PhotocopyChain, email hỗ trợ và email liên hệ của bạn.
2. **Audience**: chọn External nếu dùng các tài khoản Gmail cá nhân. Giữ Testing khi làm đồ án; nếu giao diện yêu cầu test users, thêm Gmail dùng kiểm thử.
3. Chỉ cần danh tính cơ bản: `openid`, email, profile. Không thêm Gmail API, Drive API hay quyền nhạy cảm.
4. Chưa cần đưa ứng dụng ra Production. Nếu sau này công khai, hoàn thiện chính sách riêng tư/thông tin ứng dụng và tuân thủ yêu cầu Google.

Tên menu có thể thay đổi; xem [hướng dẫn Client ID chính thức](https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid).

## 3. Tạo OAuth client loại Web application

Trong **Clients → Create client**, chọn **Web application**, đặt tên `PhotocopyChain Web`.

Ở **Authorized JavaScript origins**, thêm từng dòng:

```text
http://localhost
http://localhost:5064
```

Nếu mở Web bằng tên miền HTTPS khác, thêm đúng origin của Web đó (gồm scheme, hostname, port nếu có). Không thêm đường dẫn `/dang-nhap`; origin của API/tunnel payOS không thay thế origin của Web.

Luồng hiện tại dùng GIS popup nhận ID token, **không có OAuth redirect callback để điền Authorized redirect URIs**. Không thêm URL kết quả thanh toán payOS vào Google.

Lưu lại **Client ID của Web**, có dạng:

```text
1234567890-xxxxxxxxxxxxxxxx.apps.googleusercontent.com
```

Client ID là mã công khai; không cần chép/gửi Client Secret. Web và app dùng **cùng Web Client ID** để API kiểm audience.

## 4. Tạo OAuth client Android

Cùng Google Cloud project, tạo client thứ hai, loại **Android**.

Thông tin đã kiểm tra trên máy hiện tại:

```text
Package name: com.photocopychain.photocopy_mobile
SHA-1 (debug): 00:71:DB:0E:ED:57:DB:EC:7C:B4:C3:16:2C:CE:B8:CF:E4:7E:08:78
```

Đặt tên `PhotocopyChain Android Debug`, nhập chính xác package và SHA-1 rồi lưu. Client Android phải ở **cùng project** với client Web.

Không điền Android Client ID vào `.env`. App lấy Web Client ID qua API rồi truyền làm `serverClientId`; client Android/package/SHA-1 giúp Google nhận diện APK. Cách này không cần `google-services.json`.

SHA-1 trên chỉ dành cho keystore debug trên máy này. Máy khác/certificate release khác phải đăng ký SHA-1 tương ứng. Dự án hiện dùng debug signing cả khi cấu hình release; cần thiết lập khóa release riêng trước khi phát hành thật, không dùng debug key làm khóa sản phẩm.

Tham khảo [cấu hình plugin Android chính thức](https://pub.dev/packages/google_sign_in_android).

## 5. Bật trong file .env

File cấu hình riêng nằm ở:

```text
C:\Users\USER\Desktop\DA_CN\CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD\MaNguon\PhotocopyChain\.env
```

Mình đã thêm sẵn hai dòng Google, mặc định tắt. **Sửa hai dòng đó, không thêm trùng khóa và không thay các cấu hình database/payOS/Gmail khác**:

```dotenv
GoogleAuth__Enabled=true
GoogleAuth__WebClientId=CLIENT_ID_LOAI_WEB_CUA_BAN.apps.googleusercontent.com
```

Thay toàn bộ giá trị minh họa bằng Client ID Web thật. Giữ `.env` ngoài Git; không đưa vào `wwwroot`, không gửi ảnh/nội dung file vì các phần khác chứa bí mật.

Khi không có giao dịch đang in/thanh toán, chạy lại:

```text
MaNguon\scripts\chay_web.cmd
```

API đọc `.env` lúc khởi động. F5 trên Web không nạp cấu hình API mới. Nếu lần chạy CMD không thay được tiến trình API cũ, dừng cửa sổ API cũ bằng Ctrl+C khi không có giao dịch, rồi chạy CMD lại.

Mở [cấu hình Google công khai của API](http://localhost:5268/api/auth/google/config). Phải trả `daBat: true` và đúng Web Client ID. Endpoint này không trả mật khẩu/secret.

## 6. Chạy lại Android

Vì có thêm plugin native, **dừng phiên Flutter và chạy lại**, không chỉ hot reload:

```powershell
cd "C:\Users\USER\Desktop\DA_CN\CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD\MaNguon\photocopy_mobile"
flutter pub get
adb reverse tcp:5268 tcp:5268
flutter run
```

Nếu có nhiều thiết bị, thêm `-s DEVICE_ID` cho adb, `-d DEVICE_ID` cho Flutter. Wi-Fi debugging cần thiết bị còn kết nối và reverse đúng thiết bị; Google Sign-In vẫn cần Internet trên điện thoại.

Khi đổi Client ID lúc app đang chạy, đóng/mở lại app để khởi tạo Google đúng cấu hình.

## 7. Cách kiểm tra thực tế

1. Web tại `http://localhost:5064/dang-nhap`: bấm Google, chọn Gmail trùng email nhân sự có sẵn. Đúng quyền thì vào Web; STAFF dùng ca tự động như trước.
2. Gmail chưa được cấp nhân sự: Web phải báo chưa có tài khoản nhân sự, không tạo quyền CEO/nhân viên.
3. App: chọn Gmail khách hàng/shipper có sẵn để đăng nhập. Nhân sự không được vào app khách hàng chỉ vì đăng nhập bằng Google.
4. App với Gmail mới: phải hiện Hoàn tất đăng ký, tên có thể sửa, số điện thoại bắt buộc 10 số bắt đầu 0, điều khoản bắt buộc. Chưa bấm hoàn tất hợp lệ thì chưa tạo tài khoản.
5. Hủy chọn tài khoản Google: vẫn ở màn cũ, không báo sai mật khẩu, nút thao tác lại được.
6. Khóa tài khoản bằng chức năng quản trị: lần đăng nhập Google tiếp theo bị từ chối; không thử khóa tài khoản CEO duy nhất.
7. Đăng xuất rồi đăng nhập Google lại: dùng cùng tài khoản, không tạo trùng người dùng/liên kết.

Tài khoản Google mới không có mật khẩu nội bộ. Muốn dùng thêm mật khẩu: thực hiện Quên mật khẩu → OTP email → đặt mật khẩu mới hai lần. Tài khoản cũ vẫn giữ mật khẩu cũ.

Theo quy tắc email đã thống nhất, tính năng này chỉ nhận **Gmail Google đã xác minh**. Tài khoản cũ như `sv1@huit.edu.vn` vẫn đăng nhập mật khẩu; chưa triển khai Google Workspace/ghép email bên thứ ba.

## 8. Khi gặp lỗi

| Hiện tượng | Cách kiểm tra |
|---|---|
| Chưa cấu hình Google | Hai khóa `.env`, đúng Client ID loại Web và đã khởi động lại API; GET config phải `daBat: true`. |
| origin_mismatch / popup không mở | Authorized JavaScript origins trùng URL Web đang mở; cho phép popup, kiểm Internet/trình chặn, Ctrl+F5. |
| Android báo cấu hình/canceled dù không chủ động hủy | Package, SHA-1, Web Client ID, client cùng Cloud project; bản APK đúng certificate, không nhầm ID Android làm serverClientId. |
| Google báo access blocked | Kiểm consent screen/Audience, test users và chính sách Google của tài khoản. |
| Vé đăng ký hết hạn | Vé dùng một lần, tối đa 10 phút, mất khi API khởi động lại. Quay lại bấm Google để lấy vé mới. |
| Tài khoản đã tồn tại khi đăng ký | Có yêu cầu khác vừa tạo; quay lại Google để đăng nhập, không đăng ký lại. |
| 401 token / không kết nối Google | API cần Internet để tải/cache khóa Google; đồng hồ máy đúng giờ; client đúng audience. Không dùng token tự tạo/token hết hạn. |
| Không có quyền Web/app | Vai trò do database quyết định. Gmail đăng nhập không tự nâng quyền. |

## 9. Bảo mật và giới hạn

- API kiểm chữ ký RS256, issuer, audience, thời hạn, Gmail verified; dùng `sub` làm khóa liên kết. Cách kiểm ID token tham khảo [Google backend verification](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token).
- Token Google chỉ gửi POST để đổi lấy JWT hệ thống, không đưa vào URL/localStorage; chỉ JWT hệ thống dùng cơ chế phiên hiện có. Bảng liên kết không chứa Google token/password.
- Rate limit Google 30 yêu cầu/10 phút/IP; unique/FK và transaction bảo vệ liên kết/tạo tài khoản. Vé đăng ký giữ trong RAM của API, phù hợp một tiến trình đồ án; nhiều instance cần kho ticket chia sẻ.
- Đã build và kiểm thử tự động; chưa xác thực Google thật vì chưa có Client ID/config Cloud của bạn. Mô phỏng UI/test token không thay thế các bước kiểm thử thật ở mục 7.
- Native iOS và Flutter Web chưa nằm trong nghiệm thu này. Plugin được chọn theo [google_sign_in 7.2.0](https://pub.dev/packages/google_sign_in), Android không dùng WebView đăng nhập.
