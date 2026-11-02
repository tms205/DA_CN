import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/screens/danh_gia/danh_gia_screen.dart';
import 'package:photocopy_mobile/theme.dart';

KetQuaApi ok(dynamic value) => KetQuaApi()
  ..thanhCong = true
  ..duLieu = value;
List<Map<String, dynamic>> branches() => [
  for (int i = 1; i <= 2; i++)
    {'maChiNhanh': i, 'tenChiNhanh': 'Chi nhánh $i', 'diaChi': 'Địa chỉ $i'},
];
Map<String, dynamic> result(String text, {bool more = false, int count = 1}) =>
    {
      'diemTrungBinh': count == 0 ? 0 : 4.5,
      'soLuotDanhGia': count,
      'coTrangSau': more,
      'danhGia': count == 0
          ? []
          : [
              {
                'tenKhachHang': 'M***',
                'soSao': 5,
                'nhanXet': text,
                'ngayDanhGia': '2026-11-02T08:00:00',
              },
            ],
    };
void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);
  testWidgets('Chọn đúng chi nhánh và xem điểm, phản hồi tại màn 320px', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final requests = <String>[];
    await tester.pumpWidget(
      MaterialApp(
        home: DanhGiaScreen(
          taiDuLieu: (path) async {
            requests.add(path);
            return ok(
              path.endsWith('chi-nhanh')
                  ? branches()
                  : result(
                      path.contains('/1/') ? 'In rõ đẹp' : 'Phục vụ nhanh',
                    ),
            );
          },
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('4.5/5'), findsOneWidget);
    expect(find.text('In rõ đẹp'), findsOneWidget);
    await tester.tap(find.byType(DropdownButtonFormField<int>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Chi nhánh 2').last);
    await tester.pumpAndSettle();
    expect(find.text('Phục vụ nhanh'), findsOneWidget);
    expect(find.text('In rõ đẹp'), findsNothing);
    expect(requests.last, '/api/danh-muc/chi-nhanh/2/danh-gia?trang=1');
    expect(tester.takeException(), isNull);
  });
  testWidgets('Phản hồi chi nhánh cũ trả muộn không ghi đè chi nhánh mới', (
    tester,
  ) async {
    final slow = Completer<KetQuaApi>();
    await tester.pumpWidget(
      MaterialApp(
        home: DanhGiaScreen(
          taiDuLieu: (path) async {
            if (path.endsWith('chi-nhanh')) return ok(branches());
            if (path.contains('/1/')) return slow.future;
            return ok(result('Phản hồi chi nhánh hai'));
          },
        ),
      ),
    );
    await tester.pump();
    await tester.pump();
    await tester.tap(find.byType(DropdownButtonFormField<int>));
    await tester.pump(const Duration(milliseconds: 300));
    await tester.tap(find.text('Chi nhánh 2').last);
    await tester.pumpAndSettle();
    slow.complete(ok(result('Dữ liệu cũ')));
    await tester.pumpAndSettle();
    expect(find.text('Phản hồi chi nhánh hai'), findsOneWidget);
    expect(find.text('Dữ liệu cũ'), findsNothing);
  });
  testWidgets('Xem thêm và làm mới loại phản hồi đã bị ẩn khỏi màn hình', (
    tester,
  ) async {
    bool hidden = false;
    final requests = <String>[];
    await tester.pumpWidget(
      MaterialApp(
        home: DanhGiaScreen(
          taiDuLieu: (path) async {
            requests.add(path);
            if (path.endsWith('chi-nhanh')) return ok(branches());
            if (hidden) return ok(result('', count: 0));
            return ok(
              result(
                path.endsWith('trang=2') ? 'Trang kế tiếp' : 'Phản hồi ban đầu',
                more: !path.endsWith('trang=2'),
              ),
            );
          },
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Xem thêm đánh giá'));
    await tester.tap(find.text('Xem thêm đánh giá'));
    await tester.pumpAndSettle();
    expect(find.text('Trang kế tiếp'), findsOneWidget);
    expect(requests.last, endsWith('trang=2'));
    hidden = true;
    await tester.tap(find.byTooltip('Làm mới đánh giá'));
    await tester.pumpAndSettle();
    expect(find.text('Chưa có đánh giá'), findsOneWidget);
    expect(find.text('Phản hồi ban đầu'), findsNothing);
    expect(find.text('Trang kế tiếp'), findsNothing);
  });
  testWidgets('Lỗi tải không bị hiển thị thành chi nhánh không có đánh giá', (
    tester,
  ) async {
    bool failed = true;
    await tester.pumpWidget(
      MaterialApp(
        home: DanhGiaScreen(
          taiDuLieu: (path) async {
            if (path.endsWith('chi-nhanh')) return ok(branches());
            return failed
                ? (KetQuaApi()..loi = 'Không kết nối được máy chủ')
                : ok(result('Đã kết nối lại'));
          },
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Không kết nối được máy chủ'), findsOneWidget);
    expect(find.text('Chưa có đánh giá'), findsNothing);
    failed = false;
    await tester.tap(find.text('Thử lại'));
    await tester.pumpAndSettle();
    expect(find.text('Đã kết nối lại'), findsOneWidget);
  });
}
