import 'dart:async';
import 'dart:typed_data';
import 'package:flutter/material.dart';
import '../api.dart';
import '../realtime.dart';
import '../screens/don_hang/gui_doi_chieu_screen.dart';

class DoiChieuGiaoHang extends StatefulWidget {
  const DoiChieuGiaoHang({
    super.key,
    required this.maDonHang,
    this.tai,
    this.moGui,
    this.taiAnh,
  });
  final int maDonHang;
  final Future<KetQuaApi> Function()? tai;
  final Future<bool?> Function(BuildContext, int, int)? moGui;
  final Future<KetQuaApi> Function(int, String)? taiAnh;
  @override
  State<DoiChieuGiaoHang> createState() => _DoiChieuGiaoHangState();
}

class _DoiChieuGiaoHangState extends State<DoiChieuGiaoHang> {
  List<Map<String, dynamic>> _luot = [];
  String? _loi;
  bool _dangTai = false;
  bool _taiLai = false;
  Timer? _hen;
  @override
  void initState() {
    super.initState();
    Realtime.dangKyNghe(_coTin);
    _tai();
  }

  @override
  void dispose() {
    _hen?.cancel();
    Realtime.boNghe(_coTin);
    super.dispose();
  }

  void _coTin(Map<String, dynamic> tin) {
    if (tin['maDonHang'] == widget.maDonHang) {
      _hen?.cancel();
      _hen = Timer(const Duration(milliseconds: 350), () {
        if (mounted) _tai();
      });
    }
  }

  Future<void> _tai() async {
    if (_dangTai) {
      _taiLai = true;
      return;
    }
    setState(() {
      _dangTai = true;
      _loi = null;
    });
    try {
      final result =
          await (widget.tai?.call() ??
              Api.get('/api/giao-hang/don/${widget.maDonHang}/doi-chieu'));
      if (!mounted) return;
      setState(() {
        if (result.thanhCong) {
          _luot = (result.duLieu as List)
              .map((x) => Map<String, dynamic>.from(x))
              .toList();
        } else {
          _loi = result.loi;
        }
      });
    } catch (_) {
      if (mounted) {
        setState(() => _loi = 'Chưa tải được phản hồi. Hãy thử lại.');
      }
    } finally {
      if (mounted) setState(() => _dangTai = false);
    }
    if (!mounted) return;
    if (_taiLai) {
      _taiLai = false;
      await _tai();
    }
  }

  Future<void> _gui(Map<String, dynamic> row) async {
    final bool? result =
        await (widget.moGui?.call(context, row['maGiaoHang'], row['lanGiao']) ??
            Navigator.push<bool>(
              context,
              MaterialPageRoute(
                builder: (_) => GuiDoiChieuScreen(
                  maGiao: row['maGiaoHang'],
                  lanGiao: row['lanGiao'],
                ),
              ),
            ));
    if (mounted && result == true) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Đã gửi bằng chứng để cửa hàng đối chiếu.'),
        ),
      );
      await _tai();
    }
  }

  Future<void> _xemAnh(int id, String maAnh) async {
    await showDialog<void>(
      context: context,
      builder: (ctx) => Dialog.fullscreen(
        child: Scaffold(
          appBar: AppBar(title: const Text('Ảnh bằng chứng của bạn')),
          body: FutureBuilder<KetQuaApi>(
            future:
                widget.taiAnh?.call(id, maAnh) ?? Api.getAnhDoiChieu(id, maAnh),
            builder: (context, snapshot) {
              if (snapshot.hasError) {
                return const Center(
                  child: Text('Chưa tải được ảnh. Hãy mở lại để thử lại.'),
                );
              }
              if (!snapshot.hasData) {
                return const Center(child: CircularProgressIndicator());
              }
              if (!snapshot.data!.thanhCong) {
                return Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Text(snapshot.data!.loi),
                  ),
                );
              }
              return InteractiveViewer(
                minScale: 0.5,
                maxScale: 5,
                child: Center(
                  child: Image.memory(
                    snapshot.data!.duLieu as Uint8List,
                    cacheWidth: 1600,
                  ),
                ),
              );
            },
          ),
        ),
      ),
    );
  }

  String _gio(dynamic value) {
    final time = DateTime.tryParse(value?.toString() ?? '')?.toLocal();
    if (time == null) return 'Chưa ghi nhận';
    String hai(int x) => x.toString().padLeft(2, '0');
    return '${hai(time.day)}/${hai(time.month)}/${time.year} ${hai(time.hour)}:${hai(time.minute)}';
  }

  @override
  Widget build(BuildContext context) {
    if (_luot.isEmpty && _loi == null) {
      return _dangTai
          ? const Padding(
              padding: EdgeInsets.all(12),
              child: Text('Đang tải thông tin lượt giao…'),
            )
          : const SizedBox.shrink();
    }
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Đối chiếu giao hàng',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            if (_loi != null) ...[
              const SizedBox(height: 8),
              Text(_loi!),
              TextButton(
                onPressed: _dangTai ? null : _tai,
                child: const Text('Thử lại'),
              ),
            ],
            for (final row in _luot) ...[
              const Divider(height: 28),
              Text(
                'Lượt ${row['lanGiao']} · Giao chưa thành công',
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
              const SizedBox(height: 8),
              SelectableText(row['lyDoGiaoThatBai'] ?? 'Chưa có lý do'),
              if (row['phanHoi'] == null) ...[
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  onPressed: () => _gui(row),
                  icon: const Icon(Icons.fact_check_outlined),
                  label: const Text('Gửi bằng chứng đối chiếu'),
                  style: OutlinedButton.styleFrom(
                    minimumSize: const Size(0, 48),
                  ),
                ),
              ] else
                ..._phanHoi(row),
            ],
          ],
        ),
      ),
    );
  }

  List<Widget> _phanHoi(Map<String, dynamic> row) {
    final report = Map<String, dynamic>.from(row['phanHoi']);
    final done = report['trangThai'] == 'DA_XU_LY';
    final anh = report['anh'] as List? ?? [];
    return [
      const SizedBox(height: 12),
      Text(
        done ? 'Đã xử lý' : 'Chờ đối chiếu',
        style: TextStyle(
          fontWeight: FontWeight.w700,
          color: done ? Colors.green.shade800 : Colors.orange.shade900,
        ),
      ),
      const SizedBox(height: 8),
      SelectableText(report['noiDung'] ?? ''),
      Text('Gửi lúc ${_gio(report['guiLuc'])}'),
      Wrap(
        spacing: 8,
        children: [
          for (int i = 0; i < anh.length; i++)
            TextButton.icon(
              onPressed: () => _xemAnh(row['maGiaoHang'], anh[i]['maAnh']),
              icon: const Icon(Icons.image_outlined),
              label: Text('Ảnh ${i + 1}'),
            ),
        ],
      ),
      if (done) ...[
        const SizedBox(height: 12),
        const Text(
          'Kết quả đối chiếu',
          style: TextStyle(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 6),
        SelectableText(report['ketLuan'] ?? ''),
        Text(
          '${report['tenNguoiXuLy'] ?? 'Cửa hàng'} · ${_gio(report['xuLyLuc'])}',
        ),
      ],
    ];
  }
}
