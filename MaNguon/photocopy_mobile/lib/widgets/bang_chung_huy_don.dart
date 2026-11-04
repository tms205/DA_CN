import 'dart:typed_data';
import 'package:flutter/material.dart';
import '../api.dart';
import '../dinh_dang.dart';
import 'bien_lai_hoan_tien.dart';

class BangChungHuyDon extends StatefulWidget {
  const BangChungHuyDon({
    super.key,
    required this.maDonHang,
    this.trangThaiThanhToan = '',
    this.lanCapNhat = 0,
  });
  final int maDonHang;
  final String trangThaiThanhToan;
  final int lanCapNhat;
  @override
  State<BangChungHuyDon> createState() => _BangChungHuyDonState();
}

class _BangChungHuyDonState extends State<BangChungHuyDon> {
  Map<String, dynamic>? _data;
  String? _loi;
  bool _busy = false, _canTaiLai = false;
  @override
  void initState() {
    super.initState();
    _tai();
  }

  @override
  void didUpdateWidget(covariant BangChungHuyDon oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.maDonHang != widget.maDonHang ||
        oldWidget.lanCapNhat != widget.lanCapNhat ||
        oldWidget.trangThaiThanhToan != widget.trangThaiThanhToan) {
      _tai();
    }
  }

  String? _luc(dynamic value) {
    final time = DateTime.tryParse(value?.toString() ?? '');
    return time == null || time.year < 1900 ? null : ngayGio(time.toLocal());
  }

  Future<void> _tai() async {
    if (_busy) {
      _canTaiLai = true;
      return;
    }
    setState(() {
      _busy = true;
      _loi = null;
    });
    do {
      _canTaiLai = false;
      final id = widget.maDonHang;
      final r = await Api.get('/api/don-hang/$id/bang-chung-huy');
      if (!mounted) return;
      if (id != widget.maDonHang) {
        _canTaiLai = true;
        continue;
      }
      setState(() {
        if (r.thanhCong) {
          _data = Map<String, dynamic>.from(r.duLieu);
          _loi = null;
        } else {
          _loi = r.maHttp == 404 ? 'Đơn cũ chưa có nhật ký hủy.' : r.loi;
        }
      });
    } while (_canTaiLai && mounted);
    if (mounted) setState(() => _busy = false);
  }

  Future<void> _xem(String id) async {
    final r = await Api.getAnhHuy(widget.maDonHang, id);
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
          appBar: AppBar(title: const Text('Bằng chứng hủy đơn')),
          body: InteractiveViewer(
            minScale: 0.5,
            maxScale: 5,
            child: Center(
              child: Image.memory(r.duLieu as Uint8List, cacheWidth: 1600),
            ),
          ),
        ),
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
            _data?['loai'] == 'HUY_TRUOC_TIEP_NHAN'
                ? 'Nhật ký hủy trước tiếp nhận'
                : 'Bằng chứng hủy đơn',
            style: TextStyle(fontWeight: FontWeight.w700),
          ),
          if (_busy)
            const Padding(
              padding: EdgeInsets.all(12),
              child: Text('Đang tải…'),
            ),
          if (_loi != null) ...[
            Text(_loi!),
            TextButton(
              onPressed: _busy ? null : _tai,
              child: const Text('Thử lại'),
            ),
          ],
          if (_data != null) ...[
            Text(
              _data!['loai'] == 'TU_DONG'
                  ? 'Hệ thống tự hủy do hết hạn thanh toán'
                  : _data!['loai'] == 'HUY_TRUOC_TIEP_NHAN'
                  ? 'Hủy đơn trước khi nhân viên tiếp nhận'
                  : 'Yêu cầu hủy có ảnh bằng chứng',
            ),
            if (_luc(_data!['guiLuc']) case final time?)
              Text('Ghi nhận: $time'),
            if (_data!['loai'] == 'TU_DONG')
              Text(
                'Hết hạn: ${_data!['hetHanThanhToan'] ?? ''}\nThanh toán: ${_data!['trangThaiThanhToan'] ?? ''}',
              ),
            if (_data!['bienLaiHoanTien'] is Map) ...[
              const Divider(height: 24),
              Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: const Color(0xFFE3F1E9),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text(
                      'Kết quả hoàn tiền',
                      style: TextStyle(
                        fontWeight: FontWeight.w800,
                        color: Color(0xFF184F36),
                      ),
                    ),
                    if (_luc(_data!['bienLaiHoanTien']['luc']) case final time?)
                      Text('Cập nhật biên lai: $time'),
                    const SizedBox(height: 10),
                    const Text(
                      'Lời nhắn từ CEO',
                      style: TextStyle(fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      _data!['bienLaiHoanTien']['lyDo']
                                  ?.toString()
                                  .trim()
                                  .isNotEmpty ==
                              true
                          ? _data!['bienLaiHoanTien']['lyDo']
                          : 'Hồ sơ cũ chưa có lời nhắn.',
                      style: const TextStyle(height: 1.5),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Mã chuyển tiền: ${_data!['bienLaiHoanTien']['maChuyenKhoan'] ?? ''}',
                    ),
                    BienLaiHoanTien(
                      maDonHang: widget.maDonHang,
                      bienLai: Map<String, dynamic>.from(
                        _data!['bienLaiHoanTien'],
                      ),
                    ),
                  ],
                ),
              ),
            ] else if (!_busy &&
                widget.trangThaiThanhToan == 'DA_THANH_TOAN') ...[
              const Divider(height: 24),
              const Text(
                'Chờ hoàn tiền',
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF82500F),
                ),
              ),
              const Text(
                'Yêu cầu đã gửi tới CEO. Sau khi cửa hàng xác nhận chuyển hoàn, lời nhắn và biên lai sẽ hiển thị tại đây.',
              ),
            ] else if (!_busy &&
                widget.trangThaiThanhToan == 'DA_HOAN_TIEN') ...[
              const Divider(height: 24),
              const Text(
                'Kết quả hoàn tiền',
                style: TextStyle(
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF184F36),
                ),
              ),
              const Text(
                'Hồ sơ này đã ghi nhận hoàn tiền nhưng CEO chưa đính kèm biên lai. Cửa hàng cần bổ sung biên lai và lời nhắn.',
              ),
            ],
            for (final a in (_data!['anh'] as List? ?? []))
              TextButton.icon(
                onPressed: () => _xem(a['maAnh']),
                icon: const Icon(Icons.image_outlined),
                label: const Text('Xem ảnh bằng chứng'),
              ),
          ],
        ],
      ),
    ),
  );
}
