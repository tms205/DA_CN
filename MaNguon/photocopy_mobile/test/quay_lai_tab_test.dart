import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/phien.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/screens/trang_chinh_screen.dart';
import 'package:photocopy_mobile/screens/dat_in/chon_file_screen.dart';
import 'package:photocopy_mobile/screens/don_hang/tien_do_screen.dart';
import 'package:photocopy_mobile/screens/thong_bao/thong_bao_screen.dart';
import 'package:photocopy_mobile/screens/shipper/shipper_screen.dart';
import 'huy_don_test.dart' show don;

void main() {
  var exits = 0;
  setUp(() {
    AppTheme.dungGoogleFonts = false;
    Phien.token = null;
    Phien.nguoiDung = NguoiDung.fromJson({
      'maNguoiDung': 1,
      'hoTen': 'Khách thử',
      'email': 'test@gmail.com',
      'tenVaiTro': 'CUSTOMER',
    });
    exits = 0;
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, (call) async {
          if (call.method == 'SystemNavigator.pop') exits++;
          return null;
        });
  });
  tearDown(() {
    Phien.nguoiDung = null;
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, null);
  });
  Future<void> fixture(
    WidgetTester t,
    Future<void> Function() test, {
    String? tab,
  }) async {
    await http.runWithClient(
      () async {
        await t.pumpWidget(MaterialApp(home: TrangChinhScreen(tabBanDau: tab)));
        await t.pumpAndSettle();
        try {
          await test();
          expect(t.takeException(), isNull);
        } finally {
          await t.pumpWidget(const SizedBox());
          await t.pumpAndSettle();
        }
      },
      () => MockClient((r) async {
        Object data = [];
        if (r.url.path == '/api/thong-bao') {
          data = {
            'soChuaDoc': 0,
            'danhSach': [
              {
                'maThongBao': 5,
                'maDonHang': 15,
                'maDonHangHienThi': 'DH00015',
                'tieuDe': 'Kết quả đơn thử',
                'noiDung': 'Xem đơn thử',
                'loaiThongBao': 'DON_HANG',
                'daDoc': true,
                'ngayTao': '2026-11-04T08:00:00',
              },
            ],
          };
        }
        if (r.url.path == '/api/don-hang/15') data = don('DA_IN');
        return http.Response(
          jsonEncode(data),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        );
      }),
    );
  }

  Future<void> back(WidgetTester t) async {
    await t.binding.handlePopRoute();
    await t.pumpAndSettle();
  }

  for (final tab in ['DANH_GIA', 'DON_HANG', 'THONG_BAO', 'TOI']) {
    testWidgets('Back from $tab goes to print tab before considering exit', (
      t,
    ) async {
      await fixture(t, () async {
        await back(t);
        expect(find.byType(ChonFileScreen), findsOneWidget);
        expect(exits, 0);
        expect(find.text('Quay lại lần nữa để thoát ứng dụng'), findsNothing);
      }, tab: tab);
    });
  }
  testWidgets(
    'Home needs two back gestures within two seconds; expired prompt does not exit',
    (t) async {
      await fixture(t, () async {
        await back(t);
        expect(exits, 0);
        expect(find.text('Quay lại lần nữa để thoát ứng dụng'), findsOneWidget);
        await t.pump(const Duration(seconds: 3));
        await t.pumpAndSettle();
        await back(t);
        expect(exits, 0);
        await back(t);
        expect(exits, 1);
      });
    },
  );
  testWidgets('Changing tab resets exit confirmation', (t) async {
    await fixture(t, () async {
      await back(t);
      await t.tap(find.text('Đánh giá'));
      await t.pumpAndSettle();
      await back(t);
      expect(find.byType(ChonFileScreen), findsOneWidget);
      await back(t);
      expect(exits, 0);
      expect(find.text('Quay lại lần nữa để thoát ứng dụng'), findsOneWidget);
    });
  });
  testWidgets(
    'Order opened from notification returns to notifications, then home',
    (t) async {
      await fixture(t, () async {
        await t.tap(find.text('Kết quả đơn thử'));
        await t.pumpAndSettle();
        expect(find.byType(TienDoScreen), findsOneWidget);
        await back(t);
        expect(find.byType(ThongBaoScreen), findsOneWidget);
        expect(exits, 0);
        await back(t);
        expect(find.byType(ChonFileScreen), findsOneWidget);
        expect(exits, 0);
      }, tab: 'THONG_BAO');
    },
  );
  testWidgets('Opening a child clears the previous exit prompt', (t) async {
    await fixture(t, () async {
      await back(t);
      final context = t.element(find.byType(ChonFileScreen));
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => const Scaffold(body: Text('Màn con thử')),
        ),
      );
      await t.pumpAndSettle();
      await back(t);
      expect(find.byType(ChonFileScreen), findsOneWidget);
      await back(t);
      expect(exits, 0);
      expect(find.text('Quay lại lần nữa để thoát ứng dụng'), findsOneWidget);
    });
  });
  testWidgets(
    'Shipper returns to delivery tab and uses same two-back exit rule',
    (t) async {
      Phien.nguoiDung = NguoiDung.fromJson({
        'maNguoiDung': 2,
        'hoTen': 'Shipper thử',
        'email': 'ship@gmail.com',
        'tenVaiTro': 'SHIPPER',
      });
      await fixture(t, () async {
        await back(t);
        expect(find.byType(ShipperScreen), findsOneWidget);
        expect(find.byType(ChonFileScreen), findsNothing);
        await back(t);
        expect(exits, 0);
        await back(t);
        expect(exits, 1);
      }, tab: 'THONG_BAO');
    },
  );
}
