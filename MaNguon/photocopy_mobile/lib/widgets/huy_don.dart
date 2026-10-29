import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api.dart';
import '../theme.dart';
import 'icons.dart';
import 'widgets.dart';

// Dùng chung cho đơn chờ tiếp nhận/chờ in ở màn QR và màn tiến độ.
// Quyền hủy và trạng thái hợp lệ vẫn được API kiểm tra khi gửi yêu cầu.
class NutHuyDon extends StatefulWidget {
  const NutHuyDon({
    super.key,
    required this.maDonHang,
    required this.daHuy,
    this.daThanhToan = false,
    this.capNhatHoanTien = false,
    this.thongTinHoanTien,
  });

  final int maDonHang;
  final Future<void> Function() daHuy;
  final bool daThanhToan;
  final bool capNhatHoanTien;
  final Map<String, dynamic>? thongTinHoanTien;

  @override
  State<NutHuyDon> createState() => _NutHuyDonState();
}

class _NutHuyDonState extends State<NutHuyDon> {
  bool _dangXuLy = false;

  Future<void> _huy() async {
    if (_dangXuLy) return;
    setState(() => _dangXuLy = true);
    try {
      final thongTin = await showDialog<Map<String, dynamic>>(
        context: context,
        builder: (_) => _HoiHuyDon(
          daThanhToan: widget.daThanhToan || widget.capNhatHoanTien,
          capNhatHoanTien: widget.capNhatHoanTien,
          thongTinHoanTien: widget.thongTinHoanTien,
        ),
      );
      if (thongTin == null || !mounted) return;
      final thaoTac = widget.capNhatHoanTien ? 'thong-tin-hoan-tien' : 'huy';
      final kq = await Api.post(
        '/api/don-hang/${widget.maDonHang}/$thaoTac',
        thongTin,
      );
      if (!mounted) return;
      final thongBao = kq.thanhCong && kq.duLieu is Map
          ? kq.duLieu['message']?.toString() ?? 'Đã hủy đơn.'
          : kq.loi;
      hienThongBaoNhanh(context, thongBao);
      if (kq.thanhCong) await widget.daHuy();
    } finally {
      if (mounted) setState(() => _dangXuLy = false);
    }
  }

  @override
  Widget build(BuildContext context) => NutPhu(
    nhan: _dangXuLy
        ? 'Đang xử lý…'
        : widget.capNhatHoanTien
        ? (widget.thongTinHoanTien == null
              ? 'Bổ sung thông tin hoàn tiền'
              : 'Sửa thông tin hoàn tiền')
        : 'Hủy đơn',
    icon: const AppIcon(Ico.chamThan, size: 17, color: AppColors.loiChu),
    onPressed: _dangXuLy ? null : _huy,
  );
}

class _HoiHuyDon extends StatefulWidget {
  const _HoiHuyDon({
    required this.daThanhToan,
    required this.capNhatHoanTien,
    this.thongTinHoanTien,
  });
  final bool daThanhToan;
  final bool capNhatHoanTien;
  final Map<String, dynamic>? thongTinHoanTien;

  @override
  State<_HoiHuyDon> createState() => _HoiHuyDonState();
}

class _HoiHuyDonState extends State<_HoiHuyDon> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _lyDo;
  late final TextEditingController _nganHang;
  late final TextEditingController _soTaiKhoan;
  bool _daXacNhan = false;

  @override
  void initState() {
    super.initState();
    final info = widget.thongTinHoanTien;
    _lyDo = TextEditingController(
      text:
          info?['lyDo']?.toString() ??
          (widget.capNhatHoanTien ? '' : 'Tôi không còn nhu cầu in'),
    );
    _nganHang = TextEditingController(
      text: info?['nganHang']?.toString() ?? '',
    );
    _soTaiKhoan = TextEditingController(
      text: info?['soTaiKhoan']?.toString() ?? '',
    );
  }

  @override
  void dispose() {
    _lyDo.dispose();
    _nganHang.dispose();
    _soTaiKhoan.dispose();
    super.dispose();
  }

  void _xacNhan() {
    if (_daXacNhan) return;
    if (_form.currentState!.validate()) {
      _daXacNhan = true;
      Navigator.pop(context, {
        'lyDo': _lyDo.text.trim(),
        if (widget.daThanhToan) ...{
          'nganHang': _nganHang.text.trim(),
          'soTaiKhoan': _soTaiKhoan.text.trim(),
        },
      });
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(
      widget.capNhatHoanTien ? 'Thông tin nhận hoàn tiền' : 'Hủy đơn hàng?',
    ),
    content: SingleChildScrollView(
      child: Form(
        key: _form,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              widget.daThanhToan
                  ? 'Đơn hủy trước khi in sẽ được hoàn đủ tiền sau khi CEO xác nhận đã chuyển hoàn.'
                  : 'Chỉ hủy được trước khi bắt đầu in. Nếu tiền đã chuyển được ghi nhận sau khi hủy, cửa hàng sẽ xử lý hoàn tiền.',
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _lyDo,
              maxLength: widget.daThanhToan ? 100 : 255,
              minLines: 2,
              maxLines: 4,
              decoration: InputDecoration(
                labelText: widget.daThanhToan ? 'Lý do hoàn tiền' : 'Lý do hủy',
              ),
              validator: (value) => value == null || value.trim().isEmpty
                  ? (widget.daThanhToan
                        ? 'Vui lòng nhập lý do hoàn tiền.'
                        : 'Vui lòng nhập lý do hủy.')
                  : null,
            ),
            if (widget.daThanhToan) ...[
              TextFormField(
                key: const ValueKey('hoan-ngan-hang'),
                controller: _nganHang,
                maxLength: 60,
                textCapitalization: TextCapitalization.words,
                decoration: const InputDecoration(
                  labelText: 'Ngân hàng',
                  hintText: 'Ví dụ: Vietcombank',
                ),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Vui lòng nhập ngân hàng.'
                    : null,
              ),
              TextFormField(
                key: const ValueKey('hoan-so-tai-khoan'),
                controller: _soTaiKhoan,
                maxLength: 30,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                decoration: const InputDecoration(
                  labelText: 'Số tài khoản ngân hàng',
                ),
                validator: (value) =>
                    RegExp(r'^[0-9]{6,30}$').hasMatch(value?.trim() ?? '')
                    ? null
                    : 'Số tài khoản phải gồm 6–30 chữ số.',
              ),
              const Text(
                'Kiểm tra đúng ngân hàng và số tài khoản trước khi gửi. CEO sẽ dùng thông tin này để chuyển hoàn.',
              ),
            ],
          ],
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: Text(widget.capNhatHoanTien ? 'Để sau' : 'Giữ đơn'),
      ),
      FilledButton(
        onPressed: _xacNhan,
        child: Text(
          widget.capNhatHoanTien ? 'Lưu thông tin hoàn tiền' : 'Xác nhận hủy',
        ),
      ),
    ],
  );
}
