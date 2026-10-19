import 'dart:convert';

import 'package:http/http.dart' as http;

import 'phien.dart';
import 'kiem_tra_du_lieu.dart';

// Địa chỉ API.
// Điện thoại thật cắm USB: chạy "adb reverse tcp:5268 tcp:5268" trên máy tính để localhost:5268 trên điện thoại
// trỏ về API trên máy tính (không cần IP LAN, không cần mở Firewall).
// Muốn gọi qua Wi-Fi thì chạy: flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5268
const String diaChiApi = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://localhost:5268');

// Kết quả của 1 lần gọi API
class KetQuaApi {
  bool thanhCong = false;
  int? maHttp;       // Phân biệt mật khẩu trùng (409) với OTP không hợp lệ (400).
  dynamic duLieu;     // JSON máy chủ trả về (Map hoặc List) khi thành công
  String loi = '';    // câu báo lỗi tiếng Việt khi thất bại (API luôn trả lỗi dạng { "message": "..." })
}

// Gọi API bằng thư viện http. Màn hình nào cần dữ liệu thì tự gọi, ví dụ:
//   KetQuaApi kq = await Api.post('/api/auth/login', {'taiKhoan': ..., 'matKhau': ...});
//   if (kq.thanhCong) { ... kq.duLieu ... } else { ... kq.loi ... }
class Api {
  // Ảnh xem trước lấy từ PDF có sẵn trên máy chủ; gửi token trong header, không đưa vào URL.
  static Future<KetQuaApi> getAnhMau(String maMau, int soTrang, {int? phienBan}) async {
    try {
      final res = await http.get(
        Uri.parse('$diaChiApi/api/file-in/mau/${Uri.encodeComponent(maMau)}/trang/$soTrang${phienBan == null ? '' : '?phienBan=$phienBan'}'),
        headers: _taoHeader(),
      ).timeout(const Duration(seconds: 30));
      if (res.statusCode != 200) {
        if (res.headers['content-type']?.contains('json') == true) return _docKetQua(res);
        return KetQuaApi()..maHttp = res.statusCode..loi = 'Chưa tải được bản xem trước (${res.statusCode}).';
      }
      const png = [137, 80, 78, 71, 13, 10, 26, 10];
      final bytes = res.bodyBytes;
      bool dungPng = bytes.length >= png.length;
      for (int i = 0; dungPng && i < png.length; i++) {
        dungPng = bytes[i] == png[i];
      }
      if (res.headers['content-type']?.split(';').first != 'image/png' || bytes.length > 4 * 1024 * 1024 || !dungPng) {
        return KetQuaApi()..loi = 'Bản xem trước không hợp lệ. Vui lòng thử lại sau.';
      }
      return KetQuaApi()..thanhCong = true..maHttp = 200..duLieu = bytes;
    } catch (_) {
      return _loiKetNoi();
    }
  }

  static Future<KetQuaApi> get(String duongDan) async {
    try {
      http.Response res = await http
          .get(Uri.parse(diaChiApi + duongDan), headers: _taoHeader())
          .timeout(const Duration(seconds: 30));
      return _docKetQua(res);
    } catch (e) {
      return _loiKetNoi();
    }
  }

  static Future<KetQuaApi> post(String duongDan, Map<String, dynamic> duLieu) async {
    String? loi = kiemTraDuLieu(duongDan, duLieu);
    if (loi != null) return KetQuaApi()..loi = loi;
    try {
      http.Response res = await http
          .post(Uri.parse(diaChiApi + duongDan), headers: _taoHeader(), body: jsonEncode(duLieu))
          .timeout(const Duration(seconds: 30));
      return _docKetQua(res);
    } catch (e) {
      return _loiKetNoi();
    }
  }

  static Future<KetQuaApi> put(String duongDan, [Map<String, dynamic>? duLieu]) async {
    if (duLieu != null) {
      String? loi = kiemTraDuLieu(duongDan, duLieu);
      if (loi != null) return KetQuaApi()..loi = loi;
    }
    try {
      http.Response res = await http
          .put(Uri.parse(diaChiApi + duongDan), headers: _taoHeader(), body: duLieu == null ? null : jsonEncode(duLieu))
          .timeout(const Duration(seconds: 30));
      return _docKetQua(res);
    } catch (e) {
      return _loiKetNoi();
    }
  }

  // Tải 1 file lên (gửi dạng multipart/form-data, tên trường "file").
  // Máy chủ còn phải đổi sang PDF + đếm trang nên cho chờ tới 3 phút.
  static Future<KetQuaApi> taiFile(String duongDanFile, String tenFile, {String? kichThuocAnh, int? soLuongAnh}) async {
    if (tenFile.trim().isEmpty || tenFile.length > 255) {
      return KetQuaApi()..loi = 'Tên file không được để trống và tối đa 255 ký tự.';
    }
    try {
      final uri = Uri.parse('$diaChiApi/api/file-in').replace(
        queryParameters: kichThuocAnh == null ? null : {'kichThuocAnh': kichThuocAnh, 'soLuongAnh': '$soLuongAnh'});
      var yeuCau = http.MultipartRequest('POST', uri);
      yeuCau.headers['Authorization'] = 'Bearer ${Phien.token}';
      yeuCau.files.add(await http.MultipartFile.fromPath('file', duongDanFile, filename: tenFile));

      http.StreamedResponse traVe = await yeuCau.send().timeout(const Duration(minutes: 3));
      http.Response res = await http.Response.fromStream(traVe);
      return _docKetQua(res);
    } catch (e) {
      return _loiKetNoi();
    }
  }

  // Header gửi kèm: dữ liệu dạng JSON + token nếu đã đăng nhập
  static Map<String, String> _taoHeader() {
    Map<String, String> header = {'Content-Type': 'application/json'};
    if (Phien.token != null) header['Authorization'] = 'Bearer ${Phien.token}';
    return header;
  }

  static KetQuaApi _docKetQua(http.Response res) {
    KetQuaApi kq = KetQuaApi();
    kq.maHttp = res.statusCode;
    dynamic json;
    if (res.bodyBytes.isNotEmpty) {
      json = jsonDecode(utf8.decode(res.bodyBytes)); // utf8.decode để đọc đúng tiếng Việt
    }

    if (res.statusCode >= 200 && res.statusCode < 300) {
      kq.thanhCong = true;
      kq.duLieu = json;
    } else if (json is Map && json['message'] != null) {
      kq.loi = json['message'];
    } else {
      kq.loi = 'Có lỗi xảy ra (mã ${res.statusCode}).';
    }
    return kq;
  }

  static KetQuaApi _loiKetNoi() {
    KetQuaApi kq = KetQuaApi();
    kq.loi = 'Không kết nối được máy chủ. Kiểm tra PhotocopyApi đã chạy và đã chạy "adb reverse tcp:5268 tcp:5268".';
    return kq;
  }
}
