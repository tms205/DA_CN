import 'dart:typed_data';
import 'package:flutter/material.dart';
import '../api.dart';

class BienLaiHoanTien extends StatefulWidget {
  const BienLaiHoanTien({
    super.key,
    required this.maDonHang,
    required this.bienLai,
  });
  final int maDonHang;
  final Map<String, dynamic> bienLai;
  @override
  State<BienLaiHoanTien> createState() => _BienLaiHoanTienState();
}

class _BienLaiHoanTienState extends State<BienLaiHoanTien> {
  bool _tai = false;
  Future<void> _xem() async {
    if (_tai) return;
    final ma = widget.bienLai['anh']?['maAnh'];
    if (ma is! String) return;
    setState(() => _tai = true);
    final r = await Api.getBienLaiHoanTien(widget.maDonHang, ma);
    if (!mounted) return;
    setState(() => _tai = false);
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
          appBar: AppBar(title: const Text('Biên lai hoàn tiền')),
          body: InteractiveViewer(
            minScale: .5,
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
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 10),
    child: OutlinedButton.icon(
      onPressed: _tai ? null : _xem,
      icon: const Icon(Icons.receipt_long_outlined),
      label: Text(_tai ? 'Đang tải biên lai…' : 'Xem biên lai chuyển khoản'),
    ),
  );
}
