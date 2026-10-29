import 'dart:convert';
import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/api.dart';

void main() {
  test('Xuất Word gửi đúng nội dung người dùng đã sửa', () async {
    const text = 'Đã sửa tiếng Việt <đúng> & rõ\nDòng mới';
    final result = await http.runWithClient(
      () => Api.xuatWordOcr(text),
      () => MockClient((request) async {
        expect(request.url.path, '/api/ocr/xuat-word');
        expect(jsonDecode(request.body)['text'], text);
        return http.Response.bytes(
          [80, 75, 3, 4, 0],
          200,
          headers: {
            'content-type':
                'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
          },
        );
      }),
    );
    expect(result.thanhCong, isTrue);
    expect(result.duLieu, [80, 75, 3, 4, 0]);
  });
  test(
    'File Word lỗi và bảo trì được thông báo thay vì lưu file hỏng',
    () async {
      final result = await http.runWithClient(
        () => Api.xuatWordOcr('Văn bản'),
        () => MockClient(
          (request) async => http.Response(
            jsonEncode({'message': 'Đang bảo trì'}),
            503,
            headers: {'content-type': 'application/json'},
          ),
        ),
      );
      expect(result.thanhCong, isFalse);
      expect(result.loi, 'Đang bảo trì');
    },
  );
  test('OCR gửi vùng cắt cùng ảnh gốc tới máy chủ', () async {
    final dir = await Directory.systemTemp.createTemp('ocr-api-test-');
    try {
      final file = File('${dir.path}/source.png');
      await file.writeAsBytes([1, 2, 3]);
      final result = await http.runWithClient(
        () => Api.nhanDangChu(
          file.path,
          'source.png',
          '2b',
          vungCat: [.1, .2, .5, .6],
        ),
        () => MockClient((request) async {
          expect(request.url.path, '/api/ocr/nhan-dang');
          expect(request.body, contains('name="vungCat"'));
          expect(request.body, contains('[0.1,0.2,0.5,0.6]'));
          return http.Response(
            jsonEncode({'text': 'Nội dung vùng chọn', 'truncated': false}),
            200,
            headers: {'content-type': 'application/json'},
          );
        }),
      );
      expect(result.thanhCong, isTrue);
    } finally {
      await dir.delete(recursive: true);
    }
  });
}
