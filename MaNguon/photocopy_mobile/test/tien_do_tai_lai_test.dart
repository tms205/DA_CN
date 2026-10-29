import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/screens/don_hang/tien_do_screen.dart';
import 'package:photocopy_mobile/theme.dart';

http.Response duLieuDon() => http.Response(
  jsonEncode({
    'maDonHang': 57,
    'maDonHangHienThi': 'DH00057',
    'maChiNhanh': 1,
    'tenChiNhanh': 'Chi nhánh test',
    'hinhThucNhan': 'TAI_QUAY',
    'trangThaiDonHang': 'DA_IN',
    'ngayDat': '2026-10-26T08:00:00',
    'phuongThucThanhToan': 'VIETQR',
    'trangThaiThanhToan': 'DA_THANH_TOAN',
    'tienHang': 5000,
    'tongTien': 5000,
    'dongIn': [],
    'tienDo': [],
  }),
  200,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);

  testWidgets('Các lần tải trùng chỉ có một request đang chạy và một lượt bù', (
    tester,
  ) async {
    int calls = 0, active = 0, maxActive = 0;
    final cho = Completer<void>();
    Future<http.Response> traLoi(http.Request req) async {
      expectSync(req.url.path, '/api/don-hang/57');
      calls++;
      active++;
      if (active > maxActive) maxActive = active;
      if (calls > 1 && !cho.isCompleted) await cho.future;
      active--;
      return duLieuDon();
    }

    await http.runWithClient(() async {
      await tester.pumpWidget(
        const MaterialApp(home: TienDoScreen(maDonHang: 57)),
      );
      await tester.pumpAndSettle();
      final refresh = tester.widget<RefreshIndicator>(
        find.byType(RefreshIndicator),
      );
      Future<void> taiLai() =>
          http.runWithClient(refresh.onRefresh, () => MockClient(traLoi));
      final tasks = [taiLai(), taiLai(), taiLai()];
      await tester.pump();
      // Hoàn thành cả khi assertion thất bại để test không giữ Future treo.
      cho.complete();
      await tester.pumpAndSettle();
      await Future.wait(tasks);
      expect(maxActive, 1);
      expect(calls, 3); // tải đầu + lượt refresh + đúng một lượt bù
      expect(find.text('DH00057'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
    }, () => MockClient(traLoi));
  });

  testWidgets('Rời trang bỏ lượt tải bù đang chờ', (tester) async {
    int calls = 0;
    final cho = Completer<void>();
    Future<http.Response> traLoi(http.Request req) async {
      calls++;
      if (calls > 1) await cho.future;
      return duLieuDon();
    }

    await http.runWithClient(() async {
      await tester.pumpWidget(
        const MaterialApp(home: TienDoScreen(maDonHang: 57)),
      );
      await tester.pumpAndSettle();
      final refresh = tester.widget<RefreshIndicator>(
        find.byType(RefreshIndicator),
      );
      Future<void> taiLai() =>
          http.runWithClient(refresh.onRefresh, () => MockClient(traLoi));
      final tasks = [taiLai(), taiLai()];
      await tester.pump();
      await tester.pumpWidget(const SizedBox());
      cho.complete();
      await tester.pumpAndSettle();
      await Future.wait(tasks);
      expect(calls, 2); // tải đầu và request đã bắt đầu trước khi rời trang
      expect(tester.takeException(), isNull);
    }, () => MockClient(traLoi));
  });

  testWidgets('Lỗi mạng không giữ khóa tải và vẫn thử lại được', (
    tester,
  ) async {
    int calls = 0;
    Future<http.Response> traLoi(http.Request req) async {
      if (++calls == 2) throw http.ClientException('offline fixture');
      return duLieuDon();
    }

    await http.runWithClient(() async {
      await tester.pumpWidget(
        const MaterialApp(home: TienDoScreen(maDonHang: 57)),
      );
      await tester.pumpAndSettle();
      final refresh = tester.widget<RefreshIndicator>(
        find.byType(RefreshIndicator),
      );
      await http.runWithClient(refresh.onRefresh, () => MockClient(traLoi));
      await tester.pumpAndSettle();
      expect(find.text('Không tải được đơn hàng'), findsOneWidget);
      await http.runWithClient(refresh.onRefresh, () => MockClient(traLoi));
      await tester.pumpAndSettle();
      expect(calls, 3);
      expect(find.text('DH00057'), findsOneWidget);
      expect(find.text('Không tải được đơn hàng'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    }, () => MockClient(traLoi));
  });
}
