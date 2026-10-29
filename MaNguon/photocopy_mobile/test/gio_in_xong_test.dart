import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/widgets/gio_in_xong.dart';

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);

  testWidgets('Chỉ đơn đang in mới hiện giờ; đầy đủ ngày và giây', (
    tester,
  ) async {
    final finish = DateTime(2026, 10, 28, 16, 32, 45);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: GioInXong(trangThai: 'CHO_XAC_NHAN', duKien: finish),
        ),
      ),
    );
    expect(find.textContaining('16:32:45'), findsNothing);
    expect(find.textContaining('khi máy bắt đầu xử lý'), findsOneWidget);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: GioInXong(trangThai: 'DANG_IN', duKien: finish, giaoHang: true),
        ),
      ),
    );
    expect(find.text('Dự kiến in xong: 16:32:45 28/10/2026'), findsOneWidget);
    expect(find.textContaining('15 giây dự trù'), findsOneWidget);
    expect(find.textContaining('shipper di chuyển'), findsOneWidget);
  });

  testWidgets('Không dùng giờ dự kiến khi đã in xong hoặc hủy', (tester) async {
    for (final state in ['DA_IN', 'DA_HUY', 'DANG_GIAO', 'HOAN_THANH']) {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: GioInXong(trangThai: state, duKien: DateTime(2026)),
          ),
        ),
      );
      expect(find.textContaining('Dự kiến in xong:'), findsNothing);
    }
  });

  testWidgets('Máy chưa ước tính được thì không tự tạo ra thời gian', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(body: GioInXong(trangThai: 'DANG_IN')),
      ),
    );
    expect(find.textContaining('chờ cập nhật giờ in xong'), findsOneWidget);
    expect(find.textContaining('15 giây dự trù'), findsNothing);
  });
}
