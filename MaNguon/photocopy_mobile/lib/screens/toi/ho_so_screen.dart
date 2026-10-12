import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../api.dart';
import '../../models.dart';
import '../../phien.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';

// F-03: sửa họ tên, số điện thoại. Email là tên đăng nhập nên chỉ xem.
class HoSoScreen extends StatefulWidget {
  const HoSoScreen({super.key});

  @override
  State<HoSoScreen> createState() => _HoSoScreenState();
}

class _HoSoScreenState extends State<HoSoScreen> {
  // Điền sẵn thông tin đang có
  final TextEditingController _hoTen = TextEditingController(text: Phien.nguoiDung?.hoTen ?? '');
  final TextEditingController _sdt = TextEditingController(text: Phien.nguoiDung?.soDienThoai ?? '');
  bool _dangLuu = false;
  String? _loi;

  @override
  void dispose() {
    _hoTen.dispose();
    _sdt.dispose();
    super.dispose();
  }

  Future<void> _luu() async {
    NguoiDung nd = Phien.nguoiDung!;
    String hoTen = _hoTen.text.trim();
    String sdt = _sdt.text.trim();

    if (hoTen.isEmpty) {
      setState(() => _loi = 'Vui lòng nhập họ và tên.');
      return;
    }
    if (sdt.isNotEmpty && !RegExp(r'^0\d{9}$').hasMatch(sdt)) {
      setState(() => _loi = 'Số điện thoại phải gồm 10 chữ số, bắt đầu bằng 0.');
      return;
    }
    if (sdt.isEmpty && !nd.laShipper) {
      setState(() => _loi = 'Vui lòng nhập số điện thoại để chi nhánh liên hệ khi giao / nhận tài liệu.');
      return;
    }

    FocusScope.of(context).unfocus();
    setState(() {
      _dangLuu = true;
      _loi = null;
    });

    // PUT /api/auth/me — API ghi đè cả 3 cột nên gửi lại địa chỉ đang có
    KetQuaApi kq = await Api.put('/api/auth/me', {
      'hoTen': hoTen,
      'soDienThoai': sdt.isEmpty ? null : sdt,
      'diaChiMacDinh': nd.diaChiMacDinh,
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
    hienThongBaoNhanh(context, 'Đã lưu thông tin cá nhân.');
    Navigator.pop(context);
  }

  @override
  Widget build(BuildContext context) {
    NguoiDung? nd = Phien.nguoiDung;
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(tieuDe: 'Thông tin cá nhân', phuDe: 'Cập nhật hồ sơ của bạn'),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 22, 20, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Center(
              child: Container(
                width: 72,
                height: 72,
                decoration: BoxDecoration(
                  color: AppColors.accent,
                  borderRadius: BorderRadius.circular(22),
                  boxShadow: const [BoxShadow(color: Color(0x3322437F), blurRadius: 14, offset: Offset(0, 5))],
                ),
                child: Center(
                  child: Text(nd?.vietTat ?? '?', style: AppTheme.font(size: 24, weight: FontWeight.w800, color: Colors.white)),
                ),
              ),
            ),
            const SizedBox(height: 10),
            Text(nd?.tenVaiTroHienThi ?? '', textAlign: TextAlign.center, style: AppTheme.font(size: 12, color: AppColors.chuPhu)),
            const SizedBox(height: 20),
            if (_loi != null) ...[HopThongBao(_loi!), const SizedBox(height: 14)],
            const NhanTruong('Họ và tên'),
            const SizedBox(height: 6),
            ONhap(
              controller: _hoTen,
              icon: Ico.nguoi,
              hint: 'Nguyễn Văn A',
              textInputAction: TextInputAction.next,
              autofillHints: const [AutofillHints.name],
              onChanged: (chu) => setState(() => _loi = null),
            ),
            const SizedBox(height: 14),
            const NhanTruong('Số điện thoại'),
            const SizedBox(height: 6),
            ONhap(
              controller: _sdt,
              icon: Ico.dienThoai,
              hint: '0901234567',
              hopLe: RegExp(r'^0\d{9}$').hasMatch(_sdt.text.trim()),
              keyboardType: TextInputType.phone,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(10)],
              onChanged: (chu) => setState(() => _loi = null),
              onSubmitted: (chu) => _luu(),
            ),
            const SizedBox(height: 5),
            Text('Dùng được để đăng nhập thay cho email.', style: AppTheme.font(size: 11, color: AppColors.chuPhu)),
            const SizedBox(height: 14),
            const NhanTruong('Email đăng nhập'),
            const SizedBox(height: 6),
            Container(
              height: 50,
              padding: const EdgeInsets.symmetric(horizontal: 13),
              decoration: BoxDecoration(
                color: AppColors.nenOTrong,
                border: Border.all(color: AppColors.vien),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Row(
                children: [
                  const AppIcon(Ico.thu, size: 17, color: AppColors.iconMo),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(nd?.email ?? '',
                        overflow: TextOverflow.ellipsis, style: AppTheme.font(size: 13.5, color: AppColors.chuNhan)),
                  ),
                  const AppIcon(Ico.khoa, size: 15, color: AppColors.iconMo),
                ],
              ),
            ),
            const SizedBox(height: 5),
            Text('Email là tên đăng nhập nên không tự đổi được.', style: AppTheme.font(size: 11, color: AppColors.chuPhu)),
            if (nd != null && nd.laShipper && nd.tenChiNhanh != null) ...[
              const SizedBox(height: 14),
              const NhanTruong('Chi nhánh công tác'),
              const SizedBox(height: 6),
              Text(nd.tenChiNhanh!, style: AppTheme.font(size: 13.5, weight: FontWeight.w600)),
            ],
            const SizedBox(height: 24),
            NutChinh(nhan: 'Lưu thay đổi', iconTruoc: Ico.dauTich, dangTai: _dangLuu, onPressed: _luu),
          ],
        ),
      ),
    );
  }
}
