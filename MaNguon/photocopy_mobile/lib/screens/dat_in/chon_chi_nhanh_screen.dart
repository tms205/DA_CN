import 'dart:async';

import 'package:flutter/material.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../models.dart';
import '../../giao_hang.dart';
import '../../widgets/dia_chi_giao.dart';
import '../../phien.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'thanh_toan_screen.dart';

// F-09 / F-34 — Bước 3/4: chọn chi nhánh, hình thức nhận, mã giảm giá rồi đặt đơn.
// Giá được tính lại theo đúng chi nhánh khách chọn (bước 2 tạm tính theo chi nhánh đang xem giấy),
// vì giao tận nơi còn cộng phí ship và giờ in xong dự kiến tùy hàng đợi từng chi nhánh.
class ChonChiNhanhScreen extends StatefulWidget {
  const ChonChiNhanhScreen({
    super.key,
    required this.file,
    required this.maDichVu,
    required this.tenCauHinh,
    required this.soBan,
    required this.dsGiaCong,
    required this.hoaToc,
    this.huongGiay = 'DUNG',
    this.ghiChu,
    this.guiYeuCau,
    this.maChiNhanhGoiY,
  });

  final FileIn file;
  final int maDichVu; // dòng giá in ấn đã chọn ở bước 2
  final int? maChiNhanhGoiY;
  final String
  tenCauHinh; // "A4 70gsm · 2 mặt · Đen trắng" — chỉ để hiện lại cho khách xem
  final int soBan;
  final List<int> dsGiaCong;
  final bool hoaToc;
  final String huongGiay;
  final String? ghiChu;
  final Future<KetQuaApi> Function(String, Map<String, dynamic>)? guiYeuCau;

  @override
  State<ChonChiNhanhScreen> createState() => _ChonChiNhanhScreenState();
}

class _ChonChiNhanhScreenState extends State<ChonChiNhanhScreen> {
  Future<KetQuaApi> _post(String path, Map<String, dynamic> body) =>
      widget.guiYeuCau?.call(path, body) ?? Api.post(path, body);
  // Dữ liệu tải từ API
  List<ChiNhanh>? _dsChiNhanh; // null = đang tải
  String? _loiTai;

  // Lựa chọn của khách
  int? _maChiNhanh;
  String _hinhThucNhan = 'TAI_QUAY';
  DiaChiNhan _diaChiNhan = const DiaChiNhan();
  final TextEditingController _oSdt = TextEditingController();
  final TextEditingController _oMaGiam = TextEditingController();

  // Báo giá lại theo chi nhánh đã chọn
  BaoGia? _baoGia;
  String? _loiBaoGia;
  bool _dangTinh = false;
  Timer? _hen;
  int _lanGoi = 0;

  bool _dangDatDon = false;
  String? _loiDatDon;

  @override
  void initState() {
    super.initState();
    // Điền sẵn địa chỉ nhận hàng mặc định + số điện thoại đã lưu ở tab Tôi
    _diaChiNhan = DiaChiNhan(diaChi: Phien.nguoiDung?.diaChiMacDinh ?? '');
    _oSdt.text = Phien.nguoiDung?.soDienThoai ?? '';
    _taiChiNhanh();
  }

  @override
  void dispose() {
    _hen?.cancel();
    _oSdt.dispose();
    _oMaGiam.dispose();
    super.dispose();
  }

