import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/api.dart';

void main() {
  test('API cũ trả 404 rỗng phải báo máy chủ cần cập nhật', () async {
    String? route;
    final result = await http.runWithClient(() => Api.guiBangChungGiao(77, {}, []),
      () => MockClient((request) async { route = request.url.path; return http.Response('', 404); }));
    expect(route, '/api/giao-hang/luot/77/giao-that-bai');
    expect(result.thanhCong, false);
    expect(result.maHttp, 404);
    expect(result.loi, contains('cập nhật'));
  });

  test('404 nghiệp vụ vẫn giữ đúng thông báo không tìm thấy lượt', () async {
    final result = await http.runWithClient(() => Api.guiBangChungGiao(77, {}, []),
      () => MockClient((_) async => http.Response(jsonEncode({'message': 'Không tìm thấy lượt giao của bạn.'}),
          404, headers: {'content-type': 'application/json; charset=utf-8'})));
    expect(result.thanhCong, false);
    expect(result.loi, 'Không tìm thấy lượt giao của bạn.');
  });
}
