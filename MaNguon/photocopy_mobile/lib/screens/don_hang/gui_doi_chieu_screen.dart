import 'dart:io';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import '../../api.dart';
import '../../widgets/widgets.dart';

class GuiDoiChieuScreen extends StatefulWidget {
  const GuiDoiChieuScreen({
    super.key,
    required this.maGiao,
    required this.lanGiao,
    this.chonAnh,
    this.gui,
  });
  final int maGiao, lanGiao;
  final Future<PlatformFile?> Function()? chonAnh;
  final Future<KetQuaApi> Function(String, List<Map<String, String>>)? gui;
  @override
  State<GuiDoiChieuScreen> createState() => _GuiDoiChieuScreenState();
}

class _GuiDoiChieuScreenState extends State<GuiDoiChieuScreen> {
  final _noiDung = TextEditingController();
  final _cuon = ScrollController();
  final _anh = <PlatformFile>[];
  bool _dangGui = false, _dangChon = false;
  String? _loi;
  @override
  void initState() {
    super.initState();
    _noiDung.addListener(_doi);
  }

  void _doi() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _noiDung.removeListener(_doi);
    _noiDung.dispose();
    _cuon.dispose();
    super.dispose();
  }

  void _baoLoi(String message) {
    setState(() => _loi = message);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _cuon.hasClients) {
        _cuon.animateTo(
          _cuon.position.maxScrollExtent,
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> _chon() async {
    if (_dangGui || _dangChon || _anh.length >= 3) return;
    setState(() {
      _dangChon = true;
      _loi = null;
    });
    try {
      final file =
          await (widget.chonAnh?.call() ??
              FilePicker.pickFile(
                type: FileType.custom,
                allowedExtensions: ['jpg', 'jpeg', 'png'],
              ));
      if (!mounted || file == null) return;
      if (file.path == null ||
          !RegExp(r'\.(jpe?g|png)$', caseSensitive: false).hasMatch(file.name)) {
        throw StateError('Chọn ảnh JPG/PNG.');
      }
      final size = await File(file.path!).length();
      if (!mounted) return;
      if (size <= 0 || size > 5 * 1024 * 1024) {
        throw StateError('Mỗi ảnh tối đa 5 MB.');
      }
      if (_anh.any((x) => x.path == file.path)) {
        throw StateError('Ảnh này đã được chọn.');
      }
      setState(() => _anh.add(file));
    } catch (e) {
      if (mounted) _baoLoi(e.toString().replaceFirst('Bad state: ', ''));
    } finally {
      if (mounted) setState(() => _dangChon = false);
    }
  }

  Future<void> _gui() async {
    final text = _noiDung.text.trim();
    if (_dangGui || text.length < 5 || text.length > 1000 || _anh.isEmpty) {
      return;
    }
    setState(() {
      _dangGui = true;
      _loi = null;
    });
    try {
      final files = _anh.map((a) => {'path': a.path!, 'name': a.name}).toList();
      final result =
          await (widget.gui?.call(text, files) ??
              Api.guiDoiChieu(widget.maGiao, text, files));
      if (!mounted) return;
      if (result.thanhCong) {
        Navigator.pop(context, true);
        return;
      }
      _baoLoi(result.loi);
    } catch (_) {
      if (mounted) {
        _baoLoi(
          'Chưa xác nhận gửi phản hồi. Ảnh và nội dung vẫn giữ ở đây; kiểm tra lại đơn trước khi gửi lại.',
        );
      }
    } finally {
      if (mounted) setState(() => _dangGui = false);
    }
  }

  Future<void> _xem(PlatformFile file) => showDialog<void>(
    context: context,
    builder: (_) => Dialog.fullscreen(
      child: Scaffold(
        appBar: AppBar(title: const Text('Ảnh nhật ký cuộc gọi')),
        body: InteractiveViewer(
          minScale: 0.5,
          maxScale: 5,
          child: Center(
            child: Image.file(
              File(file.path!),
              errorBuilder: (_, _, _) =>
                  const Text('Không đọc được ảnh. Hãy chọn lại.'),
            ),
          ),
        ),
      ),
    ),
  );
  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !_dangGui,
    child: Scaffold(
      appBar: AppBar(title: const Text('Gửi bằng chứng đối chiếu')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: ListView(
              controller: _cuon,
              padding: const EdgeInsets.all(16),
              children: [
                Text(
                  'Phản hồi lượt giao ${widget.lanGiao}',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 8),
                const Text(
                  'Nếu thông tin giao thất bại chưa đúng, bạn có thể gửi ảnh nhật ký cuộc gọi của mình để cửa hàng đối chiếu.',
                ),
                const SizedBox(height: 20),
                TextField(
                  controller: _noiDung,
                  enabled: !_dangGui,
                  minLines: 3,
                  maxLines: 6,
                  maxLength: 1000,
                  decoration: const InputDecoration(
                    labelText: 'Nội dung phản hồi *',
                    hintText: 'Mô tả thời điểm gọi hoặc cuộc gọi bạn đã nhận…',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 16),
                Text(
                  'Ảnh nhật ký cuộc gọi *',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                const Text(
                  'Từ 1–3 ảnh JPG/PNG, tối đa 5 MB/ảnh. Bạn có thể che thông tin các cuộc gọi không liên quan trước khi tải lên.',
                ),
                for (int i = 0; i < _anh.length; i++)
                  Padding(
                    padding: const EdgeInsets.only(top: 12),
                    child: Row(
                      children: [
                        Semantics(
                          label: 'Xem ảnh ${i + 1}',
                          button: true,
                          child: InkWell(
                            onTap: () => _xem(_anh[i]),
                            child: Image.file(
                              File(_anh[i].path!),
                              width: 64,
                              height: 80,
                              fit: BoxFit.cover,
                              cacheWidth: 160,
                              errorBuilder: (_, _, _) => const SizedBox(
                                width: 64,
                                height: 80,
                                child: Icon(Icons.broken_image_outlined),
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            _anh[i].name,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        IconButton(
                          onPressed: _dangGui
                              ? null
                              : () => setState(() => _anh.removeAt(i)),
                          tooltip: 'Bỏ ảnh ${i + 1}',
                          icon: const Icon(Icons.delete_outline),
                        ),
                      ],
                    ),
                  ),
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  onPressed: _dangGui || _dangChon || _anh.length >= 3
                      ? null
                      : _chon,
                  icon: const Icon(Icons.add_photo_alternate_outlined),
                  label: Text(
                    _dangChon
                        ? 'Đang chọn ảnh…'
                        : 'Thêm ảnh (${_anh.length}/3)',
                  ),
                  style: OutlinedButton.styleFrom(
                    minimumSize: const Size(0, 48),
                  ),
                ),
                if (_loi != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 16),
                    child: HopThongBao(_loi!),
                  ),
                const SizedBox(height: 24),
                FilledButton(
                  onPressed:
                      _dangGui ||
                          _dangChon ||
                          _noiDung.text.trim().length < 5 ||
                          _anh.isEmpty
                      ? null
                      : _gui,
                  style: FilledButton.styleFrom(minimumSize: const Size(0, 52)),
                  child: Text(_dangGui ? 'Đang gửi…' : 'Gửi bằng chứng'),
                ),
                const SizedBox(height: 12),
                const Text(
                  'Bằng chứng đã gửi sẽ được giữ nguyên. Việc gửi phản hồi không tự hủy đơn, hoàn tiền hoặc xác nhận lỗi của bên nào.',
                ),
              ],
            ),
          ),
        ),
      ),
    ),
  );
}
