// Các widget dùng chung cho nhiều màn hình (ô nhập, nút, hộp thông báo...) — vẽ theo Figma.
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../theme.dart';
import 'icons.dart';

// Nhãn in hoa phía trên ô nhập (11.5px, đậm, giãn chữ 0.05em).
class NhanTruong extends StatelessWidget {
  const NhanTruong(this.chu, {super.key});

  final String chu;

  @override
  Widget build(BuildContext context) => Text(
        chu.toUpperCase(),
        style: AppTheme.font(size: 11.5, weight: FontWeight.w700, color: AppColors.chuNhan, letterSpacing: 0.58),
      );
}

// Ô nhập theo Figma: nền trắng, viền #E0DAD1, focus viền accent 1.5px + quầng sáng 3px.
class ONhap extends StatefulWidget {
  const ONhap({
    super.key,
    required this.controller,
    this.hint,
    this.icon,
    this.tienTo,
    this.anChu = false,
    this.hopLe = false,
    this.laMono = false,
    this.keyboardType,
    this.textInputAction,
    this.autofillHints,
    this.inputFormatters,
    this.onChanged,
    this.onSubmitted,
  });

  final TextEditingController controller;
  final String? hint;
  final String? icon;

  // Ô tiền tố bên trái có vạch ngăn (vd: "+84").
  final String? tienTo;

  // Mật khẩu: che chữ + nút con mắt.
  final bool anChu;

  // Hiện dấu tích xanh khi giá trị hợp lệ.
  final bool hopLe;
  final bool laMono;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final Iterable<String>? autofillHints;
  final List<TextInputFormatter>? inputFormatters;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;

  @override
  State<ONhap> createState() => _ONhapState();
}

class _ONhapState extends State<ONhap> {
  final _focus = FocusNode();
  bool _hienChu = false;

  @override
  void initState() {
    super.initState();
    _focus.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final dangFocus = _focus.hasFocus;
    final kieuChu = widget.laMono
        ? AppTheme.mono(size: 13.5, weight: FontWeight.w500)
        : AppTheme.font(size: 13.5, weight: FontWeight.w500);

    return AnimatedContainer(
      duration: const Duration(milliseconds: 150),
      height: 50,
      decoration: BoxDecoration(
        color: AppColors.trang,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: dangFocus ? AppColors.accent : AppColors.vien, width: dangFocus ? 1.5 : 1),
        boxShadow: dangFocus ? const [BoxShadow(color: AppColors.accentSoft, spreadRadius: 3)] : null,
      ),
      child: Row(
        children: [
          if (widget.tienTo != null)
            Container(
              width: 56,
              alignment: Alignment.center,
              margin: const EdgeInsets.only(right: 10),
              decoration: const BoxDecoration(border: Border(right: BorderSide(color: Color(0xFFEDE8E0)))),
              child: Text(widget.tienTo!, style: AppTheme.font(size: 13, weight: FontWeight.w600, color: AppColors.chuNhan)),
            )
          else
            const SizedBox(width: 13),
          if (widget.icon != null) ...[
            AppIcon(widget.icon!, size: 17, color: dangFocus ? AppColors.accent : AppColors.iconMo),
            const SizedBox(width: 10),
          ],
          Expanded(
            child: TextField(
              controller: widget.controller,
              focusNode: _focus,
              obscureText: widget.anChu && !_hienChu,
              obscuringCharacter: '●',
              keyboardType: widget.keyboardType,
              textInputAction: widget.textInputAction,
              autofillHints: widget.autofillHints,
              inputFormatters: widget.inputFormatters,
              onChanged: widget.onChanged,
              onSubmitted: widget.onSubmitted,
              style: kieuChu,
              cursorColor: AppColors.accent,
              decoration: InputDecoration.collapsed(
                hintText: widget.hint,
                hintStyle: AppTheme.font(size: 13.5, weight: FontWeight.w400, color: AppColors.iconMo),
              ),
            ),
          ),
          if (widget.hopLe) ...[
            const SizedBox(width: 8),
            const AppIcon(Ico.dauTich, size: 16, color: AppColors.thanhCong, net: 2.6),
          ],
          if (widget.anChu)
            GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: () => setState(() => _hienChu = !_hienChu),
              child: Padding(
                padding: const EdgeInsets.fromLTRB(10, 8, 0, 8),
                child: AppIcon(_hienChu ? Ico.matGach : Ico.mat, size: 17, color: AppColors.chuPhu),
              ),
            ),
          const SizedBox(width: 13),
        ],
      ),
    );
  }
}

