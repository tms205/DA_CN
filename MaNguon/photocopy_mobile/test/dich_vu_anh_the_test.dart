import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/screens/dat_in/chon_file_screen.dart';
import 'package:photocopy_mobile/screens/dat_in/cau_hinh_in_screen.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() async {
    var sdk = File(Platform.resolvedExecutable).parent;
    while (!Directory('${sdk.path}/bin/cache/artifacts/material_fonts').existsSync()) {
      if (sdk.parent.path == sdk.path) throw StateError('Không tìm thấy font Flutter.');
      sdk = sdk.parent;
    }
    final bytes = await File('${sdk.path}/bin/cache/artifacts/material_fonts/roboto-regular.ttf').readAsBytes();
    for (final ten in ['Roboto', 'monospace']) {
      await (FontLoader(ten)..addFont(Future.value(ByteData.sublistView(bytes)))).load();
    }
  });
  setUp(() => AppTheme.dungGoogleFonts = false);
  test('Nhãn dịch vụ ảnh hiển thị kích thước thay vì chỉ giấy A4', () {
    final dv = DichVu.fromJson({
      'maDichVu': 99,
      'tenDichVu': 'In ảnh thẻ',
      'loaiDichVu': 'IN_AN',
      'khoGiay': 'A4',
      'loaiGiay': 'GIAY_ANH',
      'kichThuocAnh': '3X4',
    });
    expect(dv.tenGiay, contains('3 × 4 cm'));
  });
  testWidgets('Màn đặt in cho chọn dịch vụ ảnh và mẫu đơn', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: ChonFileScreen()));
    expect(find.text('Dịch vụ đặt in'), findsOneWidget);
    await tester.tap(find.byType(DropdownButtonFormField<String>).first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('In ảnh thẻ').last);
    await tester.pumpAndSettle();
    expect(find.text('Số ảnh (1–100)'), findsOneWidget);
    expect(find.text('Kích thước mỗi ảnh'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });

  for (final rong in [320.0, 390.0]) {
    testWidgets('Ảnh thẻ báo giá đúng dịch vụ, không nhân số bản, rộng $rong', (tester) async {
      tester.view.physicalSize = Size(rong * 3, 2532);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);
      Map<String, dynamic>? yeuCauGia;
      await http.runWithClient(() async {
        await tester.pumpWidget(MaterialApp(home: CauHinhInScreen(file: FileIn.fromJson({
          'maFileIn': 1, 'tenFileGoc': 'anh.jpg', 'soTrang': 1,
          'kichThuocAnh': '3X4', 'soLuongAnh': 8, 'trangThaiFile': 'SAN_SANG',
        }))));
        await tester.pumpAndSettle();
        final dong = yeuCauGia!['fileIn'][0];
        expect(dong['maDichVu'], 99);
        expect(dong['soBanIn'], 1);
        expect(dong['huongGiay'], 'DUNG');
        expect(dong['dsGiaCong'], isEmpty);
        expect(find.text('Số bản in'), findsNothing);
        expect(find.text('40.000₫'), findsWidgets);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
      }, () => MockClient((req) async {
        Object data;
        switch (req.url.path) {
          case '/api/danh-muc/chi-nhanh':
            data = [{'maChiNhanh': 1, 'tenChiNhanh': 'Chi nhánh thử', 'diaChi': 'Địa chỉ thử', 'gioMoCua': '08:00:00', 'gioDongCua': '21:00:00', 'banKinhGiao': 10, 'diemDanhGiaTB': 0, 'soLuotDanhGia': 0}];
          case '/api/danh-muc/bang-gia':
            data = [
              {'maDichVu': 2, 'tenDichVu': 'Tài liệu', 'loaiDichVu': 'IN_AN', 'khoGiay': 'A4', 'loaiGiay': '70gsm', 'matIn': '2_MAT', 'mauSac': 'DEN_TRANG', 'donGia': 300},
              {'maDichVu': 99, 'loaiDichVu': 'IN_AN', 'tenDichVu': 'Ảnh thẻ', 'kichThuocAnh': '3X4', 'khoGiay': 'A4', 'loaiGiay': 'GIAY_ANH', 'matIn': '1_MAT', 'mauSac': 'MAU', 'donGia': 5000},
              {'maDichVu': 100, 'tenDichVu': 'Ảnh thẻ 4x6', 'loaiDichVu': 'IN_AN', 'kichThuocAnh': '4X6', 'matIn': '1_MAT', 'mauSac': 'MAU', 'donGia': 10000},
            ];
          case '/api/bao-gia':
            yeuCauGia = jsonDecode(req.body);
            data = {'tienIn': 40000, 'tongTien': 40000, 'tongSoTrang': 1, 'tienGiaCong': 0};
          default:
            throw StateError('API ngoài dự kiến: ${req.url}');
        }
        return http.Response(jsonEncode(data), 200, headers: {'content-type': 'application/json; charset=utf-8'});
      }));
    });
  }
}
