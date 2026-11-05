import 'dart:typed_data';
import 'package:flutter/material.dart';
import '../../api.dart';
import '../../models.dart';
import '../../theme.dart';

// Kiểm tra số trang khách gõ để nhảy tới. Trả về câu lỗi, hoặc null nếu hợp lệ.
String? loiSoTrang(String chu, int soTrang) {
  final text = chu.trim();
  if (text.isEmpty) return 'Nhập số trang cần xem.';
  if (text.startsWith('-')) return 'Số trang phải là số dương.';
  if (text.contains('.') || text.contains(',')) {
    return 'Số trang phải là số nguyên, không có phần thập phân.';
  }
  if (!RegExp(r'^\d+$').hasMatch(text)) return 'Số trang chỉ gồm chữ số.';
  final so = int.tryParse(text);
  if (so == null || so < 1 || so > soTrang) {
    return 'Tài liệu có $soTrang trang, hãy nhập từ 1 đến $soTrang.';
  }
  return null;
}

// Xem trước từng trang của bản PDF sẽ in (file Word/ảnh đã được máy chủ đổi sang PDF),
// để khách kiểm tra trước khi tạo đơn. Chỉ xem, không đổi gì trên file.
class XemTruocFileScreen extends StatefulWidget {
  final FileIn file;
  // Cho phép test truyền hàm tải ảnh giả; mặc định gọi API.
  final Future<KetQuaApi> Function(int maFileIn, int soTrang)? taiTrang;
  const XemTruocFileScreen({super.key, required this.file, this.taiTrang});

  @override
  State<XemTruocFileScreen> createState() => _XemTruocFileScreenState();
}

class _XemTruocFileScreenState extends State<XemTruocFileScreen> {
  final _phongTo = TransformationController();
  final _oTrang = TextEditingController();
  int _trang = 1;
  bool _dangTai = true;
  Uint8List? _anh;
  String? _loi;
  String? _loiOTrang;
  int get _soTrang => widget.file.soTrang ?? 1;

  @override
  void initState() {
    super.initState();
    _taiTrang();
  }

  @override
  void dispose() {
    _phongTo.dispose();
    _oTrang.dispose();
    super.dispose();
  }

  // File dài thì gõ số trang để nhảy tới, không phải bấm từng trang.
  void _denTrang() {
    final loi = loiSoTrang(_oTrang.text, _soTrang);
    setState(() => _loiOTrang = loi);
    if (loi != null) return;
    final trang = int.parse(_oTrang.text.trim());
    FocusScope.of(context).unfocus();
    _oTrang.clear();
    _doiTrang(trang);
  }

  Future<void> _taiTrang() async {
    setState(() {
      _dangTai = true;
      _loi = null;
      _anh = null;
    });
    _phongTo.value = Matrix4.identity();
    int trangDangTai = _trang;
    final kq = await (widget.taiTrang ?? Api.getAnhTrangFile)(
      widget.file.maFileIn,
      trangDangTai,
    );
    // Khách bấm chuyển trang nhanh thì bỏ kết quả của trang cũ.
    if (!mounted || trangDangTai != _trang) return;
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
    if (trang < 1 || trang > _soTrang || trang == _trang) return;
    _trang = trang;
    _taiTrang();
  }

  void _doiPhongTo(double heSo) {
    final tiLe = (_phongTo.value.getMaxScaleOnAxis() * heSo).clamp(1.0, 4.0);
    _phongTo.value = Matrix4.diagonal3Values(tiLe, tiLe, 1);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: AppBar(title: const Text('Xem trước bản in')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  widget.file.tenFileGoc,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: AppTheme.font(size: 15, weight: FontWeight.w700),
                ),
                const SizedBox(height: 4),
                Text(
                  widget.file.daChuyenPdf
                      ? 'Đây là bản PDF máy chủ đã chuyển từ file của bạn. Hãy kiểm tra chữ, bảng, hình có bị lệch không.'
                      : 'Đây là đúng bản PDF sẽ được in.',
                  style: AppTheme.font(size: 12, color: AppColors.chuPhu),
                ),
              ],
            ),
          ),
          Expanded(
            child: ColoredBox(
              color: AppColors.nenXam,
              child: SizedBox.expand(
                child: _dangTai
                    ? const Center(child: CircularProgressIndicator())
                    : _loi != null
                    ? Center(
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
                                  '${widget.file.tenFileGoc}, trang $_trang / $_soTrang',
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
                  Text('Trang $_trang / $_soTrang'),
                  if (_soTrang > 1)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Expanded(
                            child: TextField(
                              controller: _oTrang,
                              keyboardType: TextInputType.number,
                              textInputAction: TextInputAction.go,
                              onSubmitted: (_) => _denTrang(),
                              onChanged: (_) {
                                if (_loiOTrang != null) {
                                  setState(() => _loiOTrang = null);
                                }
                              },
                              decoration: InputDecoration(
                                isDense: true,
                                labelText: 'Đến trang (1–$_soTrang)',
                                errorText: _loiOTrang,
                                errorMaxLines: 2,
                                border: const OutlineInputBorder(),
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          FilledButton.tonal(
                            style: FilledButton.styleFrom(
                              minimumSize: const Size(64, 48),
                            ),
                            onPressed: _denTrang,
                            child: const Text('Đến'),
                          ),
                        ],
                      ),
                    ),
                  Wrap(
                    alignment: WrapAlignment.center,
                    children: [
                      IconButton(
                        tooltip: 'Trang trước',
                        onPressed: _trang == 1
                            ? null
                            : () => _doiTrang(_trang - 1),
                        icon: const Icon(Icons.chevron_left),
                      ),
                      IconButton(
                        tooltip: 'Thu nhỏ',
                        onPressed: _anh == null
                            ? null
                            : () => _doiPhongTo(1 / 1.5),
                        icon: const Icon(Icons.zoom_out),
                      ),
                      IconButton(
                        tooltip: 'Phóng to',
                        onPressed: _anh == null ? null : () => _doiPhongTo(1.5),
                        icon: const Icon(Icons.zoom_in),
                      ),
                      IconButton(
                        tooltip: 'Trang sau',
                        onPressed: _trang == _soTrang
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
                      onPressed: () => Navigator.pop(context),
                      child: const Text('Đã kiểm tra, quay lại đặt in'),
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
