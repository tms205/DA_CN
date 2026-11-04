import 'dart:typed_data';
import 'package:flutter/material.dart';
import '../api.dart';
import '../dinh_dang.dart';
import 'huy_don.dart';
import 'bien_lai_hoan_tien.dart';

// Nằm dưới phần đánh giá; yêu cầu sản phẩm lỗi tách riêng việc hủy đơn trước tiếp nhận.
class KhieuNaiHoanTien extends StatefulWidget {
  const KhieuNaiHoanTien({super.key, required this.maDonHang});
  final int maDonHang;
  @override
  State<KhieuNaiHoanTien> createState() => _KhieuNaiHoanTienState();
}

class _KhieuNaiHoanTienState extends State<KhieuNaiHoanTien> {
  bool _tai = true, _choGui = false;
  String? _loi;
  Map<String, dynamic>? _hoSo;
  @override
  void initState() {
    super.initState();
    _doc();
  }

  Future<void> _doc() async {
    final r = await Api.get(
      '/api/don-hang/${widget.maDonHang}/khieu-nai-hoan-tien',
    );
    if (!mounted) return;
    setState(() {
      _tai = false;
      _loi = r.thanhCong ? null : r.loi;
      if (r.thanhCong) {
        _choGui = r.duLieu['choPhepGui'] == true;
        _hoSo = r.duLieu['hoSo'] is Map
            ? Map<String, dynamic>.from(r.duLieu['hoSo'])
            : null;
      }
    });
  }

  String _nhan(String? s) => switch (s) {
    'CHO_DUYET' => 'Chờ CEO xem xét',
    'CHAP_THUAN' => 'CEO đã chấp thuận · Chờ chuyển tiền',
    'TU_CHOI' => 'CEO đã từ chối',
    'DA_HOAN_TIEN' => 'Đã xác nhận chuyển hoàn tiền',
    _ => 'Yêu cầu hoàn tiền',
  };

