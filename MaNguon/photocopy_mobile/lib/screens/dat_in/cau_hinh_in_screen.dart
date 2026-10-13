import 'dart:async';

import 'package:flutter/material.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../models.dart';
import '../../quy_cach_in.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'chon_chi_nhanh_screen.dart';

// Một lựa chọn trong nút chọn "viên thuốc" (Mặt in, Màu sắc)
class LuaChon {
  String giaTri; // 2_MAT, DEN_TRANG...
  String nhan; // chữ hiện trên nút
  String? phu; // dòng chữ nhỏ bên dưới (có thể không có)
  bool coSan; // bảng giá có dòng này không; không có thì nút mờ đi
  String lyDo; // câu báo khi bấm vào lựa chọn không có giá

  LuaChon(this.giaTri, this.nhan, this.phu, this.coSan, this.lyDo);
}

// F-07 / F-08 — Figma 01-Mobile-CauHinhIn. Bước 2/4: chọn quy cách in, báo giá tạm tính tự cập nhật.
// Mọi con số tiền lấy từ thủ tục sp_BaoGia trên máy chủ — App không tự tính tiền.
class CauHinhInScreen extends StatefulWidget {
  const CauHinhInScreen({super.key, required this.file});

  final FileIn file;

  @override
  State<CauHinhInScreen> createState() => _CauHinhInScreenState();
}

class _CauHinhInScreenState extends State<CauHinhInScreen> {
  // Dữ liệu tải từ API
  List<DichVu> _bangGia = [];
  List<ChiNhanh> _dsChiNhanh = [];
  bool _dangTaiBangGia = true;
  String? _loiTaiBangGia;

  // Lựa chọn của khách
  String? _giay; // "A4 70gsm"
  String _matIn = '2_MAT';
  String _huongGiay = 'DUNG';
  String? _thongBaoGiaCong;
  String _mauSac = 'DEN_TRANG';
  int _soBan = 1;
  final List<int> _dsGiaCong =
      []; // các mã dịch vụ gia công đã tick (chọn được nhiều), rỗng = không gia công
  bool _hoaToc = false;
  String? _ghiChu;

  // Báo giá
  BaoGia? _baoGia;
  String? _loiBaoGia;
  bool _dangTinh = false;
  Timer? _hen; // hẹn giờ gọi báo giá
  int _lanGoi = 0; // đánh số lần gọi báo giá

  @override
  void initState() {
    super.initState();
    _taiBangGia();
  }

  @override
  void dispose() {
    _hen?.cancel();
    super.dispose();
  }

  // Tải chi nhánh trước để lấy bảng giá tạm tính của chi nhánh đầu tiên.
  Future<void> _taiBangGia() async {
    setState(() {
      _dangTaiBangGia = true;
      _loiTaiBangGia = null;
    });

    KetQuaApi kqChiNhanh = await Api.get('/api/danh-muc/chi-nhanh');
    if (!mounted) return;

    if (!kqChiNhanh.thanhCong) {
      setState(() {
        _loiTaiBangGia = kqChiNhanh.loi;
        _dangTaiBangGia = false;
      });
      return;
    }

    List<ChiNhanh> dsChiNhanh = [];
    for (var cn in kqChiNhanh.duLieu) {
      dsChiNhanh.add(ChiNhanh.fromJson(cn));
    }
    if (dsChiNhanh.isEmpty) {
      setState(() {
        _loiTaiBangGia = 'Chưa có chi nhánh nào đang hoạt động.';
        _dangTaiBangGia = false;
      });
      return;
    }

    int maChiNhanh = dsChiNhanh[0].maChiNhanh;
    KetQuaApi kqGia = await Api.get(
      '/api/danh-muc/bang-gia?maChiNhanh=$maChiNhanh',
    );
    if (!mounted) return;
    if (!kqGia.thanhCong) {
      setState(() {
        _loiTaiBangGia = kqGia.loi;
        _dangTaiBangGia = false;
      });
      return;
    }

    List<DichVu> bangGia = [];
    for (var dong in kqGia.duLieu) {
      bangGia.add(DichVu.fromJson(dong));
    }

    setState(() {
      _bangGia = bangGia;
      _dsChiNhanh = dsChiNhanh;
      _dangTaiBangGia = false;
      _chuanHoaLuaChon();
      _kiemTraKimGoc();
    });
    _goiBaoGia();
  }

