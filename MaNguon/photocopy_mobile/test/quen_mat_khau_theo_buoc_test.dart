import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/screens/xac_thuc/xac_thuc_otp_screen.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/widgets/widgets.dart';

http.Response traVe(int ma, String loi) => http.Response(
  jsonEncode({'message': loi}),
  ma,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

Future<void> moMan(WidgetTester tester) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => Scaffold(
          body: TextButton(
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => const XacThucOtpScreen(
                  laDangKy: false,
                  email: 'test@gmail.com',
                  hetHanSauGiay: 600,
                  guiLaiSauGiay: 0,
                ),
              ),
            ),
            child: const Text('Đăng nhập'),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('Đăng nhập'));
  await tester.pumpAndSettle();
}

Future<void> bamNut(WidgetTester tester, String nhan) async {
  await tester.pump();
  final nut = find.widgetWithText(NutChinh, nhan);
  await tester.ensureVisible(nut);
  await tester.tap(nut);
  await tester.pumpAndSettle();
}

Future<void> xacMinh(WidgetTester tester) async {
  await tester.enterText(find.byType(TextField).first, '123456');
  await bamNut(tester, 'Xác minh OTP');
}

Future<void> nhapMatKhau(WidgetTester tester, String xacNhan) async {
  await tester.enterText(find.byType(TextField).at(0), 'Abcdef13');
  await tester.enterText(find.byType(TextField).at(1), xacNhan);
}

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);

  testWidgets('OTP sai không mở form mật khẩu; OTP đúng mới mở hai ô', (
    tester,
  ) async {
    int soLan = 0;
    await http.runWithClient(
      () async {
        await moMan(tester);
        expect(find.byType(TextField), findsOneWidget);
        expect(find.text('MẬT KHẨU MỚI'), findsNothing);
        await xacMinh(tester);
        expect(find.text('Mã OTP không đúng.'), findsOneWidget);
        expect(find.byType(TextField), findsOneWidget);
        await xacMinh(tester);
        expect(soLan, 2);
        expect(find.text('NHẬP LẠI MẬT KHẨU MỚI'), findsOneWidget);
        expect(find.byType(TextField), findsNWidgets(2));
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        expect(req.url.path, '/api/auth/forgot-password/verify-otp');
        expect(jsonDecode(req.body)['maOtp'], '123456');
        return ++soLan == 1
            ? traVe(400, 'Mã OTP không đúng.')
            : traVe(200, 'Đã xác minh.');
      }),
    );
  });

  testWidgets('Xác nhận bắt buộc và phải khớp; thành công quay về đăng nhập', (
    tester,
  ) async {
    final duongDan = <String>[];
    await http.runWithClient(
      () async {
        await moMan(tester);
        await xacMinh(tester);
        await nhapMatKhau(tester, '');
        await bamNut(tester, 'Đặt lại mật khẩu');
        expect(find.text('Vui lòng nhập lại mật khẩu mới.'), findsOneWidget);
        await nhapMatKhau(tester, 'Abcdef14');
        await bamNut(tester, 'Đặt lại mật khẩu');
        expect(find.text('Mật khẩu nhập lại không khớp.'), findsOneWidget);
        expect(duongDan, ['/api/auth/forgot-password/verify-otp']);
        await nhapMatKhau(tester, 'Abcdef13');
        await bamNut(tester, 'Đặt lại mật khẩu');
        expect(duongDan.last, '/api/auth/reset-password');
        expect(find.byType(XacThucOtpScreen), findsNothing);
        expect(find.text('Đăng nhập'), findsOneWidget);
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        duongDan.add(req.url.path);
        if (req.url.path == '/api/auth/reset-password') {
          expect(jsonDecode(req.body), {
            'email': 'test@gmail.com',
            'maOtp': '123456',
            'matKhauMoi': 'Abcdef13',
          });
        }
        return traVe(200, 'Thành công.');
      }),
    );
  });

  testWidgets('OTP hết hạn lúc lưu phải quay lại xác minh, xóa hai mật khẩu', (
    tester,
  ) async {
    await http.runWithClient(
      () async {
        await moMan(tester);
        await xacMinh(tester);
        await nhapMatKhau(tester, 'Abcdef13');
        await bamNut(tester, 'Đặt lại mật khẩu');
        expect(find.text('Mã OTP đã hết hạn.'), findsOneWidget);
        expect(find.byType(TextField), findsOneWidget);
        expect(
          tester.widget<TextField>(find.byType(TextField)).controller!.text,
          isEmpty,
        );
        await xacMinh(tester);
        for (final o in tester.widgetList<TextField>(find.byType(TextField))) {
          expect(o.controller!.text, isEmpty);
        }
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient(
        (req) async => req.url.path.endsWith('verify-otp')
            ? traVe(200, 'Đã xác minh.')
            : traVe(400, 'Mã OTP đã hết hạn.'),
      ),
    );
  });

  testWidgets('Mất mạng khi xác minh không mở mật khẩu và vẫn nhập lại được', (
    tester,
  ) async {
    int soLan = 0;
    await http.runWithClient(
      () async {
        await moMan(tester);
        await xacMinh(tester);
        expect(find.byType(TextField), findsOneWidget);
        expect(find.text('MẬT KHẨU MỚI'), findsNothing);
        expect(
          tester.widget<TextField>(find.byType(TextField)).controller!.text,
          '123456',
        );
        await bamNut(tester, 'Xác minh OTP');
        expect(find.byType(TextField), findsNWidgets(2));
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        if (++soLan == 1) throw http.ClientException('Mất kết nối');
        return traVe(200, 'Đã xác minh.');
      }),
    );
  });

  testWidgets(
    'Quay lại và gửi mã mới phải xác minh lại trước khi đặt mật khẩu',
    (tester) async {
      final duongDan = <String>[];
      await http.runWithClient(
        () async {
          await moMan(tester);
          await xacMinh(tester);
          await nhapMatKhau(tester, 'Abcdef13');
          await tester.ensureVisible(find.text('Quay lại nhập mã OTP'));
          await tester.tap(find.text('Quay lại nhập mã OTP'));
          await tester.pumpAndSettle();
          expect(find.byType(TextField), findsOneWidget);
          await tester.ensureVisible(find.text('Gửi lại mã'));
          await tester.tap(find.text('Gửi lại mã'));
          await tester.pumpAndSettle();
          expect(duongDan.last, '/api/auth/forgot-password');
          expect(find.text('MẬT KHẨU MỚI'), findsNothing);
          await xacMinh(tester);
          for (final o in tester.widgetList<TextField>(
            find.byType(TextField),
          )) {
            expect(o.controller!.text, isEmpty);
          }
          await tester.pumpWidget(const SizedBox());
        },
        () => MockClient((req) async {
          duongDan.add(req.url.path);
          if (req.url.path.endsWith('/forgot-password')) {
            return http.Response(
              jsonEncode({
                'message': 'Đã gửi mã mới.',
                'hetHanSauGiay': 600,
                'guiLaiSauGiay': 60,
              }),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          return traVe(200, 'Đã xác minh.');
        }),
      );
    },
  );

  testWidgets('Không gửi lặp hoặc mở form mật khẩu trong lúc chờ xác minh', (
    tester,
  ) async {
    final cho = Completer<http.Response>();
    int soLan = 0;
    await http.runWithClient(
      () async {
        await moMan(tester);
        await tester.enterText(find.byType(TextField), '123456');
        final nut = find.widgetWithText(NutChinh, 'Xác minh OTP');
        await tester.ensureVisible(nut);
        await tester.tap(nut);
        await tester.pump();
        await tester.tap(find.byType(NutChinh));
        await tester.pump();
        expect(soLan, 1);
        expect(find.text('MẬT KHẨU MỚI'), findsNothing);
        cho.complete(traVe(200, 'Đã xác minh.'));
        await tester.pumpAndSettle();
        expect(find.byType(TextField), findsNWidgets(2));
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) {
        soLan++;
        return cho.future;
      }),
    );
  });

  for (double rong in [320, 390]) {
    testWidgets('Hai bước không tràn ở $rong px, chữ lớn và bàn phím mở', (
      tester,
    ) async {
      tester.view.physicalSize = Size(rong * 3, 844 * 3);
      tester.view.devicePixelRatio = 3;
      tester.view.viewInsets = const FakeViewPadding(bottom: 280 * 3);
      tester.platformDispatcher.textScaleFactorTestValue = 1.5;
      addTearDown(tester.view.reset);
      addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
      await http.runWithClient(() async {
        await moMan(tester);
        expect(tester.takeException(), isNull);
        await xacMinh(tester);
        expect(tester.takeException(), isNull);
        await nhapMatKhau(tester, 'Abcdef13');
        await tester.ensureVisible(
          find.widgetWithText(NutChinh, 'Đặt lại mật khẩu'),
        );
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
      }, () => MockClient((req) async => traVe(200, 'Đã xác minh.')));
    });
  }
}
