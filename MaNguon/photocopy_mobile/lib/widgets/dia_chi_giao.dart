import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:url_launcher/url_launcher.dart';
import '../api.dart';
import '../giao_hang.dart';
import '../dich_vu_vi_tri.dart';
import '../theme.dart';
import 'widgets.dart';

// Địa chỉ + tọa độ + ghi chú cùng một giá trị, tránh gửi tọa độ cũ sau khi sửa chữ.
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
  List<DiaChiNhan> _ds = [];
  String? _loi;
  String? _nhacGps;
  bool _dangTim = false;
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

  void _suaDiaChi(String _) {
    _lanGoi++;
    setState(() {
      _viDo = null;
      _kinhDo = null;
      _maViTri = null;
      _ds = [];
      _loi = null;
      _nhacGps = null;
      _dangTim = false;
    });
    _baoThayDoi();
  }

  Future<void> _tim({bool gps = false}) async {
    int lan = ++_lanGoi;
    setState(() {
      _loi = null;
      _dangTim = true;
      _ds = [];
      _viDo = null;
      _kinhDo = null;
      _maViTri = null;
    });
    _baoThayDoi(); // Không được đặt bằng điểm nhận/báo giá cũ trong khi đang tìm điểm mới.
    try {
      Map<String, dynamic> req = {'diaChi': _diaChi.text.trim()};
      if (gps) {
        final diem = await (widget.layGps ?? layViTriHienTai)();
        if (!mounted || lan != _lanGoi) return;
        // GPS gần đúng phải cảnh báo để khách tìm địa chỉ cụ thể, không âm thầm tính phí theo vị trí sai.
        if (diem.doChinhXac > 150) {
          throw StateError(
            'Vị trí hiện tại còn sai lệch lớn. Hãy tìm địa chỉ bằng cách nhập tay để tính phí đúng.',
          );
        }
        req = {'viDo': diem.viDo, 'kinhDo': diem.kinhDo};
        _nhacGps =
            'Vị trí có sai số khoảng ${diem.doChinhXac.ceil()} m. Kiểm tra địa chỉ và bổ sung số nhà trước khi đặt.';
      }
      final kq =
          await (widget.timDiaChi?.call(req) ??
              Api.post('/api/giao-hang/dia-chi', req));
      if (!mounted || lan != _lanGoi) return;
      if (!kq.thanhCong) throw StateError(kq.loi);
      setState(
        () => _ds = (kq.duLieu as List)
            .map((x) => DiaChiNhan.fromJson(Map<String, dynamic>.from(x)))
            .toList(),
      );
      if (_ds.isEmpty) {
        throw StateError(
          'Chưa tìm thấy địa chỉ phù hợp. Hãy nhập rõ phường/xã, tỉnh/thành.',
        );
      }
      // Kể cả GPS cũng để khách chủ động xác nhận địa chỉ; không tự lấy điểm nhận thay họ.
    } catch (e) {
      if (mounted && lan == _lanGoi) {
        setState(
          () => _loi = e is StateError
              ? e.message.toString()
              : 'Chưa lấy được vị trí. Thử lại hoặc nhập địa chỉ bên dưới.',
        );
      }
    } finally {
      if (mounted && lan == _lanGoi) setState(() => _dangTim = false);
    }
  }

  void _chon(DiaChiNhan diaChi) {
    setState(() {
      _diaChi.text = diaChi.diaChi;
      _viDo = diaChi.viDo;
      _kinhDo = diaChi.kinhDo;
      _maViTri = diaChi.maViTriGiao;
      _ds = [];
      _loi = null;
    });
    _baoThayDoi();
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      OutlinedButton.icon(
        onPressed: _dangTim ? null : () => _tim(gps: true),
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
        onChanged: _suaDiaChi,
        decoration: const InputDecoration(
          labelText: 'Địa chỉ nhận hàng',
          hintText: 'Số nhà, đường, phường/xã, tỉnh/thành',
          border: OutlineInputBorder(),
        ),
      ),
      OutlinedButton(
        onPressed: _dangTim ? null : () => _tim(),
        style: OutlinedButton.styleFrom(minimumSize: const Size(0, 48)),
        child: const Text('Tìm địa chỉ'),
      ),
      if (_loi != null)
        Padding(
          padding: const EdgeInsets.only(top: 8),
          child: HopThongBao(_loi!),
        ),
      if (_loi != null)
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
      for (final diem in _ds)
        Card(
          child: ListTile(
            contentPadding: const EdgeInsets.all(12),
            title: Text(diem.diaChi),
            subtitle: const Text('Chạm để xác nhận điểm nhận này'),
            trailing: const Icon(Icons.location_on_outlined),
            onTap: () => _chon(diem),
          ),
        ),
      if (_viDo != null)
        const Padding(
          padding: EdgeInsets.symmetric(vertical: 8),
          child: Text('Đã chọn điểm nhận. Sửa địa chỉ sẽ cần tìm và chọn lại.'),
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
