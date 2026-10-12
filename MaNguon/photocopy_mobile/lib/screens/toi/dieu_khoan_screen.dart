import 'package:flutter/material.dart';

import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';

// Điều khoản sử dụng & chính sách bảo mật — bản tóm tắt, bám đúng cách hệ thống đang xử lý dữ liệu.
class DieuKhoanScreen extends StatelessWidget {
  const DieuKhoanScreen({super.key});

  static const _dieuKhoan = [
    'Bạn chịu trách nhiệm về nội dung tài liệu tải lên; không in tài liệu vi phạm pháp luật hoặc bản quyền.',
    'Giá, khuyến mãi và phí giao hàng do máy chủ tính theo bảng giá hiện hành của chuỗi tại thời điểm đặt đơn.',
    'Giờ in xong là ước tính theo hàng đợi thực tế của chi nhánh, có thể lệch khi máy in bảo trì hoặc đông đơn.',
    'Giao tận nơi chỉ áp dụng trong bán kính phục vụ của từng chi nhánh.',
  ];

  static const _baoMat = [
    'Mật khẩu chỉ lưu dạng băm (bcrypt); nhân viên chuỗi không xem được mật khẩu của bạn.',
    'File tải lên lưu trên máy chủ ngoài thư mục công khai, không có đường dẫn truy cập trực tiếp và bị xóa sau khi in xong.',
    'Email và số điện thoại dùng để đăng nhập, gửi mã OTP và để chi nhánh liên hệ khi giao / nhận tài liệu.',
    'Phiên đăng nhập được mã hóa (JWT) và chỉ lưu trong kho bảo mật của điện thoại khi bạn bật "Ghi nhớ đăng nhập".',
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(tieuDe: 'Điều khoản & chính sách'),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
        children: [
          _muc('Điều khoản sử dụng', Ico.taiLieu, _dieuKhoan),
          const SizedBox(height: 14),
          _muc('Chính sách bảo mật', Ico.khien, _baoMat),
          const SizedBox(height: 14),
          Text('Bản tóm tắt dùng cho đồ án CNTT-KLCN031 · cập nhật 09/2026',
              textAlign: TextAlign.center, style: AppTheme.font(size: 11, color: AppColors.iconMo)),
        ],
      ),
    );
  }

  // Một thẻ: tiêu đề + danh sách đánh số 1, 2, 3...
  Widget _muc(String tieuDe, String icon, List<String> ds) => Container(
        padding: const EdgeInsets.fromLTRB(16, 14, 16, 8),
        decoration: BoxDecoration(
          color: AppColors.trang,
          border: Border.all(color: AppColors.vach),
          borderRadius: BorderRadius.circular(14),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                AppIcon(icon, size: 18, color: AppColors.accent, net: 1.8),
                const SizedBox(width: 8),
                Text(tieuDe, style: AppTheme.font(size: 14.5, weight: FontWeight.w800)),
              ],
            ),
            const SizedBox(height: 10),
            for (var i = 0; i < ds.length; i++)
              Padding(
                padding: const EdgeInsets.only(bottom: 9),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    SizedBox(
                      width: 20,
                      child: Text('${i + 1}.', style: AppTheme.font(size: 12.5, weight: FontWeight.w700, color: AppColors.accent)),
                    ),
                    Expanded(child: Text(ds[i], style: AppTheme.font(size: 12.5, color: AppColors.chuNhan, height: 1.55))),
                  ],
                ),
              ),
          ],
        ),
      );
}
