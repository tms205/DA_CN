import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

import '../theme.dart';

// Đường vẽ icon nét (viewBox 24×24) lấy nguyên từ Figma GiaoDien/*.dc.html.
// Dùng: AppIcon(Ico.khoa, size: 18, color: AppColors.accent)
class Ico {
  static const mayIn = '<path d="M6 9V2h12v7"/><path d="M6 18H4a2 2 0 01-2-2v-5a2 2 0 012-2h16a2 2 0 012 2v5a2 2 0 01-2 2h-2"/><path d="M6 14h12v8H6z"/>';
  static const thu = '<rect x="2" y="4" width="20" height="16" rx="2"/><path d="M2 7l10 6 10-6"/>';
  static const khoa = '<rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0110 0v4"/>';
  static const mat = '<path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/>';
  static const matGach = '<path d="M17.9 17.9A10.1 10.1 0 0112 20c-6.4 0-10-7-10-7a18.5 18.5 0 015.1-6"/><path d="M9.9 4.2A10.1 10.1 0 0112 4c6.4 0 10 7 10 7a18.5 18.5 0 01-2.2 3.2"/><path d="M9.9 9.9a3 3 0 004.2 4.2"/><path d="M2 2l20 20"/>';
  static const dauTich = '<path d="M20 6L9 17l-5-5"/>';
  static const muiTenPhai = '<path d="M5 12h14"/><path d="M13 6l6 6-6 6"/>';
  static const quayLai = '<path d="M15 18l-6-6 6-6"/>';
  static const chamThan = '<circle cx="12" cy="12" r="10"/><path d="M12 8v5"/><path d="M12 16.5v.01"/>';
  static const thongTin = '<circle cx="12" cy="12" r="10"/><path d="M12 16v-5"/><path d="M12 8v.01"/>';
  static const canhBao = '<path d="M12 9v4"/><path d="M12 17v.01"/><path d="M10.3 3.9L2 18a2 2 0 001.7 3h16.6a2 2 0 001.7-3L13.7 3.9a2 2 0 00-3.4 0z"/>';
  static const nguoi = '<path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2"/><circle cx="12" cy="7" r="4"/>';
  static const dongHo = '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>';
  static const taiLieu = '<path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><path d="M14 2v6h6"/><path d="M8 13h8"/><path d="M8 17h5"/>';
  static const tiaSet = '<path d="M13 2L4.5 13.5H11l-1 8.5 8.5-11.5H12l1-8.5z"/>';
  static const cong = '<path d="M12 5v14"/><path d="M5 12h14"/>';
  static const tru = '<path d="M5 12h14"/>';
  static const taiLen = '<path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/><path d="M17 8l-5-5-5 5"/><path d="M12 3v12"/>';
  static const dangXuat = '<path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4"/><path d="M16 17l5-5-5-5"/><path d="M21 12H9"/>';
  static const xeGiao = '<path d="M1 3h13v13H1z"/><path d="M14 8h4l3 3v5h-7z"/><circle cx="5.5" cy="18.5" r="2.5"/><circle cx="17.5" cy="18.5" r="2.5"/>';
  static const but = '<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 013 3L7 19l-4 1 1-4z"/>';

