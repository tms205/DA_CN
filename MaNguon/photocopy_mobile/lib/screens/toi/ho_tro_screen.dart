import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../api.dart';
import '../../models.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';

// Trợ giúp: danh bạ chi nhánh (đọc từ /api/danh-muc/chi-nhanh) + câu hỏi thường gặp.
class HoTroScreen extends StatefulWidget {
  const HoTroScreen({super.key});

  @override
  State<HoTroScreen> createState() => _HoTroScreenState();
}

class _HoTroScreenState extends State<HoTroScreen> {
  List<ChiNhanh>? _dsChiNhanh;   // null = đang tải
  String? _loi;

  @override
  void initState() {
    super.initState();
    _taiChiNhanh();
  }

  Future<void> _taiChiNhanh() async {
    KetQuaApi kq = await Api.get('/api/danh-muc/chi-nhanh');
    if (!mounted) return;
    if (!kq.thanhCong) {
      setState(() {
        _loi = kq.loi;
        _dsChiNhanh = [];
      });
      return;
    }
    List<ChiNhanh> ds = [];
    for (var json in kq.duLieu) {
      ds.add(ChiNhanh.fromJson(json));
    }
    setState(() => _dsChiNhanh = ds);
  }

  @override
  Widget build(BuildContext context) {
    Widget phanChiNhanh;
    if (_dsChiNhanh == null) {
      phanChiNhanh = const Padding(
        padding: EdgeInsets.all(24),
        child: Center(child: CircularProgressIndicator(color: AppColors.accent)),
      );
    } else if (_loi != null) {
      phanChiNhanh = HopThongBao(_loi!);
    } else {
      phanChiNhanh = Column(
        children: [
          for (ChiNhanh cn in _dsChiNhanh!) ...[_theChiNhanh(cn), const SizedBox(height: 10)],
        ],
      );
    }

    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(tieuDe: 'Trợ giúp & liên hệ', phuDe: 'Hotline chi nhánh, câu hỏi thường gặp'),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
        children: [
          const Padding(padding: EdgeInsets.fromLTRB(4, 0, 4, 7), child: NhanTruong('Chi nhánh')),
          phanChiNhanh,
          const SizedBox(height: 8),
          const Padding(padding: EdgeInsets.fromLTRB(4, 0, 4, 7), child: NhanTruong('Câu hỏi thường gặp')),
          Material(
            color: AppColors.trang,
            clipBehavior: Clip.antiAlias,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14), side: const BorderSide(color: AppColors.vach)),
            child: Column(
              children: [
                _cauHoi(
                  'Vì sao số trang trên app khác số trang tôi thấy?',
                  'File Word, Excel, PowerPoint và ảnh được máy chủ chuyển sang PDF trước khi đếm trang, nên số trang tính theo '
                      'bản PDF — đúng bằng số trang sẽ in. Muốn chắc chắn, hãy tự xuất file sang PDF rồi tải lên.',
                  false,
                ),
                _cauHoi(
                  'Bao lâu thì in xong?',
                  'Ngay ở bước cấu hình in, app hiện giờ in xong dự kiến tính theo tốc độ máy in và hàng đợi của '
                      'chi nhánh. Bật In hỏa tốc để đơn được đưa lên đầu hàng đợi.',
                  false,
                ),
                _cauHoi(
                  'Làm sao để được giảm giá?',
                  'Nhập mã khuyến mãi ở bước xác nhận đơn. Mã giảm theo phần trăm tiền hàng, có mức giảm tối đa '
                      'và giá trị đơn tối thiểu tùy từng chương trình.',
                  false,
                ),
                _cauHoi(
                  'File của tôi có bị lưu lại không?',
                  'File chỉ lưu tạm trên máy chủ để in, không có đường dẫn công khai và bị xóa sau khi đơn in xong.',
                  false,
                ),
                _cauHoi(
                  'Tôi quên mật khẩu thì làm sao?',
                  'Khách hàng bấm "Quên mật khẩu?" ở màn đăng nhập để nhận mã OTP qua email. '
                      'Shipper liên hệ Quản lý chi nhánh để được cấp mật khẩu tạm, đăng nhập xong vào Tôi → Đổi mật khẩu.',
                  true,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _theChiNhanh(ChiNhanh cn) {
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
      decoration: BoxDecoration(
        color: AppColors.trang,
        border: Border.all(color: AppColors.vach),
        borderRadius: BorderRadius.circular(13),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const AppIcon(Ico.cuaHang, size: 17, color: AppColors.accent, net: 1.8),
              const SizedBox(width: 8),
              Expanded(child: Text(cn.tenChiNhanh, style: AppTheme.font(size: 13.5, weight: FontWeight.w700))),
              if (cn.soLuotDanhGia > 0) ...[
                const AppIcon(Ico.sao, size: 13, color: AppColors.hoaToc, net: 2),
                const SizedBox(width: 3),
                Text('${cn.diemDanhGiaTB.toStringAsFixed(1)} (${cn.soLuotDanhGia})',
                    style: AppTheme.font(size: 11.5, weight: FontWeight.w600, color: AppColors.chuNhan)),
              ],
            ],
          ),
          const SizedBox(height: 7),
          _dongThongTin(Ico.ghim, cn.diaChi),
          if (cn.gioMoCua != null && cn.gioDongCua != null) _dongThongTin(Ico.dongHo, 'Mở cửa ${cn.gioMoCua} – ${cn.gioDongCua} hằng ngày'),
          const SizedBox(height: 6),
          Row(
            children: [
              Flexible(
                child: NhanNho('Giao trong ${cn.banKinhGiao.toStringAsFixed(0)} km · phí tính theo Ahamove',
                    mau: AppColors.thanhCong, nen: AppColors.xanhNhat),
              ),
              const Spacer(),
              if (cn.soDienThoai != null)
                TextButton.icon(
                  style: TextButton.styleFrom(visualDensity: VisualDensity.compact),
                  onPressed: () => _chepSoDienThoai(cn),
                  icon: const AppIcon(Ico.dienThoai, size: 14, color: AppColors.accent, net: 1.9),
                  label: Text(cn.soDienThoai!, style: AppTheme.mono(size: 12.5, weight: FontWeight.w600, color: AppColors.accent)),
                ),
            ],
          ),
        ],
      ),
    );
  }

