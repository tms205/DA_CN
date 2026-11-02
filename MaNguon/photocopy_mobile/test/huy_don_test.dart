import 'package:photocopy_mobile/api.dart';
import 'dart:io';
import 'bang_chung_giao_test.dart' show AnhThu;
import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/screens/dat_in/thanh_toan_screen.dart';
import 'package:photocopy_mobile/screens/don_hang/tien_do_screen.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/widgets/huy_don.dart';

http.Response tra(Object data, [int status = 200]) => http.Response(
  jsonEncode(data),
  status,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

Future<KetQuaApi> uploadGia(int id, List<Map<String, String>> images) async {
  expect(id, 15);
  expect(images, isNotEmpty);
  return KetQuaApi()
    ..thanhCong = true
    ..duLieu = {'maBangChung': 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'};
}

Future<T?> chayThat<T>(WidgetTester tester, Future<T> Function() task) {
  final client =
      http.Client(); // Giữ HTTP fixture khi Flutter chuyển sang event loop thật để đọc file.
  return tester.runAsync(() => http.runWithClient(task, () => client));
}

Map<String, dynamic> don(String trangThai) => {
  'maDonHang': 15,
  'maDonHangHienThi': 'DH00015',
  'maChiNhanh': 1,
  'tenChiNhanh': 'Chi nhánh test',
  'hinhThucNhan': 'TAI_QUAY',
  'trangThaiDonHang': trangThai,
  'ngayDat': '2026-10-26T08:00:00',
  'phuongThucThanhToan': 'VIETQR',
  'trangThaiThanhToan': 'CHUA_THANH_TOAN',
  'tienHang': 5000,
  'tongTien': 5000,
  'dongIn': [],
  'tienDo': [],
};

void main() {
  late Directory imageDir;
  late String imagePath;
  setUp(() {
    AppTheme.dungGoogleFonts = false;
    imageDir = Directory.systemTemp.createTempSync('huy-bang-chung-');
    imagePath = '${imageDir.path}/proof.png';
    File(imagePath).writeAsBytesSync(
      base64Decode(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=',
      ),
    );
  });
  tearDown(() => imageDir.deleteSync(recursive: true));

  testWidgets('Hủy đơn đã trả tiền bắt buộc ngân hàng và số tài khoản', (
    tester,
  ) async {
    int calls = 0;
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: NutHuyDon(
                guiAnh: uploadGia,
                maDonHang: 15,
                thongTinHoanTien: {
                  'anhBangChung': [
                    {'path': imagePath, 'name': 'proof.png'},
                  ],
                },
                daThanhToan: true,
                daHuy: () async {},
              ),
            ),
          ),
        );
        await tester.tap(find.text('Hủy đơn'));
        await tester.pumpAndSettle();
        expect(find.widgetWithText(TextFormField, 'Ngân hàng'), findsOneWidget);
        expect(
          find.widgetWithText(TextFormField, 'Số tài khoản ngân hàng'),
          findsOneWidget,
        );
        await chayThat(tester, () async {
          await tester.tap(find.text('Xác nhận hủy'));
          await Future<void>.delayed(const Duration(milliseconds: 150));
        });
        await tester.pump();
        await chayThat(
          tester,
          () => Future<void>.delayed(const Duration(milliseconds: 200)),
        );
        await tester.pump();
        await tester.pumpAndSettle();
        expect(calls, 0);
        await tester.enterText(
          find.byKey(const ValueKey('hoan-ngan-hang')),
          'Vietcombank',
        );
        await tester.enterText(
          find.byKey(const ValueKey('hoan-so-tai-khoan')),
          '001234567890',
        );
        await chayThat(tester, () async {
          await tester.tap(find.text('Xác nhận hủy'));
          await Future<void>.delayed(const Duration(milliseconds: 150));
        });
        await tester.pump();
        await chayThat(
          tester,
          () => Future<void>.delayed(const Duration(milliseconds: 200)),
        );
        await tester.pump();
        await tester.pumpAndSettle();
        expect(
          calls,
          1,
          reason: tester
              .widgetList<Text>(find.byType(Text))
              .map((x) => x.data)
              .join(' | '),
        );
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        if (req.url.path.endsWith('/bang-chung-huy') && req.method == 'POST') {
          return tra({'maBangChung': 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'});
        }
        final body = jsonDecode(req.body);
        expectSync(body['nganHang'], 'Vietcombank');
        expectSync(body['soTaiKhoan'], '001234567890');
        expectSync((body['lyDo'] as String).isNotEmpty, true);
        calls++;
        return tra({'message': 'Đã hủy, chờ hoàn tiền.'});
      }),
    );
  });

  testWidgets('Đơn hủy nhận tiền muộn có chỗ bổ sung thông tin hoàn', (
    tester,
  ) async {
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          const MaterialApp(home: TienDoScreen(maDonHang: 15)),
        );
        await tester.pumpAndSettle();
        expect(find.text('Bổ sung thông tin hoàn tiền'), findsOneWidget);
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient(
        (req) async => tra({
          ...don('DA_HUY'),
          'trangThaiThanhToan': 'DA_THANH_TOAN',
          'choPhepBoSungHoanTien': true,
        }),
      ),
    );
  });

  testWidgets('Bổ sung hoàn tiền gửi đúng đơn, tải lại ghi chú đủ ba dòng', (
    tester,
  ) async {
    int saves = 0;
    Map<String, dynamic>? info;
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          const MaterialApp(home: TienDoScreen(maDonHang: 15)),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('Bổ sung thông tin hoàn tiền'));
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byType(TextFormField).at(0),
          'Không còn nhu cầu',
        );
        await tester.enterText(
          find.byKey(const ValueKey('hoan-ngan-hang')),
          'Vietcombank',
        );
        await tester.enterText(
          find.byKey(const ValueKey('hoan-so-tai-khoan')),
          '001234567890',
        );
        await tester.tap(find.text('Lưu thông tin hoàn tiền'));
        await tester.pumpAndSettle();
        expect(saves, 1);
        expect(
          find.text(
            'Lý do hoàn tiền: Không còn nhu cầu\nNgân hàng: Vietcombank\nSố tài khoản: 001234567890',
          ),
          findsOneWidget,
        );
        expect(find.text('Sửa thông tin hoàn tiền'), findsOneWidget);
        await tester.tap(find.text('Sửa thông tin hoàn tiền'));
        await tester.pumpAndSettle();
        expect(
          tester
              .widget<TextFormField>(
                find.byKey(const ValueKey('hoan-so-tai-khoan')),
              )
              .controller!
              .text,
          '001234567890',
        );
        await tester.tap(find.text('Để sau'));
        await tester.pumpAndSettle();
        expect(saves, 1);
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        if (req.url.path.endsWith('/bang-chung-huy') && req.method == 'POST') {
          return tra({'maBangChung': 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'});
        }
        if (req.method == 'POST') {
          expectSync(req.url.path, '/api/don-hang/15/thong-tin-hoan-tien');
          info = Map<String, dynamic>.from(jsonDecode(req.body));
          saves++;
          return tra({'message': 'Đã lưu thông tin nhận hoàn tiền.'});
        }
        return tra({
          ...don('DA_HUY'),
          'trangThaiThanhToan': 'DA_THANH_TOAN',
          'choPhepBoSungHoanTien': true,
          'thongTinHoanTien': info,
          if (info != null)
            'ghiChu':
                'Lý do hoàn tiền: ${info!['lyDo']}\nNgân hàng: ${info!['nganHang']}\nSố tài khoản: ${info!['soTaiKhoan']}',
        });
      }),
    );
  });

  testWidgets('Đã hoàn tiền không còn nút sửa tài khoản nhận hoàn', (
    tester,
  ) async {
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          const MaterialApp(home: TienDoScreen(maDonHang: 15)),
        );
        await tester.pumpAndSettle();
        expect(find.text('Bổ sung thông tin hoàn tiền'), findsNothing);
        expect(find.text('Sửa thông tin hoàn tiền'), findsNothing);
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient(
        (req) async => tra({
          ...don('DA_HUY'),
          'trangThaiThanhToan': 'DA_HOAN_TIEN',
          'choPhepBoSungHoanTien': false,
        }),
      ),
    );
  });

  testWidgets('Giữ đơn hoặc lý do trống không gửi yêu cầu hủy', (tester) async {
    int calls = 0, completed = 0;
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: NutHuyDon(
                guiAnh: uploadGia,
                maDonHang: 15,
                thongTinHoanTien: {
                  'anhBangChung': [
                    {'path': imagePath, 'name': 'proof.png'},
                  ],
                },
                daHuy: () async {
                  completed++;
                },
              ),
            ),
          ),
        );
        await tester.tap(find.text('Hủy đơn'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Giữ đơn'));
        await tester.pumpAndSettle();
        expect(calls, 0);
        await tester.tap(find.text('Hủy đơn'));
        await tester.pumpAndSettle();
        await tester.enterText(find.byType(TextFormField), '   ');
        await chayThat(tester, () async {
          await tester.tap(find.text('Xác nhận hủy'));
          await Future<void>.delayed(const Duration(milliseconds: 150));
        });
        await tester.pump();
        await chayThat(
          tester,
          () => Future<void>.delayed(const Duration(milliseconds: 200)),
        );
        await tester.pump();
        await tester.pumpAndSettle();
        expect(find.text('Vui lòng nhập lý do hủy.'), findsOneWidget);
        expect(calls, 0);
        expect(completed, 0);
        await tester.tap(find.text('Giữ đơn'));
        await tester.pumpAndSettle();
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        if (req.url.path.endsWith('/bang-chung-huy') && req.method == 'POST') {
          return tra({'maBangChung': 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'});
        }
        calls++;
        return tra({'message': 'Đã hủy đơn.'});
      }),
    );
  });

  testWidgets('Đang gửi chỉ hủy một lần, thành công mới báo tải lại', (
    tester,
  ) async {
    int calls = 0, completed = 0;
    final cho = Completer<void>();
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: NutHuyDon(
                guiAnh: uploadGia,
                maDonHang: 15,
                thongTinHoanTien: {
                  'anhBangChung': [
                    {'path': imagePath, 'name': 'proof.png'},
                  ],
                },
                daHuy: () async {
                  completed++;
                },
              ),
            ),
          ),
        );
        await tester.tap(find.text('Hủy đơn'));
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byType(TextFormField),
          '  Không cần in nữa  ',
        );
        await chayThat(tester, () async {
          await tester.tap(find.text('Xác nhận hủy'));
          await Future<void>.delayed(const Duration(milliseconds: 150));
        });
        await tester.pump();
        await chayThat(
          tester,
          () => Future<void>.delayed(const Duration(milliseconds: 200)),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 400));
        expect(
          calls,
          1,
          reason: tester
              .widgetList<Text>(find.byType(Text))
              .map((x) => x.data)
              .join(' | '),
        );
        expect(completed, 0);
        await tester.tap(find.text('Đang xử lý…'));
        await tester.pump();
        expect(
          calls,
          1,
          reason: tester
              .widgetList<Text>(find.byType(Text))
              .map((x) => x.data)
              .join(' | '),
        );
        cho.complete();
        await tester.pumpAndSettle();
        expect(completed, 1);
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        if (req.url.path.endsWith('/bang-chung-huy') && req.method == 'POST') {
          return tra({'maBangChung': 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'});
        }
        expectSync(req.method, 'POST');
        expectSync(req.url.path, '/api/don-hang/15/huy');
        expectSync(jsonDecode(req.body)['lyDo'], 'Không cần in nữa');
        calls++;
        await cho.future;
        return tra({'message': 'Đã hủy đơn.'});
      }),
    );
  });

  testWidgets('API từ chối hủy không báo thành công hoặc khóa nút vĩnh viễn', (
    tester,
  ) async {
    int completed = 0;
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: NutHuyDon(
                guiAnh: uploadGia,
                maDonHang: 15,
                thongTinHoanTien: {
                  'anhBangChung': [
                    {'path': imagePath, 'name': 'proof.png'},
                  ],
                },
                daHuy: () async {
                  completed++;
                },
              ),
            ),
          ),
        );
        await tester.tap(find.text('Hủy đơn'));
        await tester.pumpAndSettle();
        await chayThat(tester, () async {
          await tester.tap(find.text('Xác nhận hủy'));
          await Future<void>.delayed(const Duration(milliseconds: 150));
        });
        await tester.pump();
        await chayThat(
          tester,
          () => Future<void>.delayed(const Duration(milliseconds: 200)),
        );
        await tester.pump();
        await tester.pumpAndSettle();
        expect(find.text('Đơn đã được tiếp nhận.'), findsOneWidget);
        expect(find.text('Hủy đơn'), findsOneWidget);
        expect(completed, 0);
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient(
        (req) async => req.url.path.endsWith('/bang-chung-huy')
            ? tra({'maBangChung': 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'})
            : tra({'message': 'Đơn đã được tiếp nhận.'}, 400),
      ),
    );
  });

  for (final status in [
    'CHO_XAC_NHAN',
    'DA_XAC_NHAN',
    'DANG_IN',
    'DA_IN',
    'DANG_GIAO',
    'GIAO_THAT_BAI',
    'HOAN_THANH',
    'DA_HUY',
  ]) {
    testWidgets('Nút hủy tiến độ chỉ hiện trước khi in: $status', (
      tester,
    ) async {
      await http.runWithClient(() async {
        await tester.pumpWidget(
          const MaterialApp(home: TienDoScreen(maDonHang: 15)),
        );
        await tester.pumpAndSettle();
        expect(
          find.byType(NutHuyDon),
          status == 'CHO_XAC_NHAN' ? findsOneWidget : findsNothing,
        );
        if (status == 'CHO_XAC_NHAN') {
          expect(
            tester.getTopLeft(find.text('Hủy đơn')).dy,
            lessThan(tester.getTopLeft(find.text('Tiến trình xử lý')).dy),
          );
        }
        await tester.pumpWidget(const SizedBox());
      }, () => MockClient((req) async => tra(don(status))));
    });
  }

  for (final payment in ['CHUA_THANH_TOAN', 'DA_THANH_TOAN', 'DA_HOAN_TIEN']) {
    testWidgets('Đơn đã hủy hiển thị đúng tình trạng tiền: $payment', (
      tester,
    ) async {
      final expected = {
        'CHUA_THANH_TOAN': 'Đã hủy · Không cần thanh toán',
        'DA_THANH_TOAN': 'Đã hủy · Chờ hoàn tiền',
        'DA_HOAN_TIEN': 'Đã hoàn tiền',
      }[payment]!;
      await http.runWithClient(
        () async {
          await tester.pumpWidget(
            const MaterialApp(home: TienDoScreen(maDonHang: 15)),
          );
          await tester.pumpAndSettle();
          expect(find.text(expected), findsOneWidget);
          expect(find.text('Phải trả khi nhận'), findsNothing);
          expect(find.byType(NutHuyDon), findsNothing);
          await tester.pumpWidget(const SizedBox());
        },
        () => MockClient(
          (req) async => tra({...don('DA_HUY'), 'trangThaiThanhToan': payment}),
        ),
      );
    });
  }

  testWidgets('Hủy từ QR chuyển sang tiến độ đơn đã hủy, không hiện QR nữa', (
    tester,
  ) async {
    int cancellations = 0;
    await http.runWithClient(
      () async {
        await tester.pumpWidget(
          MaterialApp(
            home: ThanhToanScreen(
              maDonHang: 15,
              chonAnhHuy: () async => AnhThu('proof.png', imagePath),
              guiAnhHuy: uploadGia,
            ),
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 200));
        await tester.tap(find.text('Hủy đơn'));
        await tester.pump(const Duration(milliseconds: 400));
        await chayThat(tester, () async {
          await tester.tap(find.text('Thêm ảnh (0/3)'));
          await Future<void>.delayed(const Duration(milliseconds: 100));
        });
        await tester.pump();
        await chayThat(tester, () async {
          await tester.tap(find.text('Xác nhận hủy'));
          await Future<void>.delayed(const Duration(milliseconds: 150));
        });
        await tester.pump();
        await chayThat(
          tester,
          () => Future<void>.delayed(const Duration(milliseconds: 200)),
        );
        await tester.pump();
        await tester.pumpAndSettle();
        expect(cancellations, 1);
        expect(find.byType(TienDoScreen), findsOneWidget);
        expect(find.text('Đã hủy'), findsWidgets);
        expect(find.byType(NutHuyDon), findsNothing);
        expect(find.byKey(const ValueKey('payos-qr')), findsNothing);
        await tester.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        if (req.url.path.endsWith('/bang-chung-huy') && req.method == 'POST') {
          return tra({'maBangChung': 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'});
        }
        if (req.url.path == '/api/thanh-toan/15') {
          return tra({
            'maDonHang': 15,
            'maDonHangHienThi': 'DH00015',
            'phuongThuc': 'VIETQR',
            'trangThai': 'CHUA_THANH_TOAN',
            'soTien': 5000,
            'trangThaiDonHang': 'CHO_XAC_NHAN',
            'hanThanhToan': DateTime.now()
                .add(const Duration(minutes: 10))
                .toIso8601String(),
          });
        }
        if (req.url.path.endsWith('/payos')) {
          return tra({'maDonHang': 15, 'soTien': 5000});
        }
        if (req.url.path.endsWith('/huy')) {
          cancellations++;
          return tra({'message': 'Đã hủy đơn.'});
        }
        if (req.url.path == '/api/don-hang/15') return tra(don('DA_HUY'));
        if (req.url.path == '/api/thong-bao') return tra({'soChuaDoc': 0});
        if (req.url.path == '/api/don-hang/cua-toi') return tra([]);
        return tra({'message': 'API không mong đợi trong test'}, 404);
      }),
    );
  });
}
