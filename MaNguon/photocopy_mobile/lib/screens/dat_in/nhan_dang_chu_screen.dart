import 'dart:async';
import 'dart:io';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../api.dart';
import 'cat_anh_ocr_screen.dart';

class NhanDangChuScreen extends StatefulWidget {
  final Future<List<PlatformFile>> Function()? chonAnh;
  const NhanDangChuScreen({super.key, this.chonAnh});
  @override
  State<NhanDangChuScreen> createState() => _NhanDangChuScreenState();
}

class _AnhOcr {
  final PlatformFile file;
  final int size;
  final text = TextEditingController();
  VungCatOcr? crop;
  String status = 'Chưa nhận dạng';
  String? error;
  bool done = false, changed = false, truncated = false;
  _AnhOcr(this.file, this.size);
  bool get canRun => !done || changed || error != null;
}

class _NhanDangChuScreenState extends State<NhanDangChuScreen> {
  final List<_AnhOcr> _images = [];
  int _selected = 0;
  String _model = '2b', _progress = '';
  String? _loi, _jobId;
  bool _busy = false, _exporting = false, _stopped = false;
  _AnhOcr? get _current => _images.isEmpty ? null : _images[_selected];
  bool get _canExport =>
      _images.isNotEmpty &&
      _images.every((p) => !p.canRun && p.text.text.trim().isNotEmpty);

  @override
  void dispose() {
    _stopped = true;
    final id = _jobId;
    if (id != null) unawaited(Api.huyTacVuOcr(id));
    for (final image in _images) {
      image.text.dispose();
    }
    super.dispose();
  }

  Future<void> _chonAnh() async {
    try {
      final files =
          await (widget.chonAnh?.call() ??
              FilePicker.pickFiles(
                type: FileType.custom,
                allowedExtensions: ['jpg', 'jpeg', 'png'],
              ));
      if (!mounted || files.isEmpty) return;
      if (files.length + _images.length > 10) {
        throw const FormatException('Mỗi lượt tối đa 10 ảnh.');
      }
      final added = <_AnhOcr>[];
      int total = _images.fold(0, (n, p) => n + p.size);
      for (final file in files) {
        if (file.path == null ||
            !RegExp(
              r'\.(jpg|jpeg|png)$',
              caseSensitive: false,
            ).hasMatch(file.name)) {
          throw const FormatException('Chọn ảnh JPG hoặc PNG có thể đọc được.');
        }
        final size = await File(file.path!).length();
        if (size == 0 || size > 10 * 1024 * 1024) {
          throw const FormatException(
            'Mỗi ảnh phải có nội dung và tối đa 10 MB.',
          );
        }
        total += size;
        if (total > 50 * 1024 * 1024) {
          throw const FormatException('Tổng ảnh tối đa 50 MB.');
        }
        added.add(_AnhOcr(file, size));
      }
      if (!mounted) {
        for (final image in added) {
          image.text.dispose();
        }
        return;
      }
      setState(() {
        _loi = null;
        _selected = _images.length;
        _images.addAll(added);
      });
    } catch (e) {
      if (mounted) {
        setState(
          () => _loi = e is FormatException
              ? e.message
              : 'Không đọc được ảnh. Vui lòng chọn lại.',
        );
      }
    }
  }

  Future<void> _catAnh() async {
    final image = _current;
    if (image == null || _busy || _exporting) return;
    final crop = await Navigator.push<VungCatOcr>(
      context,
      MaterialPageRoute(
        builder: (_) =>
            CatAnhOcrScreen(path: image.file.path!, initial: image.crop?.rect),
      ),
    );
    if (mounted && crop != null) {
      setState(() {
        image.crop = crop;
        image.changed = image.done;
      });
    }
  }

