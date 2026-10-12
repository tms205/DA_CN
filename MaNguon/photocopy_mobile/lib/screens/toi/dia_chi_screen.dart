import 'package:flutter/material.dart';

import '../../api.dart';
import '../../models.dart';
import '../../phien.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';

// F-03: địa chỉ nhận hàng mặc định (cột NguoiDung.DiaChi) — bước 3 đặt đơn sẽ tự điền khi chọn Giao tận nơi.
class DiaChiScreen extends StatefulWidget {
  const DiaChiScreen({super.key});

  @override
  State<DiaChiScreen> createState() => _DiaChiScreenState();
}

class _DiaChiScreenState extends State<DiaChiScreen> {
  final TextEditingController _diaChi = TextEditingController(text: Phien.nguoiDung?.diaChiMacDinh ?? '');
  final FocusNode _focus = FocusNode();
  bool _dangLuu = false;
  String? _loi;

  @override
  void initState() {
    super.initState();
    _focus.addListener(() => setState(() {})); // bấm vào ô thì viền đổi màu
  }

  @override
  void dispose() {
    _diaChi.dispose();
    _focus.dispose();
    super.dispose();
  }

  // xoa = true: bấm "Xóa địa chỉ đã lưu"
  Future<void> _luu(bool xoa) async {
    NguoiDung nd = Phien.nguoiDung!;
    String diaChi = xoa ? '' : _diaChi.text.trim();
    if (!xoa && diaChi.length < 10) {
      setState(() => _loi = 'Vui lòng nhập đủ số nhà, đường, phường / xã, quận / huyện, tỉnh / thành.');
      return;
    }

    FocusScope.of(context).unfocus();
    setState(() {
      _dangLuu = true;
      _loi = null;
    });

    // PUT /api/auth/me — gửi lại họ tên, số điện thoại đang có, chỉ đổi địa chỉ
    KetQuaApi kq = await Api.put('/api/auth/me', {
      'hoTen': nd.hoTen,
      'soDienThoai': nd.soDienThoai,
      'diaChiMacDinh': diaChi.isEmpty ? null : diaChi,
    });
    if (!mounted) return;

    if (!kq.thanhCong) {
      setState(() {
        _loi = kq.loi;
        _dangLuu = false;
      });
      return;
    }

    await Phien.capNhatNguoiDung(NguoiDung.fromJson(kq.duLieu));
    if (!mounted) return;
    hienThongBaoNhanh(context, xoa ? 'Đã xóa địa chỉ nhận hàng.' : 'Đã lưu địa chỉ nhận hàng.');
    Navigator.pop(context);
  }

  @override
  Widget build(BuildContext context) {
    bool coDiaChi = Phien.nguoiDung?.diaChiMacDinh != null;
    bool dangFocus = _focus.hasFocus;

    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(tieuDe: 'Địa chỉ nhận hàng', phuDe: 'Dùng khi chọn Giao tận nơi'),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 18, 16, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_loi != null) ...[HopThongBao(_loi!), const SizedBox(height: 12)],
            const NhanTruong('Địa chỉ mặc định'),
            const SizedBox(height: 6),
            AnimatedContainer(
              duration: const Duration(milliseconds: 150),
              padding: const EdgeInsets.fromLTRB(13, 12, 13, 4),
              decoration: BoxDecoration(
                color: AppColors.trang,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: dangFocus ? AppColors.accent : AppColors.vien, width: dangFocus ? 1.5 : 1),
                boxShadow: dangFocus ? const [BoxShadow(color: AppColors.accentSoft, spreadRadius: 3)] : null,
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Padding(
                    padding: const EdgeInsets.only(top: 2),
                    child: AppIcon(Ico.ghim, size: 17, color: dangFocus ? AppColors.accent : AppColors.iconMo),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: TextField(
                      controller: _diaChi,
                      focusNode: _focus,
                      minLines: 3,
                      maxLines: 4,
                      maxLength: 255,
                      keyboardType: TextInputType.streetAddress,
                      style: AppTheme.font(size: 13.5, weight: FontWeight.w500, height: 1.45),
                      cursorColor: AppColors.accent,
                      onChanged: (chu) => setState(() => _loi = null),
                      decoration: InputDecoration(
                        isCollapsed: true,
                        border: InputBorder.none,
                        hintText: 'VD: 140 Lê Trọng Tấn, Tây Thạnh, Tân Phú, TP.HCM',
                        hintStyle: AppTheme.font(size: 13.5, weight: FontWeight.w400, color: AppColors.iconMo),
                        counterStyle: AppTheme.font(size: 10.5, color: AppColors.iconMo),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 14),
            Container(
              padding: const EdgeInsets.fromLTRB(12, 11, 12, 11),
              decoration: BoxDecoration(
                color: AppColors.trang,
                border: Border.all(color: AppColors.vach),
                borderRadius: BorderRadius.circular(11),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Padding(
                    padding: EdgeInsets.only(top: 1),
                    child: AppIcon(Ico.thongTin, size: 14, color: AppColors.iconMo, net: 1.9),
                  ),
                  const SizedBox(width: 9),
                  Expanded(
                    child: Text(
                      'Ghi đủ phường / xã, quận / huyện và tỉnh / thành để hệ thống tính đúng quãng đường thực tế từ chi nhánh '
                      '— phí giao hàng và việc chi nhánh có giao tới được hay không đều dựa vào quãng đường này.',
                      style: AppTheme.font(size: 11, color: AppColors.chuPhu, height: 1.55),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 22),
            NutChinh(nhan: 'Lưu địa chỉ', iconTruoc: Ico.dauTich, dangTai: _dangLuu, onPressed: () => _luu(false)),
            if (coDiaChi) ...[
              const SizedBox(height: 10),
              TextButton(
                onPressed: _dangLuu ? null : () => _luu(true),
                child: Text('Xóa địa chỉ đã lưu', style: AppTheme.font(size: 13, weight: FontWeight.w600, color: AppColors.loiChu)),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
