import 'dart:convert';
import 'dart:math';

// Cùng nội dung và cùng lượt thao tác dùng một UUID khi gửi lại sau mất mạng.
class YeuCauTaoDon {
  String? _noiDung;
  String? _ma;
  Map<String, dynamic> ganMa(Map<String, dynamic> body) {
    final data = Map<String, dynamic>.from(body)..remove('maYeuCauTao');
    final serialized = jsonEncode(data);
    if (_ma == null || serialized != _noiDung) {
      final random = Random.secure();
      final bytes = List<int>.generate(16, (_) => random.nextInt(256));
      bytes[6] = (bytes[6] & 0x0f) | 0x40;
      bytes[8] = (bytes[8] & 0x3f) | 0x80;
      final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
      _ma =
          '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
      _noiDung = serialized;
    }
    return {...data, 'maYeuCauTao': _ma};
  }
}
