import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api.dart';
import '../phien.dart';
import '../realtime.dart';
import 'xac_thuc/dang_nhap_screen.dart';
import '../theme.dart';
import '../widgets/icons.dart';
import 'dat_in/chon_file_screen.dart';
import 'don_hang/don_hang_screen.dart';
import 'shipper/shipper_screen.dart';
import 'thong_bao/thong_bao_screen.dart';
import 'toi/toi_screen.dart';
import 'danh_gia/danh_gia_screen.dart';

// Trang chính sau khi đăng nhập: thanh tab dưới đáy, mỗi vai trò thấy các tab của mình
//  - Khách hàng: Đặt in · Đơn hàng · Đánh giá · Thông báo · Tôi
//  - Shipper   : Đơn giao · Thông báo · Tôi
class TrangChinhScreen extends StatefulWidget {
  const TrangChinhScreen({super.key, this.tabBanDau});

  /// Màn con có thể yêu cầu mở sẵn một tab, ví dụ rời QR thì mở tab Đơn hàng.
  final String? tabBanDau;

  @override
  State<TrangChinhScreen> createState() => _TrangChinhScreenState();
}

class _TrangChinhScreenState extends State<TrangChinhScreen> {
  // Tab đang mở: DAT_IN / DON_HANG / DON_GIAO / THONG_BAO / TOI
  late String _tab;
  String get _tabDauTien => Phien.laShipper ? 'DON_GIAO' : 'DAT_IN';
  bool _choThoat = false;
  Timer? _hanThoat;
  ScaffoldFeatureController<SnackBar, SnackBarClosedReason>? _nhacThoat;

