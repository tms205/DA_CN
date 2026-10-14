import 'dart:async';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../api.dart';
import '../../dinh_dang.dart';
import '../../giao_hang.dart';
import '../../phien.dart';
import '../../realtime.dart';
import '../../theme.dart';
import '../../widgets/widgets.dart';

class ShipperScreen extends StatefulWidget {
  const ShipperScreen({super.key, this.taiDanhSach, this.guiThaoTac});
  final Future<KetQuaApi> Function(String)? taiDanhSach;
  final Future<KetQuaApi> Function(String, Map<String, dynamic>)? guiThaoTac;
  @override
  State<ShipperScreen> createState() => _ShipperScreenState();
}

class _ShipperScreenState extends State<ShipperScreen>
    with WidgetsBindingObserver {
  String _nhom = 'CHO_GIAO';
  List<DonGiao> _ds = [];
  String? _loi;
  bool _dangTai = true, _dangGui = false;
  int _lanGoi = 0;
  Timer? _henTai;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    Realtime.dangKyNghe(_coTin);
    _tai();
  }

  @override
  void dispose() {
    _lanGoi++;
    _henTai?.cancel();
    Realtime.boNghe(_coTin);
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _tai();
  }

  void _coTin(Map<String, dynamic> tin) {
    _henTai?.cancel();
    _henTai = Timer(const Duration(milliseconds: 350), () {
      if (mounted) _tai();
    });
  }

  Future<void> _tai() async {
    int lan = ++_lanGoi;
    setState(() {
      _dangTai = true;
      _loi = null;
    });
    final kq =
        await (widget.taiDanhSach?.call('/api/giao-hang?nhom=$_nhom') ??
            Api.get('/api/giao-hang?nhom=$_nhom'));
    if (!mounted || lan != _lanGoi) return;
    setState(() {
      _dangTai = false;
      if (kq.thanhCong) {
        _ds = (kq.duLieu as List)
            .map((x) => DonGiao.fromJson(Map<String, dynamic>.from(x)))
            .toList();
      } else {
        _loi = kq.loi;
        _ds = [];
      }
    });
  }

  Future<void> _gui(String path, Map<String, dynamic> body) async {
    setState(() {
      _dangGui = true;
      _loi = null;
    });
    try {
      final kq =
          await (widget.guiThaoTac?.call(path, body) ?? Api.post(path, body));
      if (!mounted) return;
      if (kq.thanhCong) {
        hienThongBaoNhanh(
          context,
          kq.duLieu['message'] ?? 'Đã cập nhật giao hàng.',
        );
        await _tai();
      } else {
        setState(() => _loi = kq.loi);
      }
    } finally {
      if (mounted) setState(() => _dangGui = false);
    }
  }

  Future<void> _nhan(DonGiao don) async {
    bool? dongY = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Nhận ${don.maHienThi}'),
        content: const Text(
          'Chỉ xác nhận khi bạn đã nhận đúng tài liệu tại chi nhánh. Khách đã thanh toán, không thu COD.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Đã nhận tài liệu'),
          ),
        ],
      ),
    );
    if (dongY == true && mounted && !_dangGui) {
      await _gui('/api/giao-hang/${don.maDonHang}/nhan', {});
    }
  }

  Future<void> _ketQua(DonGiao don, bool thanhCong) async {
    final lyDo = TextEditingController();
    String? loiNhap;
    final hop = DialogRoute<String>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, doi) => AlertDialog(
          title: Text(
            thanhCong ? 'Xác nhận giao thành công' : 'Ghi nhận giao thất bại',
          ),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  thanhCong
                      ? 'Bạn đã giao đúng tài liệu ${don.maHienThi} cho người nhận? Không thu thêm tiền. Đơn sẽ chuyển sang Hoàn tất.'
                      : 'Ghi rõ lý do để cửa hàng và khách biết cách xử lý. Không tự hủy đơn hoặc hoàn tiền.',
                ),
                if (!thanhCong) ...[
                  const SizedBox(height: 12),
                  TextField(
                    controller: lyDo,
                    maxLength: 255,
                    minLines: 2,
                    maxLines: 4,
                    decoration: InputDecoration(
                      labelText: 'Lý do giao thất bại',
                      errorText: loiNhap,
                      border: const OutlineInputBorder(),
                    ),
                  ),
                ],
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Hủy'),
            ),
            FilledButton(
              onPressed: () {
                if (!thanhCong && lyDo.text.trim().length < 5) {
                  doi(() => loiNhap = 'Nhập lý do ít nhất 5 ký tự.');
                  return;
                }
                Navigator.pop(ctx, lyDo.text.trim());
              },
              child: const Text('Xác nhận'),
            ),
          ],
        ),
      ),
    );
    String? ketQua = await Navigator.of(context).push(hop);
    // Chờ route thực sự tháo khỏi cây; không đoán thời gian animation.
    await hop.completed;
    lyDo.dispose();
    if (ketQua != null && mounted && !_dangGui && don.maGiaoHang != null) {
      await _gui('/api/giao-hang/luot/${don.maGiaoHang}/ket-qua', {
        'thanhCong': thanhCong,
        'lyDo': thanhCong ? null : ketQua,
      });
    }
  }

  Future<void> _moLienKet(Uri uri) async {
    try {
      if (!await launchUrl(uri, mode: LaunchMode.externalApplication)) {
        throw StateError('Không mở được');
      }
    } catch (_) {
      if (mounted) {
        hienThongBaoNhanh(
          context,
          'Không mở được ứng dụng hỗ trợ. Bạn có thể sao chép địa chỉ/SĐT bên trên.',
        );
      }
    }
  }

  Widget _the(DonGiao don) => Card(
    key: ValueKey('${don.maDonHang}-${don.maGiaoHang}-$_nhom'),
    margin: const EdgeInsets.only(bottom: 12),
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            don.maHienThi,
            style: AppTheme.font(size: 17, weight: FontWeight.w800),
          ),
          const SizedBox(height: 6),
          Text(
            '${don.tenKhach} · ${don.sdt}',
            style: AppTheme.font(size: 14, weight: FontWeight.w600),
          ),
          const SizedBox(height: 8),
          SelectableText('Lấy tại: ${don.diaChiChiNhanh}'),
          const SizedBox(height: 8),
          SelectableText('Giao đến: ${don.diaChi}'),
          const SizedBox(height: 8),
          Text(
            'Đã thanh toán ${tien(don.tongTien)} · phí giao ${tien(don.phiShip)}',
          ),
          Text(
            'Lượt giao: ${_nhom == 'LICH_SU' ? don.lanGiao ?? don.soLan : don.soLan}/3',
            style: AppTheme.font(size: 12, color: AppColors.chuPhu),
          ),
          if (don.lyDo != null && don.lyDo!.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: HopThongBao(
                'Lý do: ${don.lyDo}',
                loai: LoaiThongBao.canhBao,
              ),
            ),
          if (_nhom == 'LICH_SU')
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(
                don.trangThaiGiao == 'THANH_CONG'
                    ? 'Đã giao thành công'
                    : 'Giao thất bại',
              ),
            ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              if (don.sdt.isNotEmpty)
                OutlinedButton.icon(
                  onPressed: () =>
                      _moLienKet(Uri(scheme: 'tel', path: don.sdt)),
                  icon: const Icon(Icons.call_outlined),
                  label: const Text('Gọi khách'),
                ),
              OutlinedButton.icon(
                onPressed: () => _moLienKet(
                  Uri.https('www.google.com', '/maps/dir/', {
                    'api': '1',
                    'destination': don.diaChi.split(' · ')[0],
                    'travelmode': 'driving',
                  }),
                ),
                icon: const Icon(Icons.map_outlined),
                label: const Text('Mở đường đi'),
              ),
            ],
          ),
          if (_nhom == 'CHO_GIAO') ...[
            const SizedBox(height: 8),
            if (don.soLan >= 3)
              const HopThongBao(
                'Đã đủ 3 lượt. Cửa hàng cần liên hệ khách để xử lý.',
                loai: LoaiThongBao.canhBao,
              )
            else
              FilledButton(
                onPressed: _dangGui ? null : () => _nhan(don),
                style: FilledButton.styleFrom(minimumSize: const Size(0, 48)),
                child: Text(_dangGui ? 'Đang xử lý…' : 'Nhận tài liệu đi giao'),
              ),
          ],
          if (_nhom == 'DANG_GIAO') ...[
            const SizedBox(height: 8),
            FilledButton(
              onPressed: _dangGui ? null : () => _ketQua(don, true),
              style: FilledButton.styleFrom(minimumSize: const Size(0, 48)),
              child: const Text('Đã giao thành công'),
            ),
            const SizedBox(height: 8),
            OutlinedButton(
              onPressed: _dangGui ? null : () => _ketQua(don, false),
              style: OutlinedButton.styleFrom(minimumSize: const Size(0, 48)),
              child: const Text('Không giao được'),
            ),
          ],
        ],
      ),
    ),
  );

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.nen,
    appBar: ThanhTieuDe(
      tieuDe: 'Đơn đi giao',
      phuDe: Phien.nguoiDung?.tenChiNhanh ?? '',
      coNutQuayLai: false,
    ),
    body: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: Text(
            'Khách đã thanh toán trước · Không thu COD',
            style: AppTheme.font(
              size: 13,
              weight: FontWeight.w600,
              color: AppColors.accentDeep,
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
          child: Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final nhom in {
                'CHO_GIAO': 'Chờ giao',
                'DANG_GIAO': 'Đang giao',
                'LICH_SU': 'Lịch sử',
              }.entries)
                ChoiceChip(
                  label: Text(nhom.value),
                  selected: _nhom == nhom.key,
                  onSelected: _dangGui
                      ? null
                      : (_) {
                          setState(() => _nhom = nhom.key);
                          _tai();
                        },
                ),
            ],
          ),
        ),
        if (_loi != null)
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: HopThongBao(_loi!),
          ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  'Tối đa 100 ${_nhom == 'LICH_SU' ? 'lượt gần nhất' : 'đơn'} · ${_ds.length} mục',
                  style: AppTheme.font(size: 12, color: AppColors.chuPhu),
                ),
              ),
              TextButton.icon(
                onPressed: _dangTai ? null : _tai,
                icon: const Icon(Icons.refresh),
                label: const Text('Tải lại'),
              ),
            ],
          ),
        ),
        Expanded(
          child: _dangTai
              ? const Center(child: CircularProgressIndicator())
              : RefreshIndicator(
                  onRefresh: _tai,
                  child: ListView.builder(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                    itemCount: _ds.isEmpty ? 1 : _ds.length,
                    itemBuilder: (ctx, i) => _ds.isEmpty
                        ? const Padding(
                            padding: EdgeInsets.all(24),
                            child: Text('Không có đơn trong nhóm này.'),
                          )
                        : _the(_ds[i]),
                  ),
                ),
        ),
      ],
    ),
  );
}
