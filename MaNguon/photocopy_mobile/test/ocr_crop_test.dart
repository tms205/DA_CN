import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/screens/dat_in/cat_anh_ocr_screen.dart';

void main() {
  testWidgets('Kéo vùng chữ trả tọa độ đúng và ảnh xem trước đã cắt', (
    tester,
  ) async {
    final dir = (await tester.runAsync(
      () => Directory.systemTemp.createTemp('ocr-crop-test-'),
    ))!;
    final file = File('${dir.path}/source.png');
    await tester.runAsync(() async {
      final recorder = ui.PictureRecorder();
      final canvas = Canvas(recorder);
      canvas.drawRect(
        const Rect.fromLTWH(0, 0, 100, 100),
        Paint()..color = Colors.white,
      );
      canvas.drawRect(
        const Rect.fromLTWH(25, 20, 50, 60),
        Paint()..color = Colors.red,
      );
      final picture = recorder.endRecording();
      final image = await picture.toImage(100, 100);
      final data = await image.toByteData(format: ui.ImageByteFormat.png);
      await file.writeAsBytes(data!.buffer.asUint8List());
      image.dispose();
      picture.dispose();
    });
    VungCatOcr? result;
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: ElevatedButton(
                onPressed: () async {
                  result = await Navigator.push<VungCatOcr>(
                    context,
                    MaterialPageRoute(
                      builder: (_) => CatAnhOcrScreen(path: file.path),
                    ),
                  );
                },
                child: const Text('Mở'),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('Mở'));
    await tester.pump();
    final area = find.byKey(const Key('ocr-crop-area'));
    for (var i = 0; i < 20 && area.evaluate().isEmpty; i++) {
      await tester.runAsync(() async {
        await Future<void>.delayed(const Duration(milliseconds: 50));
      });
      await tester.pump(const Duration(milliseconds: 100));
    }
    await tester.pump(const Duration(milliseconds: 500));
    expect(area, findsOneWidget);
    final bounds = tester.getRect(area);
    final gesture = await tester.startGesture(
      bounds.topLeft + Offset(bounds.width * .25, bounds.height * .2),
    );
    await gesture.moveTo(
      bounds.topLeft + Offset(bounds.width * .75, bounds.height * .8),
    );
    await gesture.up();
    await tester.pump();
    await tester.tap(find.text('Dùng vùng đã chọn'));
    for (var i = 0; i < 20 && result == null; i++) {
      await tester.runAsync(() async {
        await Future<void>.delayed(const Duration(milliseconds: 50));
      });
      await tester.pump(const Duration(milliseconds: 100));
    }
    await tester.pumpAndSettle();
    expect(result, isNotNull);
    expect(result!.rect[0], closeTo(.25, .02));
    expect(result!.rect[1], closeTo(.2, .02));
    expect(result!.rect[2], closeTo(.5, .02));
    expect(result!.rect[3], closeTo(.6, .02));
    expect(result!.preview.length, greaterThan(20));
    await tester.pumpWidget(const SizedBox());
    await tester.runAsync(() => dir.delete(recursive: true));
  });
}