  // POST /api/don-hang/chi-nhanh-phu-hop — API trả kèm cờ chi nhánh nào in được cấu hình này
  Future<void> _taiChiNhanh() async {
    setState(() {
      _dsChiNhanh = null;
      _loiTai = null;
    });

    KetQuaApi kq = await _post('/api/don-hang/chi-nhanh-phu-hop', {
      'dsMaDichVu': [widget.maDichVu],
    });
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() => _loiTai = kq.loi);
      return;
    }

    List<ChiNhanh> ds = [];
    for (var json in kq.duLieu) {
      ds.add(ChiNhanh.fromJson(json));
    }

    // Chọn sẵn chi nhánh đầu tiên in được cho khách khỏi phải bấm
    int? chonSan;
    for (ChiNhanh cn in ds) {
      if (cn.dapUngDuoc && chonSan == null) chonSan = cn.maChiNhanh;
      if (cn.dapUngDuoc && cn.maChiNhanh == widget.maChiNhanhGoiY) {
        chonSan = cn.maChiNhanh;
        break;
      }
    }

    setState(() {
      _dsChiNhanh = ds;
      _maChiNhanh = chonSan;
    });
    _goiBaoGia();
  }

  ChiNhanh? _chiNhanhDaChon() {
    if (_dsChiNhanh == null) return null;
    for (ChiNhanh cn in _dsChiNhanh!) {
      if (cn.maChiNhanh == _maChiNhanh) return cn;
    }
    return null;
  }

  // Đổi lựa chọn: vẽ lại ngay, đợi 0,35 giây rồi mới gọi báo giá (gõ địa chỉ không gọi API liên tục)
  void _doiLuaChon(VoidCallback thayDoi) {
    _lanGoi++; // Kết quả cũ không được quay lại sau khi đổi địa chỉ/chi nhánh.
    setState(() {
      thayDoi();
      _baoGia = null;
      _dangTinh = false;
      _loiBaoGia = null;
    });
    _hen?.cancel();
    _hen = Timer(const Duration(milliseconds: 350), _goiBaoGia);
  }

  // POST /api/bao-gia — tính lại tiền theo chi nhánh + hình thức nhận + mã giảm giá
  Future<void> _goiBaoGia() async {
    if (_maChiNhanh == null) {
      setState(() {
        _baoGia = null;
        _loiBaoGia = 'Chưa chọn được chi nhánh in.';
      });
      return;
    }
    // Chưa có điểm nhận xác thực từ GPS thì không tính phí theo địa chỉ cũ.
    if (_hinhThucNhan == 'GIAO_HANG' && !_diaChiNhan.daChon) {
      setState(() {
        _baoGia = null;
        _loiBaoGia =
            'Bấm Dùng vị trí hiện tại để lấy địa chỉ nhận hàng và tính phí giao hàng.';
      });
      return;
    }

    _lanGoi++;
    int lanNay = _lanGoi;
    setState(() {
      _dangTinh = true;
      _loiBaoGia = null;
      _loiDatDon = null;
    });

    KetQuaApi kq = await _post('/api/bao-gia', {
      'maChiNhanh': _maChiNhanh,
      'hinhThucNhan': _hinhThucNhan,
      if (_hinhThucNhan == 'GIAO_HANG') ..._diaChiNhan.duLieu,
      'inGap': widget.hoaToc,
      'maCodeKhuyenMai': _oMaGiam.text.trim().isEmpty
          ? null
          : _oMaGiam.text.trim(),
      'fileIn': [
        {
          'maFileIn': widget.file.maFileIn,
          'maDichVu': widget.maDichVu,
          'soBanIn': widget.soBan,
          'huongGiay': widget.huongGiay,
          'dsGiaCong': widget.dsGiaCong,
        },
      ],
    });

    // Bấm liên tục thì chỉ nhận kết quả của lần gọi mới nhất
    if (!mounted || lanNay != _lanGoi) return;
    setState(() {
      _dangTinh = false;
      if (kq.thanhCong) {
        _baoGia = BaoGia.fromJson(kq.duLieu);
      } else {
        _baoGia = null;
        _loiBaoGia = kq.loi;
      }
    });
  }

  // POST /api/don-hang — F-09: tạo đơn thật
  Future<void> _datDon() async {
    if (_baoGia == null || _dangTinh || _dangDatDon) return;
    setState(() {
      _dangDatDon = true;
      _loiDatDon = null;
    });

    KetQuaApi kq = await _post('/api/don-hang', {
      'maChiNhanh': _maChiNhanh,
      'hinhThucNhan': _hinhThucNhan,
      if (_hinhThucNhan == 'GIAO_HANG') ..._diaChiNhan.duLieu,
      'sdtNhan': _oSdt.text.trim().isEmpty ? null : _oSdt.text.trim(),
      'maBaoGiaGiao': _hinhThucNhan == 'GIAO_HANG'
          ? _baoGia!.maBaoGiaGiao
          : null,
      'inGap': widget.hoaToc,
      'maCodeKhuyenMai': _oMaGiam.text.trim().isEmpty
          ? null
          : _oMaGiam.text.trim(),
      'ghiChu': widget.ghiChu,
      'fileIn': [
        {
          'maFileIn': widget.file.maFileIn,
          'maDichVu': widget.maDichVu,
          'soBanIn': widget.soBan,
          'huongGiay': widget.huongGiay,
          'dsGiaCong': widget.dsGiaCong,
        },
      ],
    });
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() {
        _dangDatDon = false;
        _loiDatDon = kq.loi;
      });
      return;
    }

    // Đặt xong thì sang bước 4 (màn Thanh toán). Bấm quay lại từ đó sẽ về thẳng trang chính.
    int maDonHang = kq.duLieu['maDonHang'];
    Navigator.pushReplacement(
      context,
      MaterialPageRoute(
        builder: (context) => ThanhToanScreen(maDonHang: maDonHang),
      ),
    );
  }

  // ---------------------------------------------------------------- Giao diện

  @override
  Widget build(BuildContext context) {
    Widget noiDung;
    if (_dsChiNhanh == null && _loiTai == null) {
      noiDung = const Center(
        child: CircularProgressIndicator(color: AppColors.accent),
      );
    } else if (_loiTai != null) {
      noiDung = Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            HopThongBao(_loiTai!),
            const SizedBox(height: 14),
            NutPhu(nhan: 'Thử lại', onPressed: _taiChiNhanh),
          ],
        ),
      );
    } else {
      noiDung = SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 14, 16, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _theTomTatDon(),
            const SizedBox(height: 14),
            const NhanTruong('Hình thức nhận hàng'),
            const SizedBox(height: 6),
            _chonHinhThucNhan(),
            if (_hinhThucNhan == 'GIAO_HANG') ...[
              const SizedBox(height: 12),
              DiaChiGiao(
                giaTri: _diaChiNhan,
                onChanged: (diaChi) => _doiLuaChon(() => _diaChiNhan = diaChi),
              ),
              const SizedBox(height: 8),
              const NhanTruong('Số điện thoại người nhận'),
              const SizedBox(height: 6),
              ONhap(
                controller: _oSdt,
                hint: '09xx xxx xxx',
                icon: Ico.dienThoai,
                keyboardType: TextInputType.phone,
              ),
            ],
            const SizedBox(height: 14),
            const NhanTruong('Chi nhánh in'),
            const SizedBox(height: 6),
            for (ChiNhanh cn in _dsChiNhanh!) ...[
              _theChiNhanh(cn),
              const SizedBox(height: 8),
            ],
            const SizedBox(height: 4),
            const NhanTruong('Mã giảm giá'),
            const SizedBox(height: 6),
            ONhap(
              controller: _oMaGiam,
              hint: 'Nhập mã nếu có, vd MUATHI2026',
              icon: Ico.theTien,
              laMono: true,
              onChanged: (chu) => _doiLuaChon(() {}),
            ),
            if (_baoGia != null && _baoGia!.khuyenMaiHopLe == false)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: HopThongBao(
                  _baoGia!.lyDoKhuyenMai ?? 'Mã giảm giá không dùng được.',
                  loai: LoaiThongBao.canhBao,
                ),
              ),
            const SizedBox(height: 14),
            const NhanTruong('Thanh toán'),
            const SizedBox(height: 6),
            _oThanhToan(),
          ],
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(
        tieuDe: 'Chi nhánh & nhận hàng',
        phuDe: 'Bước 3 / 4 · Chọn nơi in và cách nhận',
        soBuoc: 4,
        buocHienTai: 3,
      ),
      body: noiDung,
      bottomNavigationBar: _dsChiNhanh != null && _loiTai == null
          ? ConstrainedBox(
              constraints: BoxConstraints(
                maxHeight: MediaQuery.sizeOf(context).height * .45,
              ),
              child: SingleChildScrollView(child: _bangTongTien()),
            )
          : null,
    );
  }

  // Nhắc lại khách đang đặt in cái gì (chốt ở bước 2)
  Widget _theTomTatDon() {
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 11, 12, 11),
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Container(
            width: 38,
            height: 46,
            decoration: BoxDecoration(
              color: AppColors.accentSoft,
              border: Border.all(color: AppColors.accentTint),
              borderRadius: BorderRadius.circular(7),
            ),
            child: const Center(
              child: AppIcon(
                Ico.taiLieu,
                size: 20,
                color: AppColors.accent,
                net: 1.6,
              ),
            ),
          ),
          const SizedBox(width: 11),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  widget.file.tenFileGoc,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTheme.font(size: 13, weight: FontWeight.w600),
                ),
                const SizedBox(height: 3),
                Text(
                  widget.file.laAnhThe
                      ? widget.file.moTaSoLuong
                      : '${widget.file.soTrang ?? 0} trang × ${widget.soBan} bản',
                  style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                ),
                const SizedBox(height: 2),
                Text(
                  widget.tenCauHinh,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                ),
              ],
            ),
          ),
          if (widget.hoaToc)
            const NhanNho(
              'Hỏa tốc',
              mau: AppColors.hoaTocChu,
              nen: AppColors.hoaTocNen,
            ),
        ],
      ),
    );
  }

  // 2 nút: Nhận tại quầy / Giao tận nơi
  Widget _chonHinhThucNhan() {
    return Row(
      children: [
        Expanded(
          child: _nutHinhThuc(
            'TAI_QUAY',
            Ico.cuaHang,
            'Nhận tại quầy',
            'Miễn phí',
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _nutHinhThuc(
            'GIAO_HANG',
            Ico.xeGiao,
            'Giao tận nơi',
            'Tính theo chi nhánh',
          ),
        ),
      ],
    );
  }

  Widget _nutHinhThuc(String giaTri, String icon, String nhan, String phu) {
    bool daChon = _hinhThucNhan == giaTri;
    return GestureDetector(
      onTap: () => _doiLuaChon(() => _hinhThucNhan = giaTri),
      child: Container(
        height: 66,
        padding: const EdgeInsets.symmetric(horizontal: 11),
        decoration: BoxDecoration(
          color: daChon ? AppColors.accentSoft : AppColors.trang,
          border: Border.all(
            color: daChon ? AppColors.accent : AppColors.vien,
            width: daChon ? 1.5 : 1,
          ),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Row(
          children: [
            AppIcon(
              icon,
              size: 19,
              color: daChon ? AppColors.accent : AppColors.iconMo,
              net: 1.7,
            ),
            const SizedBox(width: 9),
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    nhan,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTheme.font(
                      size: 12.5,
                      weight: FontWeight.w700,
                      color: daChon ? AppColors.accentDeep : AppColors.chu,
                    ),
                  ),
                  Text(
                    phu,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTheme.font(size: 10.5, color: AppColors.chuPhu),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  // Thẻ 1 chi nhánh. Chi nhánh không có máy in phù hợp thì làm mờ và ghi rõ lý do.
  Widget _theChiNhanh(ChiNhanh cn) {
    bool daChon = cn.maChiNhanh == _maChiNhanh;
    bool inDuoc = cn.dapUngDuoc;

    return GestureDetector(
      onTap: () {
        if (!inDuoc) {
          // Bấm vào chi nhánh không in được thì báo lý do chứ không chọn
          hienThongBaoNhanh(
            context,
            cn.lyDoKhongDapUng ??
                'Chi nhánh này không in được cấu hình bạn chọn.',
          );
          return;
        }
        _doiLuaChon(() => _maChiNhanh = cn.maChiNhanh);
      },
      child: Opacity(
        opacity: inDuoc ? 1 : 0.55,
        child: Container(
          padding: const EdgeInsets.fromLTRB(12, 11, 12, 11),
          decoration: BoxDecoration(
            color: daChon ? AppColors.accentSoft : AppColors.trang,
            border: Border.all(
              color: daChon ? AppColors.accent : AppColors.vach,
              width: daChon ? 1.5 : 1,
            ),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  AppIcon(
                    Ico.cuaHang,
                    size: 16,
                    color: inDuoc ? AppColors.accent : AppColors.iconMo,
                    net: 1.7,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      cn.tenChiNhanh,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTheme.font(size: 13, weight: FontWeight.w700),
                    ),
                  ),
                  if (daChon)
                    const AppIcon(
                      Ico.dauTich,
                      size: 15,
                      color: AppColors.accent,
                      net: 3,
                    ),
                ],
              ),
              const SizedBox(height: 5),
              Text(
                cn.diaChi,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: AppTheme.font(size: 11.5, color: AppColors.chuPhu),
              ),
              const SizedBox(height: 5),
              Row(
                children: [
                  const AppIcon(
                    Ico.dongHo,
                    size: 12,
                    color: AppColors.iconMo,
                    net: 1.9,
                  ),
                  const SizedBox(width: 5),
                  Text(
                    '${cn.gioMoCua ?? ''}–${cn.gioDongCua ?? ''}',
                    style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                  ),
                  const SizedBox(width: 10),
                  const AppIcon(
                    Ico.sao,
                    size: 12,
                    color: AppColors.cbIcon,
                    net: 1.9,
                  ),
                  const SizedBox(width: 5),
                  Text(
                    cn.soLuotDanhGia == 0
                        ? 'Chưa có đánh giá'
                        : '${cn.diemDanhGiaTB.toStringAsFixed(1)} (${cn.soLuotDanhGia})',
                    style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                  ),
                ],
              ),
              if (_hinhThucNhan == 'GIAO_HANG') ...[
                const SizedBox(height: 5),
                Text(
                  'Giao trong ${cn.banKinhGiao.toStringAsFixed(0)} km · phí theo quãng đường',
                  style: AppTheme.font(
                    size: 11,
                    weight: FontWeight.w600,
                    color: AppColors.thanhCong,
                  ),
                ),
              ],
              if (!inDuoc) ...[
                const SizedBox(height: 7),
                HopThongBao(
                  cn.lyDoKhongDapUng ?? 'Chi nhánh chưa in được cấu hình này.',
                  loai: LoaiThongBao.canhBao,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  // Đặt trên App thì LUÔN chuyển khoản VietQR trước — không cho chọn tiền mặt nữa.
  // Tiền mặt chỉ dành cho khách tới tận tiệm ngồi đợi in (nhân viên quầy lập đơn và thu tiền).
  Widget _oThanhToan() {
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 11, 12, 11),
      decoration: BoxDecoration(
        color: AppColors.accentSoft,
        border: Border.all(color: AppColors.accentTint),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          const AppIcon(
            Ico.dauTichKep,
            size: 19,
            color: AppColors.accent,
            net: 1.7,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Chuyển khoản VietQR',
                  style: AppTheme.font(
                    size: 12.5,
                    weight: FontWeight.w700,
                    color: AppColors.accentDeep,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  'Đặt qua ứng dụng thì thanh toán trước, đơn mới vào hàng đợi in. '
                  'Muốn trả tiền mặt thì tới trực tiếp chi nhánh, nhân viên in và thu tiền tại quầy.',
                  style: AppTheme.font(
                    size: 10.5,
                    color: AppColors.chuNhan,
                    height: 1.45,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  // Tấm tổng tiền cố định dưới đáy + nút Đặt đơn
  Widget _bangTongTien() {
    BaoGia? b = _baoGia;
    ChiNhanh? cn = _chiNhanhDaChon();

    String? dongHenTra;
    if (b != null && b.ngayHenTra != null) {
      int soPhut = b.ngayHenTra!.difference(DateTime.now()).inMinutes;
      if (soPhut < 1) soPhut = 1;
      dongHenTra =
          'Dự kiến in xong sau $soPhut phút — lúc ${gioPhut(b.ngayHenTra!)}';
    }

    // Chỉ cho bấm Đặt đơn khi đã báo giá xong và chọn được chi nhánh in được
    bool datDuoc =
        b != null && cn != null && cn.dapUngDuoc && !_dangTinh && !_dangDatDon;

    return Container(
      decoration: const BoxDecoration(
        color: AppColors.trang,
        border: Border(top: BorderSide(color: AppColors.vien)),
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
        boxShadow: [
          BoxShadow(
            color: Color(0x1A17140F),
            blurRadius: 28,
            offset: Offset(0, -8),
          ),
        ],
      ),
      padding: const EdgeInsets.fromLTRB(18, 8, 18, 12),
      child: SafeArea(
        top: false,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Center(
              child: Container(
                width: 38,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.vienHop,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Text(
                  'Tổng thanh toán',
                  style: AppTheme.font(size: 13, weight: FontWeight.w700),
                ),
                if (_dangTinh) ...[
                  const SizedBox(width: 8),
                  const SizedBox(
                    width: 12,
                    height: 12,
                    child: CircularProgressIndicator(
                      strokeWidth: 1.6,
                      color: AppColors.accent,
                    ),
                  ),
                ],
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    cn?.tenChiNhanh ?? '—',
                    textAlign: TextAlign.right,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: AppTheme.font(size: 10.5, color: AppColors.chuPhu),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            if (_loiBaoGia != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: HopThongBao(_loiBaoGia!),
              )
            else if (b != null) ...[
              _dongTien(
                'Tiền in',
                widget.file.laAnhThe
                    ? widget.file.moTaSoLuong
                    : '${widget.file.soTrang ?? 0} trang × ${widget.soBan} bản',
                tien(b.tienIn),
              ),
              if (b.tienGiaCong > 0)
                _dongTien('Tiền gia công', null, tien(b.tienGiaCong)),
              if (b.phuPhiGap > 0)
                _dongTien(
                  'Phụ phí in hỏa tốc',
                  null,
                  '+${tien(b.phuPhiGap)}',
                  mau: AppColors.hoaToc,
                ),
              if (b.phiShip > 0)
                _dongTien(
                  'Phí giao nội bộ',
                  b.khoangCachKm == null
                      ? null
                      : '${b.khoangCachKm!.toStringAsFixed(2)} km · khoảng ${b.phutDiChuyen ?? 0} phút di chuyển',
                  '+${tien(b.phiShip)}',
                ),
              if (b.giamGia > 0)
                _dongTien(
                  'Mã khuyến mãi',
                  null,
                  '−${tien(b.giamGia)}',
                  mau: AppColors.thanhCong,
                ),
            ] else
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 18),
                child: Center(
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: AppColors.accent,
                  ),
                ),
              ),
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 9),
              child: Divider(height: 1, color: AppColors.vach),
            ),
            Row(
              crossAxisAlignment: CrossAxisAlignment.baseline,
              textBaseline: TextBaseline.alphabetic,
              children: [
                Text(
                  'Phải trả',
                  style: AppTheme.font(size: 13, weight: FontWeight.w700),
                ),
                const Spacer(),
                Text(
                  b == null ? '—' : tien(b.tongTien),
                  style: AppTheme.mono(
                    size: 21,
                    weight: FontWeight.w800,
                    color: AppColors.accentDeep,
                    letterSpacing: -0.42,
                  ),
                ),
              ],
            ),
            if (dongHenTra != null)
              Padding(
                padding: const EdgeInsets.only(top: 5),
                child: Row(
                  children: [
                    const AppIcon(
                      Ico.dongHo,
                      size: 13,
                      color: AppColors.thanhCong,
                      net: 1.9,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        dongHenTra,
                        style: AppTheme.font(
                          size: 11.5,
                          weight: FontWeight.w600,
                          color: AppColors.thanhCong,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            if (_loiDatDon != null) ...[
              const SizedBox(height: 8),
              HopThongBao(_loiDatDon!),
            ],
            Align(
              alignment: Alignment.centerRight,
              child: TextButton.icon(
                onPressed: _dangTinh || _dangDatDon ? null : _goiBaoGia,
                icon: const Icon(Icons.refresh, size: 18),
                label: const Text('Tính lại phí'),
              ),
            ),
            const SizedBox(height: 12),
            NutChinh(
              nhan: 'Đặt đơn và chuyển khoản',
              iconSau: Ico.muiTenPhai,
              chieuCao: 48,
              dangTai: _dangDatDon,
              onPressed: datDuoc ? _datDon : null,
            ),
          ],
        ),
      ),
    );
  }

  Widget _dongTien(
    String nhan,
    String? phu,
    String giaTri, {
    Color mau = AppColors.chu,
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 5),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.baseline,
        textBaseline: TextBaseline.alphabetic,
        children: [
          Expanded(
            child: Text.rich(
              TextSpan(
                children: [
                  TextSpan(text: nhan),
                  if (phu != null)
                    TextSpan(
                      text: ' $phu',
                      style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                    ),
                ],
              ),
              maxLines: 3,
              overflow: TextOverflow.ellipsis,
              style: AppTheme.font(size: 12, color: AppColors.chuNhan),
            ),
          ),
          Text(
            giaTri,
            style: AppTheme.mono(
              size: 12.5,
              weight: FontWeight.w600,
              color: mau,
            ),
          ),
        ],
      ),
    );
  }
}