// Nút chính màu accent (cao 52, bo 13, đổ bóng xanh).
class NutChinh extends StatelessWidget {
  const NutChinh({super.key, required this.nhan, this.onPressed, this.dangTai = false, this.iconTruoc, this.iconSau, this.chieuCao = 52});

  final String nhan;
  final VoidCallback? onPressed;
  final bool dangTai;
  final String? iconTruoc;
  final String? iconSau;
  final double chieuCao;

  @override
  Widget build(BuildContext context) {
    final tatNut = onPressed == null || dangTai;
    return Container(
      height: chieuCao,
      decoration: BoxDecoration(
        color: tatNut && !dangTai ? AppColors.accent.withValues(alpha: 0.55) : AppColors.accent,
        borderRadius: BorderRadius.circular(13),
        boxShadow: const [BoxShadow(color: Color(0x3822437F), blurRadius: 12, offset: Offset(0, 4))],
      ),
      child: Material(
        type: MaterialType.transparency,
        child: InkWell(
          borderRadius: BorderRadius.circular(13),
          onTap: tatNut ? null : onPressed,
          child: Center(
            child: dangTai
                ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2.2, color: Colors.white))
                : Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (iconTruoc != null) ...[AppIcon(iconTruoc!, size: 17, color: Colors.white, net: 2.4), const SizedBox(width: 8)],
                      Flexible(
                        child: Text(nhan,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: AppTheme.font(size: 14.5, weight: FontWeight.w700, color: Colors.white)),
                      ),
                      if (iconSau != null) ...[const SizedBox(width: 8), AppIcon(iconSau!, size: 17, color: Colors.white, net: 2.2)],
                    ],
                  ),
          ),
        ),
      ),
    );
  }
}

// Nút phụ nền trắng viền xám (vd: Tiếp tục với Google).
class NutPhu extends StatelessWidget {
  const NutPhu({super.key, required this.nhan, this.icon, this.onPressed});

