import 'package:flutter/material.dart';

import '../../api.dart';
import '../../models.dart';
import '../../phien.dart';
import '../../realtime.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'cai_dat_screen.dart';
import '../xac_thuc/dang_nhap_screen.dart';
import 'dia_chi_screen.dart';
import 'dieu_khoan_screen.dart';
import 'doi_mat_khau_screen.dart';
import 'ho_so_screen.dart';
import 'ho_tro_screen.dart';

// Tab "Tôi" — dùng chung cho Khách hàng và Shipper; mục chỉ dành cho khách (đơn hàng, địa chỉ) tự ẩn với Shipper.
class ToiScreen extends StatefulWidget {
  const ToiScreen({super.key, this.moDonHang});

  // Bấm ô "Đang xử lý", "Hoàn tất"... → trang chính mở tab Đơn hàng với bộ lọc tương ứng
  final void Function(String nhom)? moDonHang;

  @override
  State<ToiScreen> createState() => _ToiScreenState();
}

class _ToiScreenState extends State<ToiScreen> {
  List<DonHang> _dsDon = [];   // để đếm số đơn theo nhóm

  @override
  void initState() {
    super.initState();
    if (!Phien.laShipper) _taiDonHang();
  }

  Future<void> _taiDonHang() async {
    KetQuaApi kq = await Api.get('/api/don-hang/cua-toi');
    if (!kq.thanhCong || !mounted) return;

    List<DonHang> ds = [];
    for (var json in kq.duLieu) {
      ds.add(DonHang.fromJson(json));
    }
    setState(() => _dsDon = ds);
  }

  int _demTheoNhom(String nhom) {
    int dem = 0;
    for (DonHang d in _dsDon) {
      if (d.nhom == nhom) dem++;
    }
    return dem;
  }

  void _moDonHang(String nhom) {
    if (widget.moDonHang != null) widget.moDonHang!(nhom);
  }

  // Mở 1 màn con; quay lại thì vẽ lại (vd vừa sửa họ tên thì thẻ trên cùng đổi theo)
  Future<void> _moManCon(Widget manCon) async {
    await Navigator.push(context, MaterialPageRoute(builder: (context) => manCon));
    if (mounted) setState(() {});
  }