  // ---------------------------------------------------------------- Suy ra lựa chọn hợp lệ từ bảng giá

  List<DichVu> _dongIn() =>
      _bangGia.where((d) => d.hopVoiFile(widget.file)).toList();

  List<DichVu> _dongGiaCong() =>
      widget.file.laAnhThe ? [] : _bangGia.where((d) => !d.laDongIn).toList();

  // Danh sách loại giấy có trong bảng giá, A4 trước rồi A3
  List<String> _dsGiay() {
    List<String> ds = [];
    for (DichVu d in _dongIn()) {
      if (!ds.contains(d.tenGiay)) ds.add(d.tenGiay);
    }
    ds.sort((a, b) {
      if (a.startsWith('A4') && !b.startsWith('A4')) return -1;
      if (!a.startsWith('A4') && b.startsWith('A4')) return 1;
      return a.compareTo(b);
    });
    return ds;
  }

  // Bảng giá có dòng nào khớp giấy đang chọn + mặt in + màu không (null = không xét)
  bool _coDong({String? mat, String? mau}) {
    for (DichVu d in _dongIn()) {
      bool khopMat = mat == null || d.matIn == mat;
      bool khopMau = mau == null || d.mauSac == mau;
      if (d.tenGiay == _giay && khopMat && khopMau) return true;
    }
    return false;
  }

  String _tenMatIn() => _matIn == '2_MAT' ? 'in 2 mặt' : 'in 1 mặt';

  // Dòng giá ứng với lựa chọn hiện tại
  DichVu? _dongDaChon() {
    for (DichVu d in _dongIn()) {
      if (d.tenGiay == _giay && d.matIn == _matIn && d.mauSac == _mauSac) {
        return d;
      }
    }
    return null;
  }

  // Bảng giá không có đủ mọi tổ hợp (vd không có A4 in 2 mặt màu) → tự đổi sang tổ hợp có giá
  void _chuanHoaLuaChon() {
    List<String> dsGiay = _dsGiay();
    if (dsGiay.isEmpty) return;
    if (_giay == null || !dsGiay.contains(_giay)) _giay = dsGiay[0];
    if (!_coDong(mat: _matIn)) {
      _matIn = _coDong(mat: '2_MAT') ? '2_MAT' : '1_MAT';
    }
    if (!_coDong(mat: _matIn, mau: _mauSac)) {
      _mauSac = _coDong(mat: _matIn, mau: 'DEN_TRANG') ? 'DEN_TRANG' : 'MAU';
    }
  }

  // Khách đổi 1 lựa chọn: cập nhật màn hình, đợi 0,35 giây rồi mới gọi báo giá
  // (bấm "+" 5 lần liên tục thì chỉ gọi API 1 lần)
  void _doiLuaChon(VoidCallback thayDoi) {
    setState(() {
      thayDoi();
      _chuanHoaLuaChon();
      _kiemTraKimGoc();
      _baoGia = null;
      _lanGoi++; // Không nhận giá cũ trong thời gian chờ debounce.
    });
    _hen?.cancel();
    _hen = Timer(const Duration(milliseconds: 350), _goiBaoGia);
  }

