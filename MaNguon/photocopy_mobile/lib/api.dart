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
  static Future<KetQuaApi> getBienLaiHoanTien(int id, String ma) =>
      _getAnhGiaoHang('/api/don-hang/$id/bien-lai-hoan-tien/${Uri.encodeComponent(ma)}');
  static Future<KetQuaApi> guiAnhKhieuNai(int id, List<Map<String,String>> anh) =>
      _guiAnhGiaoHang('/api/don-hang/$id/khieu-nai-hoan-tien/anh', {}, anh);
  static Future<KetQuaApi> getAnhKhieuNai(int id, String ma) =>
      _getAnhGiaoHang('/api/don-hang/$id/khieu-nai-hoan-tien/anh/${Uri.encodeComponent(ma)}');
  static Future<KetQuaApi> guiBangChungGiao(int maGiao,
      Map<String, String> fields, List<Map<String, String>> anh) =>
      _guiAnhGiaoHang('/api/giao-hang/luot/$maGiao/giao-that-bai', fields, anh);

  static Future<KetQuaApi> guiDoiChieu(int maGiao, String noiDung, List<Map<String, String>> anh) =>
      _guiAnhGiaoHang('/api/giao-hang/luot/$maGiao/doi-chieu', {'noiDung': noiDung}, anh);
  static Future<KetQuaApi> guiBangChungHuy(int id,List<Map<String,String>> anh) =>
      _guiAnhGiaoHang('/api/don-hang/$id/bang-chung-huy', {}, anh);
  static Future<KetQuaApi> getAnhHuy(int id,String maAnh) =>
      _getAnhGiaoHang('/api/don-hang/$id/bang-chung-huy/anh/${Uri.encodeComponent(maAnh)}');

  static Future<KetQuaApi> _guiAnhGiaoHang(String path,
      Map<String, String> fields, List<Map<String, String>> anh) async {
    try {
      final client = http.Client();
      try {
        final request = http.MultipartRequest('POST',
            Uri.parse('$diaChiApi$path'));
        if (Phien.token != null) request.headers['Authorization'] = 'Bearer ${Phien.token}';
        request.fields.addAll(fields);
        for (final file in anh) {
          request.files.add(await http.MultipartFile.fromPath('Anh', file['path']!, filename: file['name']!));
        }
        final response = await (() async {
          final stream = await client.send(request);
          return http.Response.fromStream(stream);
        })().timeout(const Duration(seconds: 90));
        // API chưa nạp endpoint mới trả 404 rỗng; 404 có JSON vẫn là lỗi lượt giao/phạm vi.
        if (response.statusCode == 404 && response.body.trim().isEmpty) {
          return KetQuaApi()..maHttp = 404
            ..loi = 'Máy chủ chưa hỗ trợ báo cáo giao hàng này. Vui lòng liên hệ quản lý để cập nhật hệ thống; ảnh và lý do vẫn được giữ.';
        }
        return _docKetQua(response);
      } finally { client.close(); }
    } catch (_) {
      return KetQuaApi()..loi = 'Chưa xác nhận được báo cáo. Ảnh và lý do vẫn giữ ở đây; kiểm tra lịch sử trước khi gửi lại.';
    }
  }

  static Future<KetQuaApi> getAnhBangChung(int maGiao, String maAnh) =>
      _getAnhGiaoHang('/api/giao-hang/luot/$maGiao/bang-chung/anh/${Uri.encodeComponent(maAnh)}');

  static Future<KetQuaApi> getAnhDoiChieu(int maGiao, String maAnh) =>
      _getAnhGiaoHang('/api/giao-hang/luot/$maGiao/doi-chieu/anh/${Uri.encodeComponent(maAnh)}');

  static Future<KetQuaApi> _getAnhGiaoHang(String path) async {
    try {
      final res = await http.get(Uri.parse('$diaChiApi$path'),
          headers: _taoHeader()).timeout(const Duration(seconds: 30));
      if (res.statusCode != 200) return _docKetQua(res);
      if (!['image/png', 'image/jpeg'].contains(res.headers['content-type']?.split(';').first) ||
          res.bodyBytes.length > 5 * 1024 * 1024) {
        return KetQuaApi()..loi = 'Ảnh bằng chứng không hợp lệ.';
      }
      return KetQuaApi()..thanhCong = true..duLieu = res.bodyBytes;
    } catch (_) { return KetQuaApi()..loi = 'Chưa tải được ảnh bằng chứng. Vui lòng thử lại.'; }
  }

  // Ảnh xem trước lấy từ PDF có sẵn trên máy chủ; gửi token trong header, không đưa vào URL.
  static Future<KetQuaApi> getAnhMau(String maMau, int soTrang, {int? phienBan}) =>
      _getAnhPng('/api/file-in/mau/${Uri.encodeComponent(maMau)}/trang/$soTrang${phienBan == null ? '' : '?phienBan=$phienBan'}');

  // Ảnh một trang của file khách đã tải lên (bản PDF sẽ được in) để xem trước khi tạo đơn.
  static Future<KetQuaApi> getAnhTrangFile(int maFileIn, int soTrang) =>
      _getAnhPng('/api/file-in/$maFileIn/trang/$soTrang');

  static Future<KetQuaApi> _getAnhPng(String duongDan) async {
    try {
      final res = await http.get(
        Uri.parse('$diaChiApi$duongDan'),
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

  // Nhận dạng chạy trên máy chủ, có thể mất vài phút khi nạp model.
  static Future<KetQuaApi> nhanDangChu(String path, String name, String model, {List<double>? vungCat}) async {
    try {
      final request = http.MultipartRequest('POST', Uri.parse('$diaChiApi/api/ocr/nhan-dang'));
      if (Phien.token != null) request.headers['Authorization'] = 'Bearer ${Phien.token}';
      request.fields['model'] = model;
      if (vungCat != null) request.fields['vungCat'] = jsonEncode(vungCat);
      request.files.add(await http.MultipartFile.fromPath('file', path, filename: name));
      final streamed = await request.send().timeout(const Duration(minutes: 15));
      final response = await http.Response.fromStream(streamed).timeout(const Duration(minutes: 15));
      return _docKetQua(response);
    } catch (_) {
      return KetQuaApi()..loi = 'Không nhận dạng được. Kiểm tra kết nối và OCR trên máy chủ.';
    }
  }

  static Future<KetQuaApi> taoTacVuOcr(String path, String name, String model, {List<double>? vungCat}) async {
    final client = http.Client();
    try {
      final request = http.MultipartRequest('POST', Uri.parse('$diaChiApi/api/ocr/tac-vu'));
      request.headers.addAll(_taoHeader()..remove('Content-Type'));
      request.fields['model'] = model;
      if (vungCat != null) request.fields['vungCat'] = jsonEncode(vungCat);
      request.files.add(await http.MultipartFile.fromPath('file', path, filename: name));
      final response = await (() async => http.Response.fromStream(await client.send(request)))().timeout(const Duration(seconds: 90));
      return _docKetQua(response);
    } catch (_) { return KetQuaApi()..loi = 'Không gửi được ảnh. Kiểm tra kết nối rồi thử lại.'; }
    finally { client.close(); }
  }

  static Future<void> huyTacVuOcr(String id) async {
    try { await http.delete(Uri.parse('$diaChiApi/api/ocr/tac-vu/$id'), headers: _taoHeader()).timeout(const Duration(seconds: 5)); } catch (_) { }
  }

  static Future<KetQuaApi> xuatWordOcr(String text, {List<String>? pages}) async {
    try {
      final response = await http.post(Uri.parse('$diaChiApi/api/ocr/xuat-word'), headers: _taoHeader(), body: jsonEncode(pages == null ? {'text': text} : {'pages': pages})).timeout(const Duration(seconds: 60));
      if (response.statusCode < 200 || response.statusCode >= 300) {
        if (response.headers['content-type']?.contains('json') == true) return _docKetQua(response);
        return KetQuaApi()..loi = 'Không xuất được file Word.';
      }
      final bytes = response.bodyBytes;
      if (bytes.length < 4 || bytes.length > 4 * 1024 * 1024 || bytes[0] != 80 || bytes[1] != 75 || response.headers['content-type']?.split(';').first != 'application/vnd.openxmlformats-officedocument.wordprocessingml.document') {
        return KetQuaApi()..loi = 'File Word trả về không hợp lệ.';
      }
      return KetQuaApi()..thanhCong = true..duLieu = bytes;
    } catch (_) { return KetQuaApi()..loi = 'Không tải được file Word. Kiểm tra kết nối và thử lại.'; }
  }

  // Header gửi kèm: dữ liệu dạng JSON + token nếu đã đăng nhập
  static Map<String, String> _taoHeader() {
    Map<String, String> header = {'Content-Type': 'application/json', 'X-Photocopy-Platform': 'app'};
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
