import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../theme.dart';

// Chỉ sử dụng trên đơn đã được tạo, không dùng trên báo giá hoặc cấu hình in.
class GioInXong extends StatelessWidget {
  const GioInXong({
    super.key,
    required this.trangThai,
    this.duKien,
    this.giaoHang = false,
  });

  final String trangThai;
  final DateTime? duKien;
  final bool giaoHang;

  @override
  Widget build(BuildContext context) {
    if (!['CHO_XAC_NHAN', 'DA_XAC_NHAN', 'DANG_IN'].contains(trangThai)) {
      return const SizedBox.shrink();
    }
    final coGio = trangThai == 'DANG_IN' && duKien != null;
    return Container(
      margin: const EdgeInsets.only(top: 12),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.nen,
        border: Border.all(color: AppColors.vien),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            coGio
                ? 'Dự kiến in xong: ${DateFormat('HH:mm:ss dd/MM/yyyy').format(duKien!.toLocal())}'
                : trangThai == 'DANG_IN'
                ? 'Máy đang xử lý; chờ cập nhật giờ in xong.'
                : 'Giờ in xong được cập nhật khi máy bắt đầu xử lý đơn.',
            style: AppTheme.font(size: 12, weight: FontWeight.w600),
          ),
          if (coGio) ...[
            const SizedBox(height: 5),
            Text(
              'Đã gồm 15 giây dự trù. Đây là thời gian ước tính.',
              style: AppTheme.font(size: 11, color: AppColors.chuPhu),
            ),
          ],
          if (giaoHang) ...[
            const SizedBox(height: 5),
            Text(
              'Chưa bao gồm thời gian chuẩn bị giao và shipper di chuyển.',
              style: AppTheme.font(size: 11, color: AppColors.chuPhu),
            ),
          ],
        ],
      ),
    );
  }
}
