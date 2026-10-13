import 'dart:async';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/google_auth.dart';
import 'package:photocopy_mobile/screens/xac_thuc/nut_google.dart';
import 'package:photocopy_mobile/screens/xac_thuc/bo_sung_google_screen.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/widgets/widgets.dart';

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);

  testWidgets('Cấu hình tắt không mở native Google và báo dùng mật khẩu', (
    tester,
  ) async {
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          const MaterialApp(home: Scaffold(body: NutGoogle())),
        );
        await tester.tap(find.text('Tiếp tục với Google'));
        await tester.pumpAndSettle();
        expect(
          find.text(
            'Chưa cấu hình Google. Bạn vẫn có thể đăng nhập bằng mật khẩu.',
          ),
          findsOneWidget,
        );
        expect(find.text('Tiếp tục với Google'), findsOneWidget);
      },
      () => MockClient((req) async {
        expect(req.url.path, '/api/auth/google/config');
        return http.Response('{"daBat":false,"webClientId":""}', 200);
      }),
    );
  });

  testWidgets('Hủy không tạo phiên; đang xử lý không gửi lặp', (tester) async {
    final cho = Completer<KetQuaApi?>();
    int soLan = 0;
    final busy = <bool>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NutGoogle(
            xacThuc: (_) {
              soLan++;
              return cho.future;
            },
            dangXuLyChanged: busy.add,
          ),
        ),
      ),
    );
    await tester.tap(find.text('Tiếp tục với Google'));
    await tester.pump();
    expect(find.text('Đang xử lý Google…'), findsOneWidget);
    await tester.tap(find.byType(NutPhu));
    await tester.pump();
    expect(soLan, 1);
    cho.complete(null);
    await tester.pumpAndSettle();
    expect(busy, [true, false]);
    expect(find.byType(HopThongBao), findsNothing);
    expect(find.byType(BoSungGoogleScreen), findsNothing);
  });

  testWidgets('Khách mới mở bổ sung hồ sơ, Back vẫn dùng được nút Google', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NutGoogle(
            xacThuc: (_) async => KetQuaApi()
              ..thanhCong = true
              ..duLieu = {
                'canBoSungThongTin': true,
                'veDangKy': 'a' * 64,
                'email': 'test@gmail.com',
                'hoTen': 'Khách Google',
              },
          ),
        ),
      ),
    );
    await tester.tap(find.text('Tiếp tục với Google'));
    await tester.pumpAndSettle();
    expect(find.byType(BoSungGoogleScreen), findsOneWidget);
    expect(find.text('test@gmail.com'), findsOneWidget);
    expect(
      tester.widget<TextField>(find.byType(TextField).first).controller!.text,
      'Khách Google',
    );
    await tester.ensureVisible(find.text('Quay lại chọn tài khoản Google'));
    await tester.tap(find.text('Quay lại chọn tài khoản Google'));
    await tester.pumpAndSettle();
    expect(find.text('Tiếp tục với Google'), findsOneWidget);
  });

  testWidgets(
    'Hồ sơ bắt buộc điện thoại/điều khoản; vé hết hạn không tạo phiên',
    (tester) async {
      int soLan = 0;
      await http.runWithClient(
        () async {
          await tester.pumpWidget(
            MaterialApp(
              home: BoSungGoogleScreen(
                veDangKy: 'a' * 64,
                email: 'test@gmail.com',
                hoTen: 'Khách Google',
                ghiNho: false,
              ),
            ),
          );
          Future<void> gui() async {
            await tester.ensureVisible(
              find.widgetWithText(NutChinh, 'Hoàn tất đăng ký'),
            );
            await tester.tap(find.widgetWithText(NutChinh, 'Hoàn tất đăng ký'));
            await tester.pumpAndSettle();
          }

          await gui();
          expect(soLan, 0);
          await tester.enterText(find.byType(TextField).at(1), '0912345678');
          await gui();
          expect(soLan, 0);
          await tester.ensureVisible(find.byType(CheckboxListTile));
          await tester.tap(find.byType(CheckboxListTile));
          await gui();
          expect(soLan, 1);
          expect(
            find.text('Phiên đăng ký Google đã hết hạn hoặc đã dùng.'),
            findsOneWidget,
          );
          expect(find.byType(BoSungGoogleScreen), findsOneWidget);
        },
        () => MockClient((req) async {
          soLan++;
          expect(req.url.path, '/api/auth/google/register');
          expect(jsonDecode(req.body), {
            'veDangKy': 'a' * 64,
            'hoTen': 'Khách Google',
            'soDienThoai': '0912345678',
            'dongYDieuKhoan': true,
            'ghiNho': false,
          });
          return http.Response(
            jsonEncode({
              'message': 'Phiên đăng ký Google đã hết hạn hoặc đã dùng.',
            }),
            400,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
      );
    },
  );

  for (final rong in [320.0, 390.0]) {
    testWidgets('Bổ sung Google không tràn $rong px với bàn phím và chữ lớn', (
      tester,
    ) async {
      tester.view.physicalSize = Size(rong * 3, 844 * 3);
      tester.view.devicePixelRatio = 3;
      tester.view.viewInsets = const FakeViewPadding(bottom: 280 * 3);
      tester.platformDispatcher.textScaleFactorTestValue = 1.5;
      addTearDown(tester.view.reset);
      addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
      await tester.pumpWidget(
        MaterialApp(
          home: BoSungGoogleScreen(
            veDangKy: 'a' * 64,
            email: 'test@gmail.com',
            hoTen: 'Khách Google',
            ghiNho: true,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.ensureVisible(
        find.widgetWithText(NutChinh, 'Hoàn tất đăng ký'),
      );
      expect(tester.takeException(), isNull);
    });
  }

  test('API cấu hình mất mạng trả lỗi trước khi mở native', () async {
    await http.runWithClient(() async {
      final kq = await GoogleAuth.tiepTuc(true);
      expect(kq?.thanhCong, false);
      expect(kq?.loi, isNotEmpty);
    }, () => MockClient((req) async => throw http.ClientException('offline')));
  });
}
