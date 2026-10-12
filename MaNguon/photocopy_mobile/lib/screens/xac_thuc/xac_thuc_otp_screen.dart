import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../phien.dart';
import '../../realtime.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import '../trang_chinh_screen.dart';

// Figma 08-Mobile-XacThucOTP. Dùng cho 2 trường hợp:
//  - laDangKy = true : đăng ký bước 2/2 — nhập đúng mã thì API mới tạo tài khoản, đăng nhập luôn
//  - laDangKy = false: quên mật khẩu bước 2/2 — nhập mã + mật khẩu mới
class XacThucOtpScreen extends StatefulWidget {
  const XacThucOtpScreen({
    super.key,
    required this.laDangKy,
    required this.email,
    required this.hetHanSauGiay,
    required this.guiLaiSauGiay,
    this.thongTinDangKy,
  });

  final bool laDangKy;
  final String email;
  final int hetHanSauGiay;                       // mã hết hạn sau bao nhiêu giây (API trả về)
  final int guiLaiSauGiay;                       // bao nhiêu giây nữa mới được gửi lại mã
  final Map<String, dynamic>? thongTinDangKy;    // chỉ có khi đăng ký — dùng để gửi lại mã

  @override
  State<XacThucOtpScreen> createState() => _XacThucOtpScreenState();
}

class _XacThucOtpScreenState extends State<XacThucOtpScreen> {
  final TextEditingController _maOtp = TextEditingController();
  final TextEditingController _matKhauMoi = TextEditingController();
  final FocusNode _focusOtp = FocusNode();
  DateTime _hetHanLuc = DateTime.now();
  DateTime _guiLaiLuc = DateTime.now();
  Timer? _dongHo;
  bool _dangGui = false;
  String? _loi;

  @override
  void initState() {
    super.initState();
    _datMocThoiGian(widget.hetHanSauGiay, widget.guiLaiSauGiay);
    // Mỗi giây vẽ lại 1 lần để đồng hồ đếm ngược chạy
    _dongHo = Timer.periodic(const Duration(seconds: 1), (timer) => setState(() {}));
    _focusOtp.addListener(() => setState(() {}));
    // Mở màn xong thì bật bàn phím số ngay
    WidgetsBinding.instance.addPostFrameCallback((thoiDiem) => _focusOtp.requestFocus());
  }

  void _datMocThoiGian(int hetHanSauGiay, int guiLaiSauGiay) {
    DateTime bayGio = DateTime.now();
    _hetHanLuc = bayGio.add(Duration(seconds: hetHanSauGiay));
    _guiLaiLuc = bayGio.add(Duration(seconds: guiLaiSauGiay));
  }

  @override
  void dispose() {
    _dongHo?.cancel();
    _maOtp.dispose();
    _matKhauMoi.dispose();
    _focusOtp.dispose();
    super.dispose();
  }

  Future<void> _xacNhan() async {
    String ma = _maOtp.text;
    if (ma.length != 6) {
      setState(() => _loi = 'Vui lòng nhập đủ 6 chữ số.');
      return;
    }
    if (!widget.laDangKy && !RegExp(r'^(?=.*[A-Z])(?=.*\d).{8,}$').hasMatch(_matKhauMoi.text)) {
      setState(() => _loi = 'Mật khẩu mới tối thiểu 8 ký tự, có chữ hoa và số.');
      return;
    }
    FocusScope.of(context).unfocus();
    setState(() {
      _dangGui = true;
      _loi = null;
    });

    if (widget.laDangKy) {
      // Đăng ký: đúng mã thì API tạo tài khoản và trả luôn phiên đăng nhập
      KetQuaApi kq = await Api.post('/api/auth/register', {'email': widget.email, 'maOtp': ma});
      if (!mounted) return;
      if (!kq.thanhCong) {
        _baoLoi(kq.loi);
        return;
      }
      await Phien.batDau(kq.duLieu, true);
      await Realtime.batDau();   // F-32: mở kênh nhận cập nhật real-time
      if (!mounted) return;
      Navigator.pushAndRemoveUntil(
        context,
        MaterialPageRoute(builder: (context) => const TrangChinhScreen()),
        (route) => false,
      );
    } else {
      // Quên mật khẩu: đúng mã thì đổi sang mật khẩu mới rồi quay về màn đăng nhập
      KetQuaApi kq = await Api.post('/api/auth/reset-password', {
        'email': widget.email,
        'maOtp': ma,
        'matKhauMoi': _matKhauMoi.text,
      });
      if (!mounted) return;
      if (!kq.thanhCong) {
        _baoLoi(kq.loi);
        return;
      }
      hienThongBaoNhanh(context, kq.duLieu['message']);
      Navigator.popUntil(context, (route) => route.isFirst); // về màn đầu tiên = màn đăng nhập
    }
  }

