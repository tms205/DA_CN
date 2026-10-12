import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/dinh_dang.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/phien.dart';
import 'package:photocopy_mobile/screens/toi/doi_mat_khau_screen.dart';
import 'package:photocopy_mobile/screens/toi/toi_screen.dart';
import 'package:photocopy_mobile/theme.dart';

NguoiDung khach() => NguoiDung.fromJson({
  'maNguoiDung': 8,
  'hoTen': 'Nguyễn Thị Sinh Viên',
  'email': 'sv1@huit.edu.vn',
  'tenVaiTro': 'CUSTOMER',
});

NguoiDung shipper() => NguoiDung.fromJson({
  'maNguoiDung': 6,
  'hoTen': 'Đỗ Văn Giao',
  'email': 'ship1@photocopy.vn',
  'tenVaiTro': 'SHIPPER',
  'tenChiNhanh': 'Cơ sở 1',
});

DonHang donCoTrangThai(String trangThai) => DonHang.fromJson({
  'maDonHang': 1,
  'tenChiNhanh': 'Cơ sở 1',
  'hinhThucNhan': 'TAI_QUAY',
  'trangThaiDonHang': trangThai,
  'tongTien': 15750,
  'ngayDat': '2026-09-15T09:00:00',
});

DonHang donCoThanhToan(
  String phuongThuc,
  String trangThaiThanhToan, {
  String trangThaiDon = 'CHO_XAC_NHAN',
}) => DonHang.fromJson({
  'maDonHang': 2,
  'tenChiNhanh': 'Cơ sở 1',
  'hinhThucNhan': 'TAI_QUAY',
  'trangThaiDonHang': trangThaiDon,
  'phuongThucThanhToan': phuongThuc,
  'trangThaiThanhToan': trangThaiThanhToan,
  'tongTien': 17500,
  'ngayDat': '2026-09-22T18:08:00',
});

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);
  tearDown(() => Phien.nguoiDung = null);

  // Mở 1 màn hình ở khung 390×844 như Figma, với người đang đăng nhập cho trước
  Future<void> mo(WidgetTester tester, Widget man, NguoiDung nd) async {
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    Phien.nguoiDung = nd;
    await tester.pumpWidget(MaterialApp(home: man));
    await tester.pumpAndSettle();
  }

  testWidgets('Tab Tôi của khách: thẻ người dùng, ô đơn hàng, mục địa chỉ', (
    tester,
  ) async {
    await mo(tester, const ToiScreen(), khach());

    expect(find.text('Nguyễn Thị Sinh Viên'), findsOneWidget);
    expect(find.text('SV'), findsOneWidget);
    expect(find.text('Đơn hàng của tôi'), findsOneWidget);
    expect(find.text('Địa chỉ nhận hàng'), findsOneWidget);
    expect(find.text('Lịch sử mua hàng'), findsOneWidget);
    // Dòng Đăng xuất nằm dưới mép màn 844 — ListView chỉ dựng khi cuộn tới
    await tester.scrollUntilVisible(find.text('Đăng xuất'), 300);
    expect(find.text('Đăng xuất'), findsOneWidget);
  });

  testWidgets('Tab Tôi của Shipper: không có mục chỉ dành cho khách', (
    tester,
  ) async {
    await mo(tester, const ToiScreen(), shipper());

    expect(find.text('Nhân viên giao hàng · Cơ sở 1'), findsOneWidget);
    expect(find.text('Đơn hàng của tôi'), findsNothing);
    expect(find.text('Địa chỉ nhận hàng'), findsNothing);
    expect(find.text('Đổi mật khẩu'), findsOneWidget);
  });

  testWidgets('Đổi mật khẩu: nhập lại không khớp thì báo lỗi, không gọi API', (
    tester,
  ) async {
    await mo(tester, const DoiMatKhauScreen(), khach());

    Finder o = find.byType(TextField);
    await tester.enterText(o.at(0), 'Demo@123');
    await tester.enterText(o.at(1), 'Matkhau123');
    await tester.enterText(o.at(2), 'Matkhau124');
    await tester.tap(find.text('Lưu mật khẩu mới'));
    await tester.pump();

    expect(find.text('Mật khẩu nhập lại không khớp.'), findsOneWidget);
  });

  test('Nhóm lọc đơn hàng theo trạng thái', () {
    expect(donCoTrangThai('CHO_XAC_NHAN').nhom, 'DANG_XU_LY');
    expect(donCoTrangThai('DA_IN').nhom, 'DANG_XU_LY');
    expect(donCoTrangThai('GIAO_THAT_BAI').nhom, 'DANG_GIAO');
    expect(donCoTrangThai('DA_GIAO').nhom, 'HOAN_TAT');
    expect(donCoTrangThai('DA_HUY').nhom, 'DA_HUY');
  });

  test('Chỉ đơn VietQR chưa trả và chưa hủy được quay lại màn thanh toán', () {
    expect(
      donCoThanhToan('VIETQR', 'CHUA_THANH_TOAN').dangChoChuyenKhoan,
      isTrue,
    );
    expect(
      donCoThanhToan('VIETQR', 'DA_THANH_TOAN').dangChoChuyenKhoan,
      isFalse,
    );
    expect(
      donCoThanhToan('TIEN_MAT', 'CHUA_THANH_TOAN').dangChoChuyenKhoan,
      isFalse,
    );
    expect(
      donCoThanhToan(
        'VIETQR',
        'CHUA_THANH_TOAN',
        trangThaiDon: 'DA_HUY',
      ).dangChoChuyenKhoan,
      isFalse,
    );
  });

  test('Chữ viết tắt trong ô ảnh đại diện', () {
    expect(khach().vietTat, 'SV');
    expect(shipper().vietTat, 'VG');
  });

  test('Thời gian tương đối trong danh sách thông báo', () {
    DateTime bayGio = DateTime(2026, 9, 15, 14, 0);
    expect(
      thoiGianTuongDoi(DateTime(2026, 9, 15, 13, 59, 30), bayGio),
      'Vừa xong',
    );
    expect(
      thoiGianTuongDoi(DateTime(2026, 9, 15, 13, 35), bayGio),
      '25 phút trước',
    );
    expect(
      thoiGianTuongDoi(DateTime(2026, 9, 15, 9, 0), bayGio),
      '5 giờ trước',
    );
    expect(
      thoiGianTuongDoi(DateTime(2026, 9, 14, 23, 10), bayGio),
      'Hôm qua 23:10',
    );
    expect(
      thoiGianTuongDoi(DateTime(2026, 9, 12, 8, 0), bayGio),
      '3 ngày trước',
    );
    expect(thoiGianTuongDoi(DateTime(2026, 8, 1, 8, 0), bayGio), '01/08/2026');
  });
}
