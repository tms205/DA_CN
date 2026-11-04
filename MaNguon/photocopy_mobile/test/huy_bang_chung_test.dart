import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/widgets/huy_don.dart';
import 'package:photocopy_mobile/theme.dart';

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);
  testWidgets('Hủy đơn chưa tiếp nhận không có ô ảnh và không gọi upload', (
    t,
  ) async {
    t.view.physicalSize = const Size(320, 760);
    t.view.devicePixelRatio = 1;
    addTearDown(t.view.resetPhysicalSize);
    addTearDown(t.view.resetDevicePixelRatio);
    int cancelled = 0, uploads = 0;
    await http.runWithClient(
      () async {
        await t.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: NutHuyDon(
                maDonHang: 15,
                daHuy: () async {
                  cancelled++;
                },
                guiAnh: (_, _) async {
                  uploads++;
                  return KetQuaApi();
                },
              ),
            ),
          ),
        );
        await t.tap(find.text('Hủy đơn'));
        await t.pumpAndSettle();
        expect(find.textContaining('Bằng chứng yêu cầu'), findsNothing);
        await t.tap(find.text('Xác nhận hủy'));
        await t.pumpAndSettle();
        expect(cancelled, 1);
        expect(uploads, 0);
        expect(t.takeException(), isNull);
        await t.pumpWidget(const SizedBox());
      },
      () => MockClient((req) async {
        expect(req.url.path, '/api/don-hang/15/huy');
        expect(jsonDecode(req.body).containsKey('maBangChung'), false);
        return http.Response(
          jsonEncode({'message': 'Đã hủy đơn.'}),
          200,
          headers: {'content-type': 'application/json'},
        );
      }),
    );
  });
  testWidgets('Khiếu nại lỗi ảnh giữ bản nhập và không gửi yêu cầu hoàn', (
    t,
  ) async {
    int calls = 0;
    await t.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NutHuyDon(
            maDonHang: 15,
            daThanhToan: true,
            khieuNaiSanPham: true,
            daHuy: () async {
              calls++;
            },
            thongTinHoanTien: {
              'lyDo': 'Sản phẩm có nhiều trang bị nhòe cần kiểm tra',
              'nganHang': 'Ngân hàng thử',
              'soTaiKhoan': '123456',
              'chuTaiKhoan': 'Khách thử',
              'anhBangChung': [
                {'path': '/fixture.png', 'name': 'proof.png'},
              ],
            },
            guiAnh: (_, _) async => KetQuaApi()..loi = 'Chưa lưu được ảnh.',
          ),
        ),
      ),
    );
    await t.tap(find.text('Yêu cầu hoàn tiền do sản phẩm'));
    await t.pumpAndSettle();
    await t.tap(find.text('Gửi CEO xem xét'));
    await t.pumpAndSettle();
    expect(calls, 0);
    expect(find.text('Chưa lưu được ảnh.'), findsOneWidget);
    await t.tap(find.text('Yêu cầu hoàn tiền do sản phẩm'));
    await t.pumpAndSettle();
    expect(find.text('proof.png'), findsOneWidget);
    await t.pumpWidget(const SizedBox());
  });
  test('Transport gửi ảnh đúng đơn và đọc mã bằng chứng từ backend', () async {
    final dir = Directory.systemTemp.createTempSync('cancel-upload-');
    try {
      final file = File('${dir.path}/proof.png')
        ..writeAsBytesSync(
          base64Decode(
            'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=',
          ),
        );
      int sent = 0;
      await http.runWithClient(
        () async {
          final result = await Api.guiBangChungHuy(15, [
            {'path': file.path, 'name': 'proof.png'},
          ]);
          expect(result.thanhCong, true);
          expect(
            result.duLieu['maBangChung'],
            'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
          );
          expect(sent, 1);
        },
        () => MockClient((req) async {
          sent++;
          expect(req.url.path, '/api/don-hang/15/bang-chung-huy');
          expect(
            req.headers['content-type'],
            startsWith('multipart/form-data'),
          );
          expect(
            latin1.decode(req.bodyBytes),
            contains('filename="proof.png"'),
          );
          return http.Response(
            jsonEncode({'maBangChung': 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'}),
            200,
            headers: {'content-type': 'application/json'},
          );
        }),
      );
    } finally {
      dir.deleteSync(recursive: true);
    }
  });
}
