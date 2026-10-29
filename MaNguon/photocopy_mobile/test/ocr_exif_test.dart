import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter_test/flutter_test.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  test('Ảnh camera EXIF dùng cùng hướng hiển thị và cắt ở máy chủ', () async {
    final bytes = await File('test/fixtures/ocr_exif6.jpg').readAsBytes();
    final buffer = await ui.ImmutableBuffer.fromUint8List(bytes);
    final descriptor = await ui.ImageDescriptor.encoded(buffer);
    final codec = await descriptor.instantiateCodec();
    final frame = await codec.getNextFrame();
    expect(frame.image.width, 100);
    expect(frame.image.height, 80);
    frame.image.dispose();
    codec.dispose();
    descriptor.dispose();
    buffer.dispose();
  });
}
