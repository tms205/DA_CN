import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:url_launcher/url_launcher.dart';
import '../api.dart';
import '../giao_hang.dart';
import '../dich_vu_vi_tri.dart';
import '../theme.dart';
import 'widgets.dart';

// Điểm giao lấy theo 3 cách: địa chỉ đã lưu ở tab Tôi, gõ địa chỉ khác rồi tìm, hoặc GPS hiện tại.
// Cách nào cũng phải ra một điểm có tọa độ từ máy chủ thì mới tính phí giao.
// Số nhà/tầng/phòng bổ sung không đổi điểm tính phí.
class DiaChiGiao extends StatefulWidget {
  const DiaChiGiao({
    super.key,
    required this.giaTri,
    required this.onChanged,
    this.timDiaChi,
    this.layGps,
    this.diaChiDaLuu,
    this.maChiNhanh,
  });
  final DiaChiNhan giaTri;
  final ValueChanged<DiaChiNhan> onChanged;
  final Future<KetQuaApi> Function(Map<String, dynamic>)? timDiaChi;
  final Future<DiemGps> Function()? layGps;
  // Địa chỉ mặc định của khách (tab Tôi); có thì tự tìm vị trí khi mở bước đặt in.
  final String? diaChiDaLuu;
  // Chi nhánh đang chọn: máy chủ chỉ tìm địa chỉ quanh chi nhánh này.
  final int? maChiNhanh;
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
  // Các kết quả tìm theo chữ; nhiều kết quả thì khách phải tự chọn một.
  List<DiaChiNhan> _goiY = [];

  String get _daLuu => (widget.diaChiDaLuu ?? '').trim();

