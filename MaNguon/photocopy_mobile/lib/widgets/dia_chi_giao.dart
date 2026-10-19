import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:url_launcher/url_launcher.dart';
import '../api.dart';
import '../giao_hang.dart';
import '../dich_vu_vi_tri.dart';
import '../theme.dart';
import 'widgets.dart';

// Địa chỉ chính lấy từ GPS; số nhà/tầng/phòng bổ sung không đổi điểm tính phí.
class DiaChiGiao extends StatefulWidget {
  const DiaChiGiao({
    super.key,
    required this.giaTri,
    required this.onChanged,
    this.timDiaChi,
    this.layGps,
  });
  final DiaChiNhan giaTri;
  final ValueChanged<DiaChiNhan> onChanged;
  final Future<KetQuaApi> Function(Map<String, dynamic>)? timDiaChi;
  final Future<DiemGps> Function()? layGps;
  @override
  State<DiaChiGiao> createState() => _DiaChiGiaoState();
}

class _DiaChiGiaoState extends State<DiaChiGiao> {
  late final TextEditingController _diaChi;
  late final TextEditingController _chiTiet;
  late final TextEditingController _ghiChu;
  double? _viDo, _kinhDo;
  String? _maViTri;
  String? _loi;
  String? _nhacGps;
  bool _dangTim = false;
  bool _loiLayGps = false;
  int _lanGoi = 0;

  @override
  void initState() {
    super.initState();
    _diaChi = TextEditingController(text: widget.giaTri.diaChi);
    _chiTiet = TextEditingController(text: widget.giaTri.chiTiet);
    _ghiChu = TextEditingController(text: widget.giaTri.ghiChu);
    _viDo = widget.giaTri.viDo;
    _kinhDo = widget.giaTri.kinhDo;
    _maViTri = widget.giaTri.maViTriGiao;
  }

  @override
  void dispose() {
    _lanGoi++;
    _diaChi.dispose();
    _chiTiet.dispose();
    _ghiChu.dispose();
    super.dispose();
  }

  void _baoThayDoi() => widget.onChanged(
    DiaChiNhan(
      diaChi: _diaChi.text,
      viDo: _viDo,
      kinhDo: _kinhDo,
      chiTiet: _chiTiet.text,
      ghiChu: _ghiChu.text,
      maViTriGiao: _maViTri,
    ),
  );

