import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../models.dart';
import '../../phien.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'cau_hinh_in_screen.dart';

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
  bool _dangTai = false;
  String? _loi;
  FileIn? _file;   // file vừa tải lên xong

  Future<void> _chonFile() async {
    // Mở trình chọn file của điện thoại, chỉ cho chọn các đuôi máy chủ nhận
    PlatformFile? f = await FilePicker.pickFile(type: FileType.custom, allowedExtensions: dinhDangDuocChon);
    if (f == null || f.path == null) return; // người dùng bấm hủy

    setState(() {
      _dangTai = true;
      _loi = null;
    });

    // POST /api/file-in — máy chủ lưu file, đổi sang PDF, đếm số trang
    KetQuaApi kq = await Api.taiFile(f.path!, f.name);
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
    Navigator.push(context, MaterialPageRoute(builder: (context) => CauHinhInScreen(file: file)));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(tieuDe: 'Đặt in tài liệu', phuDe: 'Bước 1 / 4 · Tải file lên', soBuoc: 4, buocHienTai: 1),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Xin chào, ${Phien.nguoiDung?.hoTen ?? ''}',
                style: AppTheme.font(size: 18, weight: FontWeight.w800, letterSpacing: -0.3)),
            const SizedBox(height: 4),
            Text('Tải tài liệu lên, hệ thống tự đếm số trang và báo giá ngay.',
                style: AppTheme.font(size: 12.5, color: AppColors.chuPhu, height: 1.5)),
            const SizedBox(height: 16),
            if (_loi != null) ...[HopThongBao(_loi!), const SizedBox(height: 12)],
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
                          child: const Center(child: AppIcon(Ico.taiLen, size: 24, color: AppColors.accent, net: 1.8)),
                        ),
                        const SizedBox(height: 12),
                        if (_dangTai) ...[
                          Text('Đang tải lên, chuẩn hóa PDF và đếm số trang…',
                              textAlign: TextAlign.center, style: AppTheme.font(size: 13.5, weight: FontWeight.w700)),
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
                          Text('Chạm để chọn file cần in', style: AppTheme.font(size: 15, weight: FontWeight.w700)),
                          const SizedBox(height: 4),
                          Text('PDF, Word, Excel, PowerPoint, văn bản hoặc ảnh · tối đa 60 MB',
                              textAlign: TextAlign.center, style: AppTheme.font(size: 12, color: AppColors.chuPhu)),
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
                    child: AppIcon(Ico.thongTin, size: 14, color: AppColors.iconMo, net: 1.9),
                  ),
                  const SizedBox(width: 9),
                  Expanded(
                    child: Text(
                      'File Word, Excel, PowerPoint và ảnh được máy chủ tự chuyển sang PDF để in đúng bố cục và đếm đúng số trang. '
                      'File chỉ lưu tạm trên máy chủ và bị xóa ngay sau khi in xong.',
                      style: AppTheme.font(size: 11, color: AppColors.chuPhu, height: 1.55),
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
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12), side: const BorderSide(color: AppColors.vach)),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => _moCauHinh(f),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 11, 12, 11),
          child: Row(
            children: [
              const AppIcon(Ico.taiLieu, size: 20, color: AppColors.accent, net: 1.6),
              const SizedBox(width: 11),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(f.tenFileGoc,
                        maxLines: 1, overflow: TextOverflow.ellipsis, style: AppTheme.font(size: 13, weight: FontWeight.w600)),
                    Text('${f.soTrang ?? 0} trang · ${dungLuong(f.dungLuong)} · Tiếp tục cấu hình',
                        style: AppTheme.font(size: 11, color: AppColors.chuPhu)),
                  ],
                ),
              ),
              const AppIcon(Ico.muiTenPhai, size: 17, color: AppColors.accent, net: 2),
            ],
          ),
        ),
      ),
    );
  }
}
