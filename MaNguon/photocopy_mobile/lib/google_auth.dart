import 'package:google_sign_in/google_sign_in.dart';
import 'api.dart';

// ID công khai lấy từ API; không nhúng Client Secret hay mật khẩu Google vào app.
class GoogleAuth {
  static Future<void>? _khoiTao;
  static String? _clientId;

  // null = người dùng hủy chọn Google, không coi là lỗi mật khẩu.
  static Future<KetQuaApi?> tiepTuc(bool ghiNho) async {
    final cfg = await Api.get('/api/auth/google/config');
    if (!cfg.thanhCong) return cfg;
    final data = cfg.duLieu;
    if (data is! Map ||
        data['daBat'] != true ||
        data['webClientId'] is! String ||
        data['webClientId'].isEmpty) {
      return KetQuaApi()
        ..loi = 'Chưa cấu hình Google. Bạn vẫn có thể đăng nhập bằng mật khẩu.';
    }
    final String clientId = data['webClientId'];
    if (_clientId != null && _clientId != clientId) {
      return KetQuaApi()
        ..loi = 'Cấu hình Google đã thay đổi. Hãy đóng và mở lại ứng dụng.';
    }
    try {
      _clientId = clientId;
      _khoiTao ??= GoogleSignIn.instance.initialize(serverClientId: clientId);
      await _khoiTao;
      if (!GoogleSignIn.instance.supportsAuthenticate()) {
        return KetQuaApi()
          ..loi =
              'Đăng nhập Google này dành cho app Android. Vui lòng dùng mật khẩu trên nền tảng hiện tại.';
      }
      final user = await GoogleSignIn.instance.authenticate();
      final idToken = user.authentication.idToken;
      if (idToken == null || idToken.isEmpty) {
        return KetQuaApi()
          ..loi =
              'Google chưa trả token xác thực. Kiểm tra Web Client ID và cấu hình Android.';
      }
      return await Api.post('/api/auth/google', {
        'idToken': idToken,
        'ghiNho': ghiNho,
      });
    } on GoogleSignInException catch (e) {
      if (e.code == GoogleSignInExceptionCode.canceled) return null;
      return KetQuaApi()
        ..loi = e.code == GoogleSignInExceptionCode.clientConfigurationError
            ? 'Google cấu hình chưa đúng. Kiểm tra package Android, SHA-1 và Web Client ID.'
            : 'Không mở được Google. Kiểm tra kết nối mạng và thử lại.';
    } catch (_) {
      return KetQuaApi()
        ..loi =
            'Không hoàn tất được đăng nhập Google. Vui lòng thử lại hoặc dùng mật khẩu.';
    }
  }
}
