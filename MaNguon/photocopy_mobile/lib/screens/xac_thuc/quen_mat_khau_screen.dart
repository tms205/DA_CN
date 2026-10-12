import 'package:flutter/material.dart';

import '../../api.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'xac_thuc_otp_screen.dart';

// F-04 bước 1/2: nhập email để nhận mã OTP. Figma không có nên dựng cùng phong cách màn OTP.
// Khách hàng nhận OTP; Shipper thì API trả câu hướng dẫn liên hệ Quản lý chi nhánh (không có OTP).
class QuenMatKhauScreen extends StatefulWidget {
  const QuenMatKhauScreen({super.key});

  @override
  State<QuenMatKhauScreen> createState() => _QuenMatKhauScreenState();
}

class _QuenMatKhauScreenState extends State<QuenMatKhauScreen> {
  final TextEditingController _email = TextEditingController();
  bool _dangGui = false;
  String? _loi;
  String? _capTren;   // tài khoản Shipper: câu hướng dẫn liên hệ cấp trên

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  bool _emailHopLe() => RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(_email.text.trim());

  Future<void> _guiMa() async {
    String email = _email.text.trim();
    if (!_emailHopLe()) {
      setState(() => _loi = 'Email không hợp lệ.');
      return;
    }
    FocusScope.of(context).unfocus();
    setState(() {
      _dangGui = true;
      _loi = null;
    });

    KetQuaApi kq = await Api.post('/api/auth/forgot-password', {'email': email});
    if (!mounted) return;
    setState(() => _dangGui = false);

    if (!kq.thanhCong) {
      setState(() => _loi = kq.loi);
      return;
    }

    // Tài khoản nhân viên (Shipper): không có OTP, chỉ hiện hướng dẫn
    if (kq.duLieu['canCapTrenCapLai'] == true) {
      setState(() => _capTren = kq.duLieu['message']);
      return;
    }

    // Sang bước 2: nhập mã OTP + mật khẩu mới
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (context) => XacThucOtpScreen(
          laDangKy: false,
          email: email,
          hetHanSauGiay: kq.duLieu['hetHanSauGiay'],
          guiLaiSauGiay: kq.duLieu['guiLaiSauGiay'],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(tieuDe: 'Quên mật khẩu', phuDe: 'Bước 1 / 2 · Nhập email', soBuoc: 2, buocHienTai: 1),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(24, 30, 24, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Center(
              child: Container(
                width: 62,
                height: 62,
                decoration: BoxDecoration(
                  color: AppColors.accentSoft,
                  shape: BoxShape.circle,
                  border: Border.all(color: AppColors.accentTint),
                ),
                child: const Center(child: AppIcon(Ico.khoa, size: 26, color: AppColors.accent, net: 1.6)),
              ),
            ),
            const SizedBox(height: 14),
            Text('Lấy lại mật khẩu',
                textAlign: TextAlign.center, style: AppTheme.font(size: 19, weight: FontWeight.w800, letterSpacing: -0.38)),
            const SizedBox(height: 6),
            Text('Nhập email tài khoản, hệ thống gửi mã OTP 6 số để đặt mật khẩu mới.',
                textAlign: TextAlign.center, style: AppTheme.font(size: 12.5, color: AppColors.chuPhu, height: 1.55)),
            const SizedBox(height: 22),
            if (_capTren != null) ...[
              HopThongBao(_capTren!, loai: LoaiThongBao.canhBao),
              const SizedBox(height: 16),
              NutPhu(nhan: 'Quay lại đăng nhập', onPressed: () => Navigator.pop(context)),
              const SizedBox(height: 22),
            ],
            if (_loi != null) ...[HopThongBao(_loi!), const SizedBox(height: 14)],
            const NhanTruong('Email'),
            const SizedBox(height: 6),
            ONhap(
              controller: _email,
              icon: Ico.thu,
              hint: 'khachhang@gmail.com',
              hopLe: _emailHopLe(),
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.done,
              autofillHints: const [AutofillHints.email],
              onChanged: (chu) => setState(() {
                _loi = null;
                _capTren = null;
              }),
              onSubmitted: (chu) => _guiMa(),
            ),
            const SizedBox(height: 20),
            NutChinh(nhan: 'Gửi mã OTP', iconSau: Ico.muiTenPhai, dangTai: _dangGui, onPressed: _guiMa),
            const SizedBox(height: 16),
            Text(
              'Tài khoản Shipper không nhận OTP — vui lòng liên hệ Quản lý chi nhánh để được cấp mật khẩu tạm.',
              textAlign: TextAlign.center,
              style: AppTheme.font(size: 11, color: AppColors.chuPhu, height: 1.5),
            ),
          ],
        ),
      ),
    );
  }
}
