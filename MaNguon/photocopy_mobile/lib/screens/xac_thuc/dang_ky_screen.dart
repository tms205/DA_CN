import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../api.dart';
import '../../kiem_tra_du_lieu.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'xac_thuc_otp_screen.dart';

// F-01 — Figma 07-Mobile-DangKy. Bước 1/2: nhập thông tin, API gửi mã OTP về email.
// Tài khoản CHỈ được tạo sau khi nhập đúng mã ở bước 2 (màn XacThucOtpScreen).
class DangKyScreen extends StatefulWidget {
  const DangKyScreen({super.key});

  @override
  State<DangKyScreen> createState() => _DangKyScreenState();
}

class _DangKyScreenState extends State<DangKyScreen> {
  final TextEditingController _hoTen = TextEditingController();
  final TextEditingController _email = TextEditingController();
  final TextEditingController _soDienThoai = TextEditingController();
  final TextEditingController _matKhau = TextEditingController();
  bool _dongY = false;     // đã tick đồng ý điều khoản chưa
  bool _dangGui = false;
  String? _loi;

  @override
  void dispose() {
    _hoTen.dispose();
    _email.dispose();
    _soDienThoai.dispose();
    _matKhau.dispose();
    super.dispose();
  }

  // Người dùng gõ sau ô "+84": nhận cả "906218774" lẫn "0906218774" → luôn đổi về dạng 0xxxxxxxxx
  String _sdtChuan() {
    String so = _soDienThoai.text.trim(); // Không âm thầm bỏ ký tự sai rồi đăng ký số khác.
    if (so.startsWith('0')) return so;
    return '0$so';
  }

  bool _emailHopLe() => emailGmailHopLe(_email.text.trim());

  bool _sdtHopLe() => RegExp(r'^0\d{9}$').hasMatch(_sdtChuan());

  // Độ mạnh mật khẩu 0..4 vạch như Figma
  int _doManh() {
    String mk = _matKhau.text;
    if (mk.isEmpty) return 0;
    int diem = 0;
    if (mk.length >= 8) diem++;
    if (RegExp(r'[A-Z]').hasMatch(mk)) diem++;
    if (RegExp(r'\d').hasMatch(mk)) diem++;
    if (mk.length >= 12 || RegExp(r'[^A-Za-z0-9]').hasMatch(mk)) diem++;
    return diem;
  }

  // Kiểm tra dữ liệu trước khi gọi API. Trả về câu lỗi, hợp lệ thì trả về null.
  String? _kiemTra() {
    if (_hoTen.text.trim().isEmpty) return 'Vui lòng nhập họ và tên.';
    if (!_emailHopLe()) return 'Email nhập mới phải là địa chỉ @gmail.com hợp lệ.';
    if (!_sdtHopLe()) return 'Số điện thoại phải gồm 9 số sau +84 (hoặc 10 số bắt đầu bằng 0).';
    if (!RegExp(r'^(?=.*[A-Z])(?=.*\d).{8,}$').hasMatch(_matKhau.text)) {
      return 'Mật khẩu tối thiểu 8 ký tự, có chữ hoa và số.';
    }
    if (!_dongY) return 'Vui lòng đồng ý Điều khoản sử dụng và Chính sách bảo mật.';
    return null;
  }