  void _huyChoThoat() {
    _choThoat = false;
    _hanThoat?.cancel();
    final nhac = _nhacThoat;
    _nhacThoat = null;
    // Có thể chạy khi Navigator đang dựng màn con; đóng snackbar sau frame đó.
    if (nhac != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) nhac.close();
      });
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (ModalRoute.of(context)?.isCurrent == false) _huyChoThoat();
  }

  void _quayLai(bool daPop, Object? ketQua) {
    if (daPop || ModalRoute.of(context)?.isCurrent != true) return;
    if (_tab != _tabDauTien) {
      _chonTab(_tabDauTien);
      return;
    }
    if (_choThoat) {
      _huyChoThoat();
      SystemNavigator.pop();
      return;
    }
    _choThoat = true;
    _hanThoat = Timer(const Duration(seconds: 2), _huyChoThoat);
    _nhacThoat = ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Quay lại lần nữa để thoát ứng dụng'),
        duration: Duration(seconds: 2),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  // Bộ lọc mở sẵn khi chuyển sang tab Đơn hàng (màn Tôi bấm "Hoàn tất" → mở Đơn hàng lọc Hoàn tất)
  String _locDonHang = 'TAT_CA';

  // Số thông báo chưa đọc — hiện chấm đỏ trên tab Thông báo
  int _soChuaDoc = 0;
  Timer? _kiemTraBaoTriTimer;
  bool _dangKiemTraBaoTri = false;

  @override
  void initState() {
    super.initState();
    _tab = widget.tabBanDau ?? _tabDauTien;
    _demThongBaoChuaDoc();
    _kiemTraBaoTri();
    _kiemTraBaoTriTimer = Timer.periodic(
      const Duration(seconds: 5),
      (_) => _kiemTraBaoTri(),
    );
  }

  @override
  void dispose() {
    _kiemTraBaoTriTimer?.cancel();
    _hanThoat?.cancel();
    super.dispose();
  }

  Future<void> _kiemTraBaoTri() async {
    if (_dangKiemTraBaoTri || Phien.token == null) return;
    _dangKiemTraBaoTri = true;
    try {
      final status = await Api.get('/api/bao-tri/trang-thai?nenTang=app');
      var invalid = status.thanhCong && status.duLieu['dangBaoTri'] == true;
      if (!invalid) {
        final session = await Api.get('/api/bao-tri/phien');
        invalid = session.maHttp == 401;
      }
      if (!mounted) return;
      if (invalid) {
        _kiemTraBaoTriTimer?.cancel();
        await Realtime.dung();
        await Phien.dangXuat();
        if (mounted) {
          Navigator.pushAndRemoveUntil(
            context,
            MaterialPageRoute(builder: (_) => const DangNhapScreen()),
            (_) => false,
          );
        }
      }
    } finally {
      _dangKiemTraBaoTri = false;
    }
  }

  Future<void> _demThongBaoChuaDoc() async {
    KetQuaApi kq = await Api.get('/api/thong-bao?soLuong=1');
    if (kq.thanhCong && mounted) {
      setState(() => _soChuaDoc = kq.duLieu['soChuaDoc']);
    }
  }

  void _chonTab(String tab) {
    _huyChoThoat();
    setState(() {
      _tab = tab;
      _locDonHang = 'TAT_CA';
    });
    _demThongBaoChuaDoc();
  }

  // Mở tab Đơn hàng với bộ lọc cho trước
  void _moDonHang(String nhom) {
    _huyChoThoat();
    setState(() {
      _tab = 'DON_HANG';
      _locDonHang = nhom;
    });
  }

  // Màn hình của tab đang chọn. Mỗi lần đổi tab, màn mới được tạo lại → tự tải dữ liệu mới nhất.
  Widget _manHinhCuaTab() {
    if (_tab == 'DAT_IN') return const ChonFileScreen();
    if (_tab == 'DON_HANG') {
      return DonHangScreen(
        nhomBanDau: _locDonHang,
        moDatIn: () => _chonTab('DAT_IN'),
      );
    }
    if (_tab == 'DON_GIAO') return const ShipperScreen();
    if (_tab == 'DANH_GIA' && !Phien.laShipper) return const DanhGiaScreen();
    if (_tab == 'THONG_BAO') {
      return ThongBaoScreen(
        moDonHang: () {
          if (Phien.laShipper) {
            _chonTab('DON_GIAO');
          } else {
            _moDonHang('TAT_CA');
          }
        },
        khiDocThongBao: _demThongBaoChuaDoc,
      );
    }
    return ToiScreen(moDonHang: _moDonHang);
  }

  @override
  Widget build(BuildContext context) {
    List<Widget> dsTab = [];
    if (Phien.laShipper) {
      dsTab.add(_nutTab('DON_GIAO', 'Đơn giao', Ico.xeGiao));
    } else {
      dsTab.add(_nutTab('DAT_IN', 'Đặt in', Ico.mayIn));
      dsTab.add(_nutTab('DON_HANG', 'Đơn hàng', Ico.donHang));
      dsTab.add(_nutTab('DANH_GIA', 'Đánh giá', Ico.sao));
    }
    dsTab.add(_nutTab('THONG_BAO', 'Thông báo', Ico.chuong));
    dsTab.add(_nutTab('TOI', 'Tôi', Ico.nguoi));

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: _quayLai,
      child: Scaffold(
        body: _manHinhCuaTab(),
        bottomNavigationBar: Container(
          decoration: const BoxDecoration(
            color: AppColors.trang,
            border: Border(top: BorderSide(color: AppColors.vach)),
          ),
          child: SafeArea(
            top: false,
            child: SizedBox(height: 60, child: Row(children: dsTab)),
          ),
        ),
      ),
    );
  }

  // Một nút trên thanh tab: icon + chữ, tab đang chọn có nền xanh nhạt; tab Thông báo có số chưa đọc
  Widget _nutTab(String tab, String nhan, String icon) {
    bool dangChon = _tab == tab;
    Color mau = dangChon ? AppColors.accent : AppColors.iconMo;
    int soHuyHieu = tab == 'THONG_BAO' ? _soChuaDoc : 0;

    return Expanded(
      child: InkResponse(
        onTap: () => _chonTab(tab),
        radius: 40,
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                AnimatedContainer(
                  duration: const Duration(milliseconds: 150),
                  width: 46,
                  height: 28,
                  decoration: BoxDecoration(
                    color: dangChon ? AppColors.accentSoft : Colors.transparent,
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: Center(
                    child: AppIcon(
                      icon,
                      size: 20,
                      color: mau,
                      net: dangChon ? 2 : 1.7,
                    ),
                  ),
                ),
                if (soHuyHieu > 0)
                  Positioned(
                    right: 2,
                    top: -3,
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 4.5,
                        vertical: 1,
                      ),
                      constraints: const BoxConstraints(minWidth: 17),
                      decoration: BoxDecoration(
                        color: AppColors.loiChu,
                        borderRadius: BorderRadius.circular(9),
                        border: Border.all(color: Colors.white, width: 1.5),
                      ),
                      child: Text(
                        soHuyHieu > 9 ? '9+' : '$soHuyHieu',
                        textAlign: TextAlign.center,
                        style: AppTheme.font(
                          size: 9.5,
                          weight: FontWeight.w800,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 3),
            Text(
              nhan,
              maxLines: 1,
              style: AppTheme.font(
                size: 11,
                weight: dangChon ? FontWeight.w700 : FontWeight.w500,
                color: dangChon ? AppColors.accent : AppColors.chuPhu,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
