import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../models.dart';
import '../../quy_cach_anh_the.dart';
import '../../phien.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'cau_hinh_in_screen.dart';
import 'xem_mau_don_screen.dart';

// Các đuôi file máy chủ nhận (trùng danh sách trong FileInController của API) — mọi loại đều được đổi sang PDF
const List<String> dinhDangDuocChon = [
  'pdf', 'doc', 'docx', 'rtf', 'odt', 'txt', //
  'xls', 'xlsx', 'ods', 'csv', //
  'ppt', 'pptx', 'odp', //
  'png', 'jpg', 'jpeg',
];

// Tab "Đặt in" của khách hàng — Bước 1/4: chọn file và tải lên (F-06).
// Figma chưa vẽ bước này nên dựng cùng phong cách màn Cấu hình in.
class ChonFileScreen extends StatefulWidget {
  const ChonFileScreen({super.key});

  @override
  State<ChonFileScreen> createState() => _ChonFileScreenState();
}

class _ChonFileScreenState extends State<ChonFileScreen> {
  String _loai = 'TAI_LIEU';
  String _kichThuoc = '3X4';
  final _soAnh = TextEditingController(text: '8');
  List<Map<String, dynamic>> _mauDon = [];
  String _timMau = '';
  String _nhomMau = '';
  List<Map<String, dynamic>> get _mauDaLoc => _mauDon.where((mau) {
    final tuKhoa = _timMau.trim().toLowerCase();
    return (_nhomMau.isEmpty || mau['nhom'] == _nhomMau) &&
      ('${mau['tenMau']} ${mau['nguon']}'.toLowerCase().contains(tuKhoa));
  }).toList();

  Future<void> _xemMau(Map<String, dynamic> mau) async {
    if (_dangTai) return;
    final chon = await Navigator.push<bool>(context,
      MaterialPageRoute(builder: (context) => XemMauDonScreen(mau: mau)));
    if (!mounted || chon != true) return;
    await _chonMau(mau);
  }

  @override
  void dispose() {
    _soAnh.dispose();
    super.dispose();
  }

  Future<void> _taiMauDon() async {
    if (_dangTai) return;
    setState(() {
      _dangTai = true;
      _loi = null;
    });
    final kq = await Api.get('/api/file-in/mau');
    if (!mounted) return;
    setState(() {
      _dangTai = false;
      if (kq.thanhCong) {
        _mauDon = List<Map<String, dynamic>>.from(kq.duLieu);
        if (!_mauDon.any((mau) => mau['nhom'] == _nhomMau)) _nhomMau = '';
      } else {
        _loi = kq.loi;
      }
    });
  }

  Future<void> _chonMau(Map<String, dynamic> mau) async {
    if (_dangTai) return;
    setState(() {
      _dangTai = true;
      _loi = null;
    });
    final phienBan = mau['phienBan'] as int?;
    final kq = await Api.post('/api/file-in/mau/${Uri.encodeComponent(mau['maMau'])}${phienBan == null ? '' : '?phienBan=$phienBan'}', {});
    if (!mounted) return;
    setState(() => _dangTai = false);
    if (!kq.thanhCong) {
      setState(() => _loi = kq.loi);
      return;
    }
    final file = FileIn.fromJson(kq.duLieu);
    setState(() => _file = file);
    _moCauHinh(file);
  }

  bool _dangTai = false;
  String? _loi;
  FileIn? _file; // file vừa tải lên xong

  Future<void> _chonFile() async {
    if (_dangTai) return;
    int? soAnh = int.tryParse(_soAnh.text);
    if (_loai == 'ANH_THE' && (soAnh == null || soAnh < 1 || soAnh > 100)) {
      setState(() => _loi = 'Số lượng phải từ 1 đến 100 ảnh mỗi file.');
      return;
    }
    // Mở trình chọn file của điện thoại, chỉ cho chọn các đuôi máy chủ nhận
    PlatformFile? f = await FilePicker.pickFile(
      type: FileType.custom,
      allowedExtensions: _loai == 'ANH_THE'
          ? ['jpg', 'jpeg', 'png']
          : dinhDangDuocChon,
    );
    if (!mounted) return;
    if (f == null || f.path == null) return; // người dùng bấm hủy

    setState(() {
      _dangTai = true;
      _loi = null;
    });

    // POST /api/file-in — máy chủ lưu file, đổi sang PDF, đếm số trang
    KetQuaApi kq = await Api.taiFile(
      f.path!,
      f.name,
      kichThuocAnh: _loai == 'ANH_THE' ? _kichThuoc : null,
      soLuongAnh: _loai == 'ANH_THE' ? soAnh : null,
    );
    if (!mounted) return;
    setState(() => _dangTai = false);

    if (!kq.thanhCong) {
      setState(() => _loi = kq.loi);
      return;
    }

    FileIn file = FileIn.fromJson(kq.duLieu);
    setState(() => _file = file);
    _moCauHinh(file);
  }