  Future<void> _run(List<_AnhOcr> images) async {
    if (_busy || images.isEmpty) return;
    setState(() {
      _busy = true;
      _stopped = false;
      _loi = null;
    });
    try {
      for (final image in images) {
        if (!mounted || _stopped) break;
        final prefix = 'Ảnh ${_images.indexOf(image) + 1}/${_images.length}';
        setState(() {
          image.status = 'Đang gửi';
          image.error = null;
          _progress = '$prefix · Đang gửi ảnh';
        });
        try {
          final response = await Api.taoTacVuOcr(
            image.file.path!,
            image.file.name,
            _model,
            vungCat: image.crop?.rect,
          );
          if (!response.thanhCong) throw FormatException(response.loi);
          Map<String, dynamic> data = Map<String, dynamic>.from(
            response.duLieu,
          );
          final id = data['id'] as String;
          _jobId = id;
          if (!mounted || _stopped) {
            await Api.huyTacVuOcr(id);
            break;
          }
          while (mounted && !_stopped) {
            final state = data['state'];
            if (state == 'HOAN_TAT') {
              setState(() {
                image.text.text = data['text'] as String? ?? '';
                image.done = true;
                image.changed = false;
                image.truncated = data['truncated'] == true;
                image.status = 'Đã nhận dạng';
              });
              break;
            }
            if (state == 'THAT_BAI' || state == 'DA_HUY') {
              throw FormatException(
                data['message'] as String? ?? 'Đã dừng nhận dạng.',
              );
            }
            setState(() {
              image.status = state == 'CHO_XU_LY'
                  ? 'Đang chờ · thứ tự ${data['position']}'
                  : 'Đang nhận dạng';
              _progress = '$prefix · ${image.status}';
            });
            await Future<void>.delayed(const Duration(seconds: 2));
            if (!mounted || _stopped) break;
            final poll = await Api.get('/api/ocr/tac-vu/$id');
            if (!poll.thanhCong) throw FormatException(poll.loi);
            data = Map<String, dynamic>.from(poll.duLieu);
          }
          if (mounted && _stopped) {
            setState(() {
              image.status = 'Đã dừng';
              image.error = 'Đã dừng. Có thể nhận dạng lại ảnh này.';
            });
          }
        } catch (e) {
          if (mounted) {
            setState(() {
              image.status = 'Cần thử lại';
              image.error = e is FormatException
                  ? e.message
                  : 'Mất kết nối OCR. Hãy thử lại ảnh này.';
            });
          }
        } finally {
          final id = _jobId;
          _jobId = null;
          if (id != null) await Api.huyTacVuOcr(id);
        }
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _stop() {
    setState(() {
      _stopped = true;
      _progress = 'Đang dừng nhận dạng…';
    });
    final id = _jobId;
    if (id != null) unawaited(Api.huyTacVuOcr(id));
  }

  void _move(_AnhOcr image, int delta) {
    setState(() {
      final i = _images.indexOf(image);
      _images.removeAt(i);
      _images.insert(i + delta, image);
      _selected = i + delta;
    });
  }

  void _remove(_AnhOcr image) {
    setState(() {
      _images.remove(image);
      image.text.dispose();
      _selected = _images.isEmpty ? 0 : _selected.clamp(0, _images.length - 1);
    });
  }

  Future<void> _export() async {
    if (!_canExport || _exporting) return;
    setState(() {
      _exporting = true;
      _loi = null;
    });
    try {
      final response = await Api.xuatWordOcr(
        '',
        pages: _images.map((p) => p.text.text).toList(),
      );
      if (!mounted) return;
      if (!response.thanhCong) {
        setState(() => _loi = response.loi);
        return;
      }
      final uri = await FilePicker.saveFile(
        fileName: 'Van-ban-nhan-dang.docx',
        bytes: response.duLieu,
        mimeType:
            'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        dialogTitle: 'Lưu văn bản Word',
      );
      if (mounted && uri != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Đã lưu Word. Bạn có thể tải file này lên để đặt in.',
            ),
          ),
        );
      }
    } catch (_) {
      if (mounted) {
        setState(
          () => _loi = 'Không lưu được Word. Hãy thử lại trước khi rời trang.',
        );
      }
    } finally {
      if (mounted) setState(() => _exporting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final current = _current;
    return Scaffold(
      appBar: AppBar(title: const Text('Nhận dạng chữ từ ảnh')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'Chọn tối đa 10 ảnh, mỗi ảnh tối đa 10 MB, tổng 50 MB. Cắt sát phần chữ và kiểm tra lại kết quả.',
            ),
            const SizedBox(height: 16),
            DropdownButtonFormField<String>(
              isExpanded: true,
              initialValue: _model,
              decoration: const InputDecoration(
                labelText: 'Model nhận dạng',
                border: OutlineInputBorder(),
              ),
              items: const [
                DropdownMenuItem(
                  value: '2b',
                  child: Text('Qwen 2B · model nhẹ'),
                ),
                DropdownMenuItem(
                  value: '4b',
                  child: Text('Qwen 4B · model lớn hơn'),
                ),
              ],
              onChanged: _busy || _exporting
                  ? null
                  : (v) => setState(() => _model = v!),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _busy || _exporting || _images.length >= 10
                  ? null
                  : _chonAnh,
              icon: const Icon(Icons.add_photo_alternate_outlined),
              label: Text('Thêm ảnh (${_images.length}/10)'),
            ),
            if (_images.isNotEmpty) ...[
              const SizedBox(height: 8),
              ConstrainedBox(
                constraints: const BoxConstraints(maxHeight: 260),
                child: ListView.builder(
                  shrinkWrap: true,
                  itemCount: _images.length,
                  itemBuilder: (_, i) {
                    final image = _images[i];
                    return Card(
                      color: i == _selected
                          ? Theme.of(context).colorScheme.primaryContainer
                          : null,
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 4),
                        child: Row(
                          children: [
                            Expanded(
                              child: TextButton(
                                onPressed: () => setState(() => _selected = i),
                                style: TextButton.styleFrom(
                                  alignment: Alignment.centerLeft,
                                ),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      '${i + 1}. ${image.file.name}',
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                    Text(
                                      image.status,
                                      style: const TextStyle(fontSize: 12),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                            IconButton(
                              tooltip: 'Đưa ảnh lên trước',
                              onPressed: _busy || _exporting || i == 0
                                  ? null
                                  : () => _move(image, -1),
                              icon: const Icon(Icons.arrow_upward, size: 18),
                            ),
                            IconButton(
                              tooltip: 'Đưa ảnh xuống sau',
                              onPressed:
                                  _busy || _exporting || i == _images.length - 1
                                  ? null
                                  : () => _move(image, 1),
                              icon: const Icon(Icons.arrow_downward, size: 18),
                            ),
                            IconButton(
                              tooltip: 'Xóa ảnh',
                              onPressed: _busy || _exporting
                                  ? null
                                  : () => _remove(image),
                              icon: const Icon(Icons.close, size: 18),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
            ],
            if (current != null) ...[
              const SizedBox(height: 12),
              SizedBox(
                height: 210,
                child: current.crop == null
                    ? Image.file(
                        File(current.file.path!),
                        key: ValueKey(current),
                        fit: BoxFit.contain,
                        cacheWidth: 1200,
                      )
                    : Image.memory(current.crop!.preview, fit: BoxFit.contain),
              ),
              OutlinedButton.icon(
                onPressed: _busy || _exporting ? null : _catAnh,
                icon: const Icon(Icons.crop),
                label: const Text('Cắt vùng chữ ảnh đang chọn'),
              ),
              if (current.crop != null)
                TextButton(
                  onPressed: _busy || _exporting
                      ? null
                      : () => setState(() {
                          current.crop = null;
                          current.changed = current.done;
                        }),
                  child: const Text('Dùng ảnh gốc'),
                ),
            ],
            const SizedBox(height: 12),
            FilledButton(
              onPressed: _busy || _exporting || !_images.any((p) => p.canRun)
                  ? null
                  : () => _run(_images.where((p) => p.canRun).toList()),
              child: const Text('Nhận dạng ảnh chưa xong / bị lỗi'),
            ),
            if (_busy) ...[
              Padding(
                padding: const EdgeInsets.all(12),
                child: Semantics(liveRegion: true, child: Text(_progress)),
              ),
              OutlinedButton(
                onPressed: _stop,
                child: const Text('Dừng nhận dạng'),
              ),
            ],
            if (_loi != null)
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Text(
                  _loi!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            if (current != null) ...[
              const SizedBox(height: 20),
              Text(
                'Văn bản ảnh ${_selected + 1}/${_images.length}',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 8),
              const Text(
                'AI có thể đọc sai. Tải Word trước khi rời màn hình; bản chỉnh sửa chưa được lưu lâu dài.',
              ),
              if (current.truncated)
                const Text(
                  'Kết quả có thể bị cắt. Hãy chia vùng ảnh nhỏ hơn và nhận dạng lại.',
                ),
              if (current.changed)
                const Text(
                  'Vùng ảnh đã đổi; hãy nhận dạng lại trước khi xuất.',
                ),
              if (current.error != null)
                Text(
                  current.error!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              const SizedBox(height: 10),
              TextField(
                key: ValueKey(current.text),
                controller: current.text,
                enabled: !_busy && !_exporting && current.done,
                onChanged: (_) => setState(() {}),
                minLines: 6,
                maxLines: 18,
                decoration: const InputDecoration(
                  labelText: 'Văn bản có thể chỉnh sửa',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: _busy || _exporting ? null : () => _run([current]),
                child: const Text('Nhận dạng lại ảnh này'),
              ),
              OutlinedButton.icon(
                onPressed: current.text.text.trim().isEmpty
                    ? null
                    : () async {
                        await Clipboard.setData(
                          ClipboardData(text: current.text.text),
                        );
                        if (context.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text('Đã sao chép văn bản.'),
                            ),
                          );
                        }
                      },
                icon: const Icon(Icons.copy),
                label: const Text('Sao chép ảnh này'),
              ),
            ],
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: !_canExport || _busy || _exporting ? null : _export,
              icon: const Icon(Icons.description_outlined),
              label: Text(
                _exporting ? 'Đang xuất Word…' : 'Xuất tất cả ra Word',
              ),
            ),
            const SizedBox(height: 8),
            const Text(
              'Mỗi ảnh bắt đầu ở trang Word mới; nội dung dài có thể tràn trang. Muốn đặt in, lưu Word rồi tải file lên bước Chọn file như bình thường.',
            ),
          ],
        ),
      ),
    );
  }
}
