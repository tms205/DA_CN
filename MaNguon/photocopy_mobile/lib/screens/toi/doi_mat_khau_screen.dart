import 'package:flutter/material.dart';

import '../../api.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';

// F-04: đổi mật khẩu khi đang đăng nhập (kể cả Shipper vừa được Quản lý cấp mật khẩu tạm).
class DoiMatKhauScreen extends StatefulWidget {
  const DoiMatKhauScreen({super.key});

  @override
  State<DoiMatKhauScreen> createState() => _DoiMatKhauScreenState();
}

class _DoiMatKhauScreenState extends State<DoiMatKhauScreen> {
  final TextEditingController _cu = TextEditingController();
  final TextEditingController _moi = TextEditingController();
  final TextEditingController _nhapLai = TextEditingController();
  bool _dangLuu = false;
  String? _loi;

  @override
  void dispose() {
    _cu.dispose();
    _moi.dispose();
    _nhapLai.dispose();
    super.dispose();
  }

  bool _matKhauDuManh(String mk) => RegExp(r'^(?=.*[A-Z])(?=.*\d).{8,}$').hasMatch(mk);

  Future<void> _luu() async {
    // Kiểm tra trước khi gọi API
    if (_cu.text.isEmpty || _moi.text.isEmpty || _nhapLai.text.isEmpty) {
      setState(() => _loi = 'Vui lòng nhập đủ mật khẩu hiện tại, mật khẩu mới và nhập lại.');
      return;
    }
    if (!_matKhauDuManh(_moi.text)) {
      setState(() => _loi = 'Mật khẩu tối thiểu 8 ký tự, có chữ hoa và số.');
      return;
    }
    if (_moi.text != _nhapLai.text) {
      setState(() => _loi = 'Mật khẩu nhập lại không khớp.');
      return;
    }

    FocusScope.of(context).unfocus();
    setState(() {
      _dangLuu = true;
      _loi = null;
    });

    // PUT /api/auth/me/password
    KetQuaApi kq = await Api.put('/api/auth/me/password', {'matKhauCu': _cu.text, 'matKhauMoi': _moi.text});
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() {
        _loi = kq.loi;
        _dangLuu = false;
      });
      return;
    }
    hienThongBaoNhanh(context, '${kq.duLieu['message']} Lần đăng nhập sau dùng mật khẩu mới.');
    Navigator.pop(context);
  }

  @override
  Widget build(BuildContext context) {
    String mk = _moi.text;
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(tieuDe: 'Đổi mật khẩu', phuDe: 'Bảo vệ tài khoản của bạn'),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_loi != null) ...[HopThongBao(_loi!), const SizedBox(height: 14)],
            const NhanTruong('Mật khẩu hiện tại'),
            const SizedBox(height: 6),
            ONhap(
              controller: _cu,
              icon: Ico.khoa,
              hint: '••••••••',
              anChu: true,
              textInputAction: TextInputAction.next,
              autofillHints: const [AutofillHints.password],
              onChanged: (chu) => setState(() => _loi = null),
            ),
            const SizedBox(height: 14),
            const NhanTruong('Mật khẩu mới'),
            const SizedBox(height: 6),
            ONhap(
              controller: _moi,
              icon: Ico.khoa,
              hint: '8+ ký tự, có chữ hoa và số',
              anChu: true,
              hopLe: _matKhauDuManh(mk),
              textInputAction: TextInputAction.next,
              autofillHints: const [AutofillHints.newPassword],
              onChanged: (chu) => setState(() => _loi = null),
            ),
            const SizedBox(height: 9),
            // 3 quy tắc sáng xanh dần khi gõ
            Wrap(
              spacing: 14,
              runSpacing: 5,
              children: [
                _quyTac('Tối thiểu 8 ký tự', mk.length >= 8),
                _quyTac('Có chữ hoa', mk.contains(RegExp(r'[A-Z]'))),
                _quyTac('Có chữ số', mk.contains(RegExp(r'\d'))),
              ],
            ),
            const SizedBox(height: 14),
            const NhanTruong('Nhập lại mật khẩu mới'),
            const SizedBox(height: 6),
            ONhap(
              controller: _nhapLai,
              icon: Ico.khoa,
              hint: '••••••••',
              anChu: true,
              hopLe: _nhapLai.text.isNotEmpty && _nhapLai.text == mk,
              textInputAction: TextInputAction.done,
              onChanged: (chu) => setState(() => _loi = null),
              onSubmitted: (chu) => _luu(),
            ),
            const SizedBox(height: 24),
            NutChinh(nhan: 'Lưu mật khẩu mới', iconTruoc: Ico.dauTich, dangTai: _dangLuu, onPressed: _luu),
            const SizedBox(height: 14),
            Text(
              'Quên mật khẩu hiện tại? Đăng xuất rồi bấm "Quên mật khẩu?" ở màn đăng nhập '
              '(Shipper liên hệ Quản lý chi nhánh để được cấp mật khẩu tạm).',
              textAlign: TextAlign.center,
              style: AppTheme.font(size: 11, color: AppColors.chuPhu, height: 1.5),
            ),
          ],
        ),
      ),
    );
  }

  Widget _quyTac(String chu, bool dat) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        AppIcon(Ico.dauTich, size: 12, color: dat ? AppColors.thanhCong : AppColors.iconMo, net: 3),
        const SizedBox(width: 4),
        Text(chu,
            style: AppTheme.font(
                size: 11.5, weight: dat ? FontWeight.w600 : FontWeight.w500, color: dat ? AppColors.thanhCong : AppColors.chuPhu)),
      ],
    );
  }
}
