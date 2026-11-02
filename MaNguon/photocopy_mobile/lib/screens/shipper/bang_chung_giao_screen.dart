import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../api.dart';

class BangChungGiaoScreen extends StatefulWidget {
  const BangChungGiaoScreen({super.key, required this.maGiao});
  final int maGiao;
  @override
  State<BangChungGiaoScreen> createState() => _BangChungGiaoScreenState();
}

class _BangChungGiaoScreenState extends State<BangChungGiaoScreen> {
  Map<String, dynamic>? _data;
  String? _loi, _loiAnh;
  Uint8List? _anh;
  bool _dangTai = true;
  int _lanAnh = 0, _chiSo = 0;
  @override
  void initState() {
    super.initState();
    _tai();
  }

  Future<void> _tai() async {
    setState(() {
      _dangTai = true;
      _loi = null;
    });
    final result = await Api.get(
      '/api/giao-hang/luot/${widget.maGiao}/bang-chung',
    );
    if (!mounted) return;
    setState(() {
      _dangTai = false;
      if (result.thanhCong) {
        _data = Map<String, dynamic>.from(result.duLieu);
      } else {
        _loi = result.loi;
      }
    });
    if ((_data?['anh'] as List? ?? []).isNotEmpty) await _taiAnh(0);
  }

  Future<void> _taiAnh(int i) async {
    final lan = ++_lanAnh;
    setState(() {
      _chiSo = i;
      _anh = null;
      _loiAnh = null;
    });
    final result = await Api.getAnhBangChung(
      widget.maGiao,
      _data!['anh'][i]['maAnh'],
    );
    if (!mounted || lan != _lanAnh) return;
    setState(() {
      if (result.thanhCong) {
        _anh = result.duLieu as Uint8List;
      } else {
        _loiAnh = result.loi;
      }
    });
  }

  String _gio(dynamic value) => value == null
      ? 'Chưa ghi nhận'
      : DateTime.parse(value as String).toLocal().toString().substring(0, 19);
  Future<void> _banDo() async {
    final uri = Uri.https('www.google.com', '/maps/search/', {
      'api': '1',
      'query': '${_data!['viDo']},${_data!['kinhDo']}',
    });
    try {
      if (!await launchUrl(uri, mode: LaunchMode.externalApplication)) {
        throw StateError('Không mở được');
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Không mở được bản đồ. Bạn có thể sao chép tọa độ.'),
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Bằng chứng lượt giao')),
    body: _dangTai
        ? const Center(child: CircularProgressIndicator())
        : ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (_loi != null) ...[
                Text(_loi!),
                TextButton(onPressed: _tai, child: const Text('Thử lại')),
              ],
              if (_data != null) ...[
                Text(
                  'Lượt ${_data!['lanGiao']} · ${_data!['tenShipper']}',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 12),
                SelectableText(_data!['lyDo'] ?? ''),
                const SizedBox(height: 12),
                Text('Server ghi nhận: ${_gio(_data!['thoiDiemBaoCao'])}'),
                Text('GPS ghi nhận: ${_gio(_data!['thoiDiemGps'])}'),
                if (_data!['viDo'] != null) ...[
                  const SizedBox(height: 12),
                  SelectableText(_data!['diaChi'] ?? 'Chưa tra được địa chỉ.'),
                  SelectableText(
                    '${_data!['viDo']}, ${_data!['kinhDo']} · Sai số ${(_data!['doChinhXac'] as num).round()} m',
                  ),
                  OutlinedButton.icon(
                    onPressed: _banDo,
                    icon: const Icon(Icons.map_outlined),
                    label: const Text('Mở vị trí ghi nhận'),
                  ),
                ],
                const SizedBox(height: 16),
                if ((_data!['anh'] as List).isEmpty)
                  const Text(
                    'Lượt giao cũ chưa có ảnh bằng chứng. Hệ thống giữ nguyên lịch sử trước khi nâng cấp.',
                  )
                else ...[
                  Wrap(
                    spacing: 8,
                    children: [
                      for (int i = 0; i < (_data!['anh'] as List).length; i++)
                        ChoiceChip(
                          label: Text('Ảnh ${i + 1}'),
                          selected: i == _chiSo,
                          onSelected: (_) => _taiAnh(i),
                        ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  if (_anh != null)
                    SizedBox(
                      height: 500,
                      child: InteractiveViewer(
                        minScale: 0.5,
                        maxScale: 5,
                        child: Image.memory(
                          _anh!,
                          fit: BoxFit.contain,
                          cacheWidth: 1600,
                        ),
                      ),
                    )
                  else if (_loiAnh != null) ...[
                    Text(_loiAnh!),
                    TextButton(
                      onPressed: () => _taiAnh(_chiSo),
                      child: const Text('Tải lại ảnh'),
                    ),
                  ] else
                    const Center(child: CircularProgressIndicator()),
                ],
              ],
            ],
          ),
  );
}