  final String nhan;
  final Widget? icon;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.trang,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(13), side: const BorderSide(color: AppColors.vien)),
      child: InkWell(
        borderRadius: BorderRadius.circular(13),
        onTap: onPressed,
        child: SizedBox(
          height: 52,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (icon != null) ...[icon!, const SizedBox(width: 10)],
              Flexible(
                child: Text(nhan,
                    maxLines: 1, overflow: TextOverflow.ellipsis, style: AppTheme.font(size: 13.5, weight: FontWeight.w600)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

enum LoaiThongBao { loi, canhBao, thanhCong }

// Hộp thông báo màu theo Figma (đỏ: lỗi đăng nhập, cam: cảnh báo, xanh: thành công).
class HopThongBao extends StatelessWidget {
  const HopThongBao(this.noiDung, {super.key, this.loai = LoaiThongBao.loi});

  final String noiDung;
  final LoaiThongBao loai;

  @override
  Widget build(BuildContext context) {
    // Mặc định: hộp đỏ báo lỗi
    Color nen = AppColors.loiNen;
    Color vien = AppColors.loiVien;
    Color chu = AppColors.loiChu;
    String icon = Ico.chamThan;
    if (loai == LoaiThongBao.canhBao) {
      nen = AppColors.cbNen;
      vien = AppColors.cbVien;
      chu = AppColors.cbChu;
      icon = Ico.canhBao;
    } else if (loai == LoaiThongBao.thanhCong) {
      nen = AppColors.xanhNhat;
      vien = const Color(0xFFCBE3D5);
      chu = AppColors.thanhCong;
      icon = Ico.dauTich;
    }
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(color: nen, border: Border.all(color: vien), borderRadius: BorderRadius.circular(11)),
      child: Row(
        children: [
          AppIcon(icon, size: 15, color: chu, net: 2.1),
          const SizedBox(width: 9),
          Expanded(child: Text(noiDung, style: AppTheme.font(size: 12, weight: FontWeight.w700, color: chu, height: 1.4))),
        ],
      ),
    );
  }
}

// Đường kẻ ngang có chữ ở giữa ("hoặc").
class DuongHoac extends StatelessWidget {
  const DuongHoac(this.chu, {super.key});

  final String chu;

  @override
  Widget build(BuildContext context) => Row(
        children: [
          const Expanded(child: Divider(color: AppColors.vach, height: 1)),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 11),
            child: Text(chu, style: AppTheme.font(size: 11, color: AppColors.iconMo)),
          ),
          const Expanded(child: Divider(color: AppColors.vach, height: 1)),
        ],
      );
}

// Ô vuông tick 18px (Ghi nhớ đăng nhập, Đồng ý điều khoản).
class HopChonVuong extends StatelessWidget {
  const HopChonVuong({super.key, required this.daChon, this.size = 18});

  final bool daChon;
  final double size;

  @override
  Widget build(BuildContext context) => AnimatedContainer(
        duration: const Duration(milliseconds: 120),
        width: size,
        height: size,
        decoration: BoxDecoration(
          color: daChon ? AppColors.accent : AppColors.trang,
          borderRadius: BorderRadius.circular(5),
          border: daChon ? null : Border.all(color: AppColors.vienHop, width: 1.5),
        ),
        child: daChon ? Center(child: AppIcon(Ico.dauTich, size: size * 0.6, color: Colors.white, net: 3.4)) : null,
      );
}

// Thanh tiêu đề trắng cao 52 của các màn nhiều bước (Figma DangKy / XacThucOTP / CauHinhIn).
class ThanhTieuDe extends StatelessWidget implements PreferredSizeWidget {
  const ThanhTieuDe({
    super.key,
    required this.tieuDe,
    this.phuDe,
    this.soBuoc = 0,
    this.buocHienTai = 0,
    this.coNutQuayLai = true,
    this.hanhDong,
  });

  final String tieuDe;
  final String? phuDe;
  final int soBuoc;
  final int buocHienTai;
  final bool coNutQuayLai;
  final Widget? hanhDong;

  @override
  Size get preferredSize => const Size.fromHeight(52);

  @override
  Widget build(BuildContext context) {
    // Chỉ hiện nút quay lại khi có màn phía trước để quay về
    bool quayLaiDuoc = Navigator.of(context).canPop();

    return Container(
      color: AppColors.trang,
      child: SafeArea(
        bottom: false,
        child: Container(
          height: 52,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          decoration: const BoxDecoration(border: Border(bottom: BorderSide(color: AppColors.vach))),
          child: Row(
            children: [
              if (coNutQuayLai && quayLaiDuoc) ...[
                Transform.translate(
                  offset: const Offset(-6, 0),
                  child: InkResponse(
                    radius: 22,
                    onTap: () => Navigator.of(context).pop(),
                    child: const SizedBox(
                      width: 32,
                      height: 32,
                      child: Center(child: AppIcon(Ico.quayLai, size: 20, color: AppColors.chuNhan, net: 1.8)),
                    ),
                  ),
                ),
                const SizedBox(width: 6),
              ],
              Expanded(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Chiều cao dòng cố định + 1 dòng: khung chỉ cao 52, chữ to (cài đặt máy) cũng không tràn
                    Text(tieuDe,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppTheme.font(size: 15, weight: FontWeight.w700, letterSpacing: -0.15, height: 1.25)),
                    if (phuDe != null)
                      Text(phuDe!,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: AppTheme.font(size: 11, color: AppColors.chuPhu, height: 1.25)),
                  ],
                ),
              ),
              if (soBuoc > 0)
                Row(
                  children: [
                    for (var i = 1; i <= soBuoc; i++)
                      Container(
                        width: soBuoc > 2 ? 18 : 22,
                        height: 4,
                        margin: EdgeInsets.only(left: i == 1 ? 0 : 4),
                        decoration: BoxDecoration(
                          color: i <= buocHienTai ? AppColors.accent : AppColors.vien,
                          borderRadius: BorderRadius.circular(2),
                        ),
                      ),
                  ],
                ),
              if (hanhDong != null) ...[const SizedBox(width: 10), hanhDong!],
            ],
          ),
        ),
      ),
    );
  }
}

