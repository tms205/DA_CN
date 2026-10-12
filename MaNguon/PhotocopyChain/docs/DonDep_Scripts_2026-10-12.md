# Gom scripts thành một launcher — 12/10/2026

## Kết quả

Thư mục `MaNguon/scripts` chỉ còn **chay_web.cmd**. Không còn phụ thuộc vào các file PowerShell phụ.

- Gộp kiểm tra kết nối database và khởi động lại đúng dịch vụ của dự án vào launcher.
- Build API, Web Admin, máy in ảo; gặp lỗi thì dừng, không chạy bản build cũ.
- Chờ API và Web trả HTTP 200 rồi mở cả Web Admin và Swagger.
- Giữ chế độ chạy Flutter qua USB và giữ kết nối `adb reverse`.
- Android được chạy qua ổ đĩa ánh xạ tạm không dấu nếu đường dẫn nguồn có tiếng Việt; không di chuyển nguồn. Chỉ dùng ổ chưa có và gỡ ổ do launcher tạo khi thoát app.
- Bỏ 15 file phụ khỏi thư mục scripts, giữ bản lưu bên ngoài dự án để khôi phục.
- Không thay đổi cấu trúc database, không xóa dữ liệu hay sửa logic nghiệp vụ.

## Cách chạy

1. Bấm đúp `MaNguon/scripts/chay_web.cmd` để chạy Web, API và máy in ảo.
2. Web: http://localhost:5064 — Swagger: http://localhost:5268/swagger.
3. Muốn chạy mobile: bật Web/API trước, cắm điện thoại và cho phép gỡ lỗi USB, rồi chạy `chay_web.cmd app` trong thư mục scripts.
4. Thoát mobile bằng `q` hoặc Ctrl+C. Dừng Web/API/máy in bằng Ctrl+C trong từng cửa sổ dịch vụ.
5. Gmail, database và PayOS vẫn lấy từ `MaNguon/PhotocopyChain/.env`. Sửa cấu hình tại đó rồi chạy lại launcher; không gửi nội dung file này cho người khác.
6. Thanh toán PayOS thật vẫn cần cloudflared đang chạy và URL tunnel đúng; launcher không tự đổi webhook.

## File thay đổi

- `MaNguon/scripts/chay_web.cmd`: launcher duy nhất, gồm phần CMD và PowerShell trong cùng file.
- `MaNguon/PhotocopyChain/CauHinh/DocFileEnv.cs`: chỉ sửa thông báo thiếu .env, không hướng dẫn chạy file cấu hình đã bỏ.
- `MaNguon/PhotocopyChain/verification/kiem_thu_launcher.ps1`: kiểm thử launcher, tách khỏi thư mục scripts.
- Bản lưu scripts cũ: [mở thư mục lưu trữ](<C:/Users/USER/Desktop/Báo cáo tuần/_LuuTru_Scripts_2026-10-12_085159>). Có cả launcher cũ; xem README trước khi khôi phục.

Các báo cáo cũ là lịch sử thay đổi; sử dụng hướng dẫn chạy trong tài liệu này thay cho các lệnh script cấu hình/chạy app cũ.

## Đã kiểm tra

- Chạy thực tế launcher hai lần, gồm khởi động lại các dịch vụ đang chạy.
- Build API, Web và máy in ảo: 0 lỗi, 0 cảnh báo.
- Web đăng nhập, Swagger và Swagger JSON: HTTP 200; API, Blazor DevServer và simulator đều đang chạy.
- Kiểm thử launcher qua PowerShell Windows: thành công, gồm nhận diện đúng tiến trình, parser .env, một file duy nhất, chọn ổ tạm không đè ổ có sẵn.
- .NET: 106/106 bài kiểm thử không phụ thuộc dữ liệu SQL thành công. Không chạy 10 bài kiểm thử cần dữ liệu SQL riêng.
- Flutter: 13/13 bài kiểm thử thành công.
- Android debug APK: build thành công qua ổ ánh xạ tạm; ổ kiểm thử đã được gỡ.
- Đối chiếu hash 45 file được bảo vệ (.env, SQL và mã trong mobile/lib): không đổi.

Chưa kiểm tra chạy/cài app trực tiếp trên điện thoại USB trong đợt này. Không thực hiện thanh toán thật, gửi OTP hay nghiệm thu lại mọi màn hình nghiệp vụ.

