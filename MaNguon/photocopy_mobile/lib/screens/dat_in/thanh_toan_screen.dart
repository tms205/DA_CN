import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:qr_flutter/qr_flutter.dart';
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
//   - Đơn VietQR: hiển thị QR payOS trong App, đếm ngược hạn thanh toán.
//     Ngân hàng báo đã nhận tiền → máy chủ đẩy tin real-time xuống, màn này tự chuyển sang "Đã thanh toán".
//     Cứ 5 giây màn hình cũng tự hỏi lại máy chủ một lần cho chắc (chuyện tiền bạc, không để lỡ).
//   - Đơn tiền mặt: không phải làm gì, chỉ báo đặt đơn thành công.
class ThanhToanScreen extends StatefulWidget {
  const ThanhToanScreen({super.key, required this.maDonHang});

  final int maDonHang;

  @override
  State<ThanhToanScreen> createState() => _ThanhToanScreenState();
}

class _ThanhToanScreenState extends State<ThanhToanScreen>
    with WidgetsBindingObserver {
  ThanhToan? _tt; // null = đang tải
  LienKetPayOS? _lienKetPayOS;
  QrValidationResult? _qrDaVe;
  String? _loi;
  String? _loiPayOS;
  bool _dangTaiLink = false;
  bool _daThuTaiLink = false;
  bool _dangKiemTra = false;
  bool _kiemTraLai = false;
  bool _dangHienThi = true;

  Timer? _dongHo; // nhịp 1 giây: đếm ngược + cứ 5 nhịp thì hỏi lại máy chủ
  int _soNhip = 0;
  Duration _conLai = Duration.zero;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _taiThanhToan();
    Realtime.dangKyNghe(_khiCoTin);
    _dongHo = Timer.periodic(const Duration(seconds: 1), _moiGiay);
  }

  @override
  void dispose() {
    _dongHo?.cancel();
    WidgetsBinding.instance.removeObserver(this);
    Realtime.boNghe(_khiCoTin);
    super.dispose();
  }

  // GET /api/thanh-toan/{maDonHang}
  Future<void> _taiThanhToan({bool kiemTraNgay = false}) async {
    if (_tt?.daTra == true) return;
    if (_dangKiemTra) {
      if (kiemTraNgay) _kiemTraLai = true;
      return;
    }
    _dangKiemTra = true;
    KetQuaApi kq = await Api.get('/api/thanh-toan/${widget.maDonHang}');
    _dangKiemTra = false;
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() => _loi = kq.loi);
      _thuKiemTraLai();
      return;
    }
    ThanhToan thanhToanMoi = ThanhToan.fromJson(kq.duLieu);
    setState(() {
      _loi = null;
      _tt = thanhToanMoi;
      _tinhConLai();
    });
    if (_thuKiemTraLai()) return;

    if (thanhToanMoi.phuongThuc == 'VIETQR' &&
        !thanhToanMoi.daTra &&
        _duocThanhToan &&
        !_daThuTaiLink) {
      await _taiLienKetPayOS();
    }
  }

  // Nếu resume trùng lúc request cũ còn chờ thì đối soát tiếp khi nó kết thúc.
  bool _thuKiemTraLai() {
    if (!_kiemTraLai) return false;
    _kiemTraLai = false;
    if (!_dangHienThi || _tt?.daTra == true) return false;
    _taiThanhToan();
    return true;
  }

  // Quay lại từ app ngân hàng thì đối soát ngay, không chờ nhịp 5 giây.
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    _dangHienThi = state == AppLifecycleState.resumed;
    if (_dangHienThi) {
      setState(_tinhConLai);
      _taiThanhToan(kiemTraNgay: true);
    }
  }

  bool get _duocThanhToan =>
      _tt != null &&
      !_tt!.daTra &&
      _tt!.trangThaiDonHang != 'DA_HUY' &&
      _conLai > Duration.zero &&
      (_lienKetPayOS?.hanThanhToan == null ||
          _lienKetPayOS!.hanThanhToan!.isAfter(DateTime.now()));

  // Máy chủ báo có thay đổi (ngân hàng đã nhận tiền, quầy đã tiếp nhận…) → tải lại
  void _khiCoTin(Map<String, dynamic> tin) {
    if (_dangHienThi && tin['maDonHang'] == widget.maDonHang) _taiThanhToan();
  }

  void _moiGiay(Timer t) {
    if (!mounted || !_dangHienThi || _tt?.daTra == true) return;
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
    if (_dangTaiLink || !_duocThanhToan) return;

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

    LienKetPayOS link = LienKetPayOS.fromJson(kq.duLieu);
    final qr = link.qrCode.isEmpty
        ? null
        : QrValidator.validate(
            data: link.qrCode,
            errorCorrectionLevel: QrErrorCorrectLevel.M,
          );
    if (link.qrCode.isNotEmpty &&
        (link.maDonHang != widget.maDonHang ||
            link.soTien != _tt!.soTien ||
            qr?.isValid != true)) {
      setState(() {
        _dangTaiLink = false;
        _lienKetPayOS = null;
        _qrDaVe = null;
        _loiPayOS = 'Dữ liệu QR không khớp đơn hàng. Vui lòng thử lại.';
      });
      return;
    }
    setState(() {
      _dangTaiLink = false;
      _lienKetPayOS = link;
      _qrDaVe = qr;
    });
  }

  Future<void> _moTrangThanhToanPayOS() async {
    String? checkoutUrl = _lienKetPayOS?.checkoutUrl;
    if (!_duocThanhToan || checkoutUrl == null || checkoutUrl == '') return;
    Uri? url = Uri.tryParse(checkoutUrl);
    if (url == null || url.scheme != 'https' || url.host != 'pay.payos.vn') {
      hienThongBaoNhanh(context, 'Link thanh toán không hợp lệ.');
      return;
    }

    bool daMo = await launchUrl(url, mode: LaunchMode.externalApplication);
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
    } else if (_tt == null && _loi != null) {
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
    bool hetHan = !_duocThanhToan;

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
                        ? (tt.trangThaiDonHang == 'DA_HUY'
                              ? 'Đơn đã bị hủy. Không chuyển tiền cho đơn này.'
                              : 'Đã quá hạn. Không chuyển tiền bằng QR này.')
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

          _theQr(),
          const SizedBox(height: 12),
          if (_loi != null) ...[
            HopThongBao('Chưa kiểm tra được thanh toán. $_loi'),
            const SizedBox(height: 12),
          ],
          NutPhu(nhan: 'Kiểm tra thanh toán', onPressed: _taiThanhToan),
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
                  hetHan
                      ? 'Có thể kiểm tra lại nếu bạn đã chuyển tiền trước đó.'
                      : 'Đang chờ xác nhận tự động từ ngân hàng…',
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
                'Chụp màn hình QR, mở app ngân hàng và chọn quét QR từ ảnh. '
                'Kiểm tra người nhận, giữ nguyên số tiền và nội dung chuyển khoản. '
                'Sau khi chuyển tiền, quay lại đây để nhận kết quả từ máy chủ.',
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

  Widget _theQr() {
    LienKetPayOS? link = _lienKetPayOS;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Chuyển khoản VietQR',
            textAlign: TextAlign.center,
            style: AppTheme.font(size: 16, weight: FontWeight.w700),
          ),
          const SizedBox(height: 6),
          Text(
            tien(_tt!.soTien),
            textAlign: TextAlign.center,
            style: AppTheme.mono(
              size: 26,
              weight: FontWeight.w800,
              color: AppColors.accentDeep,
            ),
          ),
          const SizedBox(height: 14),
          if (_duocThanhToan) ...[
            if (_dangTaiLink)
              const Center(child: CircularProgressIndicator())
            else if (_loiPayOS != null) ...[
              HopThongBao(_loiPayOS!),
              const SizedBox(height: 10),
              NutPhu(
                nhan: 'Thử lại QR thanh toán',
                onPressed: _taiLienKetPayOS,
              ),
            ] else if (link != null && link.qrCode.isNotEmpty) ...[
              LayoutBuilder(
                builder: (context, kichThuoc) {
                  double rong = kichThuoc.maxWidth.clamp(0, 260).toDouble();
                  return Center(
                    child: Semantics(
                      label:
                          'Mã VietQR thanh toán đơn ${_tt!.maDonHangHienThi}',
                      image: true,
                      child: QrImageView.withQr(
                        key: const ValueKey('payos-qr'),
                        qr: _qrDaVe!.qrCode!,
                        size: rong,
                        // Vùng trắng đủ 4 ô QR ở mỗi cạnh, kể cả khi co màn hình.
                        padding: EdgeInsets.all(
                          rong * 4 / (_qrDaVe!.qrCode!.moduleCount + 8),
                        ),
                        backgroundColor: Colors.white,
                        errorCorrectionLevel: QrErrorCorrectLevel.M,
                      ),
                    ),
                  );
                },
              ),
              const SizedBox(height: 10),
              _dongNhanTien('Người nhận', link.chuTaiKhoan),
              _dongNhanTien('Mã ngân hàng (BIN)', link.maNganHang),
              _dongNhanTien('Số tài khoản', link.soTaiKhoan, saoChep: true),
              _dongNhanTien('Nội dung', link.noiDungChuyenKhoan, saoChep: true),
            ] else if (link != null) ...[
              const HopThongBao(
                'Link cũ chưa có dữ liệu QR trong app.',
                loai: LoaiThongBao.canhBao,
              ),
              const SizedBox(height: 10),
              NutChinh(
                nhan: 'Mở trang thanh toán payOS',
                onPressed: _moTrangThanhToanPayOS,
              ),
            ],
          ] else
            Text(
              'QR không còn hiệu lực. Vui lòng không chuyển tiền.',
              textAlign: TextAlign.center,
              style: AppTheme.font(size: 14, color: AppColors.loiChu),
            ),
        ],
      ),
    );
  }

  Widget _dongNhanTien(String nhan, String giaTri, {bool saoChep = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  nhan,
                  style: AppTheme.font(size: 12, color: AppColors.chuPhu),
                ),
                const SizedBox(height: 3),
                Text(
                  giaTri,
                  style: AppTheme.font(size: 14, weight: FontWeight.w600),
                ),
              ],
            ),
          ),
          if (saoChep)
            IconButton(
              constraints: const BoxConstraints(minWidth: 48, minHeight: 48),
              tooltip: 'Sao chép ${nhan.toLowerCase()}',
              icon: const Icon(Icons.copy_outlined, size: 20),
              onPressed: () async {
                await Clipboard.setData(ClipboardData(text: giaTri));
                if (mounted) {
                  hienThongBaoNhanh(
                    context,
                    'Đã sao chép ${nhan.toLowerCase()}.',
                  );
                }
              },
            ),
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
