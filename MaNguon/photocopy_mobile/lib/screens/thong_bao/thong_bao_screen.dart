import 'package:flutter/material.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../models.dart';
import '../../phien.dart';
import '../../realtime.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';

// Tab "Thông báo" (F-13): các thủ tục trong CSDL tự ghi bảng ThongBao khi đơn đổi trạng thái;
// ở đây hiện danh sách, chấm xanh = chưa đọc, chạm để đánh dấu đã đọc và mở tab Đơn hàng.
// Dùng chung cho Khách hàng và Shipper.
class ThongBaoScreen extends StatefulWidget {
  const ThongBaoScreen({super.key, this.moDonHang, this.khiDocThongBao});

  final VoidCallback? moDonHang;        // chạm thông báo về đơn → trang chính mở tab Đơn hàng
  final VoidCallback? khiDocThongBao;   // đọc xong → trang chính đếm lại số chưa đọc trên tab

  @override
  State<ThongBaoScreen> createState() => _ThongBaoScreenState();
}

class _ThongBaoScreenState extends State<ThongBaoScreen> {
  List<ThongBao>? _ds;   // null = đang tải
  int _soChuaDoc = 0;
  String? _loi;

  @override
  void initState() {
    super.initState();
    _taiDanhSach();
    Realtime.dangKyNghe(_khiCoTin);   // đơn đổi trạng thái là có thông báo mới
  }

  @override
  void dispose() {
    Realtime.boNghe(_khiCoTin);
    super.dispose();
  }

  void _khiCoTin(Map<String, dynamic> tin) {
    _taiDanhSach();
  }

  // GET /api/thong-bao → { soChuaDoc, danhSach }
  Future<void> _taiDanhSach() async {
    KetQuaApi kq = await Api.get('/api/thong-bao');
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() {
        _loi = kq.loi;
        _ds = [];
      });
      return;
    }

    List<ThongBao> ds = [];
    for (var json in kq.duLieu['danhSach']) {
      ds.add(ThongBao.fromJson(json));
    }
    setState(() {
      _loi = null;
      _ds = ds;
      _soChuaDoc = kq.duLieu['soChuaDoc'];
    });
  }

  // PUT /api/thong-bao/da-doc-tat-ca
  Future<void> _docHet() async {
    KetQuaApi kq = await Api.put('/api/thong-bao/da-doc-tat-ca');
    if (!mounted) return;
    if (!kq.thanhCong) {
      hienThongBaoNhanh(context, kq.loi);
      return;
    }
    await _taiDanhSach();
    if (widget.khiDocThongBao != null) widget.khiDocThongBao!();
  }

  // Chạm 1 thông báo: đánh dấu đã đọc (PUT /api/thong-bao/{id}/da-doc), thông báo về đơn thì mở tab Đơn hàng
  Future<void> _mo(ThongBao t) async {
    if (!t.daDoc) {
      KetQuaApi kq = await Api.put('/api/thong-bao/${t.maThongBao}/da-doc');
      if (!mounted) return;
      if (!kq.thanhCong) {
        hienThongBaoNhanh(context, kq.loi);
        return;
      }
      await _taiDanhSach();
      if (widget.khiDocThongBao != null) widget.khiDocThongBao!();
    }
    if (t.maDonHang != null && !Phien.laShipper && widget.moDonHang != null) widget.moDonHang!();
  }

  @override
  Widget build(BuildContext context) {
    Widget noiDung;
    if (_ds == null) {
      noiDung = const Center(child: CircularProgressIndicator(color: AppColors.accent));
    } else if (_loi != null) {
      noiDung = TrangTrong(
        icon: Ico.chamThan,
        tieuDe: 'Không tải được thông báo',
        moTa: _loi,
        nhanNut: 'Thử lại',
        onNut: () {
          setState(() => _ds = null);
          _taiDanhSach();
        },
      );
    } else if (_ds!.isEmpty) {
      noiDung = RefreshIndicator(
        color: AppColors.accent,
        onRefresh: _taiDanhSach,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: const [
            SizedBox(height: 70),
            TrangTrong(
              icon: Ico.chuong,
              tieuDe: 'Chưa có thông báo',
              moTa: 'Khi đơn được tiếp nhận, in xong, đang giao hay thanh toán thành công, thông báo sẽ hiện ở đây.',
            ),
          ],
        ),
      );
    } else {
      noiDung = RefreshIndicator(
        color: AppColors.accent,
        onRefresh: _taiDanhSach,
        child: ListView.builder(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.only(bottom: 24),
          itemCount: _ds!.length,
          itemBuilder: (context, i) => _dongThongBao(_ds![i]),
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: ThanhTieuDe(
        tieuDe: 'Thông báo',
        phuDe: _soChuaDoc > 0 ? '$_soChuaDoc thông báo chưa đọc' : 'Bạn đã đọc hết thông báo',
        coNutQuayLai: false,
        hanhDong: _soChuaDoc == 0
            ? null
            : TextButton.icon(
                onPressed: _docHet,
                icon: const AppIcon(Ico.dauTichKep, size: 16, color: AppColors.accent, net: 2),
                label: Text('Đọc hết', style: AppTheme.font(size: 12.5, weight: FontWeight.w700, color: AppColors.accent)),
              ),
      ),
      body: noiDung,
    );
  }

  Widget _dongThongBao(ThongBao t) {
    // Icon + màu theo loại thông báo
    String icon = Ico.donHang;
    Color mau = AppColors.accent;
    if (t.loaiThongBao == 'HE_THONG') {
      icon = Ico.thongTin;
      mau = AppColors.chuPhu;
    }

    String dongCuoi = thoiGianTuongDoi(t.ngayTao);
    if (t.maDonHangHienThi != null) dongCuoi += ' · ${t.maDonHangHienThi}';

    return Material(
      color: t.daDoc ? AppColors.nen : const Color(0xFFF3F5FA), // chưa đọc thì nền xanh nhạt
      child: InkWell(
        onTap: () => _mo(t),
        child: Container(
          padding: const EdgeInsets.fromLTRB(16, 13, 16, 13),
          decoration: const BoxDecoration(border: Border(bottom: BorderSide(color: AppColors.vach))),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(color: mau.withValues(alpha: 0.11), shape: BoxShape.circle),
                child: Center(child: AppIcon(icon, size: 18, color: mau, net: 1.8)),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(t.tieuDe, style: AppTheme.font(size: 13.5, weight: t.daDoc ? FontWeight.w600 : FontWeight.w800)),
                        ),
                        if (!t.daDoc)
                          Container(
                            width: 8,
                            height: 8,
                            margin: const EdgeInsets.only(left: 8),
                            decoration: const BoxDecoration(color: AppColors.accent, shape: BoxShape.circle),
                          ),
                      ],
                    ),
                    const SizedBox(height: 3),
                    Text(t.noiDung, style: AppTheme.font(size: 12.5, color: AppColors.chuNhan, height: 1.45)),
                    const SizedBox(height: 5),
                    Text(dongCuoi, style: AppTheme.font(size: 11, color: AppColors.chuPhu)),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
