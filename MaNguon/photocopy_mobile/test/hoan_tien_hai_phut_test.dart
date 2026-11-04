import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/widgets/huy_don.dart';
import 'package:photocopy_mobile/widgets/khieu_nai_hoan_tien.dart';
import 'package:photocopy_mobile/screens/don_hang/tien_do_screen.dart';
import 'huy_don_test.dart' show don;

http.Response json(Object value) => http.Response(
  jsonEncode(value),
  200,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);
  test(
    'Server sampled duration gates paid APP without trusting device wall clock',
    () {
      for (final remaining in [0, 120]) {
        final d = DonHangChiTiet.fromJson({
          ...don('CHO_XAC_NHAN'),
          'kenhDatDon': 'APP',
          'trangThaiThanhToan': 'DA_THANH_TOAN',
          'giayChoTiepNhan': remaining,
          'choHuyKhach': remaining > 0,
        });
        expect(d.choPhepHuy, remaining > 0);
      }
      expect(
        DonHangChiTiet.fromJson({
          ...don('DA_XAC_NHAN'),
          'kenhDatDon': 'APP',
          'trangThaiThanhToan': 'DA_THANH_TOAN',
          'giayChoTiepNhan': 120,
        }).choPhepHuy,
        false,
      );
    },
  );

  testWidgets('Open cancellation form keeps counting; expiry disables submit', (
    t,
  ) async {
    var left = 3;
    await t.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NutHuyDon(
            maDonHang: 15,
            daThanhToan: true,
            giayConLai: () => left,
            daHuy: () async {},
          ),
        ),
      ),
    );
    await t.tap(find.text('Hủy đơn & yêu cầu hoàn tiền'));
    await t.pumpAndSettle();
    expect(find.textContaining('Mở form vẫn tính giờ'), findsOneWidget);
    left = 0;
    await t.pump(const Duration(seconds: 1));
    expect(find.text('Đã hết hạn hủy 2 phút.'), findsOneWidget);
    expect(
      t
          .widget<FilledButton>(
            find.widgetWithText(FilledButton, 'Xác nhận hủy'),
          )
          .onPressed,
      isNull,
    );
    await t.tap(find.text('Giữ đơn'));
    await t.pumpAndSettle();
    await t.pumpWidget(const SizedBox());
  });

  testWidgets('Paid APP countdown does not poll API every second', (t) async {
    var requests = 0;
    await http.runWithClient(
      () async {
        await t.pumpWidget(
          const MaterialApp(home: TienDoScreen(maDonHang: 15)),
        );
        await t.pumpAndSettle();
        expect(
          find.textContaining('để hủy và yêu cầu hoàn tiền'),
          findsOneWidget,
        );
        for (var i = 0; i < 4; i++) {
          await t.pump(const Duration(seconds: 1));
        }
        expect(requests, 1);
        expect(t.takeException(), isNull);
        await t.pumpWidget(const SizedBox());
      },
      () => MockClient((r) async {
        requests++;
        return json({
          ...don('CHO_XAC_NHAN'),
          'kenhDatDon': 'APP',
          'trangThaiThanhToan': 'DA_THANH_TOAN',
          'giayChoTiepNhan': 120,
          'choHuyKhach': true,
        });
      }),
    );
  });

  testWidgets(
    'Complaint form asks owner, detailed description and photos on narrow screen',
    (t) async {
      t.view.physicalSize = const Size(360, 780);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      await t.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: NutHuyDon(
              maDonHang: 15,
              daThanhToan: true,
              khieuNaiSanPham: true,
              daHuy: () async {},
            ),
          ),
        ),
      );
      await t.tap(find.text('Yêu cầu hoàn tiền do sản phẩm'));
      await t.pumpAndSettle();
      expect(
        find.widgetWithText(TextFormField, 'Chủ tài khoản ngân hàng'),
        findsOneWidget,
      );
      expect(
        find.widgetWithText(TextFormField, 'Mô tả chi tiết (trên 10 ký tự)'),
        findsOneWidget,
      );
      await t.tap(find.text('Gửi CEO xem xét'));
      await t.pumpAndSettle();
      expect(find.text('Mô tả trên 10 ký tự.'), findsOneWidget);
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
    },
  );

  testWidgets(
    'Customer sees decision history instead of duplicate request button',
    (t) async {
      await http.runWithClient(
        () async {
          await t.pumpWidget(
            const MaterialApp(
              home: Scaffold(body: KhieuNaiHoanTien(maDonHang: 15)),
            ),
          );
          await t.pumpAndSettle();
          expect(
            find.text('CEO đã chấp thuận · Chờ chuyển tiền'),
            findsOneWidget,
          );
          expect(find.text('Yêu cầu hoàn tiền do sản phẩm'), findsNothing);
          expect(find.text('Yêu cầu của bạn'), findsOneWidget);
          expect(find.text('QUYẾT ĐỊNH CỦA CEO'), findsOneWidget);
          expect(find.text('KẾT QUẢ HOÀN TIỀN'), findsNothing);
          expect(find.textContaining('Đã xem ảnh thiếu trang'), findsOneWidget);
          await t.pumpWidget(const SizedBox());
        },
        () => MockClient(
          (r) async => json({
            'choPhepGui': false,
            'hoSo': {
              'trangThai': 'CHAP_THUAN',
              'moTa': 'Sản phẩm bị thiếu trang',
              'nganHang': 'Ngân hàng thử',
              'soTaiKhoan': '123456',
              'chuTaiKhoan': 'Khách thử',
              'anh': [],
              'lichSu': [
                {'trangThai': 'CHAP_THUAN', 'lyDo': 'Đã xem ảnh thiếu trang'},
              ],
            },
          }),
        ),
      );
    },
  );
  testWidgets(
    'Completed refund separates customer request, approval and transfer result at large text',
    (t) async {
      t.view.physicalSize = const Size(320, 900);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.reset);
      await http.runWithClient(
        () async {
          await t.pumpWidget(
            MaterialApp(
              builder: (context, child) => MediaQuery(
                data: MediaQuery.of(
                  context,
                ).copyWith(textScaler: const TextScaler.linear(1.5)),
                child: child!,
              ),
              home: const Scaffold(
                body: SingleChildScrollView(
                  child: KhieuNaiHoanTien(maDonHang: 15),
                ),
              ),
            ),
          );
          await t.pumpAndSettle();
          expect(find.text('Yêu cầu của bạn'), findsOneWidget);
          expect(find.text('QUYẾT ĐỊNH CỦA CEO'), findsOneWidget);
          expect(find.text('KẾT QUẢ HOÀN TIỀN'), findsOneWidget);
          expect(find.text('Đã chấp thuận hoàn tiền'), findsOneWidget);
          expect(find.text('Đã chuyển hoàn tiền'), findsOneWidget);
          expect(find.text('Ảnh 1'), findsOneWidget);
          expect(find.text('Ảnh 3'), findsOneWidget);
          expect(
            find.textContaining('Mã chuyển tiền: TEST-ONLY'),
            findsOneWidget,
          );
          expect(find.byIcon(Icons.fact_check_outlined), findsOneWidget);
          expect(find.byIcon(Icons.check_circle_outline), findsOneWidget);
          expect(t.takeException(), isNull);
          await t.pumpWidget(const SizedBox());
        },
        () => MockClient(
          (r) async => json({
            'choPhepGui': false,
            'hoSo': {
              'trangThai': 'DA_HOAN_TIEN',
              'guiLuc': '2026-11-04T01:00:00Z',
              'moTa': 'Sản phẩm in bị thiếu trang và nhòe chữ',
              'nganHang': 'Ngân hàng thử',
              'soTaiKhoan': '123456',
              'chuTaiKhoan': 'Khách thử',
              'anh': [
                {'maAnh': 'anh1'},
                {'maAnh': 'anh2'},
                {'maAnh': 'anh3'},
              ],
              'lichSu': [
                {
                  'trangThai': 'CHAP_THUAN',
                  'lyDo': 'Đã đối chiếu sản phẩm',
                  'luc': '2026-11-04T01:10:00Z',
                },
                {
                  'trangThai': 'DA_HOAN_TIEN',
                  'lyDo': 'Đã chuyển khoản hoàn tiền',
                  'luc': '2026-11-04T01:20:00Z',
                  'maChuyenKhoan': 'TEST-ONLY',
                },
              ],
            },
          }),
        ),
      );
    },
  );
}
