# Bàn giao Google Sign-In – 13/10/2026

## Kết quả và phần còn cần người dùng

Đã triển khai code Web + Android và áp dụng migration theo xác nhận. Chưa đăng nhập Google thật: `.env` hiện `GoogleAuth__Enabled=false`, Web Client ID chưa điền. Cần người dùng tạo client Web/Android cùng Cloud project và làm theo [hướng dẫn cấu hình](HUONG_DAN_DANG_NHAP_GOOGLE.md), rồi khởi động lại API/app.

Không xóa/sửa tài khoản cũ, không tự tạo tài khoản test trong database thật, không đổi quyền. Web chỉ nhân sự có sẵn; app khách hàng/shipper có sẵn hoặc đăng ký mới CUSTOMER. Không triển khai native iOS/Flutter Web.

## Cách hoạt động

1. GET `api/auth/google/config` chỉ trả trạng thái bật và Web Client ID công khai.
2. Web dùng GIS popup/nút Google chuẩn; Android dùng native `google_sign_in`, serverClientId là Web Client ID.
3. Client gửi ID token bằng POST, API xác minh RS256, issuer, audience, hạn token, Gmail đã xác minh. Không lấy email/vai trò do client tự khai làm danh tính.
4. Nếu đã liên kết, tìm tài khoản bằng Google `sub`; nếu chưa, ghép lần đầu bằng Gmail đã xác minh và kiểm quyền/trạng thái trước khi tạo liên kết.
5. Web không tạo tài khoản nhân sự. App với Gmail mới nhận vé ngẫu nhiên 10 phút, bổ sung tên/điện thoại/điều khoản; API tạo CUSTOMER và liên kết trong cùng transaction.
6. Đăng nhập thành công đi qua luồng JWT/phiên hiện có; STAFF Web tự mở/tái sử dụng ca 200.000đ như password login.

Vé đăng ký nằm trong RAM, dùng thành công một lần, mất khi API restart. Trùng tài khoản/phone/yêu cầu đồng thời trả lỗi để sửa hoặc quay lại đăng nhập. Tài khoản Google mới chưa có mật khẩu nội bộ: dùng Quên mật khẩu + OTP để thiết lập nếu muốn đăng nhập bằng mật khẩu.

## Các file đã thao tác trong nhiệm vụ này

Đường dẫn dưới tính từ thư mục gốc `CNTT-KLCN031_Code_CSDL_30-09-2026_KhongMD`.

| File / nhóm file | Thay đổi |
|---|---|
| `Database/011-LienKetTaiKhoanGoogle.sql` | Migration idempotent thêm bảng, PK GoogleSubject, UNIQUE MaNguoiDung, FK NguoiDung; đã chạy. |
| `Database/01_TaoBang.sql` | Bổ sung bảng tương ứng cho bộ SQL cài mới; không chạy lại toàn bộ SQL gốc vào DB đang dùng. |
| `MaNguon/PhotocopyChain/.env` | Thêm hai khóa Google mặc định tắt; file riêng bị Git ignore. |
| `PhotocopyApi/Auth/GoogleTokenVerifier.cs` | Kiểm token bằng JWT/OIDC Microsoft đang có; metadata HTTPS và cache/refresh khóa Google, không thêm dependency backend. |
| `PhotocopyApi/Auth/QuyTacDangNhap.cs` | Dùng chung quy tắc vai trò/trạng thái cho password và Google. |
| `PhotocopyApi/Controllers/AuthController.cs` | Partial class, DI verifier; tách bước hoàn tất đăng nhập chung để giữ ca/phiên/quyền. |
| `PhotocopyApi/Controllers/GoogleAuthController.cs` | Config, đăng nhập Web/app, đăng ký khách hàng bằng vé; SQL tham số/transaction/khóa unique. |
| `PhotocopyApi/Program.cs` | Đăng ký verifier và rate limit Google 30 yêu cầu/10 phút/IP; thông báo 429 đúng luồng. |
| `PhotocopyShared/Dtos/Auth/GoogleAuthDtos.cs` | DTO token/config/hồ sơ/vé; validation điện thoại/tên/điều khoản. |
| `PhotocopyAdmin/Components/DangNhapGoogle.razor` | Nút Google/config/lỗi/retry/busy/dispose; callback gửi token tới API. |
| `PhotocopyAdmin/wwwroot/js/google-auth.js` | Nạp GIS chính thức, renderButton, callback bỏ qua khi trang bị đóng. |
| `PhotocopyAdmin/Pages/DangNhap.razor` | Thay placeholder bằng Google thật, dùng chung lưu phiên và chuyển trang; chặn gửi lặp. |
| `PhotocopyAdmin/wwwroot/css/app-ui-v2.css`, `wwwroot/index.html` | CSS nhỏ cho vùng nút/chú thích Google và phiên bản cache; giữ bố cục/màu hiện có. |
| `MaNguon/photocopy_mobile/pubspec.yaml`, `pubspec.lock` | Thêm google_sign_in 7.2.0 và dependency native do pub resolve; không thêm Firebase. |
| `photocopy_mobile/lib/google_auth.dart` | Config từ API, initialize native một lần, lấy ID token/gửi API, xử lý hủy/cấu hình/mất mạng. |
| `photocopy_mobile/lib/screens/xac_thuc/nut_google.dart` | Nút dùng chung, báo lỗi, busy, chuyển bổ sung hồ sơ hoặc phiên app. |
| `photocopy_mobile/lib/screens/xac_thuc/bo_sung_google_screen.dart` | Form tên/phone/điều khoản, back, submit, lưu JWT hệ thống. |
| `photocopy_mobile/lib/screens/xac_thuc/dang_nhap_screen.dart`, `dang_ky_screen.dart` | Thay nút placeholder, giữ đường password/OTP cũ; mỗi trang chỉ một nút Google. |
| `photocopy_mobile/lib/kiem_tra_du_lieu.dart` | Kiểm vé/tên/phone/điều khoản trước gửi API. |
| `PhotocopyApi.Tests/GoogleTokenTests.cs`, `GoogleAuthHttpTests.cs`, `GoogleWebTests.cs` | Token RSA thật với khóa fixture, quyền, endpoint/config/validation, render Web. |
| `photocopy_mobile/test/google_validation_test.dart`, `google_screen_test.dart` | Validation, config tắt/mất mạng, hủy, không gửi lặp, form/Back/vé lỗi, chữ lớn/bàn phím. |
| `verification/google-auth.cjs`, `dashboard-hang-doi.cjs` | Playwright cho Google và bổ sung config fixture vào regression hiện có. |
| `docs/plans/2026-10-13-dang-nhap-google.md`, hai tài liệu bàn giao/hướng dẫn | Quyết định, kế hoạch, thiết lập Cloud và giới hạn nghiệm thu. |

