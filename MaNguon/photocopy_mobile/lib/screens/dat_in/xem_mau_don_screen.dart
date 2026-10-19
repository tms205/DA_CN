import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../api.dart';
import '../../theme.dart';

// Chỉ xem mẫu; trả true khi khách chọn để in. Chưa tạo FileIn hay đơn hàng ở màn này.
class XemMauDonScreen extends StatefulWidget {
  final Map<String, dynamic> mau;
  const XemMauDonScreen({super.key, required this.mau});

  @override
  State<XemMauDonScreen> createState() => _XemMauDonScreenState();
}

class _XemMauDonScreenState extends State<XemMauDonScreen> {
  final _phongTo = TransformationController();
  int _trang = 1;
  bool _dangTai = true;
  Uint8List? _anh;
  String? _loi;
  int get _soTrang => (widget.mau['soTrang'] as int?) ?? 1;

  @override
  void initState() {
    super.initState();
    _taiTrang();
  }

  @override
  void dispose() {
    _phongTo.dispose();
    super.dispose();
  }

  Future<void> _taiTrang() async {
    setState(() {
      _dangTai = true;
      _loi = null;
      _anh = null;
    });
    _phongTo.value = Matrix4.identity();
    final kq = await Api.getAnhMau(widget.mau['maMau'], _trang,
      phienBan: widget.mau['phienBan'] as int?);
    if (!mounted) return;
    setState(() {
      _dangTai = false;
      if (kq.thanhCong) {
        _anh = kq.duLieu as Uint8List;
      } else {
        _loi = kq.loi;
      }
    });
  }

  void _doiTrang(int trang) {
    if (_dangTai || trang < 1 || trang > _soTrang) return;
    _trang = trang;
    _taiTrang();
  }

  void _doiPhongTo(double heSo) {
    final tiLe = (_phongTo.value.getMaxScaleOnAxis() * heSo).clamp(1.0, 4.0);
    _phongTo.value = Matrix4.diagonal3Values(tiLe, tiLe, 1);
  }

  Future<void> _moNguon() async {
    final url = Uri.tryParse(widget.mau['urlNguon'] ?? '');
    if (url == null || url.scheme != 'https') return;
    try {
      if (await launchUrl(url, mode: LaunchMode.externalApplication)) return;
    } catch (_) {}
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Chưa mở được nguồn công bố trên trình duyệt.'),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final anhDangXem = _anh;
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: AppBar(title: const Text('Xem mẫu đơn')),
      body: Column(
        children: [
          ExpansionTile(
            title: Text(
              widget.mau['tenMau'],
              style: AppTheme.font(size: 16, weight: FontWeight.w700),
            ),
            subtitle: Text('${widget.mau['nguon'] ?? ''} · $_soTrang trang'),
            childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
            children: [
              Text(
                widget.mau['phamVi'] ??
                    'Bản trống để điền bằng tay; kiểm tra yêu cầu nơi nhận.',
              ),
              if (Uri.tryParse(widget.mau['urlNguon'] ?? '')?.scheme == 'https')
                TextButton(
                  onPressed: _moNguon,
                  child: const Text('Xem nguồn công bố'),
                ),
            ],
          ),
          Expanded(
            child: ColoredBox(
              color: AppColors.nenXam,
              child: SizedBox.expand(
                child: _dangTai
                    ? const Center(child: CircularProgressIndicator())
                    : _loi != null
                    ? Center(
                        child: SingleChildScrollView(
                          child: Padding(
                            padding: const EdgeInsets.all(20),
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Text(_loi!, textAlign: TextAlign.center),
                                const SizedBox(height: 12),
                                OutlinedButton(
                                  onPressed: _taiTrang,
                                  child: const Text('Thử lại'),
                                ),
                              ],
                            ),
                          ),
                        ),
                      )
                    : InteractiveViewer(
                        transformationController: _phongTo,
                        minScale: 1,
                        maxScale: 4,
                        child: Center(
                          child: Padding(
                            padding: const EdgeInsets.all(12),
                            child: Image.memory(
                              _anh!,
                              key: ValueKey(_trang),
                              fit: BoxFit.contain,
                              semanticLabel:
                                  '${widget.mau['tenMau']}, trang $_trang / $_soTrang',
                              errorBuilder: (context, error, stack) {
                                WidgetsBinding.instance.addPostFrameCallback((
                                  _,
                                ) {
                                  if (!mounted || _anh != anhDangXem) return;
                                  setState(() {
                                    _anh = null;
                                    _loi =
                                        'Không đọc được ảnh xem trước. Vui lòng thử lại.';
                                  });
                                });
                                return const SizedBox.shrink();
                              },
                            ),
                          ),
                        ),
                      ),
              ),
            ),
          ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'Trang $_trang / $_soTrang',
                    semanticsLabel: 'Trang $_trang trên $_soTrang',
                  ),
                  Wrap(
                    alignment: WrapAlignment.center,
                    children: [
                      IconButton(
                        tooltip: 'Trang trước',
                        constraints: const BoxConstraints(
                          minWidth: 48,
                          minHeight: 48,
                        ),
                        onPressed: _dangTai || _trang == 1
                            ? null
                            : () => _doiTrang(_trang - 1),
                        icon: const Icon(Icons.chevron_left),
                      ),
                      IconButton(
                        tooltip: 'Thu nhỏ',
                        constraints: const BoxConstraints(
                          minWidth: 48,
                          minHeight: 48,
                        ),
                        onPressed: _anh == null
                            ? null
                            : () => _doiPhongTo(1 / 1.5),
                        icon: const Icon(Icons.zoom_out),
                      ),
                      IconButton(
                        tooltip: 'Vừa trang',
                        constraints: const BoxConstraints(
                          minWidth: 48,
                          minHeight: 48,
                        ),
                        onPressed: () => _phongTo.value = Matrix4.identity(),
                        icon: const Icon(Icons.fit_screen),
                      ),
                      IconButton(
                        tooltip: 'Phóng to',
                        constraints: const BoxConstraints(
                          minWidth: 48,
                          minHeight: 48,
                        ),
                        onPressed: _anh == null ? null : () => _doiPhongTo(1.5),
                        icon: const Icon(Icons.zoom_in),
                      ),
                      IconButton(
                        tooltip: 'Trang sau',
                        constraints: const BoxConstraints(
                          minWidth: 48,
                          minHeight: 48,
                        ),
                        onPressed: _dangTai || _trang == _soTrang
                            ? null
                            : () => _doiTrang(_trang + 1),
                        icon: const Icon(Icons.chevron_right),
                      ),
                    ],
                  ),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                        minimumSize: const Size(48, 48),
                        backgroundColor: AppColors.accent,
                      ),
                      onPressed: _dangTai || _anh == null
                          ? null
                          : () => Navigator.pop(context, true),
                      child: const Text('Chọn để in'),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