  // Bấm số điện thoại → chép vào bộ nhớ tạm của máy
  Future<void> _chepSoDienThoai(ChiNhanh cn) async {
    await Clipboard.setData(ClipboardData(text: cn.soDienThoai!));
    if (!mounted) return;
    hienThongBaoNhanh(context, 'Đã chép số ${cn.soDienThoai} của ${cn.tenChiNhanh}.');
  }

  Widget _dongThongTin(String icon, String chu) {
    return Padding(
      padding: const EdgeInsets.only(top: 3),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(padding: const EdgeInsets.only(top: 1), child: AppIcon(icon, size: 13, color: AppColors.iconMo, net: 1.9)),
          const SizedBox(width: 7),
          Expanded(child: Text(chu, style: AppTheme.font(size: 12, color: AppColors.chuNhan, height: 1.4))),
        ],
      ),
    );
  }

  // Một câu hỏi bấm vào thì mở ra câu trả lời
  Widget _cauHoi(String hoi, String dap, bool cuoiCung) {
    return Container(
      decoration: cuoiCung ? null : const BoxDecoration(border: Border(bottom: BorderSide(color: AppColors.vach))),
      child: Theme(
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          tilePadding: const EdgeInsets.symmetric(horizontal: 14),
          childrenPadding: const EdgeInsets.fromLTRB(14, 0, 14, 14),
          iconColor: AppColors.accent,
          collapsedIconColor: AppColors.iconMo,
          title: Text(hoi, style: AppTheme.font(size: 13, weight: FontWeight.w600)),
          children: [
            Align(
              alignment: Alignment.centerLeft,
              child: Text(dap, style: AppTheme.font(size: 12.5, color: AppColors.chuNhan, height: 1.55)),
            ),
          ],
        ),
      ),
    );
  }
}
