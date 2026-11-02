import 'package:flutter/material.dart';
import 'dart:io';
import 'package:file_picker/file_picker.dart';
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
    this.chonAnh,
    this.guiAnh,
  });

  final int maDonHang;
  final Future<void> Function() daHuy;
  final bool daThanhToan;
  final bool capNhatHoanTien;
  final Map<String, dynamic>? thongTinHoanTien;
  final Future<PlatformFile?> Function()? chonAnh;
  final Future<KetQuaApi> Function(int, List<Map<String, String>>)? guiAnh;

  @override
  State<NutHuyDon> createState() => _NutHuyDonState();
}

class _NutHuyDonState extends State<NutHuyDon> {
  bool _dangXuLy = false;
  Map<String, dynamic>? _banNhap;

  Future<void> _huy() async {
    if (_dangXuLy) return;
    setState(() => _dangXuLy = true);
    try {
      final thongTin = await showDialog<Map<String, dynamic>>(
        context: context,
        builder: (_) => _HoiHuyDon(
          daThanhToan: widget.daThanhToan || widget.capNhatHoanTien,
          capNhatHoanTien: widget.capNhatHoanTien,
          thongTinHoanTien: _banNhap ?? widget.thongTinHoanTien,
          chonAnh: widget.chonAnh,
        ),
      );
      if (thongTin == null || !mounted) return;
      _banNhap = Map<String, dynamic>.from(thongTin);
      final body = Map<String, dynamic>.from(thongTin);
      final anh = (body.remove('anhBangChung') as List? ?? [])
          .map((a) => Map<String, String>.from(a))
          .toList();
      if (!widget.capNhatHoanTien) {
        final uploaded =
            await (widget.guiAnh?.call(widget.maDonHang, anh) ??
                Api.guiBangChungHuy(widget.maDonHang, anh));
        if (!mounted) return;
        if (!uploaded.thanhCong) {
          hienThongBaoNhanh(context, uploaded.loi);
          return;
        }
        body['maBangChung'] = uploaded.duLieu['maBangChung'];
      }
      final thaoTac = widget.capNhatHoanTien ? 'thong-tin-hoan-tien' : 'huy';
      final kq = await Api.post(
        '/api/don-hang/${widget.maDonHang}/$thaoTac',
        body,
      );
      if (!mounted) return;
      final thongBao = kq.thanhCong && kq.duLieu is Map
          ? kq.duLieu['message']?.toString() ?? 'Đã hủy đơn.'
          : kq.loi;
      hienThongBaoNhanh(context, thongBao);
      if (kq.thanhCong) {
        _banNhap = null;
        await widget.daHuy();
      }
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
    this.chonAnh,
  });
  final bool daThanhToan;
  final bool capNhatHoanTien;
  final Map<String, dynamic>? thongTinHoanTien;
  final Future<PlatformFile?> Function()? chonAnh;

  @override
  State<_HoiHuyDon> createState() => _HoiHuyDonState();
}

class _HoiHuyDonState extends State<_HoiHuyDon> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _lyDo;
  late final TextEditingController _nganHang;
  late final TextEditingController _soTaiKhoan;
  bool _daXacNhan = false;
  final List<Map<String, String>> _anh = [];
  String? _loiAnh;
  Future<void> _chonAnh() async {
    if (_anh.length >= 3) return;
    try {
      final file =
          await (widget.chonAnh?.call() ??
              FilePicker.pickFile(
                type: FileType.custom,
                allowedExtensions: ['jpg', 'jpeg', 'png'],
              ));
      if (file?.path == null || !mounted) return;
      final size = await File(file!.path!).length();
      if (!mounted) return;
      if (size <= 0 || size > 5 * 1024 * 1024) {
        throw StateError('Mỗi ảnh tối đa 5 MB.');
      }
      if (_anh.any((a) => a['path'] == file.path)) return;
      setState(() {
        _anh.add({'path': file.path!, 'name': file.name});
        _loiAnh = null;
      });
    } catch (_) {
      if (mounted) {
        setState(
          () => _loiAnh = 'Không đọc được ảnh. Chọn JPG/PNG tối đa 5 MB.',
        );
      }
    }
  }

  @override
  void initState() {
    super.initState();
    final info = widget.thongTinHoanTien;
    _anh.addAll(
      (info?['anhBangChung'] as List? ?? []).map(
        (a) => Map<String, String>.from(a),
      ),
    );
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
    final valid = _form.currentState!.validate();
    if (!widget.capNhatHoanTien && _anh.isEmpty) {
      setState(() => _loiAnh = 'Vui lòng chọn ảnh bằng chứng hủy đơn.');
      return;
    }
    if (valid) {
      _daXacNhan = true;
      Navigator.pop(context, {
        'lyDo': _lyDo.text.trim(),
        if (!widget.capNhatHoanTien) 'anhBangChung': _anh,
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
                  : 'Chỉ hủy khi chưa tiếp nhận và còn hơn 15 phút trước giờ hẹn. Tiền đến sau khi hủy sẽ được cửa hàng đối soát.',
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
            if (!widget.capNhatHoanTien) ...[
              const SizedBox(height: 16),
              const Text(
                'Bằng chứng yêu cầu hủy *',
                style: TextStyle(fontWeight: FontWeight.w600),
              ),
              const Text(
                '1–3 ảnh trao đổi/yêu cầu hủy, JPG/PNG, tối đa 5 MB mỗi ảnh.',
              ),
              for (int i = 0; i < _anh.length; i++)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(
                    _anh[i]['name']!,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                  trailing: IconButton(
                    tooltip: 'Bỏ ảnh ${i + 1}',
                    icon: const Icon(Icons.delete_outline),
                    onPressed: () => setState(() => _anh.removeAt(i)),
                  ),
                ),
              OutlinedButton.icon(
                onPressed: _anh.length >= 3 ? null : _chonAnh,
                icon: const Icon(Icons.add_photo_alternate_outlined),
                label: Text('Thêm ảnh (${_anh.length}/3)'),
              ),
              if (_loiAnh != null)
                Text(_loiAnh!, style: const TextStyle(color: Colors.red)),
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
