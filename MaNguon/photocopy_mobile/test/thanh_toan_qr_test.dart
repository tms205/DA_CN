import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:photocopy_mobile/screens/dat_in/thanh_toan_screen.dart';
import 'package:photocopy_mobile/theme.dart';

class MayChuThanhToan {
  int soLanXem = 0;
  int soLanTaoLink = 0;
  bool daTra = false;
  bool loiMang = false;
  bool hetHan = false;
  bool daHuy = false;
  bool thieuQr = false;
  bool saiDon = false;
  bool saiTien = false;
  Completer<void>? doiPhanHoi;
  String phuongThuc = 'VIETQR';

  Future<http.Response> traLoi(http.Request req) async {
    Object data;
    int ma = 200;
    if (req.url.path == '/api/thanh-toan/15') {
      soLanXem++;
      if (loiMang) throw const SocketException('Mất mạng test');
      data = {
        'maDonHang': 15,
        'maDonHangHienThi': 'DH00015',
        'phuongThuc': phuongThuc,
        'trangThai': daTra ? 'DA_THANH_TOAN' : 'CHUA_THANH_TOAN',
        'soTien': 7400,
        'trangThaiDonHang': daHuy ? 'DA_HUY' : 'CHO_XAC_NHAN',
        'hanThanhToan': DateTime.now()
            .add(Duration(minutes: hetHan ? -1 : 10))
            .toIso8601String(),
      };
    } else if (req.url.path == '/api/thanh-toan/15/payos') {
      soLanTaoLink++;
      data = {
        'checkoutUrl': 'https://pay.payos.vn/web/test-link',
        'hanThanhToan': DateTime.now()
            .add(const Duration(minutes: 10))
            .toIso8601String(),
        'maDonHang': saiDon ? 16 : 15,
        'soTien': saiTien ? 7500 : 7400,
        'qrCode': thieuQr
            ? ''
            : '00020101021238570010A0000007270127000697042201131133666688880208999988885303704540474005802VN62110807DH000156304AAAA',
        'maNganHang': '970422',
        'soTaiKhoan': '0000000000',
        'chuTaiKhoan': 'TEST',
        'noiDungChuyenKhoan': 'DH00015',
      };
    } else {
      ma = 404;
      data = {'message': 'API không mong đợi trong test'};
    }
    if (req.method == 'GET' && doiPhanHoi != null) {
      final cho = doiPhanHoi!;
      doiPhanHoi = null;
      await cho.future;
    }
    return http.Response(
      jsonEncode(data),
      ma,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() async {
    var thuMuc = File(Platform.resolvedExecutable).parent;
    while (!Directory(
      '${thuMuc.path}/bin/cache/artifacts/material_fonts',
    ).existsSync()) {
      if (thuMuc.parent.path == thuMuc.path) {
        throw StateError('Không tìm thấy Flutter SDK.');
      }
      thuMuc = thuMuc.parent;
    }
    final bytes = await File(
      '${thuMuc.path}/bin/cache/artifacts/material_fonts/roboto-regular.ttf',
    ).readAsBytes();
    for (final ten in ['Roboto', 'monospace']) {
      await (FontLoader(
        ten,
      )..addFont(Future.value(ByteData.sublistView(bytes)))).load();
    }
  });
  setUp(() => AppTheme.dungGoogleFonts = false);

  Future<void> thu(
    WidgetTester tester,
    MayChuThanhToan api,
    Future<void> Function() kiemTra, {
    double rong = 390,
    double tiLeChu = 1,
    bool chupAnh = false,
  }) async {
    tester.view.physicalSize = Size(rong * 3, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await http.runWithClient(() async {
      final khungAnh = GlobalKey();
      await tester.pumpWidget(
        RepaintBoundary(
          key: khungAnh,
          child: MaterialApp(
            builder: (context, child) => MediaQuery(
              data: MediaQuery.of(
                context,
              ).copyWith(textScaler: TextScaler.linear(tiLeChu)),
              child: child!,
            ),
            home: const ThanhToanScreen(maDonHang: 15),
          ),
        ),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 200));
      try {
        await kiemTra();
        // Chỉ xuất ảnh khi người chạy test yêu cầu; dùng dữ liệu giả, không quét để trả tiền.
        final fileAnh = Platform.environment['PHOTOCOPY_QR_PREVIEW'];
        if (chupAnh && fileAnh != null) {
          await tester.runAsync(() async {
            final khung =
                khungAnh.currentContext!.findRenderObject()
                    as RenderRepaintBoundary;
            final anh = await khung.toImage(pixelRatio: 2);
            final bytes = await anh.toByteData(format: ui.ImageByteFormat.png);
            await File(fileAnh).writeAsBytes(bytes!.buffer.asUint8List());
            anh.dispose();
          });
        }
      } finally {
        await tester.pumpWidget(const SizedBox());
      }
    }, () => MockClient(api.traLoi));
  }

  for (final rong in [320.0, 390.0]) {
    testWidgets(
      'QR ngay trong app, chưa nhận tiền không báo thành công ($rong)',
      (tester) async {
        final api = MayChuThanhToan();
        await thu(
          tester,
          api,
          () async {
            expect(find.byKey(const ValueKey('payos-qr')), findsOneWidget);
            final qr = tester.widget<QrImageView>(
              find.byKey(const ValueKey('payos-qr')),
            );
            expect(qr.size, lessThanOrEqualTo(260));
            expect(qr.backgroundColor, Colors.white);
            expect(find.text('Thanh toán thành công'), findsNothing);
            expect(find.text('0000000000'), findsOneWidget);
            expect(find.text('DH00015'), findsWidgets);
            expect(api.soLanTaoLink, 1);
            expect(tester.takeException(), isNull);
          },
          rong: rong,
          chupAnh: rong == 390,
        );
      },
    );
  }

  testWidgets('API xác nhận tiền thì tự hiện kết quả, không tạo link lặp', (
    tester,
  ) async {
    final api = MayChuThanhToan();
    await thu(tester, api, () async {
      api.daTra = true;
      await tester.pump(const Duration(seconds: 5));
      await tester.pump();
      expect(find.text('Thanh toán thành công'), findsOneWidget);
      expect(find.byKey(const ValueKey('payos-qr')), findsNothing);
      expect(api.soLanTaoLink, 1);
      final soLan = api.soLanXem;
      await tester.pump(const Duration(seconds: 10));
      expect(api.soLanXem, soLan);
    });
  });

  testWidgets('Quay lại từ ngân hàng kiểm tra ngay, không polling trong nền', (
    tester,
  ) async {
    final api = MayChuThanhToan();
    await thu(tester, api, () async {
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.paused);
      final soLan = api.soLanXem;
      await tester.pump(const Duration(seconds: 10));
      expect(api.soLanXem, soLan);
      api.daTra = true;
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));
      expect(find.text('Thanh toán thành công'), findsOneWidget);
    });
  });

  testWidgets('Mất mạng giữ QR, không báo đã trả; nút kiểm tra phục hồi', (
    tester,
  ) async {
    final api = MayChuThanhToan();
    await thu(tester, api, () async {
      api.loiMang = true;
      await tester.pump(const Duration(seconds: 5));
      await tester.pump();
      expect(find.byKey(const ValueKey('payos-qr')), findsOneWidget);
      expect(find.text('Thanh toán thành công'), findsNothing);
      api.loiMang = false;
      api.daTra = true;
      await tester.ensureVisible(find.text('Kiểm tra thanh toán'));
      await tester.tap(find.text('Kiểm tra thanh toán'));
      await tester.pump();
      expect(find.text('Thanh toán thành công'), findsOneWidget);
    });
  });

  for (final trangThai in ['het-han', 'da-huy']) {
    testWidgets('Đơn $trangThai không tạo link hoặc hiện QR', (tester) async {
      final api = MayChuThanhToan()
        ..hetHan = trangThai == 'het-han'
        ..daHuy = trangThai == 'da-huy';
      await thu(tester, api, () async {
        expect(api.soLanTaoLink, 0);
        expect(find.byKey(const ValueKey('payos-qr')), findsNothing);
        expect(find.text('Thanh toán thành công'), findsNothing);
      });
    });
  }

  testWidgets('Link cũ thiếu QR chỉ cho checkout dự phòng', (tester) async {
    final api = MayChuThanhToan()..thieuQr = true;
    await thu(tester, api, () async {
      expect(find.byKey(const ValueKey('payos-qr')), findsNothing);
      expect(
        find.text('Link cũ chưa có dữ liệu QR trong app.'),
        findsOneWidget,
      );
      expect(find.text('Mở trang thanh toán payOS'), findsOneWidget);
    });
  });

  testWidgets('QR khác mã đơn không được hiển thị', (tester) async {
    final api = MayChuThanhToan()..saiDon = true;
    await thu(tester, api, () async {
      expect(find.byKey(const ValueKey('payos-qr')), findsNothing);
      expect(
        find.text('Dữ liệu QR không khớp đơn hàng. Vui lòng thử lại.'),
        findsOneWidget,
      );
    });
  });

  testWidgets('COD giữ màn đặt đơn, không xin QR', (tester) async {
    final api = MayChuThanhToan()..phuongThuc = 'COD';
    await thu(tester, api, () async {
      expect(find.text('Đặt đơn thành công'), findsOneWidget);
      expect(find.text('Thanh toán thành công'), findsNothing);
      expect(api.soLanTaoLink, 0);
    });
  });

  testWidgets('QR sai số tiền không được hiển thị', (tester) async {
    final api = MayChuThanhToan()..saiTien = true;
    await thu(tester, api, () async {
      expect(find.byType(QrImageView), findsNothing);
      expect(
        find.text('Dữ liệu QR không khớp đơn hàng. Vui lòng thử lại.'),
        findsOneWidget,
      );
    });
  });

  testWidgets('Chữ phóng 150% trên 320dp không tràn bố cục', (tester) async {
    await thu(
      tester,
      MayChuThanhToan(),
      () async {
        expect(find.byType(QrImageView), findsOneWidget);
        await tester.ensureVisible(find.text('Kiểm tra thanh toán'));
        expect(tester.takeException(), isNull);
      },
      rong: 320,
      tiLeChu: 1.5,
    );
  });

  testWidgets(
    'Resume khi request cũ chưa xong vẫn đối soát lại, không gửi chồng',
    (tester) async {
      final api = MayChuThanhToan();
      await thu(tester, api, () async {
        final cho = Completer<void>();
        api.doiPhanHoi = cho;
        await tester.pump(const Duration(seconds: 5));
        final soLan = api.soLanXem;
        tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.paused);
        api.daTra = true;
        tester.binding.handleAppLifecycleStateChanged(
          AppLifecycleState.resumed,
        );
        await tester.pump();
        expect(api.soLanXem, soLan);
        cho.complete();
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 100));
        expect(api.soLanXem, soLan + 1);
        expect(find.text('Thanh toán thành công'), findsOneWidget);
      });
    },
  );
}
