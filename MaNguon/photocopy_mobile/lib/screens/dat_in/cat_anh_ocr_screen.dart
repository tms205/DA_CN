import 'dart:io';
import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';

class VungCatOcr {
  final List<double> rect;
  final Uint8List preview;
  const VungCatOcr(this.rect, this.preview);
}

// Tọa độ chuẩn hóa theo ảnh đã giải mã/xoay EXIF; API áp dụng trên ảnh gốc.
class CatAnhOcrScreen extends StatefulWidget {
  final String path;
  final List<double>? initial;
  const CatAnhOcrScreen({super.key, required this.path, this.initial});
  @override
  State<CatAnhOcrScreen> createState() => _CatAnhOcrScreenState();
}

class _CatAnhOcrScreenState extends State<CatAnhOcrScreen> {
  ui.Image? _image;
  Rect _rect = const Rect.fromLTWH(0, 0, 1, 1);
  Offset? _anchor;
  String? _loi;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final initial = widget.initial;
    if (initial != null) {
      _rect = Rect.fromLTWH(initial[0], initial[1], initial[2], initial[3]);
    }
    _load();
  }

  @override
  void dispose() {
    _image?.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final file = File(widget.path);
      if (await file.length() > 10 * 1024 * 1024) {
        throw Exception('Ảnh vượt quá 10 MB.');
      }
      final bytes = await file.readAsBytes();
      final buffer = await ui.ImmutableBuffer.fromUint8List(bytes);
      ui.Codec? codec;
      ui.ImageDescriptor? descriptor;
      try {
        descriptor = await ui.ImageDescriptor.encoded(buffer);
        if (descriptor.width * descriptor.height > 20000000) {
          throw Exception('Ảnh vượt quá 20 megapixel.');
        }
        codec = await descriptor.instantiateCodec();
        final frame = await codec.getNextFrame();
        if (!mounted) {
          frame.image.dispose();
          return;
        }
        setState(() => _image = frame.image);
      } finally {
        codec?.dispose();
        descriptor?.dispose();
        buffer.dispose();
      }
    } catch (_) {
      if (mounted) {
        setState(
          () => _loi =
              'Không mở được ảnh. Chọn JPG/PNG hợp lệ, tối đa 10 MB và 20 megapixel.',
        );
      }
    }
  }

  Offset _point(Offset position, Size size) => Offset(
    (position.dx / size.width).clamp(0.0, 1.0),
    (position.dy / size.height).clamp(0.0, 1.0),
  );

  Future<void> _apply() async {
    final image = _image;
    if (image == null || _saving) return;
    final source = Rect.fromLTWH(
      _rect.left * image.width,
      _rect.top * image.height,
      _rect.width * image.width,
      _rect.height * image.height,
    );
    if (source.width < 8 || source.height < 8) {
      setState(() => _loi = 'Vùng cắt quá nhỏ. Hãy chọn vùng chữ lớn hơn.');
      return;
    }
    setState(() {
      _saving = true;
      _loi = null;
    });
    try {
      final scale = math.min(1.0, 1600 / math.max(source.width, source.height));
      final width = math.max(1, (source.width * scale).round()),
          height = math.max(1, (source.height * scale).round());
      final recorder = ui.PictureRecorder();
      final canvas = Canvas(recorder);
      canvas.drawColor(Colors.white, BlendMode.src);
      canvas.drawImageRect(
        image,
        source,
        Rect.fromLTWH(0, 0, width.toDouble(), height.toDouble()),
        Paint()..filterQuality = FilterQuality.high,
      );
      final picture = recorder.endRecording();
      try {
        final preview = await picture.toImage(width, height);
        try {
          final data = await preview.toByteData(format: ui.ImageByteFormat.png);
          if (!mounted) return;
          if (data == null) throw Exception('Không tạo được ảnh xem trước');
          Navigator.pop(
            context,
            VungCatOcr([
              _rect.left,
              _rect.top,
              _rect.width,
              _rect.height,
            ], data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes)),
          );
        } finally {
          preview.dispose();
        }
      } finally {
        picture.dispose();
      }
    } catch (_) {
      if (mounted) {
        setState(() => _loi = 'Không cắt được ảnh. Vui lòng chọn lại vùng.');
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Cắt vùng văn bản')),
    body: SafeArea(
      child: Column(
        children: [
          const Padding(
            padding: EdgeInsets.all(16),
            child: Text(
              'Kéo ngón tay khoanh vùng chữ. Giữ đủ dấu và dòng chữ; có thể kéo lại để thay vùng.',
            ),
          ),
          if (_loi != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(
                _loi!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          Expanded(
            child: _image == null
                ? Center(
                    child: _loi == null
                        ? const CircularProgressIndicator()
                        : const Icon(Icons.broken_image_outlined, size: 48),
                  )
                : LayoutBuilder(
                    builder: (context, box) {
                      final image = _image!;
                      final scale = math.min(
                        box.maxWidth / image.width,
                        box.maxHeight / image.height,
                      );
                      final size = Size(
                        image.width * scale,
                        image.height * scale,
                      );
                      return Center(
                        child: GestureDetector(
                          key: const Key('ocr-crop-area'),
                          onPanDown: _saving
                              ? null
                              : (event) =>
                                    _anchor = _point(event.localPosition, size),
                          onPanCancel: () => _anchor = null,
                          onPanUpdate: _saving
                              ? null
                              : (event) {
                                  if (_anchor == null) return;
                                  final rect = Rect.fromPoints(
                                    _anchor!,
                                    _point(event.localPosition, size),
                                  );
                                  if (rect.width >= .005 && rect.height >= .005) {
                                    setState(() {
                                      _rect = rect;
                                      _loi = null;
                                    });
                                  }
                                },
                          onPanEnd: (_) => _anchor = null,
                          child: SizedBox(
                            width: size.width,
                            height: size.height,
                            child: CustomPaint(
                              painter: _CropPainter(image, _rect),
                            ),
                          ),
                        ),
                      );
                    },
                  ),
          ),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                OutlinedButton(
                  onPressed: _image == null || _saving
                      ? null
                      : () => setState(() {
                          _rect = const Rect.fromLTWH(0, 0, 1, 1);
                          _loi = null;
                        }),
                  child: const Text('Chọn toàn bộ ảnh'),
                ),
                const SizedBox(height: 8),
                FilledButton(
                  onPressed: _image == null || _saving ? null : _apply,
                  child: Text(_saving ? 'Đang cắt…' : 'Dùng vùng đã chọn'),
                ),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}

class _CropPainter extends CustomPainter {
  final ui.Image image;
  final Rect selection;
  _CropPainter(this.image, this.selection);
  @override
  void paint(Canvas canvas, Size size) {
    canvas.drawImageRect(
      image,
      Rect.fromLTWH(0, 0, image.width.toDouble(), image.height.toDouble()),
      Offset.zero & size,
      Paint()..filterQuality = FilterQuality.medium,
    );
    final r = Rect.fromLTWH(
      selection.left * size.width,
      selection.top * size.height,
      selection.width * size.width,
      selection.height * size.height,
    );
    final overlay = Path()
      ..fillType = PathFillType.evenOdd
      ..addRect(Offset.zero & size)
      ..addRect(r);
    canvas.drawPath(overlay, Paint()..color = const Color(0x990D1F30));
    canvas.drawRect(
      r,
      Paint()
        ..color = Colors.white
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3,
    );
    canvas.drawRect(
      r,
      Paint()
        ..color = const Color(0xFF15517F)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1,
    );
  }

  @override
  bool shouldRepaint(covariant _CropPainter oldDelegate) =>
      oldDelegate.image != image || oldDelegate.selection != selection;
}
