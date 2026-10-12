import 'package:flutter/material.dart';

import '../../phien.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';

// Tab "Đơn giao" của Shipper — màn đầu tiên khi tài khoản có vai trò SHIPPER (F-02).
// Danh sách đơn đi giao (F-17B, F-18B) để sau khi nhóm chốt nghiệp vụ giao hàng;
// tab Thông báo và Tôi đã dùng được.
class ShipperScreen extends StatelessWidget {
  const ShipperScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: ThanhTieuDe(
        tieuDe: 'Đơn đi giao',
        phuDe: Phien.nguoiDung?.tenChiNhanh ?? '',
        coNutQuayLai: false,
      ),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(
            color: AppColors.trang,
            border: Border.all(color: AppColors.vach),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const AppIcon(Ico.xeGiao, size: 26, color: AppColors.accent),
              const SizedBox(height: 10),
              Text(
                'Xin chào, ${Phien.nguoiDung?.hoTen ?? ''}',
                style: AppTheme.font(size: 17, weight: FontWeight.w800),
              ),
              const SizedBox(height: 6),
              Text(
                'Đăng nhập Shipper đã hoạt động. Danh sách đơn chờ giao, nhận đơn và xác nhận giao sẽ làm sau khi nhóm chốt nghiệp vụ giao hàng. Đơn giao đã thanh toán trước nên shipper không thu COD. '
                'Thông tin cá nhân, đổi mật khẩu và đăng xuất nằm ở tab Tôi.',
                style: AppTheme.font(
                  size: 13,
                  color: AppColors.chuNhan,
                  height: 1.55,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
