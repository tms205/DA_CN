import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/phien.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/widgets/bang_chung_huy_don.dart';
import 'package:photocopy_mobile/widgets/khieu_nai_hoan_tien.dart';

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);
  for (final canceled in [false, true]) {
    testWidgets(
      'Customer sees receipt and opens authenticated image: canceled=$canceled',
      (t) async {
        final bill = {
          'anh': {'maAnh': 'a' * 32},
          'maChuyenKhoan': 'TEST-ONLY',
          'lyDo': 'Cửa hàng đã chuyển hoàn, bạn vui lòng kiểm tra tài khoản.',
          'luc': '2026-11-04T01:00:00Z',
        };
        final old = Phien.token;
        Phien.token = 'test-receipt-only';
        addTearDown(() => Phien.token = old);
        var images = 0;
        await http.runWithClient(
          () async {
            await t.pumpWidget(
              MaterialApp(
                home: Scaffold(
                  body: SingleChildScrollView(
                    child: canceled
                        ? const BangChungHuyDon(maDonHang: 15)
                        : const KhieuNaiHoanTien(maDonHang: 15),
                  ),
                ),
              ),
            );
            await t.pumpAndSettle();
            if (canceled) {
              expect(
                find.text(
                  'Cửa hàng đã chuyển hoàn, bạn vui lòng kiểm tra tài khoản.',
                ),
                findsOneWidget,
              );
            }
            await t.ensureVisible(find.text('Xem biên lai chuyển khoản'));
            await t.tap(find.text('Xem biên lai chuyển khoản'));
            await t.pumpAndSettle();
            expect(find.text('Biên lai hoàn tiền'), findsOneWidget);
            expect(find.byType(InteractiveViewer), findsOneWidget);
            expect(images, 1);
            expect(t.takeException(), isNull);
            await t.pumpWidget(const SizedBox());
          },
          () => MockClient((r) async {
            expect(r.headers['Authorization'], 'Bearer test-receipt-only');
            if (r.url.path.contains('/bien-lai-hoan-tien/')) {
              images++;
              return http.Response.bytes(
                base64Decode(
                  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==',
                ),
                200,
                headers: {'content-type': 'image/png'},
              );
            }
            final data = canceled
                ? {
                    'loai': 'HUY_TRUOC_TIEP_NHAN',
                    'anh': [],
                    'bienLaiHoanTien': bill,
                  }
                : {
                    'choPhepGui': false,
                    'hoSo': {
                      'trangThai': 'DA_HOAN_TIEN',
                      'moTa': 'Thiếu trang cần đối chiếu',
                      'nganHang': 'Ngân hàng thử',
                      'soTaiKhoan': '123456',
                      'chuTaiKhoan': 'Khách thử',
                      'anh': [],
                      'lichSu': [
                        {
                          'trangThai': 'DA_HOAN_TIEN',
                          'lyDo': 'Đã chuyển hoàn tiền',
                          'bienLai': bill,
                        },
                      ],
                    },
                  };
            return http.Response(
              jsonEncode(data),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }),
        );
      },
    );
  }
}