  // POST /api/bao-gia
  Future<void> _goiBaoGia() async {
    if (_dsChiNhanh.isEmpty) {
      setState(() => _loiBaoGia = 'Chưa có chi nhánh nào đang hoạt động.');
      return;
    }
    DichVu? dong = _dongDaChon();
    if (dong == null) {
      setState(() {
        _baoGia = null;
        _loiBaoGia = 'Cấu hình này chưa có trong bảng giá.';
      });
      return;
    }

    _lanGoi++;
    int lanNay = _lanGoi;
    setState(() {
      _dangTinh = true;
      _loiBaoGia = null;
    });

    KetQuaApi kq = await Api.post('/api/bao-gia', {
      // Tạm tính theo chi nhánh đã dùng để tải bảng giá ở bước này.
      // Bước 3 sẽ báo giá lại theo đúng chi nhánh khách chọn.
      'maChiNhanh': _dsChiNhanh[0].maChiNhanh,
      'hinhThucNhan': 'TAI_QUAY',
      'inGap': _hoaToc,
      'fileIn': [
        {
          'maFileIn': widget.file.maFileIn,
          'maDichVu': dong.maDichVu,
          'soBanIn': _soBan,
          'huongGiay': _huongGiay,
          'dsGiaCong': _dsGiaCong, // mỗi gia công tính theo từng bản in
        },
      ],
    });

    // Khách bấm liên tục thì có nhiều lần gọi — chỉ nhận kết quả của lần gọi MỚI NHẤT
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

  // Sang bước 3 (chọn chi nhánh & đặt đơn), mang theo cấu hình khách vừa chọn
  void _sangBuoc3() {
    DichVu? dong = _dongDaChon();
    if (dong == null) return;

    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (context) => ChonChiNhanhScreen(
          file: widget.file,
          maDichVu: dong.maDichVu,
          tenCauHinh:
              '${dong.tenGiay} · ${_matIn == '2_MAT' ? '2 mặt' : '1 mặt'} · ${_mauSac == 'MAU' ? 'In màu' : 'Đen trắng'} · ${_huongGiay == 'NGANG' ? 'Giấy ngang' : 'Giấy đứng'}',
          soBan: _soBan,
          huongGiay: _huongGiay,
          dsGiaCong: _dsGiaCong,
          hoaToc: _hoaToc,
          ghiChu: _ghiChu,
        ),
      ),
    );
  }

  Future<void> _suaGhiChu() async {
    TextEditingController o = TextEditingController(text: _ghiChu);
    String? ketQua = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(
          'Ghi chú cho thợ in',
          style: AppTheme.font(size: 16, weight: FontWeight.w700),
        ),
        content: TextField(
          controller: o,
          maxLength: 255,
          maxLines: 3,
          autofocus: true,
          decoration: const InputDecoration(
            hintText: 'VD: In từ trang 1 đến 48, bìa in màu',
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, o.text.trim()),
            child: const Text('Lưu'),
          ),
        ],
      ),
    );
    if (ketQua != null && mounted) {
      setState(() => _ghiChu = ketQua.isEmpty ? null : ketQua);
    }
  }

  //Giao diện

  @override
  Widget build(BuildContext context) {
    Widget noiDung;
    if (_dangTaiBangGia) {
      noiDung = const Center(
        child: CircularProgressIndicator(color: AppColors.accent),
      );
    } else if (_loiTaiBangGia != null) {
      noiDung = Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            HopThongBao(_loiTaiBangGia!),
            const SizedBox(height: 14),
            NutPhu(nhan: 'Thử lại', onPressed: _taiBangGia),
          ],
        ),
      );
    } else {
      noiDung = Stack(
        children: [
          SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(16, 14, 16, 310),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _theFile(),
                const SizedBox(height: 12),
                if (widget.file.laAnhThe)
                  Text(
                    '${widget.file.moTaSoLuong}\nGiấy ảnh A4 · in màu một mặt · giá theo ảnh. Số ảnh đã ghép, không nhân số bản hay gia công.',
                    style: AppTheme.font(size: 14, height: 1.5),
                  )
                else ...[
                  const NhanTruong('Khổ giấy & loại giấy'),
                  const SizedBox(height: 6),
                  SizedBox(
                    height: 42,
                    child: ListView(
                      scrollDirection: Axis.horizontal,
                      children: [
                        for (String g in _dsGiay()) ...[
                          _chipGiay(g),
                          const SizedBox(width: 7),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  const NhanTruong('Mặt in'),
                  const SizedBox(height: 6),
                  _chonDoan(
                    [
                      LuaChon(
                        '2_MAT',
                        'In 2 mặt',
                        'tiết kiệm 50% giấy',
                        _coDong(mat: '2_MAT'),
                        'Bảng giá chưa có $_giay in 2 mặt.',
                      ),
                      LuaChon(
                        '1_MAT',
                        'In 1 mặt',
                        null,
                        _coDong(mat: '1_MAT'),
                        'Bảng giá chưa có $_giay in 1 mặt.',
                      ),
                    ],
                    _matIn,
                    (giaTri) => _doiLuaChon(() => _matIn = giaTri),
                  ),
                  const SizedBox(height: 12),
                  const NhanTruong('Hướng giấy'),
                  const SizedBox(height: 6),
                  _chonDoan(
                    [
                      LuaChon('DUNG', 'Giấy đứng', null, true, ''),
                      LuaChon('NGANG', 'Giấy ngang', null, true, ''),
                    ],
                    _huongGiay,
                    (giaTri) => _doiLuaChon(() => _huongGiay = giaTri),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            const NhanTruong('Màu sắc'),
                            const SizedBox(height: 6),
                            _chonDoan(
                              [
                                LuaChon(
                                  'DEN_TRANG',
                                  'Đen trắng',
                                  null,
                                  _coDong(mat: _matIn, mau: 'DEN_TRANG'),
                                  'Bảng giá chưa có $_giay ${_tenMatIn()} đen trắng.',
                                ),
                                LuaChon(
                                  'MAU',
                                  'In màu',
                                  null,
                                  _coDong(mat: _matIn, mau: 'MAU'),
                                  'Bảng giá chưa có $_giay ${_tenMatIn()} màu.',
                                ),
                              ],
                              _mauSac,
                              (giaTri) => _doiLuaChon(() => _mauSac = giaTri),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 10),
                      SizedBox(
                        width: 128,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            const NhanTruong('Số bản in'),
                            const SizedBox(height: 6),
                            _boDem(),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  const NhanTruong('Gia công đi kèm'),
                  const SizedBox(height: 6),
                  _luoiGiaCong(),
                  const SizedBox(height: 8),
                  Text(
                    'Kim góc: tối đa 50 tờ/bản (${soToMoiBan(widget.file.soTrang ?? 0, _matIn)} tờ hiện tại). Bìa kiếng và ép plastic có thể đi cùng lò xo; lò xo không chọn cùng kim góc.',
                    style: AppTheme.font(size: 12, color: AppColors.chuPhu),
                  ),
                  if (_thongBaoGiaCong != null || !_kimGocHopLe())
                    Padding(
                      padding: const EdgeInsets.only(top: 6),
                      child: Text(
                        _thongBaoGiaCong ??
                            'File vượt 50 tờ/bản, không thể bấm kim góc.',
                        style: AppTheme.font(
                          size: 12,
                          color: AppColors.accentDeep,
                        ),
                      ),
                    ),
                  const SizedBox(height: 12),
                ],
                _theHoaToc(),
              ],
            ),
          ),
          Positioned(left: 0, right: 0, bottom: 0, child: _bangBaoGia()),
        ],
      );
    }

    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(
        tieuDe: 'Cấu hình in ấn',
        phuDe: 'Bước 2 / 4 · Chọn quy cách',
        soBuoc: 4,
        buocHienTai: 2,
      ),
      body: noiDung,
    );
  }

  // Thẻ thông tin file đã tải lên + nút thêm ghi chú
  Widget _theFile() {
    FileIn f = widget.file;
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
                  f.tenFileGoc,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTheme.font(size: 13, weight: FontWeight.w600),
                ),
                const SizedBox(height: 3),
                Row(
                  children: [
                    Text(
                      f.moTaSoLuong,
                      style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                    ),
                    Text(
                      '  ·  ',
                      style: AppTheme.font(size: 11, color: AppColors.vienHop),
                    ),
                    Text(
                      dungLuong(f.dungLuong),
                      style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                    ),
                    const SizedBox(width: 6),
                    const AppIcon(
                      Ico.dauTich,
                      size: 11,
                      color: AppColors.thanhCong,
                      net: 3,
                    ),
                    const SizedBox(width: 3),
                    Flexible(
                      child: Text(
                        f.daChuyenPdf ? 'Đã chuẩn hóa PDF' : 'Sẵn sàng in',
                        overflow: TextOverflow.ellipsis,
                        style: AppTheme.font(
                          size: 11,
                          weight: FontWeight.w600,
                          color: AppColors.thanhCong,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 3),
                GestureDetector(
                  onTap: _suaGhiChu,
                  child: Text(
                    _ghiChu == null
                        ? '+ Thêm ghi chú cho thợ in'
                        : 'Ghi chú: $_ghiChu',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTheme.font(
                      size: 10.5,
                      color: _ghiChu == null
                          ? AppColors.accent
                          : AppColors.chuPhu,
                      weight: _ghiChu == null
                          ? FontWeight.w600
                          : FontWeight.w500,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  // Chip chọn loại giấy
  Widget _chipGiay(String tenGiay) {
    bool daChon = tenGiay == _giay;
    return GestureDetector(
      onTap: () => _doiLuaChon(() => _giay = tenGiay),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 120),
        height: 42,
        padding: const EdgeInsets.symmetric(horizontal: 13),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: daChon ? AppColors.accentSoft : AppColors.trang,
          border: Border.all(
            color: daChon ? AppColors.accent : AppColors.vien,
            width: daChon ? 1.5 : 1,
          ),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Text(
          tenGiay,
          style: AppTheme.font(
            size: 12.5,
            weight: daChon ? FontWeight.w600 : FontWeight.w500,
            color: daChon ? AppColors.accentDeep : AppColors.chuNhan,
          ),
        ),
      ),
    );
  }

  // Nút chọn dạng "viên thuốc" nền xám, lựa chọn đang chọn nền trắng đổ bóng (Figma).
  // Lựa chọn không có trong bảng giá thì mờ đi; bấm vào thì báo lý do để khách không tưởng app bị lỗi.
  Widget _chonDoan(
    List<LuaChon> dsLuaChon,
    String dangChon,
    void Function(String giaTri) khiChon,
  ) {
    List<Widget> dsNut = [];
    for (int i = 0; i < dsLuaChon.length; i++) {
      LuaChon lc = dsLuaChon[i];
      bool laDangChon = lc.giaTri == dangChon;
      if (i > 0) dsNut.add(const SizedBox(width: 3));

      dsNut.add(
        Expanded(
          child: GestureDetector(
            onTap: () {
              if (!lc.coSan) {
                hienThongBaoNhanh(context, lc.lyDo);
              } else if (!laDangChon) {
                khiChon(lc.giaTri);
              }
            },
            child: Opacity(
              opacity: lc.coSan ? 1 : 0.4,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 120),
                alignment: Alignment.center,
                decoration: laDangChon
                    ? BoxDecoration(
                        color: AppColors.trang,
                        borderRadius: BorderRadius.circular(8),
                        boxShadow: const [
                          BoxShadow(
                            color: Color(0x1A17140F),
                            blurRadius: 2,
                            offset: Offset(0, 1),
                          ),
                        ],
                      )
                    : null,
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Text(
                      lc.nhan,
                      style: AppTheme.font(
                        size: 12.5,
                        height: 1.1,
                        weight: laDangChon ? FontWeight.w700 : FontWeight.w500,
                        color: laDangChon
                            ? AppColors.accentDeep
                            : AppColors.chuPhu,
                      ),
                    ),
                    if (lc.phu != null && laDangChon)
                      Text(
                        lc.phu!,
                        style: AppTheme.font(
                          size: 9.5,
                          color: AppColors.thanhCong,
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),
        ),
      );
    }

    return Container(
      height: 60,
      padding: const EdgeInsets.all(3),
      decoration: BoxDecoration(
        color: AppColors.nenXam,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(11),
      ),
      child: Row(children: dsNut),
    );
  }

  // Bộ đếm số bản in: nút trừ · số · nút cộng
  Widget _boDem() {
    return Container(
      height: 44,
      padding: const EdgeInsets.symmetric(horizontal: 6),
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vien),
        borderRadius: BorderRadius.circular(11),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          _nutDem(
            Ico.tru,
            AppColors.nenXam,
            AppColors.chuNhan,
            _soBan > 1 ? () => _doiLuaChon(() => _soBan--) : null,
          ),
          Text(
            '$_soBan',
            style: AppTheme.font(size: 16, weight: FontWeight.w700),
          ),
          _nutDem(
            Ico.cong,
            AppColors.accent,
            Colors.white,
            _soBan < 999 ? () => _doiLuaChon(() => _soBan++) : null,
          ),
        ],
      ),
    );
  }

  Widget _nutDem(String icon, Color nen, Color mau, VoidCallback? khiBam) {
    return GestureDetector(
      onTap: khiBam,
      child: Opacity(
        opacity: khiBam == null ? 0.4 : 1,
        child: Container(
          width: 32,
          height: 32,
          decoration: BoxDecoration(
            color: nen,
            borderRadius: BorderRadius.circular(8),
          ),
          child: Center(child: AppIcon(icon, size: 16, color: mau, net: 2.2)),
        ),
      ),
    );
  }

  // Lưới dịch vụ gia công, mỗi hàng 2 ô
  Widget _luoiGiaCong() {
    List<DichVu> ds = _dongGiaCong();
    List<Widget> dsHang = [];
    for (int i = 0; i < ds.length; i += 2) {
      dsHang.add(
        Padding(
          padding: EdgeInsets.only(top: i == 0 ? 0 : 7),
          child: Row(
            children: [
              Expanded(child: _oGiaCong(ds[i])),
              const SizedBox(width: 7),
              Expanded(
                child: i + 1 < ds.length
                    ? _oGiaCong(ds[i + 1])
                    : const SizedBox(),
              ),
            ],
          ),
        ),
      );
    }
    return Column(children: dsHang);
  }

  bool _kimGocHopLe() =>
      widget.file.soTrang != null &&
      widget.file.soTrang! > 0 &&
      soToMoiBan(widget.file.soTrang!, _matIn) <= 50;

  void _kiemTraKimGoc() {
    if (_kimGocHopLe()) return;
    for (final dv in _dongGiaCong().where((dv) => laKimGoc(dv))) {
      if (_dsGiaCong.remove(dv.maDichVu)) {
        _thongBaoGiaCong = 'Đã bỏ bấm kim góc vì vượt 50 tờ giấy mỗi bản.';
      }
    }
  }

  // Gia công cùng nhóm và cặp lò xo/kim góc loại trừ nhau.
  void _bamGiaCong(DichVu dv) {
    if (_dsGiaCong.contains(dv.maDichVu)) {
      _doiLuaChon(() => _dsGiaCong.remove(dv.maDichVu));
      return;
    }

    if (laKimGoc(dv) && !_kimGocHopLe()) {
      hienThongBaoNhanh(
        context,
        'Kim góc chỉ áp dụng tối đa 50 tờ giấy mỗi bản.',
      );
      return;
    }
    final biBo = _dongGiaCong()
        .where((d) => _dsGiaCong.contains(d.maDichVu) && giaCongLoaiTru(d, dv))
        .toList();
    if (biBo.isNotEmpty) {
      hienThongBaoNhanh(
        context,
        'Gia công không thể chọn cùng nhau — đã bỏ "${biBo.map((d) => d.tenDichVu).join(', ')}".',
      );
    }

    _doiLuaChon(() {
      _thongBaoGiaCong = null;
      for (final d in biBo) {
        _dsGiaCong.remove(d.maDichVu);
      }
      _dsGiaCong.add(dv.maDichVu);
    });
  }

  Widget _oGiaCong(DichVu dv) {
    bool daChon = _dsGiaCong.contains(dv.maDichVu);
    return GestureDetector(
      // Bấm lần nữa thì bỏ chọn
      onTap: () => _bamGiaCong(dv),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 120),
        constraints: const BoxConstraints(minHeight: 48),
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: 11),
        decoration: BoxDecoration(
          color: laKimGoc(dv) && !_kimGocHopLe()
              ? AppColors.nenXam
              : daChon
              ? AppColors.accentSoft
              : AppColors.trang,
          border: Border.all(
            color: daChon ? AppColors.accent : AppColors.vien,
            width: daChon ? 1.5 : 1,
          ),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Row(
          children: [
            HopChonVuong(daChon: daChon, size: 17),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                dv.tenDichVu,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: AppTheme.font(
                  size: 12,
                  height: 1.15,
                  weight: daChon ? FontWeight.w600 : FontWeight.w500,
                  color: daChon ? AppColors.accentDeep : AppColors.chuNhan,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  // Thẻ bật / tắt In hỏa tốc
  Widget _theHoaToc() {
    return GestureDetector(
      onTap: () => _doiLuaChon(() => _hoaToc = !_hoaToc),
      child: Container(
        constraints: const BoxConstraints(minHeight: 56),
        padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 8),
        decoration: BoxDecoration(
          color: AppColors.hoaTocNen,
          border: Border.all(color: AppColors.hoaTocVien, width: 1.5),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Row(
          children: [
            Container(
              width: 30,
              height: 30,
              decoration: BoxDecoration(
                color: AppColors.hoaToc,
                borderRadius: BorderRadius.circular(9),
              ),
              child: const Center(
                child: AppIcon(
                  Ico.tiaSet,
                  size: 17,
                  color: Colors.white,
                  net: 1.8,
                ),
              ),
            ),
            const SizedBox(width: 11),
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'In Hỏa Tốc / Ưu tiên',
                    style: AppTheme.font(
                      size: 12.5,
                      weight: FontWeight.w700,
                      color: AppColors.hoaTocChu,
                    ),
                  ),
                  Text(
                    'Đẩy đơn lên đầu hàng đợi in · +10.000₫',
                    style: AppTheme.font(
                      size: 10.5,
                      color: AppColors.hoaTocPhu,
                    ),
                  ),
                ],
              ),
            ),
            // Công tắc gạt
            AnimatedContainer(
              duration: const Duration(milliseconds: 150),
              width: 44,
              height: 26,
              padding: const EdgeInsets.all(3),
              alignment: _hoaToc ? Alignment.centerRight : Alignment.centerLeft,
              decoration: BoxDecoration(
                color: _hoaToc ? AppColors.hoaToc : AppColors.vienHop,
                borderRadius: BorderRadius.circular(13),
              ),
              child: Container(
                width: 20,
                height: 20,
                decoration: const BoxDecoration(
                  color: Colors.white,
                  shape: BoxShape.circle,
                  boxShadow: [
                    BoxShadow(
                      color: Color(0x38000000),
                      blurRadius: 3,
                      offset: Offset(0, 1),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  // Tấm báo giá cố định dưới đáy (Figma: nền trắng, bo 20, đổ bóng lên trên)
  Widget _bangBaoGia() {
    BaoGia? b = _baoGia;

    // Tên các gia công đang chọn, vd "Đóng bìa kiếng" + "Ép plastic" → "bìa kiếng, ép plastic"
    List<String> dsTen = [];
    for (DichVu d in _dongGiaCong()) {
      if (_dsGiaCong.contains(d.maDichVu)) {
        dsTen.add(d.tenDichVu.toLowerCase().replaceFirst('đóng ', ''));
      }
    }
    String tenGiaCong = dsTen.join(', ');

    // Dòng "Dự kiến in xong sau N phút — lúc HH:mm"
    String? dongHenTra;
    if (b != null && b.ngayHenTra != null) {
      int soPhut = b.ngayHenTra!.difference(DateTime.now()).inMinutes;
      if (soPhut < 1) soPhut = 1;
      dongHenTra =
          'Dự kiến in xong sau $soPhut phút — lúc ${gioPhut(b.ngayHenTra!)}';
    }

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
                  'Báo giá tạm tính',
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
                const Spacer(),
                Text(
                  'Tự động theo bảng giá chuỗi',
                  style: AppTheme.font(size: 10.5, color: AppColors.chuPhu),
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
                    : '${widget.file.soTrang ?? 0} trang × $_soBan bản',
                tien(b.tienIn),
              ),
              if (b.tienGiaCong > 0)
                _dongTien(
                  'Tiền gia công',
                  '$tenGiaCong × $_soBan',
                  tien(b.tienGiaCong),
                ),
              if (b.phuPhiGap > 0)
                _dongTien(
                  'Phụ phí in hỏa tốc',
                  null,
                  '+${tien(b.phuPhiGap)}',
                  mau: AppColors.hoaToc,
                ),
              if (b.giamGia > 0)
                _dongTien(
                  'Mã khuyến mãi',
                  null,
                  '−${tien(b.giamGia)}',
                  mau: AppColors.thanhCong,
                ),
              Padding(
                padding: const EdgeInsets.only(top: 5),
                child: Row(
                  children: [
                    Text(
                      'Phí giao hàng',
                      style: AppTheme.font(size: 12, color: AppColors.chuPhu),
                    ),
                    const Spacer(),
                    Text(
                      'Tính khi chọn chi nhánh & địa chỉ',
                      style: AppTheme.font(
                        size: 10.5,
                        color: AppColors.chuPhu,
                        fontStyle: FontStyle.italic,
                      ),
                    ),
                  ],
                ),
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
                  'Tạm tính',
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
            const SizedBox(height: 12),
            NutChinh(
              nhan: 'Tiếp tục chọn chi nhánh',
              iconSau: Ico.muiTenPhai,
              chieuCao: 48,
              onPressed: b == null ? null : _sangBuoc3,
            ),
          ],
        ),
      ),
    );
  }

  // Một dòng tiền: "Tiền in  63 trang × 2 bản ........ 31.500₫"
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
              maxLines: 1,
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
