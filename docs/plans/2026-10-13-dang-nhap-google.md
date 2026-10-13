# Google Sign-In Implementation Plan

> Thực hiện tuần tự trong phiên hiện tại, giữ các sửa đổi trước đó; không tự commit hay tạo worktree.

**Goal:** Đăng nhập Google ở Web nhân sự và đăng ký/đăng nhập Google ở app Android, giữ phân quyền và ca làm việc.

**Architecture:** GIS trên Web, google_sign_in trên Flutter, API xác minh ID token bằng thư viện Microsoft JWT/OIDC hiện có. Lưu liên kết bằng subject Google và mã NguoiDung trong bảng riêng. Giữ JWT/phiên/mật khẩu hiện tại, chỉ dùng Gmail Google đã xác minh để liên kết lần đầu; tài khoản cũ khác đuôi tiếp tục đăng nhập mật khẩu.

**Tech Stack:** .NET 8, Dapper/SQL Server, Blazor WASM, Flutter Android, Google Identity Services, Microsoft JWT/OIDC hiện có, google_sign_in.

## Yêu cầu đã xác nhận và quyết định

- Người dùng xác nhận cả Web/app; Web chỉ nhân sự có sẵn. App chỉ khách hàng/shipper có sẵn; tài khoản đăng ký mới luôn CUSTOMER.
- Đã đồng ý tạo và áp dụng migration thêm LienKetTaiKhoanGoogle. Không sửa/xóa dữ liệu cũ; không lưu mật khẩu Google.
- Lựa chọn bảng liên kết thay cho ghép email đơn thuần để dùng subject bất biến; không thêm Firebase hay hệ thống token thứ hai.
- Giả định: quy mô đồ án hiện tại; chỉ đăng nhập online; lỗi mạng/Google hủy phải phục hồi được. Không xin Gmail API hoặc dữ liệu thư, không thêm refresh-token overhaul.
- Token Google/mật khẩu/secret không log, không URL/localStorage. App vẫn dùng secure storage cho JWT hệ thống; Google token chỉ tồn tại trong bộ nhớ.
- Chọn thư viện JWT/OIDC có sẵn thay Google.Apis.Auth (Google cho phép thư viện JWT phổ thông). Cache khóa Google qua ConfigurationManager, kiểm chữ ký RS256, issuer/audience/expiry; không thêm dependency backend.
- Khách mới xác thực Google rồi nhập số điện thoại 10 chữ số, tên và đồng ý điều khoản. Chưa đủ thông tin thì chưa tạo NguoiDung.
- Bằng chứng Google thực tế cần OAuth client của người dùng; test mô phỏng không được tuyên bố đã xác thực Google thật.

## Thiết kế UI

- Industrial/utilitarian theo giao diện sẵn có, navy, Be Vietnam Pro và token hiện có. DFII 13 (impact 3, fit 5, feasibility 5, performance 5, consistency risk 5).
- Web dùng nút Google chuẩn GIS thay nút placeholder; lỗi cấu hình rõ ràng, không thay layout login.
- Mobile checkpoint: Android/Flutter, Navigator stack hiện có, phone-first cho khách và shipper; chưa triển khai native iOS trên Windows.
- MFRI 3: clarity 5 + accessibility 4 − interaction 2 − performance 1 − online dependence 3. Thêm kiểm layout/text scale và xử lý hủy/lỗi/mất mạng.
- Đã đọc mobile-design-thinking, touch-psychology, mobile-performance, mobile-backend, mobile-testing, mobile-debugging, platform-android.
- Áp dụng: CTA tối thiểu 48dp, bước bổ sung hồ sơ riêng dễ Back, dispose controller/mounted check. Tránh WebView đăng nhập Google, lưu Google token lâu dài, đăng ký tự cấp vai trò.

## Task 1: kiểm thử trước

