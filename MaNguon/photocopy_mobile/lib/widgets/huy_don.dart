import 'chon_anh_bang_chung.dart';
import 'dart:async';
import 'package:flutter/material.dart';
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
    this.khieuNaiSanPham = false,
    this.giayConLai,
    this.thongTinHoanTien,
    this.chonAnh,
    this.chonNhieuAnh,
    this.guiAnh,
  });

  final int maDonHang;
  final Future<void> Function() daHuy;
  final bool daThanhToan;
  final bool capNhatHoanTien;
  final bool khieuNaiSanPham;
  final int Function()? giayConLai;
  final Map<String, dynamic>? thongTinHoanTien;
  final Future<PlatformFile?> Function()? chonAnh;
  final Future<List<PlatformFile>> Function()? chonNhieuAnh;
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
          khieuNaiSanPham: widget.khieuNaiSanPham,
          giayConLai: widget.giayConLai,
          thongTinHoanTien: _banNhap ?? widget.thongTinHoanTien,
          chonAnh: widget.chonAnh,
          chonNhieuAnh: widget.chonNhieuAnh,
        ),
      );
      if (thongTin == null || !mounted) return;
      if (widget.giayConLai != null && widget.giayConLai!() <= 0) {
        hienThongBaoNhanh(context, 'Đã hết hạn 2 phút. Không gửi yêu cầu hủy.');
        return;
      }
      _banNhap = Map<String, dynamic>.from(thongTin);
      final body = Map<String, dynamic>.from(thongTin);
      final anh = (body.remove('anhBangChung') as List? ?? [])
          .map((a) => Map<String, String>.from(a))
          .toList();
      if (widget.khieuNaiSanPham) {
        final uploaded =
            await (widget.guiAnh?.call(widget.maDonHang, anh) ??
                (widget.khieuNaiSanPham
                    ? Api.guiAnhKhieuNai(widget.maDonHang, anh)
                    : Api.guiBangChungHuy(widget.maDonHang, anh)));
        if (!mounted) return;
        if (!uploaded.thanhCong) {
          hienThongBaoNhanh(context, uploaded.loi);
          return;
        }
        body['maBangChung'] = uploaded.duLieu['maBangChung'];
      }
      final thaoTac = widget.khieuNaiSanPham
          ? 'khieu-nai-hoan-tien'
          : widget.capNhatHoanTien
          ? 'thong-tin-hoan-tien'
          : 'huy';
      if (widget.khieuNaiSanPham) body['moTa'] = body.remove('lyDo');
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
        : widget.khieuNaiSanPham
        ? 'Yêu cầu hoàn tiền do sản phẩm'
        : widget.daThanhToan
        ? 'Hủy đơn & yêu cầu hoàn tiền'
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
    this.chonNhieuAnh,
    this.khieuNaiSanPham = false,
    this.giayConLai,
  });
  final bool khieuNaiSanPham;
  final int Function()? giayConLai;
  final bool daThanhToan;
  final bool capNhatHoanTien;
  final Map<String, dynamic>? thongTinHoanTien;
  final Future<PlatformFile?> Function()? chonAnh;
  final Future<List<PlatformFile>> Function()? chonNhieuAnh;

  @override
  State<_HoiHuyDon> createState() => _HoiHuyDonState();
}

