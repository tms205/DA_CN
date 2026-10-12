import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/screens/xac_thuc/xac_thuc_otp_screen.dart';
import 'package:photocopy_mobile/theme.dart';

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);

  // Figma vẽ khung 390; máy thật hay hẹp hơn (Xiaomi 2412DPC0AG ≈ 375) — 6 ô OTP không được tràn.
  for (double rong in [390.0, 375.0, 320.0]) {
    testWidgets('Màn OTP không tràn ở bề ngang $rong', (tester) async {
      tester.view.physicalSize = Size(rong * 3, 844 * 3);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);

      await tester.pumpWidget(const MaterialApp(
        home: XacThucOtpScreen(laDangKy: true, email: 'khachhang@gmail.com', hetHanSauGiay: 300, guiLaiSauGiay: 60),
      ));
      await tester.pump();

      expect(tester.takeException(), isNull);
      expect(find.text('Nhập mã xác thực'), findsOneWidget);

      // Gỡ màn hình để hủy đồng hồ đếm ngược trước khi test kết thúc
      await tester.pumpWidget(const SizedBox());
    });
  }
}