  Future<void> _dungViTriHienTai() async {
    if (_dangTim) return;
    int lan = ++_lanGoi;
    setState(() {
      _loi = null;
      _nhacGps = null;
      _dangTim = true;
      _loiLayGps = true;
      _diaChi.clear();
      _viDo = null;
      _kinhDo = null;
      _maViTri = null;
    });
    _baoThayDoi(); // Không được đặt bằng điểm nhận/báo giá cũ trong khi đang tìm điểm mới.
    try {
      final diem = await (widget.layGps ?? layViTriHienTai)();
      if (!mounted || lan != _lanGoi) return;
      if (!diem.viDo.isFinite ||
          !diem.kinhDo.isFinite ||
          !diem.doChinhXac.isFinite ||
          diem.viDo.abs() > 90 ||
          diem.kinhDo.abs() > 180 ||
          diem.doChinhXac < 0) {
        throw StateError(
          'Chưa lấy được vị trí hợp lệ. Hãy bật vị trí chính xác và thử lại.',
        );
      }
      if (diem.doChinhXac > 150) {
        throw StateError(
          'Vị trí hiện tại còn sai lệch lớn. Hãy bật vị trí chính xác, di chuyển đến nơi thoáng và thử lại.',
        );
      }
      _loiLayGps = false;
      final req = {'viDo': diem.viDo, 'kinhDo': diem.kinhDo};
      final kq =
          await (widget.timDiaChi?.call(req) ??
              Api.post('/api/giao-hang/dia-chi', req));
      if (!mounted || lan != _lanGoi) return;
      if (!kq.thanhCong) throw StateError(kq.loi);
      if (kq.duLieu is! List || (kq.duLieu as List).isEmpty) {
        throw StateError(
          'Chưa xác định được địa chỉ tại vị trí này. Hãy thử lấy vị trí lại.',
        );
      }
      // API reverse GPS trả một điểm và mã vị trí; giữ nguyên bộ ba để báo giá xác thực được.
      final diaChi = DiaChiNhan.fromJson(
        Map<String, dynamic>.from((kq.duLieu as List).first),
      );
      if (!diaChi.daChon ||
          !diaChi.viDo!.isFinite ||
          !diaChi.kinhDo!.isFinite ||
          diaChi.viDo!.abs() > 90 ||
          diaChi.kinhDo!.abs() > 180 ||
          diaChi.diaChi.length > 255) {
        throw StateError(
          'Địa chỉ từ vị trí chưa hợp lệ. Hãy thử lấy vị trí lại.',
        );
      }
      setState(() {
        _diaChi.text = diaChi.diaChi;
        _viDo = diaChi.viDo;
        _kinhDo = diaChi.kinhDo;
        _maViTri = diaChi.maViTriGiao;
        _nhacGps =
            'Vị trí có sai số khoảng ${diem.doChinhXac.ceil()} m. '
            'Kiểm tra địa chỉ và bổ sung số nhà/tầng/phòng bên dưới trước khi đặt.';
      });
      _baoThayDoi();
    } catch (e) {
      if (mounted && lan == _lanGoi) {
        setState(
          () => _loi = e is StateError
              ? e.message.toString()
              : 'Chưa lấy được địa chỉ từ vị trí hiện tại. Kiểm tra kết nối và thử lại.',
        );
      }
    } finally {
      if (mounted && lan == _lanGoi) setState(() => _dangTim = false);
    }
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      OutlinedButton.icon(
        onPressed: _dangTim ? null : _dungViTriHienTai,
        icon: const Icon(Icons.my_location),
        label: Text(_dangTim ? 'Đang tìm vị trí…' : 'Dùng vị trí hiện tại'),
        style: OutlinedButton.styleFrom(minimumSize: const Size(0, 48)),
      ),
      const SizedBox(height: 10),
      TextField(
        controller: _diaChi,
        maxLength: 255,
        minLines: 1,
        maxLines: 3,
        keyboardType: TextInputType.streetAddress,
        readOnly: true,
        decoration: const InputDecoration(
          labelText: 'Địa chỉ nhận hàng',
          hintText: 'Bấm Dùng vị trí hiện tại để tự điền địa chỉ',
          helperText:
              'Địa chỉ lấy từ GPS. Bổ sung số nhà/tầng/phòng ở ô bên dưới.',
          helperMaxLines: 3,
          counterText: '',
          border: OutlineInputBorder(),
        ),
      ),
      if (_loi != null)
        Padding(
          padding: const EdgeInsets.only(top: 8),
          child: HopThongBao(_loi!),
        ),
      if (_loi != null && _loiLayGps)
        TextButton(
          onPressed: () => Geolocator.openAppSettings(),
          child: const Text('Mở cài đặt quyền vị trí'),
        ),
      if (_nhacGps != null)
        Padding(
          padding: const EdgeInsets.only(top: 8),
          child: Text(
            _nhacGps!,
            style: AppTheme.font(size: 12, color: AppColors.chuPhu),
          ),
        ),
      if (_maViTri != null)
        Semantics(
          liveRegion: true,
          child: const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Text('Đã lấy địa chỉ nhận hàng từ vị trí hiện tại.'),
          ),
        ),
      const SizedBox(height: 8),
      TextField(
        controller: _chiTiet,
        maxLength: 100,
        minLines: 1,
        maxLines: 2,
        onChanged: (_) => _baoThayDoi(),
        decoration: const InputDecoration(
          labelText: 'Địa chỉ chi tiết',
          hintText: 'Số nhà chính xác, tầng/phòng, tên tòa nhà',
          border: OutlineInputBorder(),
        ),
      ),
      const SizedBox(height: 8),
      TextField(
        controller: _ghiChu,
        maxLength: 100,
        minLines: 1,
        maxLines: 2,
        onChanged: (_) => _baoThayDoi(),
        decoration: const InputDecoration(
          labelText: 'Ghi chú giao hàng (không bắt buộc)',
          hintText: 'Ví dụ: gọi trước khi đến, nhận ở cổng',
          border: OutlineInputBorder(),
        ),
      ),
      Align(
        alignment: Alignment.centerLeft,
        child: TextButton(
          onPressed: () => launchUrl(
            Uri.parse('https://www.geoapify.com/'),
            mode: LaunchMode.externalApplication,
          ),
          child: const Text(
            'Powered by Geoapify · © OpenStreetMap contributors',
          ),
        ),
      ),
    ],
  );
}
