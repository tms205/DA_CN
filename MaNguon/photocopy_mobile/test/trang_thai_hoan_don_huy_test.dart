import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/screens/don_hang/don_hang_screen.dart';
import 'package:photocopy_mobile/widgets/bang_chung_huy_don.dart';
import 'don_hang_nhom_test.dart' show don;

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);
  for (final tt in ['CHUA_THANH_TOAN', 'DA_THANH_TOAN', 'DA_HOAN_TIEN']) {
    testWidgets(
      'Canceled order card keeps cancellation and distinguishes money: $tt',
      (t) async {
        t.view.physicalSize = const Size(320, 900);
        t.view.devicePixelRatio = 1;
        addTearDown(t.view.reset);
        await http.runWithClient(
          () async {
            await t.pumpWidget(
              MaterialApp(
                builder: (ctx, child) => MediaQuery(
                  data: MediaQuery.of(
                    ctx,
                  ).copyWith(textScaler: const TextScaler.linear(1.5)),
                  child: child!,
                ),
                home: const DonHangScreen(),
              ),
            );
            await t.pumpAndSettle();
            expect(find.text('Đã hủy'), findsOneWidget);
            expect(
              find.text('Chờ hoàn tiền'),
              tt == 'DA_THANH_TOAN' ? findsOneWidget : findsNothing,
            );
            expect(
              find.text('Đã hoàn tiền'),
              tt == 'DA_HOAN_TIEN' ? findsOneWidget : findsNothing,
            );
            expect(t.takeException(), isNull);
            await t.pumpWidget(const SizedBox());
          },
          () => MockClient(
            (r) async => http.Response(
              jsonEncode([
                {...don(91, 'DA_HUY'), 'trangThaiThanhToan': tt},
              ]),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            ),
          ),
        );
      },
    );
  }
  testWidgets(
    'Open canceled detail reloads receipt and message when parent receives updated order',
    (t) async {
      var completed = false, calls = 0;
      await http.runWithClient(
        () async {
          Future<void> render(int version) async {
            await t.pumpWidget(
              MaterialApp(
                home: Scaffold(
                  body: SingleChildScrollView(
                    child: BangChungHuyDon(
                      maDonHang: 91,
                      trangThaiThanhToan: completed
                          ? 'DA_HOAN_TIEN'
                          : 'DA_THANH_TOAN',
                      lanCapNhat: version,
                    ),
                  ),
                ),
              ),
            );
            await t.pumpAndSettle();
          }

          await render(1);
          expect(find.text('Chờ hoàn tiền'), findsOneWidget);
          completed = true;
          await render(2);
          expect(find.text('Chờ hoàn tiền'), findsNothing);
          expect(find.text('Lời nhắn từ CEO'), findsOneWidget);
          expect(
            find.text('Đã chuyển đủ tiền, bạn kiểm tra tài khoản nhé.'),
            findsOneWidget,
          );
          expect(find.text('Xem biên lai chuyển khoản'), findsOneWidget);
          expect(calls, 2);
          expect(t.takeException(), isNull);
          await t.pumpWidget(const SizedBox());
        },
        () => MockClient((r) async {
          calls++;
          return http.Response(
            jsonEncode({
              'loai': 'HUY_TRUOC_TIEP_NHAN',
              'guiLuc': '2026-11-04T09:26:22+07:00',
              'anh': [],
              if (completed)
                'bienLaiHoanTien': {
                  'anh': {'maAnh': 'a' * 32},
                  'maChuyenKhoan': 'TEST-ONLY',
                  'lyDo': 'Đã chuyển đủ tiền, bạn kiểm tra tài khoản nhé.',
                  'luc': '2026-11-04T10:00:00+07:00',
                },
            }),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
      );
    },
  );
  test('Completed product orders are not mislabeled canceled', () {
    final d = DonHang.fromJson({
      ...don(1, 'HOAN_THANH'),
      'trangThaiThanhToan': 'DA_HOAN_TIEN',
    });
    expect(d.nhanHoanTienDonHuy, isNull);
    expect(d.nhom, 'HOAN_TAT');
  });
}
