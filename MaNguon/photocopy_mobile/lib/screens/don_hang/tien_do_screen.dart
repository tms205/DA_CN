import 'package:flutter/material.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../models.dart';
import '../../realtime.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'don_hang_screen.dart';

// F-11 / F-12 — Figma 03-Mobile-TienDo: theo dõi đơn hàng theo thời gian thực.
// Các mốc tiến độ do máy chủ dựng sẵn (DonHangController.DungTienDo), màn này chỉ việc vẽ:
// mốc đã qua tô xanh, mốc đang đứng có chấm nhấp nháy, mốc chưa tới để xám.
// Quầy đổi trạng thái đơn → máy chủ đẩy tin xuống qua SignalR → màn tự tải lại.
class TienDoScreen extends StatefulWidget {
  const TienDoScreen({super.key, required this.maDonHang});

  final int maDonHang;

  @override
  State<TienDoScreen> createState() => _TienDoScreenState();
}

class _TienDoScreenState extends State<TienDoScreen> {
  DonHangChiTiet? _don; // null = đang tải
  String? _loi;

  @override
  void initState() {
    super.initState();
    _taiDon();
    Realtime.dangKyNghe(_khiCoTin);
  }

  @override
  void dispose() {
    Realtime.boNghe(_khiCoTin);
    super.dispose();
  }

