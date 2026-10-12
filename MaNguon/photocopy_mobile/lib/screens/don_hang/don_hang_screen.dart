import 'package:flutter/material.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../models.dart';
import '../../realtime.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import '../dat_in/thanh_toan_screen.dart';
import 'tien_do_screen.dart';

// Tên + màu nhãn trạng thái đơn (tên trùng TrangThaiDonHang.TenHienThi của PhotocopyShared)
String tenTrangThai(String trangThai) {
  switch (trangThai) {
    case 'CHO_XAC_NHAN':
      return 'Chờ tiếp nhận';
    case 'DA_XAC_NHAN':
      return 'Đã tiếp nhận';
    case 'DANG_IN':
      return 'Đang in ấn';
    case 'DA_IN':
      return 'Đã in xong';
    case 'DANG_GIAO':
      return 'Đang giao';
    case 'DA_GIAO':
      return 'Đã giao';
    case 'HOAN_THANH':
      return 'Hoàn tất';
    case 'GIAO_THAT_BAI':
      return 'Giao thất bại';
    case 'DA_HUY':
      return 'Đã hủy';
    default:
      return trangThai;
  }
}

Color mauChuTrangThai(String trangThai) {
  if (trangThai == 'CHO_XAC_NHAN') return AppColors.cbChu;
  if (trangThai == 'DA_XAC_NHAN' || trangThai == 'DANG_IN') {
    return AppColors.accent;
  }
  if (trangThai == 'DA_IN' ||
      trangThai == 'DA_GIAO' ||
      trangThai == 'HOAN_THANH') {
    return AppColors.thanhCong;
  }
  if (trangThai == 'DANG_GIAO') return AppColors.hoaTocChu;
  if (trangThai == 'GIAO_THAT_BAI') return AppColors.loiChu;
  return AppColors.chuPhu;
}

Color mauNenTrangThai(String trangThai) {
  if (trangThai == 'CHO_XAC_NHAN') return AppColors.cbNen;
  if (trangThai == 'DA_XAC_NHAN' || trangThai == 'DANG_IN') {
    return AppColors.accentSoft;
  }
  if (trangThai == 'DA_IN' ||
      trangThai == 'DA_GIAO' ||
      trangThai == 'HOAN_THANH') {
    return AppColors.xanhNhat;
  }
  if (trangThai == 'DANG_GIAO') return AppColors.hoaTocNen;
  if (trangThai == 'GIAO_THAT_BAI') return AppColors.loiNen;
  return AppColors.nenXam;
}

// Tab "Đơn hàng" của khách (F-12): lọc theo nhóm trạng thái, kéo xuống để tải lại.
// Figma chưa vẽ màn này nên dựng cùng phong cách màn Cấu hình in.
class DonHangScreen extends StatefulWidget {
  const DonHangScreen({super.key, this.nhomBanDau = 'TAT_CA', this.moDatIn});

  final String nhomBanDau; // bộ lọc mở sẵn (màn Tôi truyền sang)
  final VoidCallback?
  moDatIn; // bấm "Đặt in ngay" khi chưa có đơn → trang chính chuyển sang tab Đặt in

  @override
  State<DonHangScreen> createState() => _DonHangScreenState();
}

class _DonHangScreenState extends State<DonHangScreen> {
  static const List<String> dsNhom = [
    'TAT_CA',
    'DANG_XU_LY',
    'DANG_GIAO',
    'HOAN_TAT',
    'DA_HUY',
  ];

  List<DonHang>? _dsDon; // null = đang tải
  String? _loi;
  String _loc = 'TAT_CA';

  @override
  void initState() {
    super.initState();
    _loc = widget.nhomBanDau;
    _taiDanhSach();
    Realtime.dangKyNghe(
      _khiCoTin,
    ); // quầy đổi trạng thái đơn thì danh sách tự cập nhật
  }

  @override
  void dispose() {
    Realtime.boNghe(_khiCoTin);
    super.dispose();
  }

  void _khiCoTin(Map<String, dynamic> tin) {
    _taiDanhSach();
  }

