import 'dart:async';

import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../models.dart';
import '../../realtime.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import '../don_hang/tien_do_screen.dart';
import '../trang_chinh_screen.dart';

// F-10 — Figma 02-Mobile-ThanhToan. Bước 4/4: hoàn tất đặt đơn.
//   - Đơn chuyển khoản VietQR: mở trang checkout chính thức của payOS, đếm ngược 15 phút.
//     Ngân hàng báo đã nhận tiền → máy chủ đẩy tin real-time xuống, màn này tự chuyển sang "Đã thanh toán".
//     Cứ 5 giây màn hình cũng tự hỏi lại máy chủ một lần cho chắc (chuyện tiền bạc, không để lỡ).
//   - Đơn tiền mặt: không phải làm gì, chỉ báo đặt đơn thành công.
class ThanhToanScreen extends StatefulWidget {
  const ThanhToanScreen({super.key, required this.maDonHang});

  final int maDonHang;

  @override
  State<ThanhToanScreen> createState() => _ThanhToanScreenState();
}

class _ThanhToanScreenState extends State<ThanhToanScreen> {
  ThanhToan? _tt; // null = đang tải
  LienKetPayOS? _lienKetPayOS;
  String? _loi;
  String? _loiPayOS;
  bool _dangTaiLink = false;
  bool _daThuTaiLink = false;

  Timer? _dongHo; // nhịp 1 giây: đếm ngược + cứ 5 nhịp thì hỏi lại máy chủ
  int _soNhip = 0;
  Duration _conLai = Duration.zero;

  @override
  void initState() {
    super.initState();
    _taiThanhToan();
    Realtime.dangKyNghe(_khiCoTin);
    _dongHo = Timer.periodic(const Duration(seconds: 1), _moiGiay);
  }

  @override
  void dispose() {
    _dongHo?.cancel();
    Realtime.boNghe(_khiCoTin);
    super.dispose();
  }

