import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/screens/dat_in/cau_hinh_in_screen.dart';
import 'package:photocopy_mobile/theme.dart';

Map<String, dynamic> chiNhanh(int ma) => {
  'maChiNhanh': ma,
  'tenChiNhanh': 'Chi nhánh $ma',
  'diaChi': 'Địa chỉ $ma',
  'gioMoCua': '09:00:00',
  'gioDongCua': '21:00:00',
  'banKinhGiao': 10,
  'diemDanhGiaTB': 0,
  'soLuotDanhGia': 0,
  'dapUngDuoc': true,
};

http.Response traJson(Object duLieu, [int ma = 200]) => http.Response(
  jsonEncode(duLieu),
  ma,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() async {
    // Dùng font sẵn trong Flutter SDK thay vì font Ahem của test (ký tự rất rộng).
    var thuMuc = File(Platform.resolvedExecutable).parent;
    while (!Directory(
      '${thuMuc.path}/bin/cache/artifacts/material_fonts',
    ).existsSync()) {
      if (thuMuc.parent.path == thuMuc.path) {
        throw StateError('Không tìm thấy font trong Flutter SDK.');
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

  Future<void> mo(
    WidgetTester tester, {
    int soTrang = 2,
    double rong = 390,
    double coChu = 1,
  }) async {
    tester.view.physicalSize = Size(rong * 3, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      MaterialApp(
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(
            context,
          ).copyWith(textScaler: TextScaler.linear(coChu)),
          child: child!,
        ),
        home: CauHinhInScreen(
          file: FileIn.fromJson({
            'maFileIn': 50,
            'tenFileGoc': 'tai-lieu.pdf',
            'dinhDangGoc': 'pdf',
            'soTrang': soTrang,
            'trangThaiFile': 'SAN_SANG',
          }),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets(
    'Tải giá theo chi nhánh đầu tiên, đổi chi nhánh thì báo giá lại',
    (tester) async {
      final duongDan = <String>[];
      final chiNhanhBaoGia = <int>[];
      await http.runWithClient(
        () async {
          await mo(tester);
          expect(
            find.text('Hãy chọn chi nhánh để xem bảng giá.'),
            findsNothing,
          );
          expect(find.text('A4 70gsm'), findsWidgets);
          expect(duongDan.take(2), [
            '/api/danh-muc/chi-nhanh',
            '/api/danh-muc/bang-gia?maChiNhanh=11',
          ]);
          expect(chiNhanhBaoGia, [11]);

          await tester.tap(find.text('Tiếp tục chọn chi nhánh'));
          await tester.pumpAndSettle();
          expect(find.text('Chi nhánh 11'), findsWidgets);
          expect(find.text('Chi nhánh 22'), findsOneWidget);
          await tester.ensureVisible(find.text('Chi nhánh 22'));
          await tester.tap(find.text('Chi nhánh 22'));
          await tester.pump(const Duration(milliseconds: 400));
          await tester.pumpAndSettle();
          expect(chiNhanhBaoGia.last, 22);
          expect(find.text('1.200₫'), findsWidgets);
        },
        () => MockClient((req) async {
          duongDan.add(
            req.url.hasQuery
                ? '${req.url.path}?${req.url.query}'
                : req.url.path,
          );
          switch (req.url.path) {
            case '/api/danh-muc/chi-nhanh':
            case '/api/don-hang/chi-nhanh-phu-hop':
              return traJson([chiNhanh(11), chiNhanh(22)]);
            case '/api/danh-muc/bang-gia':
              if (req.url.queryParameters['maChiNhanh'] != '11') {
                return traJson({
                  'message': 'Hãy chọn chi nhánh để xem bảng giá.',
                }, 400);
              }
              return traJson([
                {
                  'maDichVu': 2,
                  'tenDichVu': 'In A4 70gsm 2 mặt đen trắng',
                  'loaiDichVu': 'IN_AN',
                  'khoGiay': 'A4',
                  'loaiGiay': '70gsm',
                  'matIn': '2_MAT',
                  'mauSac': 'DEN_TRANG',
                  'nhomGiaCong': null,
                  'donGia': 300,
                },
              ]);
            case '/api/bao-gia':
              final ma = jsonDecode(req.body)['maChiNhanh'] as int;
              chiNhanhBaoGia.add(ma);
              final tien = ma == 11 ? 600 : 1200;
              return traJson({
                'tienIn': tien,
                'tienGiaCong': 0,
                'phuPhiGap': 0,
                'phiShip': 0,
                'giamGia': 0,
                'tongTien': tien,
                'tongSoTrang': 2,
              });
            default:
              throw StateError('API không mong đợi: ${req.url.path}');
          }
        }),
      );
    },
  );

  testWidgets('Không có chi nhánh thì báo rõ, không gọi bảng giá', (
    tester,
  ) async {
    final duongDan = <String>[];
    await http.runWithClient(
      () async {
        await mo(tester);
        expect(
          find.text('Chưa có chi nhánh nào đang hoạt động.'),
          findsOneWidget,
        );
        expect(find.text('Thử lại'), findsOneWidget);
        expect(duongDan, ['/api/danh-muc/chi-nhanh']);
      },
      () => MockClient((req) async {
        duongDan.add(req.url.path);
        if (req.url.path == '/api/danh-muc/chi-nhanh') return traJson([]);
        return traJson({'message': 'Hãy chọn chi nhánh để xem bảng giá.'}, 400);
      }),
    );
  });

  testWidgets('Giấy lấy tên kho, hai vật tư cùng quy cách không bị gộp', (
    tester,
  ) async {
    final baoGia = <Map<String, dynamic>>[];
    await http.runWithClient(
      () async {
        await mo(tester);
        expect(find.text('Giấy A4 100gsm loại A'), findsOneWidget);
        final giayB = find.text('Giấy A4 100gsm loại B');
        await tester.drag(find.byType(ListView).first, const Offset(-320, 0));
        await tester.pumpAndSettle();
        await tester.ensureVisible(giayB);
        await tester.tap(giayB);
        await tester.pumpAndSettle();
        expect(baoGia.last['fileIn'][0]['maDichVu'], 102);
        await tester.tap(find.text('Tiếp tục chọn chi nhánh'));
        await tester.pumpAndSettle();
        expect(baoGia.last['fileIn'][0]['maDichVu'], 102);
      },
      () => MockClient((req) async {
        if (req.url.path == '/api/danh-muc/chi-nhanh' ||
            req.url.path == '/api/don-hang/chi-nhanh-phu-hop') {
          return traJson([chiNhanh(11)]);
        }
        if (req.url.path == '/api/danh-muc/bang-gia') {
          return traJson([
            for (final i in [1, 2])
              {
                'maDichVu': 100 + i,
                'tenDichVu': 'In A4 100gsm',
                'loaiDichVu': 'IN_AN',
                'maVatTu': 20 + i,
                'tenVatTu': 'Giấy A4 100gsm loại ${i == 1 ? 'A' : 'B'}',
                'khoGiay': 'A4',
                'loaiGiay': '100gsm',
                'matIn': '1_MAT',
                'mauSac': 'DEN_TRANG',
                'donGia': 500,
              },
          ]);
        }
        if (req.url.path == '/api/bao-gia') {
          baoGia.add(Map<String, dynamic>.from(jsonDecode(req.body)));
          return traJson({'tienIn': 1000, 'tongTien': 1000, 'tongSoTrang': 2});
        }
        throw StateError('API không mong đợi: ${req.url.path}');
      }),
    );
  });

  testWidgets('Chọn chi nhánh để thấy giấy chỉ bán ở chi nhánh đó', (
    tester,
  ) async {
    final baoGia = <Map<String, dynamic>>[];
    await http.runWithClient(
      () async {
        await mo(tester);
        await tester.tap(find.byType(DropdownButtonFormField<int>));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Chi nhánh 22').last);
        await tester.pumpAndSettle();
        expect(find.text('Giấy A4 120gsm'), findsOneWidget);
        expect(baoGia.last['maChiNhanh'], 22);
        expect(baoGia.last['fileIn'][0]['maDichVu'], 122);
        await tester.tap(find.text('Tiếp tục chọn chi nhánh'));
        await tester.pumpAndSettle();
        expect(baoGia.last['maChiNhanh'], 22);
      },
      () => MockClient((req) async {
        if (req.url.path == '/api/danh-muc/chi-nhanh' ||
            req.url.path == '/api/don-hang/chi-nhanh-phu-hop') {
          return traJson([chiNhanh(11), chiNhanh(22)]);
        }
        if (req.url.path == '/api/danh-muc/bang-gia') {
          final cn = int.parse(req.url.queryParameters['maChiNhanh']!);
          return traJson([
            {
              'maDichVu': 100 + cn,
              'tenDichVu': 'In A4',
              'loaiDichVu': 'IN_AN',
              'maVatTu': cn,
              'tenVatTu': cn == 22 ? 'Giấy A4 120gsm' : 'Giấy A4 70gsm',
              'khoGiay': 'A4',
              'loaiGiay': cn == 22 ? '120gsm' : '70gsm',
              'matIn': '1_MAT',
              'mauSac': 'DEN_TRANG',
              'donGia': 500,
            },
          ]);
        }
        if (req.url.path == '/api/bao-gia') {
          baoGia.add(Map<String, dynamic>.from(jsonDecode(req.body)));
          return traJson({'tienIn': 1000, 'tongTien': 1000, 'tongSoTrang': 2});
        }
        throw StateError('API không mong đợi: ${req.url.path}');
      }),
    );
  });

  testWidgets('Tải chi nhánh lỗi thì có thể thử lại', (tester) async {
    int soLan = 0;
    await http.runWithClient(
      () async {
        await mo(tester);
        expect(find.text('Không tải được chi nhánh.'), findsOneWidget);
        await tester.tap(find.text('Thử lại'));
        await tester.pumpAndSettle();
        expect(soLan, 2);
        expect(
          find.text('Chưa có chi nhánh nào đang hoạt động.'),
          findsOneWidget,
        );
      },
      () => MockClient((req) async {
        if (req.url.path == '/api/danh-muc/chi-nhanh') {
          soLan++;
          return soLan == 1
              ? traJson({'message': 'Không tải được chi nhánh.'}, 500)
              : traJson([]);
        }
        return traJson({'message': 'Hãy chọn chi nhánh để xem bảng giá.'}, 400);
      }),
    );
  });

  MockClient clientQuyCach(
    List<Map<String, dynamic>> baoGia, [
    List<Map<String, dynamic>>? don,
  ]) => MockClient((req) async {
    if (req.url.path == '/api/danh-muc/chi-nhanh' ||
        req.url.path == '/api/don-hang/chi-nhanh-phu-hop') {
      return traJson([chiNhanh(11)]);
    }
    if (req.url.path == '/api/danh-muc/bang-gia') {
      return traJson([
        for (final mat in ['1_MAT', '2_MAT'])
          {
            'maDichVu': mat == '1_MAT' ? 1 : 2,
            'tenDichVu': 'In A4',
            'loaiDichVu': 'IN_AN',
            'khoGiay': 'A4',
            'loaiGiay': '70gsm',
            'matIn': mat,
            'mauSac': 'DEN_TRANG',
            'donGia': 300,
          },
        for (final item in [
          [71, 'Đóng lò xo', 'DONG_GAY'],
          [72, 'Ép plastic', null],
          [73, 'Bấm kim góc', null],
          [74, 'Đóng bìa kiếng', 'DONG_GAY'],
        ])
          {
            'maDichVu': item[0],
            'tenDichVu': item[1],
            'nhomGiaCong': item[2],
            'loaiDichVu': 'GIA_CONG',
            'donGia': 1000,
          },
      ]);
    }
    if (req.url.path == '/api/bao-gia') {
      baoGia.add(Map<String, dynamic>.from(jsonDecode(req.body)));
      return traJson({
        'tienIn': 600,
        'tienGiaCong': 0,
        'phuPhiGap': 0,
        'phiShip': 0,
        'giamGia': 0,
        'tongTien': 600,
        'tongSoTrang': 2,
      });
    }
    if (req.url.path == '/api/don-hang' && don != null) {
      don.add(Map<String, dynamic>.from(jsonDecode(req.body)));
      return traJson({
        'message': 'Dừng tại kiểm thử, không tạo đơn thật.',
      }, 400);
    }
    throw StateError('API không mong đợi: ${req.url.path}');
  });

  testWidgets('Giấy ngang được giữ trong báo giá bước 2 và bước 3', (
    tester,
  ) async {
    final baoGia = <Map<String, dynamic>>[];
    final don = <Map<String, dynamic>>[];
    await http.runWithClient(() async {
      await mo(tester);
      expect(baoGia.last['fileIn'][0]['huongGiay'], 'DUNG');
      await tester.ensureVisible(find.text('Giấy ngang'));
      await tester.tap(find.text('Giấy ngang'));
      await tester.pumpAndSettle();
      expect(baoGia.last['fileIn'][0]['huongGiay'], 'NGANG');
      await tester.tap(find.text('Tiếp tục chọn chi nhánh'));
      await tester.pumpAndSettle();
      expect(baoGia.last['fileIn'][0]['huongGiay'], 'NGANG');
      expect(find.textContaining('Giấy ngang'), findsWidgets);
      await tester.tap(find.text('Đặt đơn và chuyển khoản'));
      await tester.pumpAndSettle();
      expect(don.single['fileIn'][0]['huongGiay'], 'NGANG');
    }, () => clientQuyCach(baoGia, don));
  });

  testWidgets(
    'Plastic cùng lò xo; kim góc thay lò xo; đổi mặt in xét lại giới hạn',
    (tester) async {
      final baoGia = <Map<String, dynamic>>[];
      await http.runWithClient(() async {
        await mo(tester, soTrang: 100);
        for (final ten in ['Ép plastic', 'Đóng lò xo']) {
          await tester.ensureVisible(find.text(ten));
          await tester.tap(find.text(ten));
          await tester.pumpAndSettle();
        }
        expect(baoGia.last['fileIn'][0]['dsGiaCong'], containsAll([71, 72]));
        await tester.ensureVisible(find.text('Bấm kim góc'));
        await tester.tap(find.text('Bấm kim góc'));
        await tester.pumpAndSettle();
        expect(baoGia.last['fileIn'][0]['dsGiaCong'], containsAll([72, 73]));
        expect(baoGia.last['fileIn'][0]['dsGiaCong'], isNot(contains(71)));
        await tester.ensureVisible(find.text('In 1 mặt'));
        await tester.tap(find.text('In 1 mặt'));
        await tester.pumpAndSettle();
        expect(baoGia.last['fileIn'][0]['dsGiaCong'], isNot(contains(73)));
        expect(find.textContaining('50 tờ'), findsWidgets);
      }, () => clientQuyCach(baoGia));
    },
  );

  for (final kichThuoc in [const Size(320, 1), const Size(390, 1.3)]) {
    testWidgets(
      'Quy cách không tràn ở ${kichThuoc.width}px, cỡ chữ ${kichThuoc.height}',
      (tester) async {
        final baoGia = <Map<String, dynamic>>[];
        await http.runWithClient(() async {
          await mo(tester, rong: kichThuoc.width, coChu: kichThuoc.height);
          await tester.ensureVisible(find.text('Giấy ngang'));
          await tester.tap(find.text('Giấy ngang'));
          await tester.pumpAndSettle();
          await tester.ensureVisible(find.text('Bấm kim góc'));
        }, () => clientQuyCach(baoGia));
      },
    );
  }
}
