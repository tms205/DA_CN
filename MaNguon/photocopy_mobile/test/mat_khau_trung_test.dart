import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/screens/toi/doi_mat_khau_screen.dart';
import 'package:photocopy_mobile/screens/xac_thuc/xac_thuc_otp_screen.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/widgets/widgets.dart';

const thongBao =
    'Mật khẩu mới trùng với mật khẩu hiện tại. Vui lòng chọn mật khẩu khác.';

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);

  testWidgets('Đặt lại mật khẩu trùng: hiện lỗi và giữ OTP để sửa mật khẩu', (
    tester,
  ) async {
    var soRequest = 0;
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          const MaterialApp(
            home: XacThucOtpScreen(
              laDangKy: false,
              email: 'test@gmail.com',
              hetHanSauGiay: 600,
              guiLaiSauGiay: 60,
            ),
          ),
        );
        await tester.pump();
        final oNhap = find.byType(TextField);
        await tester.enterText(oNhap.at(0), '123456');
        await tester.pump();
        final nutXacMinh = find.widgetWithText(NutChinh, 'Xác minh OTP');
        await tester.ensureVisible(nutXacMinh);
        await tester.tap(nutXacMinh);
        await tester.pumpAndSettle();
        expect(find.byType(TextField), findsNWidgets(2));
        await tester.enterText(oNhap.at(0), 'Abcdef12');
        await tester.enterText(oNhap.at(1), 'Abcdef12');
        final nutDatLai = find.widgetWithText(NutChinh, 'Đặt lại mật khẩu');
        await tester.ensureVisible(nutDatLai);
        await tester.tap(nutDatLai);
        await tester.pumpAndSettle();
        expect(soRequest, 1);
        expect(find.text(thongBao), findsOneWidget);
        expect(find.text('NHẬP LẠI MẬT KHẨU MỚI'), findsOneWidget);
        await tester.enterText(oNhap.at(0), 'Abcdef13');
        await tester.enterText(oNhap.at(1), 'Abcdef13');
        await tester.ensureVisible(nutDatLai);
        await tester.tap(nutDatLai);
        await tester.pumpAndSettle();
        expect(soRequest, 2);
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        if (req.url.path == '/api/auth/forgot-password/verify-otp') {
          return http.Response(
            jsonEncode({'message': 'Đã xác minh.'}),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }
        expect(req.url.path, '/api/auth/reset-password');
        expect(jsonDecode(req.body)['maOtp'], '123456');
        soRequest++;
        return http.Response(
          jsonEncode({'message': thongBao}),
          409,
          headers: {'content-type': 'application/json; charset=utf-8'},
        );
      }),
    );
  });

  testWidgets('Đổi mật khẩu: hiển thị thông báo trùng do API trả về', (
    tester,
  ) async {
    await http.runWithClient(
      () async {
        await tester.pumpWidget(const MaterialApp(home: DoiMatKhauScreen()));
        final oNhap = find.byType(TextField);
        for (int i = 0; i < 3; i++) {
          await tester.enterText(oNhap.at(i), 'Abcdef12');
        }
        await tester.ensureVisible(find.text('Lưu mật khẩu mới'));
        await tester.tap(find.text('Lưu mật khẩu mới'));
        await tester.pumpAndSettle();
        expect(find.text(thongBao), findsOneWidget);
        expect(find.byType(DoiMatKhauScreen), findsOneWidget);
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        expect(req.url.path, '/api/auth/me/password');
        return http.Response(
          jsonEncode({'message': thongBao}),
          409,
          headers: {'content-type': 'application/json; charset=utf-8'},
        );
      }),
    );
  });
}
