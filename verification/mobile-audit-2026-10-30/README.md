# Kiểm thử tái hiện mobile ngày 30/10/2026

Đây là bộ tái hiện **hiện trạng có vấn đề**. Sáu test pass nghĩa là tái hiện được sáu tình huống trong báo cáo, không phải các lỗi đã được sửa. Dùng HTTP mock/dữ liệu giả, không tạo đơn, gọi API thật, OTP hoặc thanh toán.

Chạy từ thư mục `MaNguon/photocopy_mobile` của repository:

```powershell
flutter test --no-pub --reporter expanded ../../verification/mobile-audit-2026-10-30/reproduce_mobile_test.dart
```

- `reproductions.log`: stdout kết quả sáu test cuối cùng.
- `build-debug.log`: kết quả build Android debug.
- `evidence.json`: phạm vi, kết quả lệnh, hash 43 nguồn Dart và APK.
- Báo cáo đầy đủ: `docs/BAO_CAO_RA_SOAT_MOBILE_2026-10-30.md` tại gốc repository.

Lệnh analyze và 153 test có sẵn đã chạy riêng trước đó, kết quả được ghi nhận trong evidence và báo cáo; không có raw log riêng của hai lệnh đó trong thư mục này. Không trộn test tái hiện vào bộ test mặc định của app; khi sửa lỗi phải đổi các assertion sang kỳ vọng đúng.