  // GET /api/don-hang/cua-toi
  Future<void> _taiDanhSach() async {
    KetQuaApi kq = await Api.get('/api/don-hang/cua-toi');
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() {
        _loi = kq.loi;
        _dsDon = [];
      });
      return;
    }

    List<DonHang> ds = [];
    for (var json in kq.duLieu) {
      ds.add(DonHang.fromJson(json));
    }
    setState(() {
      _loi = null;
      _dsDon = ds;
    });
  }

  // Các đơn thuộc nhóm đang lọc
  List<DonHang> _donDaLoc() {
    List<DonHang> ketQua = [];
    for (DonHang d in _dsDon!) {
      if (_loc == 'TAT_CA' || d.nhom == _loc) ketQua.add(d);
    }
    return ketQua;
  }

  int _demTheoNhom(String nhom) {
    int dem = 0;
    for (DonHang d in _dsDon!) {
      if (nhom == 'TAT_CA' || d.nhom == nhom) dem++;
    }
    return dem;
  }

  @override
  Widget build(BuildContext context) {
    Widget noiDung;
    if (_dsDon == null) {
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
          setState(() => _dsDon = null);
          _taiDanhSach();
        },
      );
    } else {
      List<DonHang> daLoc = _donDaLoc();
      Widget danhSach;
      if (daLoc.isEmpty) {
        danhSach = ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: [
            const SizedBox(height: 50),
            if (_loc == 'TAT_CA')
              TrangTrong(
                icon: Ico.donHang,
                tieuDe: 'Chưa có đơn hàng nào',
                moTa:
                    'Tải tài liệu lên, chọn quy cách in và đặt đơn chỉ trong vài bước.',
                nhanNut: 'Đặt in ngay',
                onNut: widget.moDatIn,
              )
            else
              TrangTrong(
                icon: Ico.donHang,
                tieuDe: 'Không có đơn "${tenNhomDon(_loc).toLowerCase()}"',
                moTa: 'Chọn "Tất cả" để xem mọi đơn đã đặt.',
              ),
          ],
        );
      } else {
        danhSach = ListView.separated(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
          itemCount: daLoc.length,
          separatorBuilder: (context, i) => const SizedBox(height: 10),
          itemBuilder: (context, i) => _theDon(daLoc[i]),
        );
      }

      noiDung = Column(
        children: [
          _thanhLoc(),
          Expanded(
            // Kéo xuống để tải lại
            child: RefreshIndicator(
              color: AppColors.accent,
              onRefresh: _taiDanhSach,
              child: danhSach,
            ),
          ),
        ],
      );
    }

    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(
        tieuDe: 'Đơn hàng của tôi',
        phuDe: 'Theo dõi và xem lại các đơn đã đặt',
        coNutQuayLai: false,
      ),
      body: noiDung,
    );
  }

  // Hàng chip lọc: "Tất cả · 4", "Đang xử lý · 1"...
  Widget _thanhLoc() {
    return SizedBox(
      height: 54,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
        children: [
          for (String nhom in dsNhom) ...[
            _chipLoc(nhom),
            const SizedBox(width: 7),
          ],
        ],
      ),
    );
  }

  Widget _chipLoc(String nhom) {
    bool daChon = nhom == _loc;
    return GestureDetector(
      onTap: () => setState(() => _loc = nhom),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 120),
        padding: const EdgeInsets.symmetric(horizontal: 13),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: daChon ? AppColors.accent : AppColors.trang,
          border: Border.all(color: daChon ? AppColors.accent : AppColors.vien),
          borderRadius: BorderRadius.circular(18),
        ),
        child: Text(
          '${tenNhomDon(nhom)} · ${_demTheoNhom(nhom)}',
          style: AppTheme.font(
            size: 12,
            weight: FontWeight.w600,
            color: daChon ? Colors.white : AppColors.chuNhan,
          ),
        ),
      ),
    );
  }

  // Thẻ 1 đơn hàng trong danh sách
  Widget _theDon(DonHang d) {
    String tenFile = d.tenFileDau ?? 'Tài liệu in';
    return Material(
      color: AppColors.trang,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(13),
        side: const BorderSide(color: AppColors.vach),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(13),
        // Đơn VietQR còn nợ tiền thì quay lại QR; các đơn khác mở tiến độ như cũ.
        onTap: () async {
          await Navigator.push(
            context,
            MaterialPageRoute(
              builder: (context) => d.dangChoChuyenKhoan
                  ? ThanhToanScreen(maDonHang: d.maDonHang)
                  : TienDoScreen(maDonHang: d.maDonHang),
            ),
          );
          _taiDanhSach(); // quay về thì tải lại cho đúng trạng thái mới nhất
        },
        child: Padding(
          padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Flexible(
                    child: Text(
                      d.maHienThi,
                      overflow: TextOverflow.ellipsis,
                      style: AppTheme.mono(size: 13, weight: FontWeight.w700),
                    ),
                  ),
                  if (d.inHoaToc) ...[
                    const SizedBox(width: 6),
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
                    Ico.taiLieu,
                    size: 15,
                    color: AppColors.accent,
                    net: 1.7,
                  ),
                  const SizedBox(width: 7),
                  Expanded(
                    child: Text(
                      d.soFile > 1
                          ? '$tenFile  +${d.soFile - 1} file'
                          : tenFile,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTheme.font(size: 12.5, weight: FontWeight.w600),
                    ),
                  ),
                ],
              ),
              if (d.dangChoChuyenKhoan) ...[
                const SizedBox(height: 9),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 8,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.cbNen,
                    border: Border.all(color: AppColors.cbVien),
                    borderRadius: BorderRadius.circular(9),
                  ),
                  child: Row(
                    children: [
                      const AppIcon(
                        Ico.dongHo,
                        size: 14,
                        color: AppColors.cbIcon,
                        net: 1.8,
                      ),
                      const SizedBox(width: 7),
                      Expanded(
                        child: Text(
                          'Chờ chuyển khoản VietQR',
                          style: AppTheme.font(
                            size: 11.5,
                            weight: FontWeight.w600,
                            color: AppColors.cbChu,
                          ),
                        ),
                      ),
                      Text(
                        'Tiếp tục thanh toán →',
                        style: AppTheme.font(
                          size: 11,
                          weight: FontWeight.w700,
                          color: AppColors.accent,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
              const SizedBox(height: 5),
              Row(
                children: [
                  AppIcon(
                    d.laGiaoTanNoi ? Ico.xeGiao : Ico.cuaHang,
                    size: 15,
                    color: AppColors.iconMo,
                    net: 1.7,
                  ),
                  const SizedBox(width: 7),
                  Expanded(
                    child: Text(
                      '${d.tenChiNhanh} · ${d.laGiaoTanNoi ? 'Giao tận nơi' : 'Nhận tại quầy'}',
                      maxLines: 1,
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
                    '${thoiGianTuongDoi(d.ngayDat)} · ${d.tongSoTrang} trang in',
                    style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                  ),
                  const Spacer(),
                  Text(
                    tien(d.tongTien),
                    style: AppTheme.mono(
                      size: 14.5,
                      weight: FontWeight.w800,
                      color: AppColors.accentDeep,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
