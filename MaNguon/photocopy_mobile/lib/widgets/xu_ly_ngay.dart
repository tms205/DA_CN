import 'dart:async';
import 'package:flutter/material.dart';
import '../api.dart';
import '../models.dart';

class NutXuLyNgay extends StatefulWidget {
  const NutXuLyNgay({
    super.key,
    required this.maDonHang,
    required this.sauXacNhan,
  });
  final int maDonHang;
  final Future<void> Function() sauXacNhan;
  @override
  State<NutXuLyNgay> createState() => _NutXuLyNgayState();
}

class _NutXuLyNgayState extends State<NutXuLyNgay> {
  bool _dangXacNhanNgay = false;
  bool _daXacNhan = false;
  bool _conThoiGian = false;
  Timer? _hetHan;

  @override
  void initState() {
    super.initState();
    _kiemTra();
  }

  Future<void> _kiemTra() async {
    final kq = await Api.get('/api/don-hang/${widget.maDonHang}');
    if (!mounted || !kq.thanhCong) return;
    final don = DonHangChiTiet.fromJson(kq.duLieu);
    setState(() {
      _daXacNhan = don.daXacNhanXuLyNgay;
      _conThoiGian =
          don.kenhDatDon == 'APP' &&
          don.daTra &&
          don.trangThaiDonHang == 'CHO_XAC_NHAN' &&
          don.giayChoTiepNhan > 0;
    });
    if (_conThoiGian) {
      _hetHan = Timer(Duration(seconds: don.giayChoTiepNhan), () {
        if (mounted) setState(() => _conThoiGian = false);
      });
    }
  }

  @override
  void dispose() {
    _hetHan?.cancel();
    super.dispose();
  }

  Future<void> _xacNhanXuLyNgay() async {
    if (_dangXacNhanNgay || !_conThoiGian) return;
    final dongY = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Xử lý đơn ngay?'),
        content: const Text(
          'Bạn đồng ý bỏ qua thời gian chờ 2 phút và không còn được hủy/hoàn theo thời gian chờ. Nhân viên có thể tiếp nhận ngay; đơn chưa tự động bắt đầu in.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Tiếp tục chờ'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Đồng ý xử lý ngay'),
          ),
        ],
      ),
    );
    if (dongY != true || !mounted) return;
    setState(() => _dangXacNhanNgay = true);
    try {
      final kq = await Api.post(
        '/api/don-hang/${widget.maDonHang}/xu-ly-ngay',
        {},
      );
      if (!mounted) return;
      if (!kq.thanhCong) {
        await showDialog<void>(
          context: context,
          builder: (ctx) => AlertDialog(
            title: const Text('Chưa xác nhận được'),
            content: Text(kq.loi),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(ctx),
                child: const Text('Đóng'),
              ),
            ],
          ),
        );
      }
      if (kq.thanhCong) {
        setState(() => _daXacNhan = true);
        await widget.sauXacNhan();
      }
    } finally {
      if (mounted) setState(() => _dangXacNhanNgay = false);
    }
  }

  @override
  Widget build(BuildContext context) => _daXacNhan
      ? const Text('Đã xác nhận xử lý ngay. Đơn chờ nhân viên tiếp nhận.')
      : !_conThoiGian
      ? const SizedBox.shrink()
      : FilledButton.icon(
          onPressed: _dangXacNhanNgay ? null : _xacNhanXuLyNgay,
          icon: const Icon(Icons.fast_forward),
          label: Text(
            _dangXacNhanNgay
                ? 'Đang xác nhận…'
                : 'Xử lý ngay · bỏ qua thời gian chờ',
          ),
        );
}