  Future<void> _xemAnh(String ma) async {
    final r = await Api.getAnhKhieuNai(widget.maDonHang, ma);
    if (!mounted) return;
    if (!r.thanhCong) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(r.loi)));
      return;
    }
    await showDialog<void>(
      context: context,
      builder: (_) => Dialog.fullscreen(
        child: Scaffold(
          appBar: AppBar(title: const Text('Bằng chứng sản phẩm')),
          body: InteractiveViewer(
            minScale: .5,
            maxScale: 5,
            child: Center(
              child: Image.memory(r.duLieu as Uint8List, cacheWidth: 1600),
            ),
          ),
        ),
      ),
    );
  }

  String? _thoiDiem(dynamic value) {
    final time = DateTime.tryParse(value?.toString() ?? '');
    return time == null ? null : ngayGio(time.toLocal());
  }

  Widget _buocXuLy(Map d) {
    final (nen, chu, icon, nhan, tieuDe) = switch (d['trangThai']) {
      'CHAP_THUAN' => (
        const Color(0xFFEAF2FD),
        const Color(0xFF214F80),
        Icons.fact_check_outlined,
        'QUYẾT ĐỊNH CỦA CEO',
        'Đã chấp thuận hoàn tiền',
      ),
      'DA_HOAN_TIEN' => (
        const Color(0xFFE3F1E9),
        const Color(0xFF184F36),
        Icons.check_circle_outline,
        'KẾT QUẢ HOÀN TIỀN',
        'Đã chuyển hoàn tiền',
      ),
      'TU_CHOI' => (
        const Color(0xFFFCECEF),
        const Color(0xFF8F2732),
        Icons.cancel_outlined,
        'QUYẾT ĐỊNH CỦA CEO',
        'Yêu cầu không được chấp thuận',
      ),
      _ => (
        const Color(0xFFFFF5E3),
        const Color(0xFF82500F),
        Icons.schedule_outlined,
        'TRẠNG THÁI YÊU CẦU',
        _nhan(d['trangThai']),
      ),
    };
    final luc = _thoiDiem(d['luc']);
    return Container(
      margin: const EdgeInsets.only(top: 10),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: nen,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: chu.withValues(alpha: .18)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(icon, size: 22, color: chu),
              const SizedBox(width: 9),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      nhan,
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: chu,
                        letterSpacing: .3,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      tieuDe,
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w800,
                        color: chu,
                      ),
                    ),
                    if (luc != null) ...[
                      const SizedBox(height: 4),
                      Text(luc, style: TextStyle(fontSize: 12, color: chu)),
                    ],
                  ],
                ),
              ),
            ],
          ),
          if (d['trangThai'] == 'CHAP_THUAN') ...[
            const SizedBox(height: 10),
            Text(
              'Chấp thuận là bước duyệt yêu cầu. Khoản hoàn chỉ hoàn tất khi cửa hàng xác nhận đã chuyển tiền.',
              style: TextStyle(height: 1.5, color: chu),
            ),
          ],
          if ((d['lyDo']?.toString().trim() ?? '').isNotEmpty) ...[
            const SizedBox(height: 10),
            Text(
              d['lyDo'].toString(),
              style: TextStyle(
                height: 1.5,
                fontWeight: FontWeight.w600,
                color: chu,
              ),
            ),
          ],
          if ((d['maChuyenKhoan']?.toString().trim() ?? '').isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(
              'Mã chuyển tiền: ${d['maChuyenKhoan']}',
              style: TextStyle(fontWeight: FontWeight.w700, color: chu),
            ),
          ],
          if (d['bienLai'] is Map)
            BienLaiHoanTien(
              maDonHang: widget.maDonHang,
              bienLai: Map<String, dynamic>.from(d['bienLai']),
            ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            _hoSo == null ? 'Sản phẩm có vấn đề?' : 'Yêu cầu hoàn tiền',
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
          ),
          const SizedBox(height: 8),
          if (_tai)
            const LinearProgressIndicator()
          else if (_loi != null) ...[
            Text(_loi!),
            TextButton(onPressed: _doc, child: const Text('Thử lại')),
          ] else if (_hoSo case final h?) ...[
            Text(
              _nhan(h['trangThai']),
              style: TextStyle(
                fontWeight: h['trangThai'] == 'CHO_DUYET'
                    ? FontWeight.w600
                    : FontWeight.w800,
                color: h['trangThai'] == 'CHO_DUYET'
                    ? const Color(0xFF82500F)
                    : h['trangThai'] == 'CHAP_THUAN'
                    ? const Color(0xFF214F80)
                    : h['trangThai'] == 'TU_CHOI'
                    ? const Color(0xFF8F2732)
                    : const Color(0xFF184F36),
              ),
            ),
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: const Color(0xFFF7F9FC),
                border: Border.all(color: const Color(0xFFDCE3EC)),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Row(
                    children: [
                      Icon(
                        Icons.description_outlined,
                        size: 20,
                        color: Color(0xFF42546B),
                      ),
                      SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Yêu cầu của bạn',
                          style: TextStyle(
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF283B52),
                          ),
                        ),
                      ),
                    ],
                  ),
                  if (_thoiDiem(h['guiLuc']) case final luc?) ...[
                    const SizedBox(height: 4),
                    Text(
                      'Đã gửi: $luc',
                      style: const TextStyle(
                        fontSize: 12,
                        color: Color(0xFF52647A),
                      ),
                    ),
                  ],
                  const SizedBox(height: 10),
                  Text(
                    h['moTa'] ?? '',
                    style: const TextStyle(
                      height: 1.5,
                      color: Color(0xFF283B52),
                    ),
                  ),
                  const Divider(height: 24),
                  const Text(
                    'Tài khoản nhận hoàn',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF52647A),
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Ngân hàng: ${h['nganHang']}\nSố tài khoản: ${h['soTaiKhoan']}\nChủ tài khoản: ${h['chuTaiKhoan']}',
                    style: const TextStyle(
                      height: 1.5,
                      color: Color(0xFF283B52),
                    ),
                  ),
                  if ((h['anh'] as List? ?? []).isNotEmpty) ...[
                    const SizedBox(height: 8),
                    Wrap(
                      spacing: 8,
                      runSpacing: 4,
                      children: [
                        for (final (i, a) in (h['anh'] as List).indexed)
                          OutlinedButton.icon(
                            onPressed: () => _xemAnh(a['maAnh']),
                            icon: const Icon(Icons.image_outlined, size: 18),
                            label: Text('Ảnh ${i + 1}'),
                          ),
                      ],
                    ),
                  ],
                ],
              ),
            ),
            if ((h['lichSu'] as List? ?? []).isNotEmpty) ...[
              const SizedBox(height: 18),
              const Text(
                'Phản hồi từ cửa hàng',
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF283B52),
                ),
              ),
              for (final d in (h['lichSu'] as List)) _buocXuLy(d),
            ] else ...[
              const SizedBox(height: 12),
              const Text(
                'Cửa hàng đã nhận yêu cầu và đang chờ CEO xem xét.',
                style: TextStyle(color: Color(0xFF82500F), height: 1.5),
              ),
            ],
          ] else if (_choGui) ...[
            const Text(
              'Gửi mô tả chi tiết, ảnh sản phẩm bị lỗi và thông tin ngân hàng để CEO xem xét. Yêu cầu không tự hủy đơn hoặc tự chuyển tiền.',
            ),
            const SizedBox(height: 12),
            NutHuyDon(
              maDonHang: widget.maDonHang,
              daThanhToan: true,
              khieuNaiSanPham: true,
              daHuy: _doc,
            ),
          ] else
            const Text(
              'Đơn này hiện không đủ điều kiện gửi yêu cầu hoàn tiền do sản phẩm.',
            ),
        ],
      ),
    ),
  );
}