// Khung viền nét đứt (hộp ghi chú Figma DangNhap, vùng chọn file).
class KhungNetDut extends StatelessWidget {
  const KhungNetDut({super.key, required this.child, this.mauVien = AppColors.vienNetDut, this.nen = AppColors.trang, this.banKinh = 11});

  final Widget child;
  final Color mauVien;
  final Color nen;
  final double banKinh;

  @override
  Widget build(BuildContext context) => CustomPaint(
        foregroundPainter: _NetDutPainter(mauVien, banKinh),
        child: Container(
          decoration: BoxDecoration(color: nen, borderRadius: BorderRadius.circular(banKinh)),
          child: child,
        ),
      );
}

class _NetDutPainter extends CustomPainter {
  _NetDutPainter(this.mau, this.banKinh);

  final Color mau;
  final double banKinh;

  @override
  void paint(Canvas canvas, Size size) {
    Paint but = Paint();
    but.color = mau;
    but.style = PaintingStyle.stroke;
    but.strokeWidth = 1;

    // Đường viền bo góc quanh khung
    Path duong = Path();
    duong.addRRect(RRect.fromRectAndRadius((Offset.zero & size).deflate(0.5), Radius.circular(banKinh)));

    // Đi dọc đường viền: vẽ 4px, bỏ trống 3px → nét đứt
    for (var doanDuong in duong.computeMetrics()) {
      for (double d = 0; d < doanDuong.length; d += 7) {
        canvas.drawPath(doanDuong.extractPath(d, d + 4), but);
      }
    }
  }

  @override
  bool shouldRepaint(_NetDutPainter old) => old.mau != mau || old.banKinh != banKinh;
}

// Ô logo máy in 58px bo 16 (Figma DangNhap).
class LogoApp extends StatelessWidget {
  const LogoApp({super.key, this.size = 58});

  final double size;

  @override
  Widget build(BuildContext context) => Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          color: AppColors.accent,
          borderRadius: BorderRadius.circular(size * 0.28),
          boxShadow: const [BoxShadow(color: Color(0x3D22437F), blurRadius: 16, offset: Offset(0, 6))],
        ),
        child: Center(child: AppIcon(Ico.mayIn, size: size * 0.48, color: Colors.white)),
      );
}

// Trạng thái rỗng / lỗi giữa màn hình (danh sách đơn, thông báo…).
class TrangTrong extends StatelessWidget {
  const TrangTrong({super.key, required this.icon, required this.tieuDe, this.moTa, this.nhanNut, this.onNut});