  // GET /api/don-hang/{maDonHang}
  Future<void> _taiDon() async {
    KetQuaApi kq = await Api.get('/api/don-hang/${widget.maDonHang}');
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() => _loi = kq.loi);
      return;
    }
    setState(() {
      _loi = null;
      _don = DonHangChiTiet.fromJson(kq.duLieu);
    });
  }

  // Máy chủ báo đơn này vừa đổi trạng thái
  void _khiCoTin(Map<String, dynamic> tin) {
    if (tin['maDonHang'] == widget.maDonHang) _taiDon();
  }

  @override
  Widget build(BuildContext context) {
    Widget noiDung;
    if (_don == null && _loi == null) {
      noiDung = const Center(
        child: CircularProgressIndicator(color: AppColors.accent),
      );
    } else if (_loi != null) {
      noiDung = TrangTrong(
        icon: Ico.chamThan,
        tieuDe: 'Không tải được đơn hàng',
        moTa: _loi,
        nhanNut: 'Thử lại',
        onNut: () {
          setState(() => _loi = null);
          _taiDon();
        },
      );
    } else {
      noiDung = RefreshIndicator(
        color: AppColors.accent,
        onRefresh: _taiDon,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(16, 14, 16, 26),
          children: [
            _theDon(),
            const SizedBox(height: 14),
            _khoiTienDo(),
            const SizedBox(height: 14),
            _khoiTaiLieu(),
            const SizedBox(height: 14),
            _khoiThanhToan(),
            if (_don!.trangThaiDonHang == 'CHO_XAC_NHAN') ...[
              const SizedBox(height: 14),
              NutPhu(
                nhan: 'Hủy đơn',
                icon: const AppIcon(
                  Ico.chamThan,
                  size: 17,
                  color: AppColors.loiChu,
                ),
                onPressed: _huyDon,
              ),
            ],
            if (_don!.trangThaiDonHang == 'HOAN_THANH' && !_don!.daDanhGia) ...[
              const SizedBox(height: 14),
              NutChinh(
                nhan: 'Đánh giá dịch vụ',
                iconTruoc: Ico.dauTich,
                onPressed: _danhGia,
              ),
            ],
            if (_don!.laGiaoTanNoi && _don!.soDienThoaiShipper != null) ...[
              const SizedBox(height: 14),
              _khoiShipper(),
            ],
          ],
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(
        tieuDe: 'Theo dõi tiến độ',
        phuDe: 'Cập nhật ngay khi quầy xử lý đơn',
      ),
      body: noiDung,
    );
  }

  // Thẻ đầu: mã đơn, chi nhánh, hình thức nhận, tổng tiền
  Widget _theDon() {
    DonHangChiTiet d = _don!;
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Flexible(
                child: Text(
                  d.maDonHangHienThi,
                  overflow: TextOverflow.ellipsis,
                  style: AppTheme.mono(size: 16, weight: FontWeight.w700),
                ),
              ),
              if (d.inHoaToc) ...[
                const SizedBox(width: 7),
                const NhanNho(
                  'Hỏa tốc',
                  mau: AppColors.hoaTocChu,
                  nen: AppColors.hoaTocNen,
                ),
              ],
              const Spacer(),
              NhanNho(
                tenTrangThai(d.trangThaiDonHang),
                mau: mauChuTrangThai(d.trangThaiDonHang),
                nen: mauNenTrangThai(d.trangThaiDonHang),
              ),
            ],
          ),
          const SizedBox(height: 9),
          Row(
            children: [
              const AppIcon(
                Ico.cuaHang,
                size: 15,
                color: AppColors.iconMo,
                net: 1.7,
              ),
              const SizedBox(width: 7),
              Expanded(
                child: Text(
                  '${d.tenChiNhanh} — ${d.diaChiChiNhanh}',
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: AppTheme.font(size: 11.5, color: AppColors.chuPhu),
                ),
              ),
            ],
          ),
          const SizedBox(height: 5),
          Row(
            children: [
              AppIcon(
                d.laGiaoTanNoi ? Ico.xeGiao : Ico.nguoi,
                size: 15,
                color: AppColors.iconMo,
                net: 1.7,
              ),
              const SizedBox(width: 7),
              Expanded(
                child: Text(
                  d.laGiaoTanNoi
                      ? 'Giao tận nơi · ${d.diaChiGiao ?? ''}'
                      : 'Nhận tại quầy · ${d.tongSoTrang} trang in',
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: AppTheme.font(size: 11.5, color: AppColors.chuPhu),
                ),
              ),
            ],
          ),
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 10),
            child: Divider(height: 1, color: AppColors.vach),
          ),
          Row(
            crossAxisAlignment: CrossAxisAlignment.baseline,
            textBaseline: TextBaseline.alphabetic,
            children: [
              Text(
                d.daTra ? 'Đã thanh toán' : 'Phải trả khi nhận',
                style: AppTheme.font(
                  size: 11.5,
                  weight: FontWeight.w600,
                  color: d.daTra ? AppColors.thanhCong : AppColors.cbChu,
                ),
              ),
              const Spacer(),
              Text(
                tien(d.tongTien),
                style: AppTheme.mono(
                  size: 18,
                  weight: FontWeight.w800,
                  color: AppColors.accentDeep,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  // Thanh tiến độ dọc: mỗi mốc 1 chấm + đường nối xuống mốc sau
  Widget _khoiTienDo() {
    DonHangChiTiet d = _don!;
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 13, 14, 6),
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(
                'Tiến trình xử lý',
                style: AppTheme.font(size: 13, weight: FontWeight.w700),
              ),
              const Spacer(),
              const AppIcon(
                Ico.taiLai,
                size: 12,
                color: AppColors.thanhCong,
                net: 2,
              ),
              const SizedBox(width: 5),
              Text(
                Realtime.dangNoi ? 'Đồng bộ realtime' : 'Kéo xuống để tải lại',
                style: AppTheme.font(
                  size: 10.5,
                  weight: FontWeight.w600,
                  color: AppColors.thanhCong,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          for (int i = 0; i < d.tienDo.length; i++)
            _mocTienDo(d.tienDo[i], i == d.tienDo.length - 1),
        ],
      ),
    );
  }

  // Một mốc: cột trái là chấm + đường kẻ, cột phải là tiêu đề / mô tả / giờ
  Widget _mocTienDo(MocTienDo moc, bool laMocCuoi) {
    // Mốc đã qua: xanh đậm. Mốc đang đứng: xanh accent. Mốc chưa tới: xám.
    Color mauCham = AppColors.vienHop;
    if (moc.daXong) mauCham = AppColors.thanhCong;
    if (moc.dangO) mauCham = AppColors.accent;
    if (moc.dangO && moc.ma == 'DA_HUY') mauCham = AppColors.loiChu;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Column(
            children: [
              Container(
                width: 18,
                height: 18,
                decoration: BoxDecoration(
                  color: moc.daXong || moc.dangO ? mauCham : AppColors.trang,
                  border: Border.all(color: mauCham, width: 2),
                  shape: BoxShape.circle,
                ),
                child: moc.daXong
                    ? const Center(
                        child: AppIcon(
                          Ico.dauTich,
                          size: 10,
                          color: Colors.white,
                          net: 4,
                        ),
                      )
                    : null,
              ),
              if (!laMocCuoi)
                Expanded(
                  child: Container(
                    width: 2,
                    color: moc.daXong ? AppColors.thanhCong : AppColors.vach,
                  ),
                ),
            ],
          ),
          const SizedBox(width: 11),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(bottom: laMocCuoi ? 10 : 16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          moc.tieuDe,
                          style: AppTheme.font(
                            size: 12.5,
                            weight: moc.dangO
                                ? FontWeight.w700
                                : FontWeight.w600,
                            color: moc.daXong || moc.dangO
                                ? AppColors.chu
                                : AppColors.chuPhu,
                          ),
                        ),
                      ),
                      Text(
                        moc.thoiDiem == null
                            ? '—'
                            : gioPhut(moc.thoiDiem!.toLocal()),
                        style: AppTheme.mono(
                          size: 11.5,
                          weight: FontWeight.w600,
                          color: AppColors.chuPhu,
                        ),
                      ),
                    ],
                  ),
                  if (moc.moTa != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      moc.moTa!,
                      style: AppTheme.font(
                        size: 11,
                        color: AppColors.chuPhu,
                        height: 1.4,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  // Danh sách file trong đơn + quy cách in
  Widget _khoiTaiLieu() {
    DonHangChiTiet d = _don!;
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Tài liệu in',
            style: AppTheme.font(size: 13, weight: FontWeight.w700),
          ),
          for (DongInChiTiet dong in d.dongIn) ...[
            const SizedBox(height: 10),
            Row(
              children: [
                const AppIcon(
                  Ico.taiLieu,
                  size: 15,
                  color: AppColors.accent,
                  net: 1.7,
                ),
                const SizedBox(width: 7),
                Expanded(
                  child: Text(
                    dong.tenFile,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTheme.font(size: 12.5, weight: FontWeight.w600),
                  ),
                ),
                Text(
                  tien(dong.thanhTien),
                  style: AppTheme.mono(size: 12, weight: FontWeight.w600),
                ),
              ],
            ),
            const SizedBox(height: 3),
            Padding(
              padding: const EdgeInsets.only(left: 22),
              child: Text(
                dong.kichThuocAnh != null
                    ? '${dong.quyCach} · ${tien(dong.donGia)}/ảnh'
                    : '${dong.soTrang} trang × ${dong.soBan} bản · ${dong.quyCach}',
                style: AppTheme.font(size: 11, color: AppColors.chuPhu),
              ),
            ),
            if (dong.tenGiaCong != null)
              Padding(
                padding: const EdgeInsets.only(left: 22, top: 2),
                child: Text(
                  'Gia công: ${dong.tenGiaCong}',
                  style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                ),
              ),
          ],
          if (d.ghiChu != null) ...[
            const SizedBox(height: 10),
            KhungNetDut(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(11, 9, 11, 9),
                child: Text(
                  'Ghi chú cho thợ in: ${d.ghiChu}',
                  style: AppTheme.font(
                    size: 11,
                    color: AppColors.chuPhu,
                    height: 1.5,
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _khoiThanhToan() {
    DonHangChiTiet d = _don!;
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Thanh toán',
            style: AppTheme.font(size: 13, weight: FontWeight.w700),
          ),
          const SizedBox(height: 9),
          _dongTien('Tiền hàng', tien(d.tienHang)),
          if (d.phiShip > 0) _dongTien('Phí giao hàng', tien(d.phiShip)),
          if (d.giamGia > 0) _dongTien('Giảm giá', '−${tien(d.giamGia)}'),
          _dongTien(
            'Hình thức',
            d.phuongThucThanhToan == 'VIETQR'
                ? 'Chuyển khoản VietQR'
                : 'Tiền mặt',
          ),
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Divider(height: 1, color: AppColors.vach),
          ),
          Row(
            children: [
              Text(
                'Tổng cộng',
                style: AppTheme.font(size: 13, weight: FontWeight.w700),
              ),
              const Spacer(),
              Text(
                tien(d.tongTien),
                style: AppTheme.mono(
                  size: 17,
                  weight: FontWeight.w800,
                  color: AppColors.accentDeep,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  // Đơn giao tận nơi đã có shipper nhận: hiện tên + số điện thoại để khách gọi
  Widget _khoiShipper() {
    DonHangChiTiet d = _don!;
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
      decoration: BoxDecoration(
        color: AppColors.hoaTocNen,
        border: Border.all(color: AppColors.hoaTocVien),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          const AppIcon(
            Ico.xeGiao,
            size: 20,
            color: AppColors.hoaToc,
            net: 1.7,
          ),
          const SizedBox(width: 11),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Shipper ${d.tenShipper}',
                  style: AppTheme.font(
                    size: 12.5,
                    weight: FontWeight.w700,
                    color: AppColors.hoaTocChu,
                  ),
                ),
                Text(
                  d.soDienThoaiShipper ?? '',
                  style: AppTheme.mono(
                    size: 12,
                    weight: FontWeight.w600,
                    color: AppColors.hoaTocPhu,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _dongTien(String nhan, String giaTri) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 5),
      child: Row(
        children: [
          Text(nhan, style: AppTheme.font(size: 12, color: AppColors.chuNhan)),
          const Spacer(),
          Text(
            giaTri,
            style: AppTheme.mono(size: 12.5, weight: FontWeight.w600),
          ),
        ],
      ),
    );
  }

  Future<void> _huyDon() async {
    TextEditingController lyDo = TextEditingController(
      text: 'Tôi không còn nhu cầu in',
    );
    bool? dongY = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Hủy đơn hàng?'),
        content: TextField(
          controller: lyDo,
          maxLength: 255,
          decoration: const InputDecoration(labelText: 'Lý do hủy'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Không'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Hủy đơn'),
          ),
        ],
      ),
    );
    if (dongY != true) return;
    KetQuaApi kq = await Api.post('/api/don-hang/${widget.maDonHang}/huy', {
      'lyDo': lyDo.text.trim(),
    });
    if (!mounted) return;
    hienThongBaoNhanh(
      context,
      kq.thanhCong ? (kq.duLieu['message'] ?? 'Đã hủy đơn.') : kq.loi,
    );
    if (kq.thanhCong) _taiDon();
  }

  Future<void> _danhGia() async {
    int soSao = 5;
    TextEditingController nhanXet = TextEditingController();
    bool? gui = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Đánh giá dịch vụ'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  for (int i = 1; i <= 5; i++)
                    IconButton(
                      onPressed: () => setDialogState(() => soSao = i),
                      icon: Icon(
                        i <= soSao ? Icons.star : Icons.star_border,
                        color: AppColors.cbIcon,
                      ),
                    ),
                ],
              ),
              TextField(
                controller: nhanXet,
                maxLength: 500,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'Nhận xét (không bắt buộc)',
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Để sau'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Gửi đánh giá'),
            ),
          ],
        ),
      ),
    );
    if (gui != true) return;
    KetQuaApi kq = await Api.post(
      '/api/don-hang/${widget.maDonHang}/danh-gia',
      {'soSao': soSao, 'nhanXet': nhanXet.text.trim()},
    );
    if (!mounted) return;
    hienThongBaoNhanh(
      context,
      kq.thanhCong ? 'Cảm ơn bạn đã đánh giá.' : kq.loi,
    );
    if (kq.thanhCong) _taiDon();
  }
}