  Future<void> _dangXuat() async {
    bool? dongY = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Đăng xuất?', style: AppTheme.font(size: 16, weight: FontWeight.w700)),
        content: Text('Bạn sẽ cần đăng nhập lại để đặt in và xem đơn hàng.',
            style: AppTheme.font(size: 13, color: AppColors.chuNhan, height: 1.5)),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Hủy')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.loiChu),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Đăng xuất'),
          ),
        ],
      ),
    );
    if (dongY != true) return;

    await Realtime.dung();   // đóng kênh real-time trước khi bỏ token
    await Phien.dangXuat();
    if (!mounted) return;
    // Về màn đăng nhập, xóa hết các màn phía trước
    Navigator.pushAndRemoveUntil(
      context,
      MaterialPageRoute(builder: (context) => const DangNhapScreen()),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    NguoiDung? nd = Phien.nguoiDung;
    if (nd == null) return const Scaffold(backgroundColor: AppColors.nen);
    bool laKhach = !nd.laShipper;

    return Scaffold(
      backgroundColor: AppColors.nen,
      body: SafeArea(
        bottom: false,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
          children: [
            _theNguoiDung(nd),
            if (laKhach) ...[
              const SizedBox(height: 14),
              _theDonHang(),
            ],
            const SizedBox(height: 18),
            NhomDong(
              tieuDe: 'Tài khoản',
              children: [
                DongMenu(
                  icon: Ico.nguoi,
                  nhan: 'Thông tin cá nhân',
                  phu: 'Họ tên, số điện thoại',
                  onTap: () => _moManCon(const HoSoScreen()),
                ),
                if (laKhach)
                  DongMenu(
                    icon: Ico.ghim,
                    nhan: 'Địa chỉ nhận hàng',
                    phu: nd.diaChiMacDinh ?? 'Chưa có — thêm để chọn Giao tận nơi nhanh hơn',
                    onTap: () => _moManCon(const DiaChiScreen()),
                  ),
                if (laKhach)
                  DongMenu(
                    icon: Ico.lichSu,
                    nhan: 'Lịch sử mua hàng',
                    phu: 'Các đơn đã hoàn tất',
                    onTap: () => _moDonHang('HOAN_TAT'),
                  ),
                DongMenu(icon: Ico.khoa, nhan: 'Đổi mật khẩu', onTap: () => _moManCon(const DoiMatKhauScreen())),
              ],
            ),
            const SizedBox(height: 18),
            NhomDong(
              tieuDe: 'Ứng dụng',
              children: [
                DongMenu(
                  icon: Ico.caiDat,
                  nhan: 'Cài đặt',
                  phu: 'Ghi nhớ đăng nhập, phiên bản',
                  onTap: () => _moManCon(const CaiDatScreen()),
                ),
                DongMenu(
                  icon: Ico.troGiup,
                  nhan: 'Trợ giúp & liên hệ chi nhánh',
                  phu: 'Hotline, giờ mở cửa, câu hỏi thường gặp',
                  onTap: () => _moManCon(const HoTroScreen()),
                ),
                DongMenu(icon: Ico.khien, nhan: 'Điều khoản & chính sách', onTap: () => _moManCon(const DieuKhoanScreen())),
              ],
            ),
            const SizedBox(height: 18),
            NhomDong(
              children: [
                DongMenu(
                  icon: Ico.dangXuat,
                  nhan: 'Đăng xuất',
                  mau: AppColors.loiChu,
                  mauChu: AppColors.loiChu,
                  cuoi: const SizedBox(),
                  onTap: _dangXuat,
                ),
              ],
            ),
            const SizedBox(height: 16),
            Text('PhotocopyChain App · phiên bản 1.0.0',
                textAlign: TextAlign.center, style: AppTheme.font(size: 11, color: AppColors.iconMo)),
          ],
        ),
      ),
    );
  }

  // Thẻ xanh trên cùng: chữ viết tắt, họ tên, email, vai trò
  Widget _theNguoiDung(NguoiDung nd) {
    String nhanVaiTro = nd.tenVaiTroHienThi;
    if (nd.laShipper) nhanVaiTro = '${nd.tenVaiTroHienThi} · ${nd.tenChiNhanh ?? ''}';

    return Container(
      padding: const EdgeInsets.fromLTRB(16, 16, 10, 16),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [AppColors.accent, AppColors.accentDeep],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(16),
        boxShadow: const [BoxShadow(color: Color(0x3322437F), blurRadius: 14, offset: Offset(0, 5))],
      ),
      child: Row(
        children: [
          Container(
            width: 54,
            height: 54,
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.16),
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: Colors.white.withValues(alpha: 0.22)),
            ),
            child: Center(child: Text(nd.vietTat, style: AppTheme.font(size: 19, weight: FontWeight.w800, color: Colors.white))),
          ),
          const SizedBox(width: 13),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(nd.hoTen,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTheme.font(size: 17, weight: FontWeight.w800, color: Colors.white, letterSpacing: -0.2)),
                const SizedBox(height: 2),
                Text(nd.email,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTheme.font(size: 12, color: Colors.white.withValues(alpha: 0.75))),
                const SizedBox(height: 7),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(color: Colors.white.withValues(alpha: 0.14), borderRadius: BorderRadius.circular(6)),
                  child: Text(nhanVaiTro, style: AppTheme.font(size: 10.5, weight: FontWeight.w700, color: Colors.white)),
                ),
              ],
            ),
          ),
          IconButton(
            tooltip: 'Sửa thông tin',
            onPressed: () => _moManCon(const HoSoScreen()),
            icon: const AppIcon(Ico.but, size: 18, color: Colors.white, net: 1.9),
          ),
        ],
      ),
    );
  }

  // Thẻ "Đơn hàng của tôi" với 4 ô đếm theo nhóm (kiểu "Đơn mua" của các app bán hàng)
  Widget _theDonHang() {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        children: [
          InkWell(
            borderRadius: const BorderRadius.vertical(top: Radius.circular(14)),
            onTap: () => _moDonHang('TAT_CA'),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(14, 12, 12, 10),
              child: Row(
                children: [
                  Expanded(
                    child: Text('Đơn hàng của tôi',
                        maxLines: 1, overflow: TextOverflow.ellipsis, style: AppTheme.font(size: 13.5, weight: FontWeight.w700)),
                  ),
                  const SizedBox(width: 8),
                  Flexible(
                    child: Text('Xem tất cả (${_dsDon.length})',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppTheme.font(size: 12, weight: FontWeight.w600, color: AppColors.accent)),
                  ),
                  const AppIcon(Ico.chevronPhai, size: 15, color: AppColors.accent, net: 2),
                ],
              ),
            ),
          ),
          const Divider(height: 1, color: AppColors.vach),
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 8),
            child: Row(
              children: [
                _oNhom('DANG_XU_LY', Ico.mayIn),
                _oNhom('DANG_GIAO', Ico.xeGiao),
                _oNhom('HOAN_TAT', Ico.dauTich),
                _oNhom('DA_HUY', Ico.chamThan),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _oNhom(String nhom, String icon) {
    int so = _demTheoNhom(nhom);
    return Expanded(
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: () => _moDonHang(nhom),
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 6),
          child: Column(
            children: [
              Stack(
                clipBehavior: Clip.none,
                children: [
                  AppIcon(icon, size: 23, color: AppColors.chuNhan, net: 1.6),
                  if (so > 0)
                    Positioned(
                      right: -9,
                      top: -6,
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
                        constraints: const BoxConstraints(minWidth: 17),
                        decoration: BoxDecoration(
                          color: AppColors.accent,
                          borderRadius: BorderRadius.circular(9),
                          border: Border.all(color: Colors.white, width: 1.5),
                        ),
                        child: Text('$so',
                            textAlign: TextAlign.center, style: AppTheme.font(size: 9.5, weight: FontWeight.w800, color: Colors.white)),
                      ),
                    ),
                ],
              ),
              const SizedBox(height: 6),
              Text(tenNhomDon(nhom), style: AppTheme.font(size: 11, color: AppColors.chuNhan)),
            ],
          ),
        ),
      ),
    );
  }
}
