import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/screens/dat_in/xem_truoc_file_screen.dart';
import 'package:photocopy_mobile/screens/shipper/shipper_screen.dart';

KetQuaApi thanhCong(dynamic data) => KetQuaApi()
  ..thanhCong = true
  ..duLieu = data;

Map<String, dynamic> luotDangGiao(String trangThaiGiao, {String? lyDoTuChoi}) =>
    {
      'maDonHang': 15,
      'maDonHangHienThi': 'DH00015',
      'maGiaoHang': 8,
      'tenKhachHang': 'Khách thử',
      'sdtNhan': '0901234567',
      'diaChiChiNhanh': '140 Lê Trọng Tấn',
      'diaChiGiao': '123 Lê Lợi · Chi tiết: Tầng 2',
      'trangThaiDonHang': 'DANG_GIAO',
      'trangThaiGiao': trangThaiGiao,
      'lyDo': trangThaiGiao == 'CHO_XAC_NHAN' ? 'Khách không nghe máy' : null,
      'lyDoTuChoi': lyDoTuChoi,
      'tongTien': 39000,
      'phiShip': 19000,
      'soLanGiao': 1,
    };

Future<void> moTabDangGiao(WidgetTester tester, Map<String, dynamic> luot) async {
  await tester.pumpWidget(
    MaterialApp(
      home: ShipperScreen(
        taiDanhSach: (path) async =>
            thanhCong(path.contains('DANG_GIAO') ? [luot] : []),
      ),
    ),
  );
  await tester.pumpAndSettle();
  await tester.tap(find.widgetWithText(ChoiceChip, 'Đang giao'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('Lượt chờ cửa hàng xác nhận thì ẩn nút báo kết quả', (
    tester,
  ) async {
    await moTabDangGiao(tester, luotDangGiao('CHO_XAC_NHAN'));
    expect(find.textContaining('Chờ cửa hàng xác nhận'), findsOneWidget);
    expect(find.text('Đã giao thành công'), findsNothing);
    expect(find.text('Không giao được'), findsNothing);
    expect(find.text('Xem bằng chứng'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('Bị từ chối thì hiện lý do và cho giao tiếp', (tester) async {
    await moTabDangGiao(
      tester,
      luotDangGiao('DANG_GIAO', lyDoTuChoi: 'Vị trí cách địa chỉ giao 3 km'),
    );
    expect(
      find.textContaining('Vị trí cách địa chỉ giao 3 km'),
      findsOneWidget,
    );
    expect(find.text('Đã giao thành công'), findsOneWidget);
    expect(find.text('Không giao được'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('Xem trước bản in tải đúng file và chuyển trang', (tester) async {
    final daTai = <String>[];
    // Ảnh PNG 1×1 hợp lệ.
    final png = Uint8List.fromList([
      137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1,
      0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 13, 73, 68, 65, 84,
      120, 156, 99, 248, 255, 255, 63, 0, 5, 254, 2, 254, 167, 53, 129, 132, 0,
      0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130,
    ]);
    final file = FileIn.fromJson({
      'maFileIn': 12,
      'tenFileGoc': 'baocao.docx',
      'dinhDangGoc': 'docx',
      'soTrang': 3,
      'trangThaiFile': 'SAN_SANG',
    });
    await tester.pumpWidget(
      MaterialApp(
        home: XemTruocFileScreen(
          file: file,
          taiTrang: (ma, trang) async {
            daTai.add('$ma/$trang');
            return thanhCong(png);
          },
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Trang 1 / 3'), findsOneWidget);
    expect(find.textContaining('máy chủ đã chuyển'), findsOneWidget);
    await tester.tap(find.byTooltip('Trang sau'));
    await tester.pumpAndSettle();
    expect(find.text('Trang 2 / 3'), findsOneWidget);
    expect(daTai, ['12/1', '12/2']);
    await tester.pumpWidget(const SizedBox());
  });

  test('Kiểm tra số trang khách gõ để nhảy tới', () {
    expect(loiSoTrang('', 120), 'Nhập số trang cần xem.');
    expect(loiSoTrang('   ', 120), 'Nhập số trang cần xem.');
    expect(loiSoTrang('-5', 120), 'Số trang phải là số dương.');
    expect(loiSoTrang('2.5', 120), contains('số nguyên'));
    expect(loiSoTrang('2,5', 120), contains('số nguyên'));
    expect(loiSoTrang('abc', 120), 'Số trang chỉ gồm chữ số.');
    expect(loiSoTrang('1e3', 120), 'Số trang chỉ gồm chữ số.');
    expect(loiSoTrang('0', 120), 'Tài liệu có 120 trang, hãy nhập từ 1 đến 120.');
    expect(loiSoTrang('121', 120), contains('từ 1 đến 120'));
    expect(loiSoTrang('99999999999999999999', 120), contains('từ 1 đến 120'));
    expect(loiSoTrang('1', 120), isNull);
    expect(loiSoTrang(' 120 ', 120), isNull);
  });

  testWidgets('Gõ số trang để nhảy tới, gõ sai thì báo lỗi và không tải', (
    tester,
  ) async {
    final daTai = <int>[];
    final png = Uint8List.fromList([
      137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1,
      0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 13, 73, 68, 65, 84,
      120, 156, 99, 248, 255, 255, 63, 0, 5, 254, 2, 254, 167, 53, 129, 132, 0,
      0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130,
    ]);
    final file = FileIn.fromJson({
      'maFileIn': 7,
      'tenFileGoc': 'giao_trinh.pdf',
      'dinhDangGoc': 'pdf',
      'soTrang': 150,
      'trangThaiFile': 'SAN_SANG',
    });
    await tester.pumpWidget(
      MaterialApp(
        home: XemTruocFileScreen(
          file: file,
          taiTrang: (ma, trang) async {
            daTai.add(trang);
            return thanhCong(png);
          },
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), '200');
    await tester.tap(find.text('Đến'));
    await tester.pumpAndSettle();
    expect(find.textContaining('từ 1 đến 150'), findsOneWidget);
    expect(daTai, [1]);
    await tester.enterText(find.byType(TextField), '-3');
    await tester.tap(find.text('Đến'));
    await tester.pumpAndSettle();
    expect(find.text('Số trang phải là số dương.'), findsOneWidget);
    await tester.enterText(find.byType(TextField), '120');
    await tester.tap(find.text('Đến'));
    await tester.pumpAndSettle();
    expect(find.text('Trang 120 / 150'), findsOneWidget);
    expect(daTai, [1, 120]);
    await tester.pumpWidget(const SizedBox());
  });
}
