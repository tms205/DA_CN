import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/screens/xac_thuc/dang_nhap_screen.dart';
import 'package:photocopy_mobile/theme.dart';

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);

  Future<void> moManDangNhap(WidgetTester tester) async {
    // Kích thước điện thoại 390×844 như khung Figma
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(const MaterialApp(home: DangNhapScreen()));
  }

  testWidgets('Màn đăng nhập hiển thị đúng nội dung Figma', (tester) async {
    await moManDangNhap(tester);

    expect(find.text('Chào mừng trở lại'), findsOneWidget);
    expect(find.text('EMAIL HOẶC SỐ ĐIỆN THOẠI'), findsOneWidget);
    expect(find.text('Đăng nhập'), findsOneWidget);
    expect(find.text('Đăng ký ngay'), findsOneWidget);
  });

  testWidgets('Bấm đăng nhập khi để trống thì báo lỗi, không gọi API', (tester) async {
    await moManDangNhap(tester);

    await tester.tap(find.text('Đăng nhập'));
    await tester.pump();

    expect(find.text('Vui lòng nhập email/số điện thoại và mật khẩu.'), findsOneWidget);
  });
}
