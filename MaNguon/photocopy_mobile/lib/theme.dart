import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

// Màu lấy đúng từ Figma (GiaoDien/*.dc.html). Màu chính (accent) = #22437F.
class AppColors {
  static const accent = Color(0xFF22437F);
  static const accentDam = Color(0xFF16305E);
  static const accentDeep = Color(0xFF182F59);
  static const accentSoft = Color(0xFFEDF0F5);
  static const accentTint = Color(0xFFD3D9E5);

  static const nen = Color(0xFFFBF9F5);
  static const nenXam = Color(0xFFF1EDE6);
  static const nenOTrong = Color(0xFFF5F1EA);
  static const trang = Colors.white;

  static const chu = Color(0xFF17140F);
  static const chuNhan = Color(0xFF4A443D);
  static const chuPhu = Color(0xFF837B72);
  static const iconMo = Color(0xFFA29A90);

  static const vien = Color(0xFFE0DAD1);
  static const vach = Color(0xFFE6E0D8);
  static const vienHop = Color(0xFFD3CBC0);
  static const vienNetDut = Color(0xFFDDD6CC);

  static const thanhCong = Color(0xFF1F6B47);
  static const xanhNhat = Color(0xFFEEF6F1);
  static const loiNen = Color(0xFFFDF0EE);
  static const loiVien = Color(0xFFF0D2CB);
  static const loiChu = Color(0xFFA83B26);
  static const loiChuNhat = Color(0xFF9B5B4B);

  static const cbNen = Color(0xFFFFF6E9);
  static const cbVien = Color(0xFFF0DFC4);
  static const cbChu = Color(0xFF7A5411);
  static const cbIcon = Color(0xFFB5730E);

  static const hoaToc = Color(0xFFC2610C);
  static const hoaTocNen = Color(0xFFFBEEDF);
  static const hoaTocVien = Color(0xFFE9C08A);
  static const hoaTocChu = Color(0xFF7A3D06);
  static const hoaTocPhu = Color(0xFF96541A);
}

class AppTheme {
  // Khi chạy test thì không tải được font qua mạng nên tắt đi
  static bool dungGoogleFonts = true;

  // Chữ thường — font Be Vietnam Pro (cỡ chữ ghi đúng số px trong Figma)
  static TextStyle font({
    double size = 14,
    FontWeight weight = FontWeight.w500,
    Color color = AppColors.chu,
    double? height,
    double? letterSpacing,
    FontStyle? fontStyle,
  }) {
    TextStyle kieu = TextStyle(
      fontSize: size,
      fontWeight: weight,
      color: color,
      height: height,
      letterSpacing: letterSpacing,
      fontStyle: fontStyle,
    );
    if (dungGoogleFonts) return GoogleFonts.beVietnamPro(textStyle: kieu);
    return kieu;
  }

  // Chữ số tiền, mã OTP — font IBM Plex Mono (các chữ số rộng bằng nhau)
  static TextStyle mono({
    double size = 14,
    FontWeight weight = FontWeight.w600,
    Color color = AppColors.chu,
    double? letterSpacing,
  }) {
    TextStyle kieu = TextStyle(fontSize: size, fontWeight: weight, color: color, letterSpacing: letterSpacing);
    if (dungGoogleFonts) return GoogleFonts.ibmPlexMono(textStyle: kieu);
    return kieu.copyWith(fontFamily: 'monospace');
  }

  static ThemeData light() {
    ThemeData goc = ThemeData(
      useMaterial3: true,
      colorScheme: ColorScheme.fromSeed(seedColor: AppColors.accent, primary: AppColors.accent, surface: AppColors.trang),
      scaffoldBackgroundColor: AppColors.nen,
    );
    return goc.copyWith(
      textTheme: dungGoogleFonts ? GoogleFonts.beVietnamProTextTheme(goc.textTheme) : goc.textTheme,
      snackBarTheme: const SnackBarThemeData(behavior: SnackBarBehavior.floating),
      textSelectionTheme: const TextSelectionThemeData(cursorColor: AppColors.accent),
    );
  }
}
