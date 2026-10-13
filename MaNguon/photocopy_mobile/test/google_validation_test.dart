import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/kiem_tra_du_lieu.dart';

void main() {
  Map<String, dynamic> hopLe() => {
    'veDangKy': 'a' * 64,
    'hoTen': 'Nguyễn Văn A',
    'soDienThoai': '0912345678',
    'dongYDieuKhoan': true,
  };
  test('Google đăng ký cần vé, tên, điện thoại và điều khoản', () {
    for (final thay in [
      {'veDangKy': ''},
      {'hoTen': ' '},
      {'soDienThoai': '09123'},
      {'soDienThoai': '0١٢٣٤٥٦٧٨٩'},
      {'dongYDieuKhoan': false},
    ]) {
      expect(
        kiemTraDuLieu('/api/auth/google/register', {...hopLe(), ...thay}),
        isNotNull,
      );
    }
    expect(kiemTraDuLieu('/api/auth/google/register', hopLe()), isNull);
  });
}
