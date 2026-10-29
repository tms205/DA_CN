import 'dart:io';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../api.dart';
import 'cat_anh_ocr_screen.dart';

class NhanDangChuScreen extends StatefulWidget {
  const NhanDangChuScreen({super.key});
  @override
  State<NhanDangChuScreen> createState() => _NhanDangChuScreenState();
}

class _NhanDangChuScreenState extends State<NhanDangChuScreen> {
  PlatformFile? _file;
  VungCatOcr? _crop;
  bool _dangXuat = false, _anhDaDoi = false;
  String _model = '2b';
  String? _loi;
  bool _dangXuLy = false, _daNhanDang = false, _biCat = false;
  final _text = TextEditingController();
  @override
  void initState() {
    super.initState();
    _text.addListener(_vanBanThayDoi);
  }

  void _vanBanThayDoi() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _text.removeListener(_vanBanThayDoi);
    _text.dispose();
    super.dispose();
  }

  Future<void> _chonAnh() async {
    final file = await FilePicker.pickFile(
      type: FileType.custom,
      allowedExtensions: ['jpg', 'jpeg', 'png'],
    );
    if (!mounted || file == null || file.path == null) return;
    setState(() {
      _file = file;
      _crop = null;
      _loi = null;
      _daNhanDang = false;
      _anhDaDoi = false;
      _text.clear();
    });
    await _catAnh();
  }

  Future<void> _nhanDang() async {
    final file = _file;
    if (file == null || file.path == null || _dangXuLy) return;
    int size;
    try {
      size = await File(file.path!).length();
    } catch (_) {
      if (mounted) {
        setState(() => _loi = 'Không đọc được ảnh. Vui lòng chọn lại.');
      }
      return;
    }
    if (!mounted) return;
    if (size > 10 * 1024 * 1024) {
      setState(() => _loi = 'Ảnh vượt quá 10 MB.');
      return;
    }
    setState(() {
      _dangXuLy = true;
      _loi = null;
      _daNhanDang = false;
      _anhDaDoi = false;
    });
    final result = await Api.nhanDangChu(
      file.path!,
      file.name,
      _model,
      vungCat: _crop?.rect,
    );
    if (!mounted) return;
    setState(() {
      _dangXuLy = false;
      if (!result.thanhCong) {
        _loi = result.loi;
        return;
      }
      _text.text = result.duLieu['text'] as String? ?? '';
      _biCat = result.duLieu['truncated'] == true;
      _daNhanDang = true;
    });
  }

  Future<void> _catAnh() async {
    final file = _file;
    if (file?.path == null || _dangXuLy) return;
    final crop = await Navigator.push<VungCatOcr>(
      context,
      MaterialPageRoute(
        builder: (_) =>
            CatAnhOcrScreen(path: file!.path!, initial: _crop?.rect),
      ),
    );
    if (!mounted || crop == null) return;
    setState(() {
      _crop = crop;
      _anhDaDoi = _daNhanDang;
    });
  }

  Future<void> _xuatWord() async {
    if (_dangXuat || _text.text.trim().isEmpty) return;
    setState(() {
      _dangXuat = true;
      _loi = null;
    });
    try {
      final result = await Api.xuatWordOcr(_text.text);
      if (!mounted) return;
      if (!result.thanhCong) {
        setState(() => _loi = result.loi);
        return;
      }
      final uri = await FilePicker.saveFile(
        fileName: 'Van-ban-nhan-dang.docx',
        bytes: result.duLieu,
        mimeType:
            'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        dialogTitle: 'Lưu văn bản Word',
      );
      if (mounted && uri != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Đã lưu file Word vào vị trí bạn chọn.'),
          ),
        );
      }
    } catch (_) {
      if (mounted) {
        setState(() => _loi = 'Không lưu được file Word. Vui lòng thử lại.');
      }
    } finally {
      if (mounted) setState(() => _dangXuat = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Nhận dạng chữ từ ảnh')),
    body: SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Chụp rõ và cắt sát vùng chữ để dễ nhận dạng. Ảnh được xử lý trên máy chủ cửa hàng.',
          ),
          const SizedBox(height: 18),
          DropdownButtonFormField<String>(
            initialValue: _model,
            decoration: const InputDecoration(
              labelText: 'Model nhận dạng',
              border: OutlineInputBorder(),
            ),
            items: const [
              DropdownMenuItem(value: '2b', child: Text('Qwen 2B — nhanh hơn')),
              DropdownMenuItem(
                value: '4b',
                child: Text('Qwen 4B — ưu tiên chất lượng'),
              ),
            ],
            onChanged: _dangXuLy
                ? null
                : (value) => setState(() => _model = value!),
          ),
          const SizedBox(height: 14),
          OutlinedButton.icon(
            onPressed: _dangXuLy || _dangXuat ? null : _chonAnh,
            icon: const Icon(Icons.image_outlined),
            label: Text(_file == null ? 'Chọn ảnh JPG/PNG' : 'Đổi ảnh'),
          ),
          if (_file != null) ...[
            const SizedBox(height: 8),
            Text(_file!.name),
            const SizedBox(height: 8),
            SizedBox(
              height: 200,
              child: _crop == null
                  ? Image.file(
                      File(_file!.path!),
                      fit: BoxFit.contain,
                      cacheWidth: 1200,
                    )
                  : Image.memory(_crop!.preview, fit: BoxFit.contain),
            ),
            OutlinedButton.icon(
              onPressed: _dangXuLy || _dangXuat ? null : _catAnh,
              icon: const Icon(Icons.crop),
              label: Text(_crop == null ? 'Cắt vùng chữ' : 'Cắt lại vùng chữ'),
            ),
            if (_crop != null)
              TextButton(
                onPressed: _dangXuLy
                    ? null
                    : () => setState(() {
                        _crop = null;
                        _anhDaDoi = _daNhanDang;
                      }),
                child: const Text('Dùng ảnh gốc'),
              ),
          ],
          const SizedBox(height: 14),
          FilledButton(
            onPressed: _file == null || _dangXuLy || _dangXuat
                ? null
                : _nhanDang,
            child: Text(
              _dangXuLy ? 'Đang nhận dạng, vui lòng chờ…' : 'Nhận dạng chữ',
            ),
          ),
          if (_dangXuLy)
            const Padding(
              padding: EdgeInsets.all(12),
              child: Center(child: CircularProgressIndicator()),
            ),
          if (_loi != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                _loi!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          if (_daNhanDang) ...[
            const SizedBox(height: 22),
            if (_anhDaDoi)
              const Text(
                'Ảnh đã thay đổi. Văn bản dưới đây thuộc lần nhận dạng trước.',
              ),
            Text(
              _biCat
                  ? 'Kết quả có thể bị cắt. Hãy đối chiếu và chỉnh sửa trước khi dùng.'
                  : 'AI có thể đọc sai. Hãy đối chiếu và chỉnh sửa trước khi dùng.',
            ),
            const SizedBox(height: 10),
            TextField(
              controller: _text,
              enabled: !_dangXuat,
              minLines: 8,
              maxLines: 20,
              decoration: const InputDecoration(
                labelText: 'Văn bản nhận dạng',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: () async {
                await Clipboard.setData(ClipboardData(text: _text.text));
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Đã sao chép văn bản.')),
                  );
                }
              },
              icon: const Icon(Icons.copy),
              label: const Text('Sao chép văn bản'),
            ),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: _dangXuat || _text.text.trim().isEmpty
                  ? null
                  : _xuatWord,
              icon: const Icon(Icons.description_outlined),
              label: Text(
                _dangXuat ? 'Đang xuất Word…' : 'Xuất file Word (.docx)',
              ),
            ),
          ],
        ],
      ),
    ),
  );
}
