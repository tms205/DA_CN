import 'package:flutter/material.dart';

import '../../api.dart';
import '../../phien.dart';
import '../../realtime.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'dang_ky_screen.dart';
import 'quen_mat_khau_screen.dart';
import '../trang_chinh_screen.dart';

// F-02 — Figma 06-Mobile-DangNhap: một màn dùng chung cho Khách hàng và Shipper.
// Đăng nhập xong vào trang chính; trang chính tự hiện tab theo vai trò.
class DangNhapScreen extends StatefulWidget {
  const DangNhapScreen({super.key});

  @override
  State<DangNhapScreen> createState() => _DangNhapScreenState();
}

class _DangNhapScreenState extends State<DangNhapScreen> {
  final TextEditingController _taiKhoan = TextEditingController();
  final TextEditingController _matKhau = TextEditingController();
  bool _ghiNho = true;
  bool _dangGui = false;   // đang chờ API trả lời thì nút hiện vòng xoay
  String? _loi;            // câu lỗi hiện trong hộp đỏ

  @override
  void dispose() {
    _taiKhoan.dispose();
    _matKhau.dispose();
    super.dispose();
  }

  // Ô tài khoản trông giống email hoặc số điện thoại 10 số thì hiện dấu tích xanh
  bool _taiKhoanHopLe() {
    String tk = _taiKhoan.text.trim();
    bool laEmail = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(tk);
    bool laSdt = RegExp(r'^0\d{9}$').hasMatch(tk);
    return laEmail || laSdt;
  }

