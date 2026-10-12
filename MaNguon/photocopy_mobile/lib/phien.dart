import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'models.dart';

// Phiên đăng nhập của App: token + người đang đăng nhập.
// Để trong biến static nên màn hình nào cũng đọc được: Phien.token, Phien.nguoiDung.
// Tick "Ghi nhớ đăng nhập" thì lưu vào kho bảo mật của điện thoại (Keystore / Keychain)
// để lần sau mở app không phải đăng nhập lại; không tick thì tắt app là mất.
class Phien {
  static String? token;
  static DateTime? hetHanLuc;
  static NguoiDung? nguoiDung;
  static bool ghiNho = false;

  static const FlutterSecureStorage _kho = FlutterSecureStorage();
  static const String _tenKhoa = 'photocopychain.phien';

  static bool get laShipper => nguoiDung != null && nguoiDung!.laShipper;

  // Gọi sau khi API đăng nhập / đăng ký trả về { accessToken, hetHanLuc, nguoiDung }
  static Future<void> batDau(Map<String, dynamic> json, bool coGhiNho) async {
    token = json['accessToken'];
    hetHanLuc = DateTime.parse(json['hetHanLuc']).toUtc();
    nguoiDung = NguoiDung.fromJson(json['nguoiDung']);
    ghiNho = coGhiNho;
    await luuVaoMay();
  }

  // Sửa hồ sơ xong: đổi thông tin người dùng, giữ nguyên token
  static Future<void> capNhatNguoiDung(NguoiDung nd) async {
    nguoiDung = nd;
    await luuVaoMay();
  }

  // Ghi (hoặc xóa) phiên trong kho bảo mật tùy theo "ghiNho"
  static Future<void> luuVaoMay() async {
    try {
      if (ghiNho && token != null) {
        Map<String, dynamic> json = {
          'accessToken': token,
          'hetHanLuc': hetHanLuc!.toIso8601String(),
          'nguoiDung': nguoiDung!.toJson(),
        };
        await _kho.write(key: _tenKhoa, value: jsonEncode(json));
      } else {
        await _kho.delete(key: _tenKhoa);
      }
    } catch (e) {
      // không lưu được thì vẫn dùng phiên trong bộ nhớ
    }
  }

  // Lúc mở app: đọc phiên đã lưu. Trả về true nếu còn phiên đăng nhập hợp lệ.
  static Future<bool> docTuMay() async {
    try {
      String? chuoi = await _kho.read(key: _tenKhoa);
      if (chuoi == null) return false;

      Map<String, dynamic> json = jsonDecode(chuoi);
      DateTime hetHan = DateTime.parse(json['hetHanLuc']).toUtc();
      if (hetHan.isBefore(DateTime.now().toUtc())) {
        await _kho.delete(key: _tenKhoa); // token hết hạn thì bỏ, bắt đăng nhập lại
        return false;
      }

      token = json['accessToken'];
      hetHanLuc = hetHan;
      nguoiDung = NguoiDung.fromJson(json['nguoiDung']);
      ghiNho = true;
      return true;
    } catch (e) {
      return false; // kho bảo mật lỗi hoặc dữ liệu cũ hỏng → coi như chưa đăng nhập
    }
  }

  static Future<void> dangXuat() async {
    token = null;
    hetHanLuc = null;
    nguoiDung = null;
    ghiNho = false;
    try {
      await _kho.delete(key: _tenKhoa);
    } catch (e) {
      // bỏ qua
    }
  }
}
