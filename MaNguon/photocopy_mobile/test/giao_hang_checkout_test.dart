import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/screens/dat_in/chon_chi_nhanh_screen.dart';

void main() {
  testWidgets(
    'Lỗi báo giá có thể tính lại, không tự đặt đơn và không tràn ở 390px',
    (tester) async {
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      int baoGia = 0, datDon = 0;
      Future<KetQuaApi> post(String path, Map<String, dynamic> body) async {
        if (path.endsWith('/chi-nhanh-phu-hop')) {
          return KetQuaApi()
            ..thanhCong = true
            ..duLieu = [
              {
                'maChiNhanh': 1,
                'tenChiNhanh': 'Chi nhánh thử nghiệm có tên dài',
                'diaChi': '140 Lê Trọng Tấn',
                'banKinhGiao': 10,
              },
            ];
        }
        if (path == '/api/bao-gia') {
          baoGia++;
          if (baoGia == 1) return KetQuaApi()..loi = 'Bản đồ phản hồi chậm.';
          return KetQuaApi()
            ..thanhCong = true
            ..duLieu = {'tienIn': 20000, 'tongTien': 20000};
        }
        datDon++;
        return KetQuaApi()
          ..loi = 'Báo giá giao hàng đã hết hạn. Vui lòng tính lại giá.';
      }

      await tester.pumpWidget(
        MaterialApp(
          home: ChonChiNhanhScreen(
            file: FileIn.fromJson({
              'maFileIn': 1,
              'tenFileGoc': 'Bài thử.pdf',
              'soTrang': 10,
              'trangThaiFile': 'SAN_SANG',
            }),
            maDichVu: 1,
            tenCauHinh: 'A4 · Đen trắng',
            soBan: 1,
            dsGiaCong: const [],
            hoaToc: false,
            guiYeuCau: post,
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Tính lại phí'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.tap(find.text('Tính lại phí'));
      await tester.pumpAndSettle();
      expect(baoGia, 2);
      expect(datDon, 0);
      await tester.tap(find.text('Đặt đơn và chuyển khoản'));
      await tester.pumpAndSettle();
      expect(datDon, 1);
      expect(find.textContaining('đã hết hạn'), findsOneWidget);
      await tester.tap(find.text('Tính lại phí'));
      await tester.pumpAndSettle();
      expect(baoGia, 3);
      expect(datDon, 1); // Tính lại không tự gửi đơn lần nữa.
      await tester.tap(find.text('Giao tận nơi'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Ahamove'), findsNothing);
      expect(find.textContaining('phí theo quãng đường'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );
}
