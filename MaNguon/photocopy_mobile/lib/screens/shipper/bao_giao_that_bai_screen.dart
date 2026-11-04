import 'dart:io';
import '../../widgets/chon_anh_bang_chung.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import '../../api.dart';
import '../../dich_vu_vi_tri.dart';
import '../../giao_hang.dart';
import '../../widgets/widgets.dart';

class BaoGiaoThatBaiScreen extends StatefulWidget {
  const BaoGiaoThatBaiScreen({
    super.key,
    required this.don,
    this.layGps,
    this.chonAnh,
    this.chonNhieuAnh,
    this.traDiaChi,
    this.guiBaoCao,
  });
  final DonGiao don;
  final Future<DiemGps> Function()? layGps;
  final Future<PlatformFile?> Function()? chonAnh;
  final Future<List<PlatformFile>> Function()? chonNhieuAnh;
  final Future<KetQuaApi> Function(Map<String, dynamic>)? traDiaChi;
  final Future<KetQuaApi> Function(
    Map<String, String>,
    List<Map<String, String>>,
  )?
  guiBaoCao;
  @override
  State<BaoGiaoThatBaiScreen> createState() => _BaoGiaoThatBaiScreenState();
}

class _BaoGiaoThatBaiScreenState extends State<BaoGiaoThatBaiScreen> {
  final _chiTiet = TextEditingController();
  final _cuon = ScrollController();
  final _anh = <PlatformFile>[];
  String? _lyDo, _loi, _loiGps, _diaChi;
  DiemGps? _gps;
  DateTime? _thoiDiem;
  bool _dangGps = false, _dangGui = false, _dangChon = false;
  static const _cacLyDo = [
    'Khách không nghe máy',
    'Không có người nhận',
    'Sai địa chỉ nhận hàng',
    'Khách từ chối nhận',
    'Khác',
  ];

  @override
  void initState() {
    super.initState();
    _chiTiet.addListener(_doi);
    _layGps();
  }