class _HoiHuyDonState extends State<_HoiHuyDon> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _lyDo;
  late final TextEditingController _nganHang;
  late final TextEditingController _soTaiKhoan;
  final _chuTaiKhoan = TextEditingController();
  Timer? _nhipHan;
  bool _daXacNhan = false;
  final List<Map<String, String>> _anh = [];
  String? _loiAnh;
  bool _dangChonAnh = false;
  Future<void> _chonAnh() async {
    if (_anh.length >= 3 || _dangChonAnh) return;
    setState(() => _dangChonAnh = true);
    try {
      final files = await chonAnhBangChung(
        daChon: _anh.map((a) => a['path']!),
        chonMot: widget.chonAnh,
        chonNhieu: widget.chonNhieuAnh,
      );
      if (!mounted) return;
      setState(() {
        _anh.addAll(files.map((f) => {'path': f.path!, 'name': f.name}));
        _loiAnh = null;
      });
    } catch (e) {
      if (mounted) {
        setState(
          () => _loiAnh = e is FormatException
              ? e.message.toString()
              : 'Không đọc được ảnh. Chọn JPG/PNG tối đa 5 MB.',
        );
      }
    } finally {
      if (mounted) setState(() => _dangChonAnh = false);
    }
  }

  @override
  void initState() {
    super.initState();
    final info = widget.thongTinHoanTien;
    _chuTaiKhoan.text = info?['chuTaiKhoan']?.toString() ?? '';
    if (widget.giayConLai != null) {
      _nhipHan = Timer.periodic(const Duration(seconds: 1), (_) {
        if (mounted) setState(() {});
      });
    }
    _anh.addAll(
      (info?['anhBangChung'] as List? ?? []).map(
        (a) => Map<String, String>.from(a),
      ),
    );
    _lyDo = TextEditingController(
      text:
          info?['lyDo']?.toString() ??
          (widget.capNhatHoanTien || widget.khieuNaiSanPham
              ? ''
              : 'Tôi không còn nhu cầu in'),
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
    _nhipHan?.cancel();
    _chuTaiKhoan.dispose();
    _lyDo.dispose();
    _nganHang.dispose();
    _soTaiKhoan.dispose();
    super.dispose();
  }

  void _xacNhan() {
    if (_daXacNhan ||
        (widget.giayConLai != null && widget.giayConLai!() <= 0)) {
      return;
    }
    final valid = _form.currentState!.validate();
    if (widget.khieuNaiSanPham && _anh.isEmpty) {
      setState(() => _loiAnh = 'Vui lòng chọn ít nhất một ảnh sản phẩm.');
      return;
    }
    if (valid) {
      _daXacNhan = true;
      Navigator.pop(context, {
        'lyDo': _lyDo.text.trim(),
        if (widget.khieuNaiSanPham) 'chuTaiKhoan': _chuTaiKhoan.text.trim(),
        if (widget.khieuNaiSanPham) 'anhBangChung': _anh,
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
      widget.khieuNaiSanPham
          ? 'Sản phẩm có vấn đề?'
          : widget.capNhatHoanTien
          ? 'Thông tin nhận hoàn tiền'
          : 'Hủy đơn hàng?',
    ),
    content: SingleChildScrollView(
      child: Form(
        key: _form,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (widget.giayConLai != null)
              Text(
                widget.giayConLai!() <= 0
                    ? 'Đã hết hạn hủy 2 phút.'
                    : 'Còn ${widget.giayConLai!() ~/ 60}:${(widget.giayConLai!() % 60).toString().padLeft(2, '0')} · Mở form vẫn tính giờ',
                style: const TextStyle(
                  color: Colors.red,
                  fontWeight: FontWeight.bold,
                ),
              ),
            Text(
              widget.khieuNaiSanPham
                  ? 'Mô tả rõ chỗ lỗi/không hài lòng và đính kèm ảnh sản phẩm. CEO xem xét; gửi yêu cầu chưa đồng nghĩa được hoàn tiền.'
                  : widget.capNhatHoanTien
                  ? 'Đơn đã hủy đang chờ hoàn tiền. Kiểm tra và cập nhật đúng tài khoản để CEO chuyển hoàn.'
                  : widget.daThanhToan
                  ? 'Đơn chỉ hủy khi chưa tiếp nhận và còn trong 2 phút sau thanh toán. Không cần ảnh; khoản hoàn sẽ chờ CEO xác nhận chuyển tiền.'
                  : 'Đơn chưa thanh toán và chưa tiếp nhận có thể hủy ngay, không cần ảnh. Tiền đến sau khi hủy sẽ được cửa hàng đối soát.',
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _lyDo,
              maxLength: widget.khieuNaiSanPham
                  ? 2000
                  : widget.daThanhToan
                  ? 100
                  : 255,
              minLines: 2,
              maxLines: 4,
              decoration: InputDecoration(
                labelText: widget.khieuNaiSanPham
                    ? 'Mô tả chi tiết (trên 10 ký tự)'
                    : widget.daThanhToan
                    ? 'Lý do hoàn tiền'
                    : 'Lý do hủy',
              ),
              validator: (value) =>
                  widget.khieuNaiSanPham && (value?.trim().length ?? 0) < 11
                  ? 'Mô tả trên 10 ký tự.'
                  : value == null || value.trim().isEmpty
                  ? (widget.daThanhToan
                        ? 'Vui lòng nhập lý do hoàn tiền.'
                        : 'Vui lòng nhập lý do hủy.')
                  : null,
            ),
            if (widget.khieuNaiSanPham)
              TextFormField(
                controller: _chuTaiKhoan,
                maxLength: 100,
                decoration: const InputDecoration(
                  labelText: 'Chủ tài khoản ngân hàng',
                ),
                validator: (v) => (v?.trim().length ?? 0) < 2
                    ? 'Nhập tên chủ tài khoản.'
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
            if (widget.khieuNaiSanPham) ...[
              const SizedBox(height: 16),
              Text(
                widget.khieuNaiSanPham
                    ? 'Ảnh bằng chứng sản phẩm *'
                    : 'Bằng chứng yêu cầu hủy *',
                style: TextStyle(fontWeight: FontWeight.w600),
              ),
              Text(
                widget.khieuNaiSanPham
                    ? '1–3 ảnh sản phẩm và phần bị lỗi, JPG/PNG, tối đa 5 MB/ảnh.'
                    : '1–3 ảnh trao đổi/yêu cầu hủy, JPG/PNG, tối đa 5 MB mỗi ảnh.',
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
                onPressed: _anh.length >= 3 || _dangChonAnh ? null : _chonAnh,
                icon: const Icon(Icons.add_photo_alternate_outlined),
                label: Text(
                  _dangChonAnh
                      ? 'Đang đọc ảnh…'
                      : 'Chọn ảnh (${_anh.length}/3) · Chọn nhiều ảnh',
                ),
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
        child: Text(
          widget.capNhatHoanTien || widget.khieuNaiSanPham
              ? 'Để sau'
              : 'Giữ đơn',
        ),
      ),
      FilledButton(
        onPressed: widget.giayConLai != null && widget.giayConLai!() <= 0
            ? null
            : _xacNhan,
        child: Text(
          widget.khieuNaiSanPham
              ? 'Gửi CEO xem xét'
              : widget.capNhatHoanTien
              ? 'Lưu thông tin hoàn tiền'
              : 'Xác nhận hủy',
        ),
      ),
    ],
  );
}