  // GET /api/thanh-toan/{maDonHang}
  Future<void> _taiThanhToan() async {
    KetQuaApi kq = await Api.get('/api/thanh-toan/${widget.maDonHang}');
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() => _loi = kq.loi);
      return;
    }
    ThanhToan thanhToanMoi = ThanhToan.fromJson(kq.duLieu);
    setState(() {
      _loi = null;
      _tt = thanhToanMoi;
      _tinhConLai();
    });

    if (thanhToanMoi.phuongThuc == 'VIETQR' &&
        !thanhToanMoi.daTra &&
        !_daThuTaiLink) {
      await _taiLienKetPayOS();
    }
  }

  // Máy chủ báo có thay đổi (ngân hàng đã nhận tiền, quầy đã tiếp nhận…) → tải lại
  void _khiCoTin(Map<String, dynamic> tin) {
    if (tin['maDonHang'] == widget.maDonHang) _taiThanhToan();
  }

  void _moiGiay(Timer t) {
    if (!mounted) return;
    setState(_tinhConLai);

    _soNhip++;
    // Còn chờ tiền thì 5 giây hỏi máy chủ 1 lần (phòng khi đường nối real-time bị rớt)
    if (_soNhip % 5 == 0 &&
        _tt != null &&
        _tt!.phuongThuc == 'VIETQR' &&
        !_tt!.daTra) {
      _taiThanhToan();
    }
  }

  void _tinhConLai() {
    if (_tt == null || _tt!.hanThanhToan == null) {
      _conLai = Duration.zero;
      return;
    }
    _conLai = _tt!.hanThanhToan!.toLocal().difference(DateTime.now());
    if (_conLai.isNegative) _conLai = Duration.zero;
  }

  // Tạo link lần đầu; nếu App mở lại đơn cũ thì API lấy lại link theo mã đơn.
  Future<void> _taiLienKetPayOS() async {
    if (_dangTaiLink) return;

    setState(() {
      _dangTaiLink = true;
      _daThuTaiLink = true;
      _loiPayOS = null;
    });

    KetQuaApi kq = await Api.post(
      '/api/thanh-toan/${widget.maDonHang}/payos',
      {},
    );
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() {
        _dangTaiLink = false;
        _loiPayOS = kq.loi;
      });
      return;
    }

    setState(() {
      _dangTaiLink = false;
      _lienKetPayOS = LienKetPayOS.fromJson(kq.duLieu);
    });
  }

  Future<void> _moTrangThanhToanPayOS() async {
    String? checkoutUrl = _lienKetPayOS?.checkoutUrl;
    if (checkoutUrl == null || checkoutUrl == '') return;

    bool daMo = await launchUrl(
      Uri.parse(checkoutUrl),
      mode: LaunchMode.externalApplication,
    );
    if (!daMo && mounted) {
      hienThongBaoNhanh(
        context,
        'Không mở được trang payOS, vui lòng thử lại.',
      );
    }
  }

  // Về trang chính (xóa hết các màn đặt in phía trước)
  void _veTrangChinh() {
    Navigator.pushAndRemoveUntil(
      context,
      MaterialPageRoute(
        builder: (context) => const TrangChinhScreen(tabBanDau: 'DON_HANG'),
      ),
      (route) => false,
    );
  }

  void _xemTienDo() {
    Navigator.pushAndRemoveUntil(
      context,
      MaterialPageRoute(
        builder: (context) => const TrangChinhScreen(tabBanDau: 'DON_HANG'),
      ),
      (route) => false,
    );
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (context) => TienDoScreen(maDonHang: widget.maDonHang),
      ),
    );
  }

  // ---------------------------------------------------------------- Giao diện

  @override
  Widget build(BuildContext context) {
    Widget noiDung;
    if (_tt == null && _loi == null) {
      noiDung = const Center(
        child: CircularProgressIndicator(color: AppColors.accent),
      );
    } else if (_loi != null) {
      noiDung = Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            HopThongBao(_loi!),
            const SizedBox(height: 14),
            NutPhu(nhan: 'Thử lại', onPressed: _taiThanhToan),
          ],
        ),
      );
      // Nhánh COD/TIEN_MAT để phòng khi mở màn này bằng đơn do quầy lập.
    } else if (_tt!.daTra || _laTraKhiNhan(_tt!.phuongThuc)) {
      noiDung = _manDaXong();
    } else {
      noiDung = _manChoChuyenKhoan();
    }

    // Chặn nút Back của điện thoại: đơn đã tạo rồi, quay lại màn chọn chi nhánh không còn ý nghĩa
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (daThoat, ketQua) {
        if (!daThoat) _veTrangChinh();
      },
      child: Scaffold(
        backgroundColor: AppColors.nen,
        appBar: const ThanhTieuDe(
          tieuDe: 'Thanh toán đơn hàng',
          phuDe: 'Bước 4 / 4 · Hoàn tất đặt đơn',
          soBuoc: 4,
          buocHienTai: 4,
          coNutQuayLai: false,
        ),
        body: noiDung,
      ),
    );
  }

  // ---- Màn chờ khách chuyển khoản ----
  Widget _manChoChuyenKhoan() {
    ThanhToan tt = _tt!;
    bool hetHan = _conLai == Duration.zero;

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _theMaDon(),
          const SizedBox(height: 12),

          // Đồng hồ đếm ngược
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 11),
            decoration: BoxDecoration(
              color: hetHan ? AppColors.loiNen : AppColors.cbNen,
              border: Border.all(
                color: hetHan ? AppColors.loiVien : AppColors.cbVien,
              ),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              children: [
                AppIcon(
                  Ico.dongHo,
                  size: 17,
                  color: hetHan ? AppColors.loiChu : AppColors.cbIcon,
                  net: 1.9,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    hetHan
                        ? 'Đã quá hạn chuyển khoản, đơn sẽ bị hủy trong ít phút.'
                        : 'Đơn tự động hủy nếu quá hạn mà chưa nhận được thanh toán',
                    style: AppTheme.font(
                      size: 11.5,
                      height: 1.4,
                      color: hetHan ? AppColors.loiChu : AppColors.cbChu,
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Text(
                  phutGiay(_conLai),
                  style: AppTheme.mono(
                    size: 18,
                    weight: FontWeight.w700,
                    color: hetHan ? AppColors.loiChu : AppColors.cbChu,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),

          // Thẻ mở trang thanh toán chính thức của payOS
          Container(
            padding: const EdgeInsets.fromLTRB(16, 14, 16, 16),
            decoration: BoxDecoration(
              color: AppColors.trang,
              border: Border.all(color: AppColors.vach),
              borderRadius: BorderRadius.circular(16),
            ),
            child: Column(
              children: [
                Text(
                  'Thanh toán an toàn qua payOS',
                  style: AppTheme.font(size: 13, weight: FontWeight.w700),
                ),
                const SizedBox(height: 3),
                Text(
                  'Trang thanh toán sẽ hiển thị mã VietQR đúng số tiền của đơn',
                  textAlign: TextAlign.center,
                  style: AppTheme.font(size: 10.5, color: AppColors.chuPhu),
                ),
                const SizedBox(height: 12),
                Text(
                  tien(tt.soTien),
                  style: AppTheme.mono(
                    size: 26,
                    weight: FontWeight.w800,
                    color: AppColors.accentDeep,
                    letterSpacing: -0.5,
                  ),
                ),
                const SizedBox(height: 14),
                if (_loiPayOS != null) ...[
                  HopThongBao(_loiPayOS!),
                  const SizedBox(height: 10),
                  NutPhu(
                    nhan: 'Thử tạo lại link thanh toán',
                    onPressed: hetHan ? null : _taiLienKetPayOS,
                  ),
                ] else
                  NutChinh(
                    nhan: 'Mở trang thanh toán payOS',
                    dangTai: _dangTaiLink,
                    iconSau: Ico.muiTenPhai,
                    onPressed: hetHan || _lienKetPayOS == null
                        ? null
                        : _moTrangThanhToanPayOS,
                  ),
              ],
            ),
          ),
          const SizedBox(height: 12),

          // Dòng "đang chờ ngân hàng"
          Row(
            children: [
              const SizedBox(
                width: 13,
                height: 13,
                child: CircularProgressIndicator(
                  strokeWidth: 1.7,
                  color: AppColors.accent,
                ),
              ),
              const SizedBox(width: 9),
              Expanded(
                child: Text(
                  'Đang chờ xác nhận tự động từ ngân hàng…',
                  style: AppTheme.font(
                    size: 12,
                    weight: FontWeight.w600,
                    color: AppColors.accent,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          KhungNetDut(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
              child: Text(
                'Không tự sửa số tiền hoặc nội dung trên trang payOS. '
                'Khi ngân hàng ghi nhận giao dịch, màn hình này sẽ tự chuyển sang trạng thái đã thanh toán.',
                style: AppTheme.font(
                  size: 11,
                  color: AppColors.chuPhu,
                  height: 1.5,
                ),
              ),
            ),
          ),
          const SizedBox(height: 14),
          NutPhu(nhan: 'Để sau, về trang chính', onPressed: _veTrangChinh),
        ],
      ),
    );
  }

  // ---- Màn đặt đơn xong (đã chuyển khoản xong, hoặc là đơn tiền mặt của khách tới tận tiệm) ----
  Widget _manDaXong() {
    ThanhToan tt = _tt!;
    bool laTienMat = _laTraKhiNhan(tt.phuongThuc);

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(16, 30, 16, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Center(
            child: Container(
              width: 74,
              height: 74,
              decoration: const BoxDecoration(
                color: AppColors.xanhNhat,
                shape: BoxShape.circle,
              ),
              child: const Center(
                child: AppIcon(
                  Ico.dauTich,
                  size: 34,
                  color: AppColors.thanhCong,
                  net: 3,
                ),
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            laTienMat ? 'Đặt đơn thành công' : 'Thanh toán thành công',
            textAlign: TextAlign.center,
            style: AppTheme.font(size: 19, weight: FontWeight.w800),
          ),
          const SizedBox(height: 6),
          Text(
            laTienMat
                ? 'Đơn đã vào hàng đợi in của chi nhánh. Bạn trả ${tien(tt.soTien)} khi nhận tài liệu.'
                : 'Chi nhánh đã nhận được ${tien(tt.soTien)} và bắt đầu xử lý đơn của bạn.',
            textAlign: TextAlign.center,
            style: AppTheme.font(
              size: 12.5,
              color: AppColors.chuPhu,
              height: 1.5,
            ),
          ),
          const SizedBox(height: 18),
          _theMaDon(),
          const SizedBox(height: 18),
          NutChinh(
            nhan: 'Theo dõi tiến độ đơn',
            iconSau: Ico.muiTenPhai,
            onPressed: _xemTienDo,
          ),
          const SizedBox(height: 10),
          NutPhu(nhan: 'Về trang chính', onPressed: _veTrangChinh),
        ],
      ),
    );
  }

  // Thẻ mã đơn + trạng thái
  Widget _theMaDon() {
    ThanhToan tt = _tt!;
    return Container(
      padding: const EdgeInsets.fromLTRB(13, 11, 13, 11),
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  tt.maDonHangHienThi,
                  style: AppTheme.mono(size: 15, weight: FontWeight.w700),
                ),
                const SizedBox(height: 3),
                Text(
                  tt.phuongThuc == 'VIETQR'
                      ? 'Chuyển khoản VietQR'
                      : 'Thanh toán tiền mặt',
                  style: AppTheme.font(size: 11.5, color: AppColors.chuPhu),
                ),
              ],
            ),
          ),
          NhanNho(
            tt.daTra ? 'Đã thanh toán' : 'Chưa thanh toán',
            mau: tt.daTra ? AppColors.thanhCong : AppColors.cbChu,
            nen: tt.daTra ? AppColors.xanhNhat : AppColors.cbNen,
          ),
        ],
      ),
    );
  }

  bool _laTraKhiNhan(String phuongThuc) {
    return phuongThuc == 'COD' || phuongThuc == 'TIEN_MAT';
  }
}
