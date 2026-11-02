import 'dart:typed_data';
import 'package:flutter/material.dart';
import '../api.dart';

class BangChungHuyDon extends StatefulWidget {
  const BangChungHuyDon({super.key, required this.maDonHang});
  final int maDonHang;
  @override
  State<BangChungHuyDon> createState() => _BangChungHuyDonState();
}

class _BangChungHuyDonState extends State<BangChungHuyDon> {
  Map<String, dynamic>? _data;
  String? _loi;
  bool _busy = false;
  @override
  void initState() {
    super.initState();
    _tai();
  }

  Future<void> _tai() async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _loi = null;
    });
    final r = await Api.get('/api/don-hang/${widget.maDonHang}/bang-chung-huy');
    if (!mounted) return;
    setState(() {
      _busy = false;
      if (r.thanhCong) {
        _data = Map<String, dynamic>.from(r.duLieu);
      } else {
        _loi = r.maHttp == 404 ? 'Đơn cũ chưa có bằng chứng hủy.' : r.loi;
      }
    });
  }

  Future<void> _xem(String id) async {
    final r = await Api.getAnhHuy(widget.maDonHang, id);
    if (!mounted) return;
    if (!r.thanhCong) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(r.loi)));
      return;
    }
    await showDialog<void>(
      context: context,
      builder: (_) => Dialog.fullscreen(
        child: Scaffold(
          appBar: AppBar(title: const Text('Bằng chứng hủy đơn')),
          body: InteractiveViewer(
            minScale: 0.5,
            maxScale: 5,
            child: Center(
              child: Image.memory(r.duLieu as Uint8List, cacheWidth: 1600),
            ),
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Bằng chứng hủy đơn',
            style: TextStyle(fontWeight: FontWeight.w700),
          ),
          if (_busy)
            const Padding(
              padding: EdgeInsets.all(12),
              child: Text('Đang tải…'),
            ),
          if (_loi != null) ...[
            Text(_loi!),
            TextButton(
              onPressed: _busy ? null : _tai,
              child: const Text('Thử lại'),
            ),
          ],
          if (_data != null) ...[
            Text(
              _data!['loai'] == 'TU_DONG'
                  ? 'Hệ thống tự hủy do hết hạn thanh toán'
                  : 'Yêu cầu hủy có ảnh bằng chứng',
            ),
            Text('Ghi nhận: ${_data!['guiLuc'] ?? ''}'),
            if (_data!['loai'] == 'TU_DONG')
              Text(
                'Hết hạn: ${_data!['hetHanThanhToan'] ?? ''}\nThanh toán: ${_data!['trangThaiThanhToan'] ?? ''}',
              ),
            for (final a in (_data!['anh'] as List? ?? []))
              TextButton.icon(
                onPressed: () => _xem(a['maAnh']),
                icon: const Icon(Icons.image_outlined),
                label: const Text('Xem ảnh bằng chứng'),
              ),
          ],
        ],
      ),
    ),
  );
}
