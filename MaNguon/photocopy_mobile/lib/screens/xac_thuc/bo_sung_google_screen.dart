import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../api.dart';
import '../../kiem_tra_du_lieu.dart';
import '../../theme.dart';
import '../../widgets/widgets.dart';
import '../../phien.dart';
import '../../realtime.dart';
import '../trang_chinh_screen.dart';

// Chỉ lưu phiên JWT hệ thống sau khi API hoàn tất xác thực/đăng ký.
Future<void> moPhienGoogle(
  BuildContext context,
  Map<String, dynamic> json,
  bool ghiNho,
) async {
  await Phien.batDau(json, ghiNho);
  await Realtime.batDau();
  if (!context.mounted) return;
  Navigator.pushAndRemoveUntil(
    context,
    MaterialPageRoute(builder: (_) => const TrangChinhScreen()),
    (_) => false,
  );
}

class BoSungGoogleScreen extends StatefulWidget {
  const BoSungGoogleScreen({
    super.key,
    required this.veDangKy,
    required this.email,
    required this.hoTen,
    required this.ghiNho,
  });
  final String veDangKy, email, hoTen;
  final bool ghiNho;
  @override
  State<BoSungGoogleScreen> createState() => _BoSungGoogleScreenState();
}

class _BoSungGoogleScreenState extends State<BoSungGoogleScreen> {
  late final TextEditingController _ten = TextEditingController(
    text: widget.hoTen,
  );
  final TextEditingController _sdt = TextEditingController();
  bool _dongY = false, _dangGui = false;
  String? _loi;

  @override
  void dispose() {
    _sdt.dispose();
    _ten.dispose();
    super.dispose();
  }

  Future<void> _hoanTat() async {
    if (_dangGui) return;
    FocusScope.of(context).unfocus();
    final data = {
      'veDangKy': widget.veDangKy,
      'hoTen': _ten.text.trim(),
      'soDienThoai': _sdt.text.trim(),
      'dongYDieuKhoan': _dongY,
      'ghiNho': widget.ghiNho,
    };
    final loi = kiemTraDuLieu('/api/auth/google/register', data);
    if (loi != null) {
      setState(() => _loi = loi);
      return;
    }
    setState(() {
      _dangGui = true;
      _loi = null;
    });
    try {
      final kq = await Api.post('/api/auth/google/register', data);
      if (!mounted) return;
      if (!kq.thanhCong) {
        setState(() => _loi = kq.loi);
        return;
      }
      await moPhienGoogle(
        context,
        Map<String, dynamic>.from(kq.duLieu),
        widget.ghiNho,
      );
    } catch (_) {
      if (mounted) {
        setState(
          () => _loi =
              'Không hoàn tất được đăng ký. Vui lòng quay lại đăng nhập Google để kiểm tra tài khoản.',
        );
      }
    } finally {
      if (mounted) setState(() => _dangGui = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: AppBar(title: const Text('Hoàn tất đăng ký')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Google đã xác thực email',
                style: AppTheme.font(size: 22, weight: FontWeight.w700),
              ),
              const SizedBox(height: 12),
              SelectableText(
                widget.email,
                style: AppTheme.font(size: 16, color: AppColors.accent),
              ),
              const SizedBox(height: 12),
              Text(
                'Bổ sung số điện thoại để liên hệ đơn in. Tài khoản mới là khách hàng; không có quyền quản trị.',
                style: AppTheme.font(
                  size: 15,
                  color: AppColors.chuPhu,
                  height: 1.5,
                ),
              ),
              if (_loi != null) ...[
                const SizedBox(height: 16),
                HopThongBao(_loi!),
              ],
              const SizedBox(height: 24),
              TextField(
                controller: _ten,
                enabled: !_dangGui,
                maxLength: 100,
                textInputAction: TextInputAction.next,
                autofillHints: const [AutofillHints.name],
                decoration: const InputDecoration(
                  labelText: 'Họ và tên',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _sdt,
                enabled: !_dangGui,
                maxLength: 10,
                keyboardType: TextInputType.phone,
                autofillHints: const [AutofillHints.telephoneNumberNational],
                inputFormatters: [
                  FilteringTextInputFormatter.allow(RegExp(r'[0-9]')),
                ],
                decoration: const InputDecoration(
                  labelText: 'Số điện thoại',
                  hintText: '0xxxxxxxxx',
                  helperText: '10 chữ số, bắt đầu bằng 0',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 16),
              CheckboxListTile(
                value: _dongY,
                onChanged: _dangGui
                    ? null
                    : (v) => setState(() => _dongY = v ?? false),
                controlAffinity: ListTileControlAffinity.leading,
                contentPadding: EdgeInsets.zero,
                title: Text(
                  'Tôi đồng ý Điều khoản sử dụng và Chính sách bảo mật.',
                  style: AppTheme.font(size: 15, height: 1.5),
                ),
              ),
              const SizedBox(height: 24),
              NutChinh(
                nhan: 'Hoàn tất đăng ký',
                dangTai: _dangGui,
                onPressed: _hoanTat,
                chieuCao: 56,
              ),
              const SizedBox(height: 12),
              TextButton(
                onPressed: _dangGui ? null : () => Navigator.pop(context),
                child: const Text('Quay lại chọn tài khoản Google'),
              ),
              const SizedBox(height: 12),
              Text(
                'Phiên bổ sung thông tin có hiệu lực 10 phút. Nếu hết hạn, hãy quay lại chọn Google.',
                style: AppTheme.font(
                  size: 14,
                  color: AppColors.chuPhu,
                  height: 1.5,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
