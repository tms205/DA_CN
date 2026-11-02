import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../api.dart';
import '../../models.dart';
import '../../theme.dart';
import '../../widgets/widgets.dart';

class DanhGiaScreen extends StatefulWidget {
  final Future<KetQuaApi> Function(String)? taiDuLieu;
  const DanhGiaScreen({super.key, this.taiDuLieu});
  @override
  State<DanhGiaScreen> createState() => _DanhGiaScreenState();
}

class _DanhGiaScreenState extends State<DanhGiaScreen> {
  List<ChiNhanh> _branches = [];
  final List<Map<String, dynamic>> _reviews = [];
  int? _branch;
  int _page = 1, _generation = 0, _count = 0;
  double _average = 0;
  bool _loadingBranches = true,
      _loading = false,
      _more = false,
      _hasMore = false;
  String? _error;
  Future<KetQuaApi> _get(String path) =>
      widget.taiDuLieu?.call(path) ?? Api.get(path);
  @override
  void initState() {
    super.initState();
    _loadBranches();
  }

  Future<void> _loadBranches() async {
    setState(() {
      _loadingBranches = true;
      _error = null;
    });
    final response = await _get('/api/danh-muc/chi-nhanh');
    if (!mounted) return;
    if (!response.thanhCong) {
      setState(() {
        _loadingBranches = false;
        _error = response.loi;
      });
      return;
    }
    final branches = (response.duLieu as List)
        .map((p) => ChiNhanh.fromJson(Map<String, dynamic>.from(p)))
        .toList();
    setState(() {
      _branches = branches;
      _branch = branches.any((p) => p.maChiNhanh == _branch)
          ? _branch
          : branches.firstOrNull?.maChiNhanh;
      _loadingBranches = false;
    });
    if (_branch != null) await _load();
  }

  Future<void> _load({bool more = false}) async {
    final branch = _branch;
    if (branch == null || (more && (_loading || _more || !_hasMore))) return;
    final generation = more ? _generation : ++_generation;
    final page = more ? _page + 1 : 1;
    setState(() {
      _error = null;
      if (more) {
        _more = true;
      } else {
        _loading = true;
        _more = false;
        _reviews.clear();
        _hasMore = false;
        _count = 0;
        _average = 0;
      }
    });
    final response = await _get(
      '/api/danh-muc/chi-nhanh/$branch/danh-gia?trang=$page',
    );
    if (!mounted || generation != _generation || branch != _branch) return;
    setState(() {
      _loading = false;
      _more = false;
      if (!response.thanhCong) {
        _error = response.loi;
        return;
      }
      final data = Map<String, dynamic>.from(response.duLieu);
      _count = (data['soLuotDanhGia'] as num?)?.toInt() ?? 0;
      _average = (data['diemTrungBinh'] as num?)?.toDouble() ?? 0;
      _reviews.addAll(
        (data['danhGia'] as List).map((p) => Map<String, dynamic>.from(p)),
      );
      _hasMore = data['coTrangSau'] == true;
      _page = page;
    });
  }