  // Sang bước 2: chọn quy cách in
  void _moCauHinh(FileIn file) {
    Navigator.push(
      context,
      MaterialPageRoute(builder: (context) => CauHinhInScreen(file: file)),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(
        tieuDe: 'Đặt in tài liệu',
        phuDe: 'Bước 1 / 4 · Tải file lên',
        soBuoc: 4,
        buocHienTai: 1,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Xin chào, ${Phien.nguoiDung?.hoTen ?? ''}',
              style: AppTheme.font(
                size: 18,
                weight: FontWeight.w800,
                letterSpacing: -0.3,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              'Tải tài liệu lên, hệ thống tự đếm số trang và báo giá ngay.',
              style: AppTheme.font(
                size: 12.5,
                color: AppColors.chuPhu,
                height: 1.5,
              ),
            ),
            const SizedBox(height: 16),
            DropdownButtonFormField<String>(
              initialValue: _loai,
              decoration: const InputDecoration(
                labelText: 'Dịch vụ đặt in',
                border: OutlineInputBorder(),
              ),
              isExpanded: true,
              items: const [
                DropdownMenuItem(value: 'TAI_LIEU', child: Text('In tài liệu')),
                DropdownMenuItem(
                  value: 'MAU_DON',
                  child: Text('In mẫu đơn trống'),
                ),
                DropdownMenuItem(value: 'ANH_THE', child: Text('In ảnh thẻ')),
              ],
              onChanged: _dangTai
                  ? null
                  : (v) {
                      setState(() {
                        _loai = v!;
                        _loi = null;
                        _timMau = '';
                        _nhomMau = '';
                      });
                      if (_loai == 'MAU_DON') _taiMauDon();
                    },
            ),
            const SizedBox(height: 16),
            if (_loai == 'ANH_THE') ...[
              DropdownButtonFormField<String>(
                initialValue: _kichThuoc,
                decoration: const InputDecoration(
                  labelText: 'Kích thước mỗi ảnh',
                  border: OutlineInputBorder(),
                ),
                items: kichThuocAnhThe.entries
                    .map(
                      (q) =>
                          DropdownMenuItem(value: q.key, child: Text(q.value)),
                    )
                    .toList(),
                onChanged: _dangTai
                    ? null
                    : (v) => setState(() => _kichThuoc = v!),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _soAnh,
                enabled: !_dangTai,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                  labelText: 'Số ảnh (1–100)',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'Giá tính từng ảnh. Ảnh được căn vừa khung, giữ tỉ lệ và có nền trắng nếu lệch tỉ lệ; không tự cắt mặt. Giấy ảnh A4, in màu một mặt.',
              ),
              const SizedBox(height: 16),
            ],
            if (_loi != null) ...[
              HopThongBao(_loi!),
              const SizedBox(height: 12),
            ],
            if (_loai == 'MAU_DON') ...[
              const Text(
                'Xem bản trống tải sẵn, kiểm tra nguồn rồi chọn để in. Dùng mẫu riêng nếu nơi nhận yêu cầu.',
              ),
              const SizedBox(height: 12),
              if (_dangTai) const LinearProgressIndicator(),
              OutlinedButton.icon(
                style: OutlinedButton.styleFrom(minimumSize: const Size(48, 48)),
                onPressed: _dangTai ? null : _taiMauDon,
                icon: const Icon(Icons.refresh),
                label: const Text('Làm mới thư viện mẫu'),
              ),
              const SizedBox(height: 12),
              TextField(
                decoration: const InputDecoration(labelText: 'Tìm mẫu đơn', hintText: 'Tên mẫu hoặc nơi công bố', prefixIcon: Icon(Icons.search)),
                onChanged: (value) => setState(() => _timMau = value),
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                key: ValueKey(_nhomMau),
                initialValue: _nhomMau, isExpanded: true,
                decoration: const InputDecoration(labelText: 'Nhóm mẫu'),
                items: [const DropdownMenuItem(value: '', child: Text('Tất cả')),
                  ..._mauDon.map((m) => m['nhom'] as String? ?? '').where((n) => n.isNotEmpty).toSet().map((n) => DropdownMenuItem(value: n, child: Text(n)))],
                onChanged: (value) => setState(() => _nhomMau = value ?? ''),
              ),
              const SizedBox(height: 12),
              for (final mau in _mauDaLoc)
                Card(
                  child: ListTile(
                    title: Text(mau['tenMau']),
                    subtitle: Text('${mau['nguon'] ?? mau['moTa']} · ${mau['soTrang'] ?? 1} trang\nXem mẫu trước khi in'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: _dangTai ? null : () => _xemMau(mau),
                  ),
                ),
              if (!_dangTai && _mauDon.isEmpty)
                OutlinedButton(
                  onPressed: _taiMauDon,
                  child: const Text('Thử lại'),
                ),
              if (!_dangTai && _mauDon.isNotEmpty && _mauDaLoc.isEmpty)
                const Padding(padding: EdgeInsets.all(16), child: Text('Không có mẫu phù hợp. Thử tên khác hoặc chọn tất cả nhóm.')),
            ] else
              GestureDetector(
                onTap: _dangTai ? null : _chonFile,
                child: KhungNetDut(
                  mauVien: AppColors.accentTint,
                  banKinh: 14,
                  child: SizedBox(
                    height: 190,
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 24),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Container(
                            width: 54,
                            height: 54,
                            decoration: BoxDecoration(
                              color: AppColors.accentSoft,
                              shape: BoxShape.circle,
                              border: Border.all(color: AppColors.accentTint),
                            ),
                            child: const Center(
                              child: AppIcon(
                                Ico.taiLen,
                                size: 24,
                                color: AppColors.accent,
                                net: 1.8,
                              ),
                            ),
                          ),
                          const SizedBox(height: 12),
                          if (_dangTai) ...[
                            Text(
                              'Đang tải lên, chuẩn hóa PDF và đếm số trang…',
                              textAlign: TextAlign.center,
                              style: AppTheme.font(
                                size: 13.5,
                                weight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 10),
                            ClipRRect(
                              borderRadius: BorderRadius.circular(3),
                              child: const LinearProgressIndicator(
                                minHeight: 5,
                                color: AppColors.accent,
                                backgroundColor: AppColors.vach,
                              ),
                            ),
                          ] else ...[
                            Text(
                              'Chạm để chọn file cần in',
                              style: AppTheme.font(
                                size: 15,
                                weight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              'PDF, Word, Excel, PowerPoint, văn bản hoặc ảnh · tối đa 60 MB',
                              textAlign: TextAlign.center,
                              style: AppTheme.font(
                                size: 12,
                                color: AppColors.chuPhu,
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            if (_file != null) ...[
              const SizedBox(height: 14),
              _theFileVuaTai(_file!),
            ],
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.fromLTRB(12, 11, 12, 11),
              decoration: BoxDecoration(
                color: AppColors.trang,
                border: Border.all(color: AppColors.vach),
                borderRadius: BorderRadius.circular(11),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Padding(
                    padding: EdgeInsets.only(top: 1),
                    child: AppIcon(
                      Ico.thongTin,
                      size: 14,
                      color: AppColors.iconMo,
                      net: 1.9,
                    ),
                  ),
                  const SizedBox(width: 9),
                  Expanded(
                    child: Text(
                      'File Word, Excel, PowerPoint và ảnh được máy chủ tự chuyển sang PDF để in đúng bố cục và đếm đúng số trang. '
                      'File chỉ lưu tạm trên máy chủ và bị xóa ngay sau khi in xong.',
                      style: AppTheme.font(
                        size: 11,
                        color: AppColors.chuPhu,
                        height: 1.55,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  // Thẻ file vừa tải — bấm vào để quay lại màn cấu hình in
  Widget _theFileVuaTai(FileIn f) {
    return Material(
      color: AppColors.trang,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: AppColors.vach),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => _moCauHinh(f),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 11, 12, 11),
          child: Row(
            children: [
              const AppIcon(
                Ico.taiLieu,
                size: 20,
                color: AppColors.accent,
                net: 1.6,
              ),
              const SizedBox(width: 11),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      f.tenFileGoc,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTheme.font(size: 13, weight: FontWeight.w600),
                    ),
                    Text(
                      '${f.moTaSoLuong} · ${dungLuong(f.dungLuong)} · Tiếp tục cấu hình',
                      style: AppTheme.font(size: 11, color: AppColors.chuPhu),
                    ),
                  ],
                ),
              ),
              const AppIcon(
                Ico.muiTenPhai,
                size: 17,
                color: AppColors.accent,
                net: 2,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
