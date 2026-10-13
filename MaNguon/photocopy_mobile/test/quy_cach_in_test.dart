import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/quy_cach_in.dart';

void main() {
  DichVu dv(String ten, [String? nhom]) => DichVu.fromJson({
    'maDichVu': 1,
    'tenDichVu': ten,
    'loaiDichVu': 'GIA_CONG',
    'nhomGiaCong': nhom,
  });
  test('Số tờ thực tế từng bản ở các biên 50/51 và 100/101', () {
    expect(soToMoiBan(50, '1_MAT'), 50);
    expect(soToMoiBan(51, '1_MAT'), 51);
    expect(soToMoiBan(100, '2_MAT'), 50);
    expect(soToMoiBan(101, '2_MAT'), 51);
    expect(soToMoiBan(1, '2_MAT'), 1);
  });
  test('Loại trừ hai chiều, giữ plastic và kim góc + bìa kiếng', () {
    final loXo = dv('Đóng lò xo', 'DONG_GAY'),
        kim = dv('Bấm kim góc'),
        bia = dv('Đóng bìa kiếng', 'DONG_GAY');
    expect(giaCongLoaiTru(loXo, kim), isTrue);
    expect(giaCongLoaiTru(kim, loXo), isTrue);
    expect(giaCongLoaiTru(loXo, bia), isFalse);
    expect(giaCongLoaiTru(bia, loXo), isFalse);
    expect(giaCongLoaiTru(dv(' ĐÓNG BÌA KIẾNG ', 'DONG_GAY'), loXo), isFalse);
    expect(giaCongLoaiTru(dv('Kiểu A', 'DONG_GAY'), dv('Kiểu B', 'DONG_GAY')), isTrue);
    expect(giaCongLoaiTru(loXo, dv('Ép plastic')), isFalse);
    expect(giaCongLoaiTru(bia, kim), isFalse);
  });
  test('Chi tiết đơn cũ mặc định đứng, đơn mới hiển thị ngang', () {
    final json = <String, dynamic>{
      'maChiTiet': 1,
      'maFile': 1,
      'tenFile': 'a.pdf',
      'trangThaiFile': 'SAN_SANG',
      'tenDichVu': 'In A4',
      'khoGiay': 'A4',
      'loaiGiay': '70gsm',
      'matIn': '2_MAT',
      'mauSac': 'DEN_TRANG',
    };
    expect(DongInChiTiet.fromJson(json).quyCach, contains('Giấy đứng'));
    expect(
      DongInChiTiet.fromJson({...json, 'huongGiay': 'NGANG'}).quyCach,
      contains('Giấy ngang'),
    );
  });
}