  Future<void> _tiepTuc() async {
    FocusScope.of(context).unfocus();
    String? loi = _kiemTra();
    if (loi != null) {
      setState(() => _loi = loi);
      return;
    }

    // Thông tin đăng ký — giữ lại để màn OTP bấm "Gửi lại mã"
    Map<String, dynamic> thongTin = {
      'hoTen': _hoTen.text.trim(),
      'email': _email.text.trim(),
      'soDienThoai': _sdtChuan(),
      'matKhau': _matKhau.text,
    };

    setState(() {
      _dangGui = true;
      _loi = null;
    });
    KetQuaApi kq = await Api.post('/api/auth/register/otp', thongTin);
    if (!mounted) return;
    setState(() => _dangGui = false);

    if (!kq.thanhCong) {
      setState(() => _loi = kq.loi);
      return;
    }

    // Sang bước 2: nhập mã OTP
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (context) => XacThucOtpScreen(
          laDangKy: true,
          email: thongTin['email'],
          hetHanSauGiay: kq.duLieu['hetHanSauGiay'],
          guiLaiSauGiay: kq.duLieu['guiLaiSauGiay'],
          thongTinDangKy: thongTin,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    // Nhãn + màu của thanh độ mạnh mật khẩu
    int doManh = _doManh();
    String nhanDoManh = '';
    Color mauDoManh = AppColors.vien;
    if (doManh == 1) {
      nhanDoManh = 'Yếu';
      mauDoManh = AppColors.loiChu;
    } else if (doManh == 2) {
      nhanDoManh = 'Trung bình';
      mauDoManh = AppColors.cbIcon;
    } else if (doManh >= 3) {
      nhanDoManh = 'Mạnh';
      mauDoManh = AppColors.thanhCong;
    }

    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(tieuDe: 'Tạo tài khoản', phuDe: 'Bước 1 / 2 · Thông tin cá nhân', soBuoc: 2, buocHienTai: 1),
      body: Column(
        children: [
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(22, 18, 22, 18),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  NutPhu(
                    nhan: 'Đăng ký nhanh với Google',
                    icon: const GoogleLogo(),
                    onPressed: () => hienThongBaoNhanh(context, 'Đăng ký bằng Google chưa được bật ở phiên bản này.'),
                  ),
                  const SizedBox(height: 14),
                  const DuongHoac('hoặc điền biểu mẫu'),
                  const SizedBox(height: 14),
                  if (_loi != null) ...[HopThongBao(_loi!), const SizedBox(height: 14)],
                  const NhanTruong('Họ và tên'),
                  const SizedBox(height: 6),
                  ONhap(
                    controller: _hoTen,
                    icon: Ico.nguoi,
                    hint: 'Nguyễn Văn A',
                    textInputAction: TextInputAction.next,
                    autofillHints: const [AutofillHints.name],
                    onChanged: (chu) => setState(() => _loi = null),
                  ),
                  const SizedBox(height: 14),
                  const NhanTruong('Email'),
                  const SizedBox(height: 6),
                  ONhap(
                    controller: _email,
                    icon: Ico.thu,
                    hint: 'khachhang@gmail.com',
                    hopLe: _emailHopLe(),
                    keyboardType: TextInputType.emailAddress,
                    textInputAction: TextInputAction.next,
                    autofillHints: const [AutofillHints.email],
                    onChanged: (chu) => setState(() => _loi = null),
                  ),
                  const SizedBox(height: 6),
                  _goiY('Mã OTP 6 số sẽ được gửi tới địa chỉ này.'),
                  const SizedBox(height: 14),
                  const NhanTruong('Số điện thoại'),
                  const SizedBox(height: 6),
                  ONhap(
                    controller: _soDienThoai,
                    tienTo: '+84',
                    hint: '906 218 774',
                    laMono: true,
                    hopLe: _sdtHopLe(),
                    keyboardType: TextInputType.phone,
                    textInputAction: TextInputAction.next,
                    autofillHints: const [AutofillHints.telephoneNumberNational],
                    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9 ]')), LengthLimitingTextInputFormatter(13)],
                    onChanged: (chu) => setState(() => _loi = null),
                  ),
                  const SizedBox(height: 14),
                  const NhanTruong('Mật khẩu'),
                  const SizedBox(height: 6),
                  ONhap(
                    controller: _matKhau,
                    icon: Ico.khoa,
                    hint: '8+ ký tự, có chữ hoa và số',
                    anChu: true,
                    textInputAction: TextInputAction.done,
                    autofillHints: const [AutofillHints.newPassword],
                    onChanged: (chu) => setState(() => _loi = null),
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      // 4 vạch độ mạnh
                      Expanded(
                        child: Row(
                          children: [
                            for (int i = 0; i < 4; i++)
                              Expanded(
                                child: Container(
                                  height: 4,
                                  margin: EdgeInsets.only(left: i == 0 ? 2 : 4),
                                  decoration: BoxDecoration(
                                    color: i < doManh ? mauDoManh : AppColors.vien,
                                    borderRadius: BorderRadius.circular(2),
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 8),
                      SizedBox(
                        width: 64,
                        child: Text(nhanDoManh,
                            textAlign: TextAlign.right,
                            style: AppTheme.font(size: 10.5, weight: FontWeight.w700, color: mauDoManh)),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  _goiY('Tối thiểu 8 ký tự, có chữ hoa và số.'),
                  const SizedBox(height: 16),
                  GestureDetector(
                    behavior: HitTestBehavior.opaque,
                    onTap: () => setState(() {
                      _dongY = !_dongY;
                      _loi = null;
                    }),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Padding(padding: const EdgeInsets.only(top: 1), child: HopChonVuong(daChon: _dongY)),
                        const SizedBox(width: 9),
                        Expanded(
                          child: Text.rich(
                            TextSpan(children: [
                              const TextSpan(text: 'Tôi đồng ý với '),
                              _chuXanh('Điều khoản sử dụng'),
                              const TextSpan(text: ' và '),
                              _chuXanh('Chính sách bảo mật'),
                              const TextSpan(text: ' của PhotocopyChain.'),
                            ]),
                            style: AppTheme.font(size: 11.5, color: AppColors.chuNhan, height: 1.5),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          Container(
            padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
            decoration: const BoxDecoration(color: AppColors.trang, border: Border(top: BorderSide(color: AppColors.vach))),
            child: SafeArea(
              top: false,
              child: Column(
                children: [
                  NutChinh(nhan: 'Tiếp tục', iconSau: Ico.muiTenPhai, dangTai: _dangGui, onPressed: _tiepTuc),
                  SizedBox(
                    height: 52,
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Text('Đã có tài khoản?', style: AppTheme.font(size: 12.5, color: AppColors.chuPhu)),
                        const SizedBox(width: 5),
                        GestureDetector(
                          onTap: () => Navigator.pop(context), // quay về màn đăng nhập
                          child: Text('Đăng nhập',
                              style: AppTheme.font(size: 12.5, weight: FontWeight.w700, color: AppColors.accent)),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _goiY(String chu) {
    return Padding(
      padding: const EdgeInsets.only(left: 2),
      child: Text(chu, style: AppTheme.font(size: 10.5, color: AppColors.chuPhu)),
    );
  }

  TextSpan _chuXanh(String chu) {
    return TextSpan(text: chu, style: AppTheme.font(size: 11.5, weight: FontWeight.w700, color: AppColors.accent, height: 1.5));
  }
}