Files: PhotocopyApi.Tests/GoogleAuthTests.cs, GoogleAuthHttpTests.cs; test của app và browser login.

1. Test endpoint chưa cấu hình trả trạng thái rõ ràng, input sai bị MVC chặn.
2. Test token sai/audience/issuer/expiry/email unverified không tạo phiên.
3. Test logic role/status: customer không vào Web; nhân sự không vào app; khóa luôn bị từ chối; ca staff dùng luồng chung.
4. Chạy dotnet test PhotocopyApi.Tests --filter FullyQualifiedName~Google --artifacts-path thư_mục_tạm để xác nhận RED.

## Task 2: migration và backend

Files: Database/011-LienKetTaiKhoanGoogle.sql, Database/01_TaoBang.sql; AuthController.cs/partial GoogleAuthController.cs; Auth/Google*.cs; Shared/Dtos/Auth/GoogleAuthDtos.cs; Program.cs/csproj.

1. Bảng GoogleSubject VARCHAR(255) khóa chính, MaNguoiDung FK + UNIQUE; thời điểm liên kết; không thêm filtered index vào bảng cũ.
2. Có transaction/khóa unique để liên kết và đăng ký đồng thời không tạo bản ghi trùng.
3. Xác minh Google bằng audience WebClientId cấu hình, chữ ký/issuer/expiry; kiểm Gmail verified trước lần ghép đầu.
4. GET google/config chỉ trả Client ID công khai và trạng thái bật. POST admin/google, google, google/register dùng role từ CSDL, không nhận role từ client.
5. Trả ticket ngẫu nhiên ngắn hạn cho khách mới, giữ danh tính đã xác minh ở cache; tiêu thụ ticket thành công một lần. Đăng ký lỗi input không làm mất cơ hội sửa.
6. Dùng chung HoanTatDangNhap với password login để giữ trạng thái khóa/role/sp_MoCa và JWT.
7. Áp dụng migration bằng cấu hình máy, chỉ báo kết quả/tên bảng; không in .env. Không tạo tài khoản thử thật.

## Task 3: Web

Files: Components/DangNhapGoogle.razor, wwwroot/js/google-auth.js, Pages/DangNhap.razor.

1. Nạp GIS từ Google khi bật; renderButton chuẩn, callback chỉ gửi credential tới API.
2. Đăng nhập thành công dùng Phien/NavigateTo hiện tại; lỗi hiển thị alert; dispose ref/callback khi rời trang.
3. Không dùng token trong returnUrl, không chuyển sang trang tùy ý.
4. Browser fixture kiểm cấu hình tắt/bật, token API lỗi, callback thành công, mobile-width và rời trang.

## Task 4: Android

Files: photocopy_mobile/pubspec.yaml, lib/google_auth.dart, screens/xac_thuc/*google*.dart, dang_nhap_screen.dart, dang_ky_screen.dart, kiem_tra_du_lieu.dart, tests.

1. GoogleSignIn initialize một lần với serverClientId = Web Client ID; dùng native authenticate, không cần Firebase.
2. Nút Google trên login và đăng ký; kết quả thành công hoặc mở màn bổ sung hồ sơ theo response API.
3. Lưu phiên bằng Phien.batDau và mở realtime/trang chính như trước; hủy Google không tạo phiên.
4. Widget/validation test, flutter analyze, flutter test, build APK debug. Đăng nhập Google thật cần client Android đúng package/SHA.

## Task 5: cấu hình và bàn giao

- GoogleAuth__Enabled=false, GoogleAuth__WebClientId= trong .env (không công khai secret).
- Hướng dẫn Google Cloud External/Testing, test users, origins, Android package/SHA-1, serverClientId dùng chung Web ID.
- Native Android phải build/chạy lại sau khi thêm plugin, không chỉ hot reload.
- Full .NET/app/browser test; ghi chính xác test nào dùng fixture, test nào dùng CSDL/Google thật; không restart API khi còn giao dịch.
