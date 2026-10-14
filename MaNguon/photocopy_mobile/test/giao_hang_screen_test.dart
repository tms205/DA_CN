import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/screens/shipper/shipper_screen.dart';
import 'package:photocopy_mobile/api.dart';

KetQuaApi thanhCong(dynamic data) => KetQuaApi()
  ..thanhCong = true
  ..duLieu = data;
Map<String, dynamic> don({String trangThai = 'DA_IN', int soLan = 0}) => {
  'maDonHang': 15,
  'maDonHangHienThi': 'DH00015',
  'maGiaoHang': 8,
  'tenKhachHang': 'Khách thử',
  'sdtNhan': '0901234567',
  'diaChiChiNhanh': '140 Lê Trọng Tấn',
  'diaChiGiao': '123 Lê Lợi · Chi tiết: Tầng 2',
  'trangThaiDonHang': trangThai,
  'tongTien': 39000,
  'phiShip': 19000,
  'soLanGiao': soLan,
};

void main() {
  testWidgets('Shipper có đủ nhóm chờ giao, đang giao và lịch sử', (
    tester,
  ) async {
    await tester.pumpWidget(const MaterialApp(home: ShipperScreen()));
    expect(find.text('Chờ giao'), findsOneWidget);
    expect(find.text('Đang giao'), findsOneWidget);
    expect(find.text('Lịch sử'), findsOneWidget);
    expect(find.textContaining('Không thu COD'), findsWidgets);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets(
    'Nhận đơn phải xác nhận tài liệu, cập nhật theo server và giao thành công',
    (tester) async {
      bool daNhan = false, daGiao = false;
      final posts = <String>[];
      await tester.pumpWidget(
        MaterialApp(
          home: ShipperScreen(
            taiDanhSach: (path) async => thanhCong(
              path.contains('DANG_GIAO')
                  ? (daNhan && !daGiao
                        ? [don(trangThai: 'DANG_GIAO', soLan: 1)]
                        : [])
                  : (daNhan ? [] : [don()]),
            ),
            guiThaoTac: (path, body) async {
              posts.add(path);
              if (path.endsWith('/nhan')) {
                daNhan = true;
              } else {
                expect(body['thanhCong'], true);
                daGiao = true;
              }
              return thanhCong({'message': 'Đã cập nhật'});
            },
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Nhận tài liệu đi giao'));
      await tester.pumpAndSettle();
      expect(posts, isEmpty);
      await tester.tap(find.text('Đã nhận tài liệu'));
      await tester.pumpAndSettle();
      expect(posts, ['/api/giao-hang/15/nhan']);
      await tester.tap(find.text('Đang giao'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Đã giao thành công'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Xác nhận'));
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();
      expect(posts.last, '/api/giao-hang/luot/8/ket-qua');
      expect(daGiao, true);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets(
    'Thất bại phải có lý do và lỗi server không được coi là giao thành công',
    (tester) async {
      final bodies = <Map<String, dynamic>>[];
      await tester.pumpWidget(
        MaterialApp(
          home: ShipperScreen(
            taiDanhSach: (path) async => thanhCong(
              path.contains('DANG_GIAO')
                  ? [don(trangThai: 'DANG_GIAO', soLan: 1)]
                  : [],
            ),
            guiThaoTac: (_, body) async {
              bodies.add(body);
              return KetQuaApi()..loi = 'Mất kết nối, chưa xác nhận kết quả.';
            },
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Đang giao'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Không giao được'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Xác nhận'));
      await tester.pumpAndSettle();
      expect(find.text('Nhập lý do ít nhất 5 ký tự.'), findsOneWidget);
      expect(bodies, isEmpty);
      await tester.enterText(find.byType(TextField), 'Khách không nghe máy');
      await tester.tap(find.text('Xác nhận'));
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();
      expect(bodies.single, {
        'thanhCong': false,
        'lyDo': 'Khách không nghe máy',
      });
      expect(find.text('Mất kết nối, chưa xác nhận kết quả.'), findsOneWidget);
      expect(
        find.text('Đã giao thành công'),
        findsOneWidget,
      ); // Nút còn, chưa chuyển sang lịch sử.
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets('Đủ ba lượt không có nút nhận lại, màn 320px không tràn', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: ShipperScreen(
          taiDanhSach: (_) async =>
              thanhCong([don(trangThai: 'GIAO_THAT_BAI', soLan: 3)]),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Nhận tài liệu đi giao'), findsNothing);
    expect(find.textContaining('Đã đủ 3 lượt'), findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
  });
}
