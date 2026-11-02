import 'dart:convert';
import 'dart:io';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/screens/dat_in/nhan_dang_chu_screen.dart';

base class AnhThu extends PlatformFile {
  AnhThu(this.name, String path) : uri = Uri.file(path);
  @override
  final String name;
  @override
  final Uri uri;
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  test('Word nhiều ảnh gửi các bản sửa đúng thứ tự', () async {
    final result = await http.runWithClient(
      () => Api.xuatWordOcr('', pages: ['Ảnh một đã sửa', 'Ảnh hai đã sửa']),
      () => MockClient((request) async {
        expect(jsonDecode(request.body)['pages'], [
          'Ảnh một đã sửa',
          'Ảnh hai đã sửa',
        ]);
        return http.Response.bytes(
          [80, 75, 3, 4],
          200,
          headers: {
            'content-type':
                'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
          },
        );
      }),
    );
    expect(result.thanhCong, isTrue);
  });

  testWidgets('Nhiều ảnh đổi thứ tự, xóa và không tràn ở màn hình nhỏ', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(360, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final path = File('test/fixtures/ocr_exif6.jpg').absolute.path;
    await tester.pumpWidget(
      MaterialApp(
        home: NhanDangChuScreen(
          chonAnh: () async => [
            AnhThu('dau.jpg', path),
            AnhThu('sau.jpg', path),
          ],
        ),
      ),
    );
    await tester.runAsync(() async {
      await tester.tap(find.text('Thêm ảnh (0/10)'));
      await Future<void>.delayed(const Duration(milliseconds: 100));
    });
    await tester.pumpAndSettle();
    expect(find.text('1. dau.jpg'), findsOneWidget);
    expect(find.text('2. sau.jpg'), findsOneWidget);
    await tester.tap(find.byTooltip('Đưa ảnh lên trước').last);
    await tester.pumpAndSettle();
    expect(find.text('1. sau.jpg'), findsOneWidget);
    await tester.tap(find.byTooltip('Xóa ảnh').first);
    await tester.pumpAndSettle();
    expect(find.text('1. dau.jpg'), findsOneWidget);
    expect(find.text('Thêm ảnh (1/10)'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('Không nhận quá 10 ảnh và giữ giao diện hoạt động', (
    tester,
  ) async {
    final path = File('test/fixtures/ocr_exif6.jpg').absolute.path;
    await tester.pumpWidget(
      MaterialApp(
        home: NhanDangChuScreen(
          chonAnh: () async => List.generate(11, (i) => AnhThu('$i.jpg', path)),
        ),
      ),
    );
    await tester.tap(find.text('Thêm ảnh (0/10)'));
    await tester.pumpAndSettle();
    expect(find.text('Mỗi lượt tối đa 10 ảnh.'), findsOneWidget);
    expect(find.text('Thêm ảnh (0/10)'), findsOneWidget);
  });
  testWidgets('Theo dõi hàng chờ, ảnh lỗi không làm mất kết quả ảnh khác', (tester) async {
    final path = File('test/fixtures/ocr_exif6.jpg').absolute.path;
    int created = 0;
    await http.runWithClient(() async {
      await tester.pumpWidget(MaterialApp(home: NhanDangChuScreen(chonAnh: () async => [AnhThu('dau.jpg', path), AnhThu('sau.jpg', path)])));
      await tester.runAsync(() async { await tester.tap(find.text('Thêm ảnh (0/10)')); await Future<void>.delayed(const Duration(milliseconds: 100)); });
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Nhận dạng ảnh chưa xong / bị lỗi'));
      await tester.runAsync(() async { await tester.tap(find.text('Nhận dạng ảnh chưa xong / bị lỗi')); await Future<void>.delayed(const Duration(milliseconds: 150)); });
      await tester.pump();
      expect(find.text('Ảnh 1/2 · Đang chờ · thứ tự 2'), findsOneWidget);
      for (int i = 0; i < 6; i++) {
        await tester.pump();
        await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 900)));
        await tester.pump();
      }
      expect(created, 2);
      expect(find.text('Đã nhận dạng'), findsOneWidget);
      expect(find.text('Cần thử lại'), findsOneWidget);
      final export = tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Xuất tất cả ra Word'));
      expect(export.onPressed, isNull);
      await tester.ensureVisible(find.text('1. dau.jpg'));
      await tester.tap(find.text('1. dau.jpg'));
      await tester.pump();
      expect(find.text('Kết quả ảnh đầu'), findsOneWidget);
      expect(tester.takeException(), isNull);
    }, () => MockClient((request) async {
      dynamic data;
      if (request.method == 'POST') {
        data = {'id': 'job-${++created}', 'state': 'CHO_XU_LY', 'position': 2};
      } else if (request.method == 'DELETE') {
        data = {'message': 'Đã dừng'};
      } else if (request.url.path.endsWith('job-1')) {
        data = {'state': 'HOAN_TAT', 'text': 'Kết quả ảnh đầu', 'truncated': false};
      } else {
        data = {'state': 'THAT_BAI', 'message': 'Ảnh không có chữ rõ'};
      }
      return http.Response(jsonEncode(data), 200, headers: {'content-type': 'application/json'});
    }));
  });

}
