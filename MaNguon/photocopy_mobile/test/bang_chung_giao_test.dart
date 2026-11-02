import 'dart:convert';
import 'dart:io';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/dich_vu_vi_tri.dart';
import 'package:photocopy_mobile/giao_hang.dart';
import 'package:photocopy_mobile/screens/shipper/bao_giao_that_bai_screen.dart';

base class AnhThu extends PlatformFile {
  AnhThu(this.name, String path) : uri = Uri.file(path);
  @override
  final String name;
  @override
  final Uri uri;
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

KetQuaApi ok(dynamic value) => KetQuaApi()
  ..thanhCong = true
  ..duLieu = value;
DonGiao don() => DonGiao.fromJson({
  'maDonHang': 15,
  'maDonHangHienThi': 'DH00015',
  'maGiaoHang': 8,
  'soLanGiao': 1,
  'diaChiGiao': '123 Lê Lợi',
});

void main() {
  testWidgets('Thiếu GPS thì không gửi; màn nhỏ và chữ lớn không tràn', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: MediaQuery(
          data: const MediaQueryData(textScaler: TextScaler.linear(1.5)),
          child: BaoGiaoThatBaiScreen(
            don: don(),
            layGps: () async => throw StateError('Chưa cấp quyền GPS'),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      find.text('Gửi báo cáo giao thất bại'),
      350,
      scrollable: find.byType(Scrollable).first,
    );
    expect(
      tester
          .widget<FilledButton>(
            find.widgetWithText(FilledButton, 'Gửi báo cáo giao thất bại'),
          )
          .onPressed,
      isNull,
    );
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets(
    'Có GPS ảnh lý do mới gửi; lỗi mạng giữ ảnh và nội dung để thử lại',
    (tester) async {
      final dir = Directory.systemTemp.createTempSync('photocopy-proof-test-');
      final image = File('${dir.path}/nhat-ky.png')
        ..writeAsBytesSync(
          base64Decode(
            'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=',
          ),
        );
      addTearDown(() => dir.deleteSync(recursive: true));
      int calls = 0;
      Map<String, String>? fields;
      List<Map<String, String>>? sentFiles;
      await tester.pumpWidget(
        MaterialApp(
          home: BaoGiaoThatBaiScreen(
            don: don(),
            layGps: () async =>
                DiemGps(10.7, 106.7, 12, thoiDiem: DateTime.now().toUtc()),
            traDiaChi: (_) async => ok({'diaChi': null}),
            chonAnh: () async => AnhThu('nhat-ky.png', image.path),
            guiBaoCao: (body, files) async {
              calls++;
              fields = body;
              sentFiles = files;
              return KetQuaApi()..loi = 'Chưa xác nhận được báo cáo. Thử lại.';
            },
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Khách không nghe máy').last);
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'Đã gọi hai lần');
      tester.testTextInput.hide();
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('Thêm ảnh (0/3)'),
        350,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.ensureVisible(
        find.widgetWithText(OutlinedButton, 'Thêm ảnh (0/3)'),
      );
      await tester.pumpAndSettle();
      await tester.runAsync(() async {
        await tester.tap(find.text('Thêm ảnh (0/3)'));
        await Future<void>.delayed(const Duration(milliseconds: 100));
      });
      await tester.pumpAndSettle();
      expect(find.text('nhat-ky.png'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('Gửi báo cáo giao thất bại'),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(FilledButton, 'Gửi báo cáo giao thất bại'),
            )
            .onPressed,
        isNotNull,
      );
      await tester.ensureVisible(
        find.widgetWithText(FilledButton, 'Gửi báo cáo giao thất bại'),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Gửi báo cáo giao thất bại'));
      await tester.pumpAndSettle();
      expect(calls, 1);
      expect(
        File(sentFiles!.single['path']!).readAsBytesSync(),
        image.readAsBytesSync(),
      );
      expect(fields!['lyDo'], 'Khách không nghe máy: Đã gọi hai lần');
      expect(fields!['viDo'], '10.7');
      expect(fields!['thoiDiemGps'], endsWith('Z'));
      await tester.scrollUntilVisible(
        find.textContaining('Chưa xác nhận được báo cáo'),
        -150,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      expect(find.textContaining('Chưa xác nhận được báo cáo'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('nhat-ky.png'),
        -200,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('nhat-ky.png'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );
}