  @override
  void initState() {
    super.initState();
    _diaChi = TextEditingController(text: widget.giaTri.diaChi);
    _chiTiet = TextEditingController(text: widget.giaTri.chiTiet);
    _ghiChu = TextEditingController(text: widget.giaTri.ghiChu);
    _viDo = widget.giaTri.viDo;
    _kinhDo = widget.giaTri.kinhDo;
    _maViTri = widget.giaTri.maViTriGiao;
    if (_daLuu.isNotEmpty && !widget.giaTri.daChon) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _timTheoChu(_daLuu);
      });
    }
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
      _goiY = [];
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

  // Tìm vị trí theo địa chỉ chữ (địa chỉ đã lưu hoặc địa chỉ khác khách vừa gõ).
  Future<void> _timTheoChu(String chu) async {
    final text = chu.trim();
    if (_dangTim) return;
    if (text.length < 10) {
      setState(
        () => _loi =
            'Nhập đủ địa chỉ (số nhà, đường, phường/xã, tỉnh/thành) rồi bấm Tìm địa chỉ này.',
      );
      return;
    }
    int lan = ++_lanGoi;
    setState(() {
      _loi = null;
      _nhacGps = null;
      _goiY = [];
      _dangTim = true;
      _loiLayGps = false;
      _diaChi.text = text;
      _viDo = null;
      _kinhDo = null;
      _maViTri = null;
    });
    _baoThayDoi(); // Chưa chọn điểm mới thì không dùng điểm cũ để báo giá.
    try {
      final req = <String, dynamic>{'diaChi': text};
      if (widget.maChiNhanh != null) req['maChiNhanh'] = widget.maChiNhanh;
      final kq =
          await (widget.timDiaChi?.call(req) ??
              Api.post('/api/giao-hang/dia-chi', req));
      if (!mounted || lan != _lanGoi) return;
      if (!kq.thanhCong) throw StateError(kq.loi);
      final ds = <DiaChiNhan>[];
      if (kq.duLieu is List) {
        for (final x in kq.duLieu as List) {
          final d = DiaChiNhan.fromJson(Map<String, dynamic>.from(x));
          if (d.daChon && d.diaChi.length <= 255) ds.add(d);
        }
      }
      if (ds.isEmpty) {
        throw StateError(
          'Chưa tìm thấy địa chỉ này. Hãy ghi rõ số nhà, đường, phường/xã, tỉnh/thành.',
        );
      }
      // Bản đồ hay bỏ mất số hẻm ("320/42"), nên giữ số nhà khách gõ ở ô chi tiết cho shipper.
      final soNha = RegExp(
        r'^\s*(số\s*)?(\d+[a-zA-Z]?(/[0-9a-zA-Z]+)*)\b',
        caseSensitive: false,
      ).firstMatch(text);
      // Chỉ ghi đè khi ô trống hoặc đang là dòng "Số nhà …" App tự điền lần trước; chữ khách tự gõ thì giữ.
      final chiTietTuDien = RegExp(
        r'^Số nhà [0-9a-zA-Z/]+$',
      ).hasMatch(_chiTiet.text.trim());
      if (_chiTiet.text.trim().isEmpty || chiTietTuDien) {
        _chiTiet.text = soNha == null ? '' : 'Số nhà ${soNha.group(2)}';
      }
      setState(() => _goiY = ds);
      // Một kết quả thì chọn luôn; nhiều kết quả thì để khách chọn đúng nơi giao.
      if (ds.length == 1) _chon(ds.first);
    } catch (e) {
      if (mounted && lan == _lanGoi) {
        setState(
          () => _loi = e is StateError
              ? e.message.toString()
              : 'Chưa tìm được địa chỉ. Kiểm tra kết nối và thử lại.',
        );
      }
    } finally {
      if (mounted && lan == _lanGoi) setState(() => _dangTim = false);
    }
  }

  void _chon(DiaChiNhan d) {
    setState(() {
      _diaChi.text = d.diaChi;
      _viDo = d.viDo;
      _kinhDo = d.kinhDo;
      _maViTri = d.maViTriGiao;
      _nhacGps =
          'Kiểm tra lại địa chỉ đã chọn và bổ sung số nhà/tầng/phòng bên dưới trước khi đặt.';
    });
    _baoThayDoi();
  }

  // Khách sửa chữ thì điểm đã chọn không còn đúng nữa, phải tìm lại.
  void _suaDiaChi(String _) {
    if (_maViTri == null && _goiY.isEmpty) return;
    _lanGoi++;
    setState(() {
      _viDo = null;
      _kinhDo = null;
      _maViTri = null;
      _goiY = [];
      _nhacGps = null;
      _dangTim = false;
    });
    _baoThayDoi();
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      if (_daLuu.isNotEmpty) ...[
        OutlinedButton.icon(
          onPressed: _dangTim ? null : () => _timTheoChu(_daLuu),
          icon: const Icon(Icons.home_outlined),
          label: const Text('Dùng địa chỉ đã lưu'),
          style: OutlinedButton.styleFrom(minimumSize: const Size(0, 48)),
        ),
        const SizedBox(height: 8),
      ],
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
        onChanged: _suaDiaChi,
        decoration: const InputDecoration(
          labelText: 'Địa chỉ nhận hàng',
          hintText: 'Gõ địa chỉ khác hoặc bấm Dùng vị trí hiện tại',
          helperText:
              'Muốn giao tới nơi khác: gõ địa chỉ rồi bấm Tìm địa chỉ này. Số nhà/tầng/phòng ghi ở ô bên dưới.',
          helperMaxLines: 3,
          counterText: '',
          border: OutlineInputBorder(),
        ),
      ),
      Align(
        alignment: Alignment.centerLeft,
        child: TextButton.icon(
          onPressed: _dangTim ? null : () => _timTheoChu(_diaChi.text),
          icon: const Icon(Icons.search),
          label: const Text('Tìm địa chỉ này'),
        ),
      ),
      if (_goiY.length > 1) ...[
        Text(
          'Chọn đúng nơi giao:',
          style: AppTheme.font(size: 13, weight: FontWeight.w600),
        ),
        for (final d in _goiY)
          ListTile(
            dense: true,
            contentPadding: EdgeInsets.zero,
            leading: Icon(
              d.maViTriGiao == _maViTri
                  ? Icons.radio_button_checked
                  : Icons.radio_button_unchecked,
            ),
            title: Text(d.diaChi),
            onTap: _dangTim ? null : () => _chon(d),
          ),
      ],
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
            child: Text('Đã chọn điểm nhận hàng để tính phí giao.'),
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