  Future<void> _dangNhap() async {
    FocusScope.of(context).unfocus(); // ẩn bàn phím
    if (_taiKhoan.text.trim().isEmpty || _matKhau.text.isEmpty) {
      setState(() => _loi = 'Vui lòng nhập email/số điện thoại và mật khẩu.');
      return;
    }

    setState(() {
      _dangGui = true;
      _loi = null;
    });

    // App gọi /api/auth/login — API chỉ cho Khách hàng và Shipper
    KetQuaApi kq = await Api.post('/api/auth/login', {
      'taiKhoan': _taiKhoan.text.trim(),
      'matKhau': _matKhau.text,
      'ghiNho': _ghiNho,
    });
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() {
        _loi = kq.loi;
        _dangGui = false;
      });
      return;
    }

    // Lưu token + thông tin người dùng rồi vào trang chính (xóa hết các màn phía trước)
    await Phien.batDau(kq.duLieu, _ghiNho);
    await Realtime.batDau();   // F-32: mở kênh nhận cập nhật real-time
    if (!mounted) return;
    Navigator.pushAndRemoveUntil(
      context,
      MaterialPageRoute(builder: (context) => const TrangChinhScreen()),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.nen,
      body: Column(
        children: [
          Expanded(
            child: SafeArea(
              bottom: false,
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(24, 50, 24, 20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Center(child: LogoApp()),
                    const SizedBox(height: 14),
                    Text('Chào mừng trở lại',
                        textAlign: TextAlign.center,
                        style: AppTheme.font(size: 22, weight: FontWeight.w800, letterSpacing: -0.44)),
                    const SizedBox(height: 5),
                    Text.rich(
                      TextSpan(children: [
                        const TextSpan(text: 'Đăng nhập vào ứng dụng\nchuỗi in ấn '),
                        TextSpan(
                            text: 'PhotocopyChain',
                            style: AppTheme.font(size: 12.5, weight: FontWeight.w700, color: AppColors.chuNhan)),
                      ]),
                      textAlign: TextAlign.center,
                      style: AppTheme.font(size: 12.5, color: AppColors.chuPhu, height: 1.5),
                    ),
                    const SizedBox(height: 20),
                    if (_loi != null) ...[HopThongBao(_loi!), const SizedBox(height: 12)],
                    const NhanTruong('Email hoặc số điện thoại'),
                    const SizedBox(height: 6),
                    ONhap(
                      controller: _taiKhoan,
                      icon: Ico.thu,
                      hint: 'khachhang@gmail.com',
                      hopLe: _taiKhoanHopLe(),
                      keyboardType: TextInputType.emailAddress,
                      textInputAction: TextInputAction.next,
                      autofillHints: const [AutofillHints.username],
                      onChanged: (chu) => setState(() => _loi = null),
                    ),
                    const SizedBox(height: 12),
                    const NhanTruong('Mật khẩu'),
                    const SizedBox(height: 6),
                    ONhap(
                      controller: _matKhau,
                      icon: Ico.khoa,
                      hint: '••••••••',
                      anChu: true,
                      textInputAction: TextInputAction.done,
                      autofillHints: const [AutofillHints.password],
                      onChanged: (chu) => setState(() => _loi = null),
                      onSubmitted: (chu) => _dangNhap(),
                    ),
                    const SizedBox(height: 13),
                    Row(
                      children: [
                        Flexible(
                          child: GestureDetector(
                            behavior: HitTestBehavior.opaque,
                            onTap: () => setState(() => _ghiNho = !_ghiNho),
                            child: Row(mainAxisSize: MainAxisSize.min, children: [
                              HopChonVuong(daChon: _ghiNho),
                              const SizedBox(width: 8),
                              Flexible(
                                child: Text('Ghi nhớ đăng nhập',
                                    overflow: TextOverflow.ellipsis,
                                    style: AppTheme.font(size: 12, color: AppColors.chuNhan)),
                              ),
                            ]),
                          ),
                        ),
                        const SizedBox(width: 12),
                        GestureDetector(
                          onTap: () {
                            Navigator.push(context, MaterialPageRoute(builder: (context) => const QuenMatKhauScreen()));
                          },
                          child: Text('Quên mật khẩu?',
                              style: AppTheme.font(size: 12, weight: FontWeight.w600, color: AppColors.accent)),
                        ),
                      ],
                    ),
                    const SizedBox(height: 15),
                    NutChinh(nhan: 'Đăng nhập', iconSau: Ico.muiTenPhai, dangTai: _dangGui, onPressed: _dangNhap),
                    const SizedBox(height: 14),
                    const DuongHoac('hoặc'),
                    const SizedBox(height: 14),
                    NutPhu(
                      nhan: 'Tiếp tục với Google',
                      icon: const GoogleLogo(),
                      onPressed: () => hienThongBaoNhanh(context, 'Đăng nhập Google chưa được bật ở phiên bản này.'),
                    ),
                    const SizedBox(height: 14),
                    KhungNetDut(
                      child: Padding(
                        padding: const EdgeInsets.fromLTRB(12, 11, 12, 11),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Padding(
                              padding: EdgeInsets.only(top: 1),
                              child: AppIcon(Ico.thongTin, size: 14, color: AppColors.iconMo, net: 1.9),
                            ),
                            const SizedBox(width: 9),
                            Expanded(
                              child: Text.rich(
                                TextSpan(children: [
                                  const TextSpan(text: 'Màn hình dùng chung cho '),
                                  _chuDam('Khách hàng'),
                                  const TextSpan(text: ' và '),
                                  _chuDam('Shipper'),
                                  const TextSpan(
                                      text: '. Vai trò trong JWT quyết định điểm đến: khách vào trang đặt in, Shipper vào danh sách đơn đi giao. '
                                          'Nhân viên in ấn và Quản lý đăng nhập tại Web quản trị.'),
                                ]),
                                style: AppTheme.font(size: 10.5, color: AppColors.chuPhu, height: 1.55),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          Container(
            decoration: const BoxDecoration(
              color: AppColors.trang,
              border: Border(top: BorderSide(color: AppColors.vach)),
            ),
            child: SafeArea(
              top: false,
              child: SizedBox(
                height: 60,
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Text('Chưa có tài khoản?', style: AppTheme.font(size: 12.5, color: AppColors.chuPhu)),
                    const SizedBox(width: 5),
                    GestureDetector(
                      onTap: () {
                        Navigator.push(context, MaterialPageRoute(builder: (context) => const DangKyScreen()));
                      },
                      child: Text('Đăng ký ngay',
                          style: AppTheme.font(size: 12.5, weight: FontWeight.w700, color: AppColors.accent)),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  TextSpan _chuDam(String chu) {
    return TextSpan(text: chu, style: AppTheme.font(size: 10.5, weight: FontWeight.w700, color: AppColors.chuNhan));
  }
}