  Widget _review(Map<String, dynamic> review) {
    final stars = ((review['soSao'] as num?)?.toInt() ?? 0).clamp(0, 5);
    final date = DateTime.tryParse(review['ngayDanhGia'] as String? ?? '');
    final text = (review['nhanXet'] as String? ?? '').trim();
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.trang,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.vien),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  review['tenKhachHang'] as String? ?? 'Khách hàng',
                  style: AppTheme.font(size: 14, weight: FontWeight.w700),
                ),
              ),
              if (date != null)
                Text(
                  DateFormat('dd/MM/yyyy').format(date),
                  style: AppTheme.font(size: 12, color: AppColors.chuNhan),
                ),
            ],
          ),
          const SizedBox(height: 8),
          Semantics(
            label: '$stars trên 5 sao',
            child: Row(
              children: List.generate(
                5,
                (i) => Icon(
                  i < stars ? Icons.star_rounded : Icons.star_outline_rounded,
                  size: 20,
                  color: const Color(0xFFB66D10),
                ),
              ),
            ),
          ),
          const SizedBox(height: 10),
          Text(
            text.isEmpty
                ? 'Khách hàng đánh giá bằng sao, không kèm nhận xét.'
                : text,
            style: AppTheme.font(size: 14, color: AppColors.chuNhan),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Đánh giá chi nhánh'),
      automaticallyImplyLeading: false,
      actions: [
        IconButton(
          tooltip: 'Làm mới đánh giá',
          onPressed: _loadingBranches || _loading || _more
              ? null
              : () => _loadBranches(),
          icon: const Icon(Icons.refresh),
        ),
      ],
    ),
    body: RefreshIndicator(
      onRefresh: _loadBranches,
      child: ListView(
        padding: const EdgeInsets.all(16),
        physics: const AlwaysScrollableScrollPhysics(),
        children: [
          Text(
            'Khách hàng nói gì về cửa hàng?',
            style: AppTheme.font(size: 20, weight: FontWeight.w800),
          ),
          const SizedBox(height: 6),
          Text(
            'Chọn chi nhánh để xem điểm và những phản hồi đang được hiển thị.',
            style: AppTheme.font(size: 13, color: AppColors.chuNhan),
          ),
          const SizedBox(height: 20),
          if (_loadingBranches)
            const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: CircularProgressIndicator(),
              ),
            )
          else if (_branches.isEmpty && _error == null)
            const HopThongBao('Chưa có chi nhánh đang hoạt động.')
          else if (_branches.isNotEmpty)
            DropdownButtonFormField<int>(
              key: ValueKey(_branch),
              initialValue: _branch,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'Chi nhánh',
                border: OutlineInputBorder(),
              ),
              items: _branches
                  .map(
                    (p) => DropdownMenuItem(
                      value: p.maChiNhanh,
                      child: Text(
                        p.tenChiNhanh,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  )
                  .toList(),
              onChanged: (id) {
                if (id == null || id == _branch) return;
                setState(() => _branch = id);
                _load();
              },
            ),
          const SizedBox(height: 16),
          if (_loading)
            const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: CircularProgressIndicator(),
              ),
            )
          else if (!_loadingBranches && _error == null && _branch != null)
            Container(
              padding: const EdgeInsets.all(20),
              margin: const EdgeInsets.only(bottom: 20),
              decoration: BoxDecoration(
                color: AppColors.accentSoft,
                borderRadius: BorderRadius.circular(16),
              ),
              child: Row(
                children: [
                  const Icon(
                    Icons.star_rounded,
                    color: Color(0xFFB66D10),
                    size: 36,
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          _count == 0
                              ? 'Chưa có đánh giá'
                              : '${_average.toStringAsFixed(1)}/5',
                          style: AppTheme.font(
                            size: 24,
                            weight: FontWeight.w800,
                            color: AppColors.accentDam,
                          ),
                        ),
                        Text(
                          '$_count đánh giá đang hiển thị',
                          style: AppTheme.font(
                            size: 13,
                            color: AppColors.chuNhan,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          if (_error != null) ...[
            HopThongBao(_error!, loai: LoaiThongBao.loi),
            const SizedBox(height: 8),
            OutlinedButton(
              onPressed: () => _branches.isEmpty
                  ? _loadBranches()
                  : _load(more: _reviews.isNotEmpty),
              child: const Text('Thử lại'),
            ),
          ],
          if (!_loading &&
              !_loadingBranches &&
              _error == null &&
              _branch != null &&
              _reviews.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 24),
              child: Text(
                'Chi nhánh chưa có phản hồi công khai. Bạn có thể đánh giá sau khi hoàn tất đơn hàng.',
              ),
            ),
          if (!_loading && !_loadingBranches) ..._reviews.map(_review),
          if (_hasMore)
            OutlinedButton(
              onPressed: _loading || _more ? null : () => _load(more: true),
              child: Text(_more ? 'Đang tải…' : 'Xem thêm đánh giá'),
            ),
        ],
      ),
    ),
  );
}
