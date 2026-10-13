import 'package:flutter/material.dart';
import '../../api.dart';
import '../../google_auth.dart';
import '../../widgets/widgets.dart';
import '../../widgets/icons.dart';
import 'bo_sung_google_screen.dart';

// Cùng nút/luồng cho đăng nhập và đăng ký. Callback cho phép thay nhà cung cấp xác thực khi cần.
class NutGoogle extends StatefulWidget {
  const NutGoogle({
    super.key,
    this.ghiNho = true,
    this.dangBan = false,
    this.dangXuLyChanged,
    this.xacThuc = GoogleAuth.tiepTuc,
  });
  final bool ghiNho, dangBan;
  final ValueChanged<bool>? dangXuLyChanged;
  final Future<KetQuaApi?> Function(bool) xacThuc;
  @override
  State<NutGoogle> createState() => _NutGoogleState();
}

class _NutGoogleState extends State<NutGoogle> {
  bool _dangGui = false;
  String? _loi;

  Future<void> _tiepTuc() async {
    if (_dangGui || widget.dangBan) return;
    FocusScope.of(context).unfocus();
    setState(() {
      _dangGui = true;
      _loi = null;
    });
    widget.dangXuLyChanged?.call(true);
    try {
      final kq = await widget.xacThuc(widget.ghiNho);
      if (!mounted || kq == null) return;
      if (!kq.thanhCong) {
        setState(() => _loi = kq.loi);
        return;
      }
      final data = Map<String, dynamic>.from(kq.duLieu);
      if (data['canBoSungThongTin'] == true) {
        await Navigator.push(
          context,
          MaterialPageRoute(
            builder: (_) => BoSungGoogleScreen(
              veDangKy: data['veDangKy'],
              email: data['email'],
              hoTen: data['hoTen'],
              ghiNho: widget.ghiNho,
            ),
          ),
        );
      } else {
        await moPhienGoogle(context, data, widget.ghiNho);
      }
    } catch (_) {
      if (mounted) {
        setState(
          () =>
              _loi = 'Không hoàn tất được đăng nhập Google. Vui lòng thử lại.',
        );
      }
    } finally {
      if (mounted) {
        setState(() => _dangGui = false);
        widget.dangXuLyChanged?.call(false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (_loi != null) ...[HopThongBao(_loi!), const SizedBox(height: 12)],
        Semantics(
          button: true,
          label: 'Tiếp tục với Google',
          enabled: !_dangGui && !widget.dangBan,
          child: NutPhu(
            nhan: _dangGui ? 'Đang xử lý Google…' : 'Tiếp tục với Google',
            icon: _dangGui
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const GoogleLogo(),
            onPressed: _dangGui || widget.dangBan ? null : _tiepTuc,
          ),
        ),
      ],
    );
  }
}