  final String icon;
  final String tieuDe;
  final String? moTa;
  final String? nhanNut;
  final VoidCallback? onNut;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 64,
                height: 64,
                decoration: BoxDecoration(
                  color: AppColors.accentSoft,
                  shape: BoxShape.circle,
                  border: Border.all(color: AppColors.accentTint),
                ),
                child: Center(child: AppIcon(icon, size: 27, color: AppColors.accent, net: 1.6)),
              ),
              const SizedBox(height: 14),
              Text(tieuDe, textAlign: TextAlign.center, style: AppTheme.font(size: 15, weight: FontWeight.w700)),
              if (moTa != null) ...[
                const SizedBox(height: 6),
                Text(moTa!,
                    textAlign: TextAlign.center, style: AppTheme.font(size: 12.5, color: AppColors.chuPhu, height: 1.5)),
              ],
              if (nhanNut != null) ...[
                const SizedBox(height: 18),
                SizedBox(width: 210, child: NutChinh(nhan: nhanNut!, onPressed: onNut, chieuCao: 44)),
              ],
            ],
          ),
        ),
      );
}

// Nhóm dòng menu nền trắng bo 14 (màn Tôi, Cài đặt, Trợ giúp).
class NhomDong extends StatelessWidget {
  const NhomDong({super.key, this.tieuDe, required this.children});

  final String? tieuDe;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (tieuDe != null)
            Padding(padding: const EdgeInsets.fromLTRB(4, 0, 4, 7), child: NhanTruong(tieuDe!)),
          Material(
            color: AppColors.trang,
            clipBehavior: Clip.antiAlias,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(14),
              side: const BorderSide(color: AppColors.vach),
            ),
            child: Column(
              children: [
                for (var i = 0; i < children.length; i++) ...[
                  if (i > 0) const Divider(height: 1, indent: 56, color: AppColors.vach),
                  children[i],
                ],
              ],
            ),
          ),
        ],
      );
}

// Một dòng menu: ô icon màu nhạt · nhãn + dòng phụ · mũi tên (hoặc widget cuối tùy chọn).
class DongMenu extends StatelessWidget {
  const DongMenu({
    super.key,
    required this.icon,
    required this.nhan,
    this.phu,
    this.cuoi,
    this.onTap,
    this.mau = AppColors.accent,
    this.mauChu = AppColors.chu,
  });

  final String icon;
  final String nhan;
  final String? phu;
  final Widget? cuoi;
  final VoidCallback? onTap;
  final Color mau;
  final Color mauChu;

  @override
  Widget build(BuildContext context) => InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(14, 12, 12, 12),
          child: Row(
            children: [
              Container(
                width: 30,
                height: 30,
                decoration: BoxDecoration(color: mau.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(9)),
                child: Center(child: AppIcon(icon, size: 16, color: mau, net: 1.9)),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(nhan, style: AppTheme.font(size: 13.5, weight: FontWeight.w600, color: mauChu)),
                    if (phu != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Text(phu!,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: AppTheme.font(size: 11.5, color: AppColors.chuPhu, height: 1.35)),
                      ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              cuoi ?? (onTap != null ? const AppIcon(Ico.chevronPhai, size: 16, color: AppColors.iconMo, net: 2) : const SizedBox()),
            ],
          ),
        ),
      );
}

// Nhãn nhỏ bo góc (trạng thái đơn, "Sắp có"…).
class NhanNho extends StatelessWidget {
  const NhanNho(this.chu, {super.key, this.mau = AppColors.chuPhu, this.nen = AppColors.nenXam});

  final String chu;
  final Color mau;
  final Color nen;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
        decoration: BoxDecoration(color: nen, borderRadius: BorderRadius.circular(6)),
        child: Text(chu, style: AppTheme.font(size: 10.5, weight: FontWeight.w700, color: mau)),
      );
}

// Hiện 1 dòng thông báo ngắn dưới đáy màn hình (SnackBar)
void hienThongBaoNhanh(BuildContext context, String noiDung) {
  ScaffoldMessenger.of(context).hideCurrentSnackBar();
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(content: Text(noiDung, style: AppTheme.font(size: 13, color: Colors.white))),
  );
}