Các nhóm `PhotocopyApi`, `PhotocopyShared`, `PhotocopyAdmin`, `verification` ở bảng trên nằm trong `MaNguon/PhotocopyChain`. Các sửa đổi danh mục/dashboard có sẵn trong working tree được giữ nguyên, không gộp chúng thành tính năng Google.

## Kiểm chứng đã chạy

- .NET toàn solution: **349 passed, 17 skipped, 0 failed**. Test CSDL nghiệp vụ opt-in cũ không được tự bật để tránh ghi dữ liệu thật.
- Riêng Google .NET: **24 passed**, gồm token sai chữ ký/audience/issuer/hết hạn/không signed/email chưa verified/không Gmail/thiếu sub, role/lock, config/endpoint/DTO, Web render. HTTP config bật + token giả bị 401; vé không có bị 400 trước SQL.
- Flutter toàn bộ: **98 passed**; `flutter analyze`: **No issues found**.
- APK debug đã build thành công, đường dẫn `MaNguon/photocopy_mobile/build/app/outputs/flutter-apk/app-debug.apk`.
- Web Debug build: 0 warning/error. Release publish thành công; thiếu workload wasm-tools nên chưa có tối ưu WASM native bổ sung (không phải lỗi compile).
- Regression dashboard/danh mục/sidebar/header/footer/auth: các trường hợp trước Google đã qua ở 390, 768, 1024, 1366, 1920px. Lần chạy kết hợp ban đầu dừng tại test Google vì automation bấm checkbox custom 0px; đã sửa test bấm nhãn như người dùng (không sửa UI/không force click).
- Google browser chạy lại riêng **PASS ở 390 và 1366px**: tắt config, SDK lỗi/retry, API từ chối, Enter bàn phím, callback muộn sau rời trang, chuyển đúng returnUrl và ghi nhớ sessionStorage. Google SDK/API được thay fixture; không đăng nhập tài khoản thật.
- SELECT database thật: bảng có PK/UNIQUE/FK; 0 liên kết tại lúc kiểm tra. Không có tài khoản/đơn/tiền thật bị thay đổi bởi test.
- Không xuất Google credential/JWT thật/secret trong log, URL hay tài liệu; `.env` không được đưa vào Git/wwwroot.

## Cần nghiệm thu sau khi cấu hình Cloud

Đăng nhập Google thật Web (CEO/quản lý/STAFF + ca), Android (khách hàng/shipper), đăng ký Gmail mới, tài khoản khóa/ngoài quyền, đăng nhập lại không tạo trùng. Chưa kiểm được popup/certificate/token Google thật hoặc callback liên kết/tạo tài khoản SQL end-to-end trước khi có client; không dùng build/test fixture làm bằng chứng cho những phần này.

Nếu triển khai nhiều instance: thay RAM ticket bằng kho chia sẻ; nếu phát hành Android: dùng certificate release riêng, cấu hình SHA-1/OAuth tương ứng. Các việc đó chưa nằm trong phạm vi đồ án chạy cá nhân hiện tại.