  // Sai mã: hiện lỗi, xóa 6 ô để nhập lại
  void _baoLoi(String loi) {
    setState(() {
      _loi = loi;
      _dangGui = false;
      _maOtp.clear();
    });
    _focusOtp.requestFocus();
  }

  Future<void> _guiLai() async {
    setState(() {
      _dangGui = true;
      _loi = null;
    });

    KetQuaApi kq;
    if (widget.laDangKy) {
      kq = await Api.post('/api/auth/register/otp', widget.thongTinDangKy!);
    } else {
      kq = await Api.post('/api/auth/forgot-password', {'email': widget.email});
    }
    if (!mounted) return;

    setState(() => _dangGui = false);
    if (!kq.thanhCong) {
      setState(() => _loi = kq.loi);
      return;
    }
    setState(() {
      _datMocThoiGian(kq.duLieu['hetHanSauGiay'], kq.duLieu['guiLaiSauGiay']);
      _maOtp.clear();
    });
    hienThongBaoNhanh(context, kq.duLieu['message']);
  }

  @override
  Widget build(BuildContext context) {
    Duration conLai = _hetHanLuc.difference(DateTime.now());
    Duration choGuiLai = _guiLaiLuc.difference(DateTime.now());
    bool daHetHan = conLai.isNegative;

    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: ThanhTieuDe(
        tieuDe: widget.laDangKy ? 'Xác thực OTP' : 'Đặt lại mật khẩu',
        phuDe: widget.laDangKy ? 'Bước 2 / 2 · Kích hoạt tài khoản' : 'Bước 2 / 2 · Xác thực email',
        soBuoc: 2,
        buocHienTai: 2,
      ),
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
                child: const Center(child: AppIcon(Ico.thu, size: 28, color: AppColors.accent, net: 1.6)),
              ),
            ),
            const SizedBox(height: 14),
            Text('Nhập mã xác thực',
                textAlign: TextAlign.center, style: AppTheme.font(size: 19, weight: FontWeight.w800, letterSpacing: -0.38)),
            const SizedBox(height: 6),
            Text.rich(
              TextSpan(children: [
                const TextSpan(text: 'Mã gồm 6 chữ số đã được gửi tới\n'),
                TextSpan(text: widget.email, style: AppTheme.font(size: 12.5, weight: FontWeight.w700, height: 1.55)),
              ]),
              textAlign: TextAlign.center,
              style: AppTheme.font(size: 12.5, color: AppColors.chuPhu, height: 1.55),
            ),
            const SizedBox(height: 20),
            _oNhapOtp(),
            if (!widget.laDangKy) ...[
              const SizedBox(height: 16),
              const NhanTruong('Mật khẩu mới'),
              const SizedBox(height: 6),
              ONhap(
                controller: _matKhauMoi,
                icon: Ico.khoa,
                hint: '8+ ký tự, có chữ hoa và số',
                anChu: true,
                autofillHints: const [AutofillHints.newPassword],
                onChanged: (chu) => setState(() => _loi = null),
              ),
            ],
            const SizedBox(height: 20),
            if (_loi != null) ...[HopThongBao(_loi!), const SizedBox(height: 14)],
            // Hộp đồng hồ đếm ngược
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: BoxDecoration(
                color: daHetHan ? AppColors.loiNen : AppColors.cbNen,
                border: Border.all(color: daHetHan ? AppColors.loiVien : AppColors.cbVien),
                borderRadius: BorderRadius.circular(11),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  AppIcon(Ico.dongHo, size: 15, color: daHetHan ? AppColors.loiChu : AppColors.cbIcon, net: 2),
                  const SizedBox(width: 7),
                  if (daHetHan)
                    Flexible(
                      child: Text('Mã đã hết hiệu lực — bấm Gửi lại mã',
                          textAlign: TextAlign.center,
                          style: AppTheme.font(size: 12, weight: FontWeight.w600, color: AppColors.loiChu)),
                    )
                  else ...[
                    Flexible(child: Text('Mã hết hiệu lực sau', style: AppTheme.font(size: 12, color: AppColors.cbChu))),
                    const SizedBox(width: 7),
                    Text(phutGiay(conLai),
                        style: AppTheme.mono(size: 14, weight: FontWeight.w700, color: AppColors.cbIcon, letterSpacing: 0.28)),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 20),
            // Wrap: máy hẹp / cỡ chữ lớn thì tự xuống dòng thay vì tràn
            Wrap(
              alignment: WrapAlignment.center,
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 5,
              runSpacing: 4,
              children: [
                Text('Không nhận được mã?', style: AppTheme.font(size: 12.5, color: AppColors.chuPhu)),
                if (choGuiLai.isNegative)
                  GestureDetector(
                    onTap: _dangGui ? null : _guiLai,
                    child: Text('Gửi lại mã', style: AppTheme.font(size: 12.5, weight: FontWeight.w700, color: AppColors.accent)),
                  )
                else
                  Text('Gửi lại sau ${phutGiay(choGuiLai)}',
                      style: AppTheme.font(size: 12.5, weight: FontWeight.w600, color: AppColors.iconMo)),
              ],
            ),
            const SizedBox(height: 20),
            NutChinh(
              nhan: widget.laDangKy ? 'Xác nhận & hoàn tất' : 'Đặt lại mật khẩu',
              iconTruoc: Ico.dauTich,
              dangTai: _dangGui,
              onPressed: _xacNhan,
            ),
            if (widget.laDangKy) ...[
              const SizedBox(height: 20),
              Container(
                padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
                decoration: BoxDecoration(
                  color: AppColors.trang,
                  border: Border.all(color: AppColors.vach),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('SAU KHI XÁC THỰC THÀNH CÔNG',
                        style: AppTheme.font(size: 11, weight: FontWeight.w700, color: AppColors.chuNhan, letterSpacing: 0.55)),
                    const SizedBox(height: 9),
                    _gachDauDong(const TextSpan(text: 'Mật khẩu được mã hóa và lưu vào CSDL')),
                    _gachDauDong(TextSpan(children: [
                      const TextSpan(text: 'Tài khoản được cấp vai trò '),
                      TextSpan(text: 'CUSTOMER', style: AppTheme.mono(size: 11.5, weight: FontWeight.w700, color: AppColors.chuNhan)),
                    ])),
                    _gachDauDong(const TextSpan(text: 'Chuyển thẳng vào màn hình đặt in')),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              Container(
                padding: const EdgeInsets.fromLTRB(12, 11, 12, 11),
                decoration: BoxDecoration(
                  color: AppColors.loiNen,
                  border: Border.all(color: AppColors.loiVien),
                  borderRadius: BorderRadius.circular(11),
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Padding(
                      padding: EdgeInsets.only(top: 1),
                      child: AppIcon(Ico.canhBao, size: 14, color: AppColors.loiChu, net: 2),
                    ),
                    const SizedBox(width: 9),
                    Expanded(
                      child: Text.rich(
                        TextSpan(children: [
                          const TextSpan(text: 'Nhập sai quá '),
                          TextSpan(text: '5 lần', style: AppTheme.font(size: 10.5, weight: FontWeight.w700, color: AppColors.loiChuNhat)),
                          const TextSpan(text: ' sẽ khóa tạm chức năng đăng ký trên thiết bị này trong 15 phút.'),
                        ]),
                        style: AppTheme.font(size: 10.5, color: AppColors.loiChuNhat, height: 1.5),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  // 6 ô nhập mã (Figma: ô 48×58). Cách làm: 1 ô TextField trong suốt nằm đè lên để nhận bàn phím,
  // còn 6 ô bên dưới chỉ để hiển thị từng chữ số. Màn hẹp thì ô tự co lại cho khỏi tràn.
  Widget _oNhapOtp() {
    return LayoutBuilder(builder: (context, khungChoPhep) {
      double khoangCach = 8;
      double rongO = min(48.0, (khungChoPhep.maxWidth - khoangCach * 5) / 6);
      double caoO = rongO * 58 / 48;
      String ma = _maOtp.text;

      List<Widget> dsO = [];
      for (int i = 0; i < 6; i++) {
        if (i > 0) dsO.add(SizedBox(width: khoangCach));
        dsO.add(_oSo(
          i < ma.length ? ma[i] : null,
          rong: rongO,
          cao: caoO,
          dangNhap: i == ma.length && _focusOtp.hasFocus,  // ô đang chờ gõ
          conTrong: i > ma.length,
        ));
      }

      return SizedBox(
        height: caoO,
        child: Stack(
          children: [
            Positioned.fill(
              child: Opacity(
                opacity: 0,
                child: TextField(
                  controller: _maOtp,
                  focusNode: _focusOtp,
                  keyboardType: TextInputType.number,
                  autofillHints: const [AutofillHints.oneTimeCode],
                  inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(6)],
                  showCursor: false,
                  enableInteractiveSelection: false,
                  onChanged: (chu) {
                    setState(() => _loi = null);
                    // Đăng ký: gõ đủ 6 số là tự gửi luôn
                    if (chu.length == 6 && widget.laDangKy) _xacNhan();
                  },
                ),
              ),
            ),
            IgnorePointer(
              child: Row(mainAxisAlignment: MainAxisAlignment.center, children: dsO),
            ),
          ],
        ),
      );
    });
  }

  Widget _oSo(String? so, {required double rong, required double cao, required bool dangNhap, required bool conTrong}) {
    Widget? noiDung;
    if (so != null) {
      noiDung = Text(so, style: AppTheme.mono(size: 22, weight: FontWeight.w700));
    } else if (dangNhap) {
      noiDung = Container(width: 1.5, height: 24, color: AppColors.accent); // con trỏ nhấp nháy
    }

    return Container(
      width: rong,
      height: cao,
      decoration: BoxDecoration(
        color: conTrong ? AppColors.nenOTrong : AppColors.trang,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: dangNhap ? AppColors.accent : (conTrong ? AppColors.vach : AppColors.vien),
          width: dangNhap ? 1.5 : 1,
        ),
        boxShadow: dangNhap ? const [BoxShadow(color: AppColors.accentSoft, spreadRadius: 3)] : null,
      ),
      child: Center(child: noiDung),
    );
  }

  Widget _gachDauDong(InlineSpan noiDung) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 5,
            height: 5,
            margin: const EdgeInsets.only(top: 6, right: 8),
            decoration: const BoxDecoration(color: AppColors.accent, shape: BoxShape.circle),
          ),
          Expanded(child: Text.rich(noiDung, style: AppTheme.font(size: 11.5, color: AppColors.chuNhan, height: 1.5))),
        ],
      ),
    );
  }
}
