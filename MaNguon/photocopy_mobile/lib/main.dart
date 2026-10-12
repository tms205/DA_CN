import 'package:flutter/material.dart';

import 'phien.dart';
import 'realtime.dart';
import 'screens/xac_thuc/dang_nhap_screen.dart';
import 'screens/trang_chinh_screen.dart';
import 'theme.dart';
import 'widgets/widgets.dart';

void main() {
  runApp(const PhotocopyApp());
}

class PhotocopyApp extends StatelessWidget {
  const PhotocopyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'PhotocopyChain',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      home: const ManHinhCho(),
    );
  }
}

// Màn chờ lúc mở app: đọc phiên đăng nhập đã lưu trong máy.
// Còn phiên → vào thẳng trang chính; không có → màn đăng nhập.
class ManHinhCho extends StatefulWidget {
  const ManHinhCho({super.key});

  @override
  State<ManHinhCho> createState() => _ManHinhChoState();
}

class _ManHinhChoState extends State<ManHinhCho> {
  @override
  void initState() {
    super.initState();
    _kiemTraPhien();
  }

  Future<void> _kiemTraPhien() async {
    bool daDangNhap = await Phien.docTuMay();
    if (daDangNhap) await Realtime.batDau();   // F-32: mở kênh nhận cập nhật real-time
    if (!mounted) return;

    // pushReplacement: thay màn chờ bằng màn mới (bấm Back không quay lại màn chờ)
    if (daDangNhap) {
      Navigator.pushReplacement(context, MaterialPageRoute(builder: (context) => const TrangChinhScreen()));
    } else {
      Navigator.pushReplacement(context, MaterialPageRoute(builder: (context) => const DangNhapScreen()));
    }
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: AppColors.nen,
      body: Center(child: LogoApp()),
    );
  }
}