  void _doi() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _chiTiet.removeListener(_doi);
    _chiTiet.dispose();
    _cuon.dispose();
    super.dispose();
  }

  void _baoLoi(String message) {
    setState(() => _loi = message);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _cuon.hasClients) {
        _cuon.animateTo(
          _cuon.position.maxScrollExtent,
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Map<String, dynamic> get _viTri => {
    'viDo': _gps!.viDo,
    'kinhDo': _gps!.kinhDo,
    'doChinhXac': _gps!.doChinhXac,
    'thoiDiemGps': _thoiDiem!.toUtc().toIso8601String(),
  };
  String get _noiDung => _lyDo == 'Khác'
      ? _chiTiet.text.trim()
      : '${_lyDo ?? ''}${_chiTiet.text.trim().isEmpty ? '' : ': ${_chiTiet.text.trim()}'}';
  bool get _du =>
      _lyDo != null &&
      _noiDung.length >= 5 &&
      _noiDung.length <= 255 &&
      _gps != null &&
      _thoiDiem != null &&
      _anh.isNotEmpty &&
      !_dangGps &&
      !_dangChon;

  Future<void> _layGps() async {
    setState(() {
      _dangGps = true;
      _gps = null;
      _diaChi = null;
      _loiGps = null;
    });
    try {
      final gps = await (widget.layGps?.call() ?? layViTriHienTai());
      if (!mounted) return;
      if (!gps.viDo.isFinite ||
          !gps.kinhDo.isFinite ||
          !gps.doChinhXac.isFinite) {
        throw StateError('Chưa lấy được GPS hợp lệ. Hãy thử lại.');
      }
      _gps = gps;
      _thoiDiem = gps.thoiDiem ?? DateTime.now().toUtc();
      final result =
          await (widget.traDiaChi?.call(_viTri) ??
              Api.post('/api/giao-hang/vi-tri-shipper', _viTri));
      if (!mounted) return;
      setState(() {
        _diaChi = result.thanhCong ? result.duLieu['diaChi'] as String? : null;
        if (!result.thanhCong) {
          _loiGps =
              'Đã lấy GPS; chưa tra được địa chỉ. Vẫn có thể gửi báo cáo.';
        }
      });
    } catch (e) {
      if (mounted) {
        setState(() {
          _gps = null;
          _loiGps = e is FormatException
              ? e.message.toString()
              : e.toString().replaceFirst('Bad state: ', '');
        });
      }
    } finally {
      if (mounted) setState(() => _dangGps = false);
    }
  }

  Future<void> _chonAnh() async {
    if (_anh.length >= 3 || _dangChon || _dangGui) return;
    setState(() {
      _dangChon = true;
      _loi = null;
    });
    try {
      final files = await chonAnhBangChung(
        daChon: _anh.map((a) => a.path!),
        chonMot: widget.chonAnh,
        chonNhieu: widget.chonNhieuAnh,
      );
      if (!mounted) return;
      setState(() => _anh.addAll(files));
    } catch (e) {
      if (mounted) {
        setState(
          () => _loi = e is FormatException
              ? e.message.toString()
              : e.toString().replaceFirst('Bad state: ', ''),
        );
      }
    } finally {
      if (mounted) setState(() => _dangChon = false);
    }
  }

  Future<void> _gui() async {
    if (!_du || _dangGui) return;
    if (DateTime.now().toUtc().difference(_thoiDiem!).inMinutes >= 4) {
      await _layGps();
      if (!mounted || !_du) return;
    }
    setState(() {
      _dangGui = true;
      _loi = null;
    });
    try {
      final fields = {
        for (final e in _viTri.entries) e.key: e.value.toString(),
        'lyDo': _noiDung,
      };
      final files = _anh.map((a) => {'path': a.path!, 'name': a.name}).toList();
      final result =
          await (widget.guiBaoCao?.call(fields, files) ??
              Api.guiBangChungGiao(widget.don.maGiaoHang!, fields, files));
      if (!mounted) return;
      if (result.thanhCong) {
        Navigator.pop(context, true);
        return;
      }
      _baoLoi(result.loi);
    } catch (_) {
      if (mounted) {
        _baoLoi(
          'Chưa gửi được báo cáo. Ảnh và nội dung vẫn được giữ để thử lại.',
        );
      }
    } finally {
      if (mounted) setState(() => _dangGui = false);
    }
  }

  Future<void> _xemAnh(PlatformFile file) => showDialog<void>(
    context: context,
    builder: (ctx) => Dialog.fullscreen(
      child: Scaffold(
        appBar: AppBar(title: const Text('Ảnh nhật ký cuộc gọi')),
        body: InteractiveViewer(
          minScale: 0.5,
          maxScale: 4,
          child: Center(
            child: Image.file(
              File(file.path!),
              errorBuilder: (_, _, _) =>
                  const Text('Không đọc được ảnh. Chọn lại ảnh khác.'),
            ),
          ),
        ),
      ),
    ),
  );

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !_dangGui,
    child: Scaffold(
      appBar: AppBar(title: const Text('Báo giao thất bại')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: ListView(
              controller: _cuon,
              padding: const EdgeInsets.all(16),
              children: [
                Text(
                  '${widget.don.maHienThi} · Lượt ${widget.don.lanGiao ?? widget.don.soLan}/3',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                Text(widget.don.diaChi),
                const SizedBox(height: 24),
                Text(
                  '1. Lý do không giao được',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  initialValue: _lyDo,
                  isExpanded: true,
                  decoration: const InputDecoration(
                    labelText: 'Chọn lý do *',
                    border: OutlineInputBorder(),
                  ),
                  items: _cacLyDo
                      .map((x) => DropdownMenuItem(value: x, child: Text(x)))
                      .toList(),
                  onChanged: _dangGui
                      ? null
                      : (value) => setState(() => _lyDo = value),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _chiTiet,
                  enabled: !_dangGui,
                  maxLength: 200,
                  minLines: 2,
                  maxLines: 4,
                  decoration: InputDecoration(
                    labelText: _lyDo == 'Khác'
                        ? 'Mô tả lý do * (ít nhất 5 ký tự)'
                        : 'Chi tiết thêm (nếu có)',
                    border: const OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 16),
                Text(
                  '2. Vị trí tại thời điểm báo cáo',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                if (_dangGps)
                  const Padding(
                    padding: EdgeInsets.all(12),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                        SizedBox(width: 12),
                        Expanded(child: Text('Đang lấy vị trí hiện tại…')),
                      ],
                    ),
                  ),
                if (_gps != null) ...[
                  SelectableText(
                    _diaChi ?? 'Đã lấy GPS; chưa có địa chỉ mô tả.',
                  ),
                  Text(
                    '${_gps!.viDo.toStringAsFixed(6)}, ${_gps!.kinhDo.toStringAsFixed(6)} · Sai số khoảng ${_gps!.doChinhXac.round()} m',
                  ),
                  Text(
                    'Lấy lúc ${_thoiDiem!.toLocal().toString().substring(0, 19)}',
                  ),
                ],
                if (_loiGps != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(_loiGps!),
                  ),
                OutlinedButton.icon(
                  onPressed: _dangGps || _dangGui ? null : _layGps,
                  icon: const Icon(Icons.my_location),
                  label: const Text('Lấy lại vị trí hiện tại'),
                  style: OutlinedButton.styleFrom(
                    minimumSize: const Size(0, 48),
                  ),
                ),
                const SizedBox(height: 24),
                Text(
                  '3. Ảnh nhật ký cuộc gọi *',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                const Text(
                  'Chụp màn hình cuộc gọi liên quan đến khách và tải lên. Có thể che thông tin cuộc gọi khác. Từ 1–3 ảnh JPG/PNG, tối đa 5 MB mỗi ảnh.',
                ),
                for (int i = 0; i < _anh.length; i++)
                  Padding(
                    padding: const EdgeInsets.only(top: 12),
                    child: Row(
                      children: [
                        Semantics(
                          label: 'Xem ảnh bằng chứng ${i + 1}',
                          button: true,
                          child: InkWell(
                            onTap: () => _xemAnh(_anh[i]),
                            child: Image.file(
                              File(_anh[i].path!),
                              width: 64,
                              height: 80,
                              fit: BoxFit.cover,
                              cacheWidth: 160,
                              errorBuilder: (_, _, _) => const SizedBox(
                                width: 64,
                                height: 80,
                                child: Icon(Icons.broken_image_outlined),
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            _anh[i].name,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        IconButton(
                          onPressed: _dangGui
                              ? null
                              : () => setState(() => _anh.removeAt(i)),
                          tooltip: 'Bỏ ảnh ${i + 1}',
                          icon: const Icon(Icons.delete_outline),
                        ),
                      ],
                    ),
                  ),
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  onPressed: _dangGui || _dangChon || _anh.length >= 3
                      ? null
                      : _chonAnh,
                  icon: const Icon(Icons.add_photo_alternate_outlined),
                  label: Text(
                    _dangChon
                        ? 'Đang chọn ảnh…'
                        : 'Thêm ảnh (${_anh.length}/3)',
                  ),
                  style: OutlinedButton.styleFrom(
                    minimumSize: const Size(0, 48),
                  ),
                ),
                if (_loi != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 16),
                    child: HopThongBao(_loi!),
                  ),
                const SizedBox(height: 24),
                FilledButton(
                  onPressed: _du && !_dangGui ? _gui : null,
                  style: FilledButton.styleFrom(minimumSize: const Size(0, 52)),
                  child: Text(
                    _dangGui
                        ? 'Đang gửi báo cáo…'
                        : 'Gửi báo cáo giao thất bại',
                  ),
                ),
                const SizedBox(height: 8),
                const Text(
                  'Báo cáo không tự hủy đơn hoặc hoàn tiền. Cửa hàng sẽ xem bằng chứng để xử lý.',
                ),
              ],
            ),
          ),
        ),
      ),
    ),
  );
}