  // Thanh tab dưới + màn Tôi / Đơn hàng / Thông báo (cùng bộ nét 24×24 với Figma)
  static const donHang =
      '<path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2"/><rect x="9" y="3" width="6" height="4" rx="1"/><path d="M9 12h6"/><path d="M9 16h4"/>';
  static const chuong = '<path d="M18 8a6 6 0 00-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 01-3.4 0"/>';
  static const caiDat = '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 00.3 1.8l.1.1a2 2 0 11-2.8 2.8l-.1-.1a1.7 1.7 0 00-1.8-.3 1.7 1.7 0 00-1 1.5V21a2 2 0 11-4 0v-.1a1.7 1.7 0 00-1.1-1.5 1.7 1.7 0 00-1.8.3l-.1.1a2 2 0 11-2.8-2.8l.1-.1a1.7 1.7 0 00.3-1.8 1.7 1.7 0 00-1.5-1H3a2 2 0 110-4h.1a1.7 1.7 0 001.5-1.1 1.7 1.7 0 00-.3-1.8l-.1-.1a2 2 0 112.8-2.8l.1.1a1.7 1.7 0 001.8.3H9a1.7 1.7 0 001-1.5V3a2 2 0 114 0v.1a1.7 1.7 0 001 1.5 1.7 1.7 0 001.8-.3l.1-.1a2 2 0 112.8 2.8l-.1.1a1.7 1.7 0 00-.3 1.8V9a1.7 1.7 0 001.5 1H21a2 2 0 110 4h-.1a1.7 1.7 0 00-1.5 1z"/>';
  static const ghim = '<path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0118 0z"/><circle cx="12" cy="10" r="3"/>';
  static const dienThoai =
      '<path d="M22 16.9v3a2 2 0 01-2.2 2 19.8 19.8 0 01-8.6-3.1 19.5 19.5 0 01-6-6A19.8 19.8 0 012.1 4.2 2 2 0 014.1 2h3a2 2 0 012 1.7c.1.9.4 1.8.7 2.7a2 2 0 01-.5 2.1L8 9.8a16 16 0 006 6l1.3-1.3a2 2 0 012.1-.4c.9.3 1.8.6 2.7.7a2 2 0 011.7 2z"/>';
  static const troGiup = '<circle cx="12" cy="12" r="10"/><path d="M9.1 9a3 3 0 015.8 1c0 2-3 3-3 3"/><path d="M12 17h.01"/>';
  static const khien = '<path d="M12 2l8 4v6c0 5-3.4 9.2-8 10-4.6-.8-8-5-8-10V6z"/><path d="M9 12l2 2 4-4"/>';
  static const lichSu = '<path d="M3 12a9 9 0 109-9 9.7 9.7 0 00-6.7 2.7L3 8"/><path d="M3 3v5h5"/><path d="M12 7v5l4 2"/>';
  static const taiLai = '<path d="M21 12a9 9 0 11-2.6-6.4L21 8"/><path d="M21 3v5h-5"/>';
  static const chevronPhai = '<path d="M9 18l6-6-6-6"/>';
  static const cuaHang = '<path d="M3 9l1.5-5h15L21 9"/><path d="M4 9v11h16V9"/><path d="M3 9a3 3 0 006 0 3 3 0 006 0 3 3 0 006 0"/>';
  static const sao = '<path d="M12 2l3.1 6.3 6.9 1-5 4.9 1.2 6.8L12 17.8 5.8 21l1.2-6.8-5-4.9 6.9-1z"/>';
  static const theTien = '<rect x="2" y="6" width="20" height="12" rx="2"/><circle cx="12" cy="12" r="2.5"/>';
  static const hopKho = '<path d="M21 8l-9-5-9 5 9 5 9-5z"/><path d="M3 8v8l9 5 9-5V8"/><path d="M12 13v8"/>';
  static const dauTichKep = '<path d="M18 6L7 17l-5-5"/><path d="M22 10l-7.5 7.5L13 16"/>';
  static const saoChep = '<rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"/>';
  static const vanBan = '<path d="M4 6h16"/><path d="M4 12h16"/><path d="M4 18h10"/>';
}

// Vẽ icon nét Figma với màu và độ dày tùy chọn.
class AppIcon extends StatelessWidget {
  const AppIcon(this.duong, {super.key, this.size = 18, this.color, this.net = 1.7});

  final String duong;
  final double size;
  final Color? color;
  final double net;

  @override
  Widget build(BuildContext context) {
    Color mau = color ?? IconTheme.of(context).color ?? AppColors.chu;
    // Đổi màu thành mã hex 6 ký tự (vd 22437f) để ghép vào chuỗi SVG
    String hex = (mau.toARGB32() & 0xFFFFFF).toRadixString(16).padLeft(6, '0');
    return SvgPicture.string(
      '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="#$hex" '
      'stroke-width="$net" stroke-linecap="round" stroke-linejoin="round">$duong</svg>',
      width: size,
      height: size,
    );
  }
}

// Logo chữ G nhiều màu của Google (Figma DangNhap / DangKy).
class GoogleLogo extends StatelessWidget {
  const GoogleLogo({super.key, this.size = 19});

  final double size;

  @override
  Widget build(BuildContext context) => SvgPicture.string(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48">'
        '<path fill="#4285F4" d="M45.1 24.5c0-1.6-.1-3.1-.4-4.5H24v8.5h11.8c-.5 2.7-2.1 5-4.4 6.6v5.5h7.1c4.2-3.8 6.6-9.5 6.6-16.1z"/>'
        '<path fill="#34A853" d="M24 46c6 0 11-2 14.6-5.4l-7.1-5.5c-2 1.3-4.5 2.1-7.5 2.1-5.8 0-10.7-3.9-12.4-9.1H4.3v5.7C7.9 41 15.3 46 24 46z"/>'
        '<path fill="#FBBC05" d="M11.6 28.1c-.4-1.3-.7-2.7-.7-4.1s.3-2.8.7-4.1v-5.7H4.3C2.8 17.2 2 20.5 2 24s.8 6.8 2.3 9.8l7.3-5.7z"/>'
        '<path fill="#EA4335" d="M24 10.8c3.3 0 6.2 1.1 8.5 3.3l6.3-6.3C35 4.2 30 2 24 2 15.3 2 7.9 7 4.3 14.2l7.3 5.7c1.7-5.2 6.6-9.1 12.4-9.1z"/>'
        '</svg>',
        width: size,
        height: size,
      );
}
