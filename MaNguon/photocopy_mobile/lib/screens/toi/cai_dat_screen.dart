import 'package:flutter/material.dart';

import '../../api.dart';
import '../../dinh_dang.dart';
import '../../phien.dart';
import '../../realtime.dart';
import '../../theme.dart';
import '../../widgets/icons.dart';
import '../../widgets/widgets.dart';
import 'doi_mat_khau_screen.dart';

// Cài đặt — chỉ đặt những mục thật sự có tác dụng; mục chưa làm ghi rõ tuần dự kiến.
class CaiDatScreen extends StatefulWidget {
  const CaiDatScreen({super.key});

  @override
  State<CaiDatScreen> createState() => _CaiDatScreenState();
}

class _CaiDatScreenState extends State<CaiDatScreen> {
  // Bật / tắt "Ghi nhớ đăng nhập" mà không phải đăng nhập lại
  Future<void> _doiGhiNho(bool batLen) async {
    setState(() => Phien.ghiNho = batLen);
    await Phien.luuVaoMay();
    if (!mounted) return;
    hienThongBaoNhanh(
      context,
      batLen
          ? 'Đã ghi nhớ đăng nhập trên máy này.'
          : 'Tắt ứng dụng hẳn thì sẽ phải đăng nhập lại.',
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.nen,
      appBar: const ThanhTieuDe(tieuDe: 'Cài đặt'),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
        children: [
          NhomDong(
            tieuDe: 'Bảo mật',
            children: [
              DongMenu(
                icon: Ico.khoa,
                nhan: 'Đổi mật khẩu',
                onTap: () {
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (context) => const DoiMatKhauScreen(),
                    ),
                  );
                },
              ),
              DongMenu(
                icon: Ico.khien,
                nhan: 'Ghi nhớ đăng nhập trên máy này',
                phu: Phien.ghiNho
                    ? 'Mở lại ứng dụng không cần đăng nhập'
                    : 'Tắt hẳn ứng dụng là phải đăng nhập lại',
                cuoi: Switch(value: Phien.ghiNho, onChanged: _doiGhiNho),
              ),
            ],
          ),
          const SizedBox(height: 18),
          NhomDong(
            tieuDe: 'Thông báo',
            children: [
              // Đang mở app: đơn đổi trạng thái là màn hình tự cập nhật ngay (SignalR, làm ở Tuần 2)
              DongMenu(
                icon: Ico.taiLai,
                nhan: 'Cập nhật đơn theo thời gian thực',
                phu: 'SignalR — đang mở ứng dụng',
                cuoi: NhanNho(
                  Realtime.dangNoi ? 'Đang bật' : 'Mất kết nối',
                  mau: Realtime.dangNoi
                      ? AppColors.thanhCong
                      : AppColors.loiChu,
                  nen: Realtime.dangNoi ? AppColors.xanhNhat : AppColors.loiNen,
                ),
              ),
              // Tắt app thì phải có Firebase mới đẩy được thông báo — chờ tạo project Firebase
              const DongMenu(
                icon: Ico.tiaSet,
                nhan: 'Thông báo đẩy khi tắt ứng dụng',
                phu: 'Firebase Cloud Messaging — chờ tạo project Firebase',
                cuoi: NhanNho(
                  'Chờ Firebase',
                  mau: AppColors.cbChu,
                  nen: AppColors.cbNen,
                ),
              ),
            ],
          ),
          const SizedBox(height: 18),
          NhomDong(
            tieuDe: 'Thông tin ứng dụng',
            children: [
              DongMenu(
                icon: Ico.mayIn,
                nhan: 'Phiên bản',
                cuoi: _giaTri('1.0.0 (1)', false),
              ),
              DongMenu(
                icon: Ico.vanBan,
                nhan: 'Máy chủ API',
                cuoi: _giaTri(diaChiApi, true),
              ),
              if (Phien.hetHanLuc != null)
                DongMenu(
                  icon: Ico.dongHo,
                  nhan: 'Phiên đăng nhập hết hạn',
                  cuoi: _giaTri(ngayGio(Phien.hetHanLuc!.toLocal()), false),
                ),
            ],
          ),
        ],
      ),
    );
  }

  // Chữ xám bên phải dòng menu
  Widget _giaTri(String chu, bool laMono) {
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 170),
      child: Text(
        chu,
        textAlign: TextAlign.right,
        overflow: TextOverflow.ellipsis,
        style: laMono
            ? AppTheme.mono(
                size: 11.5,
                weight: FontWeight.w500,
                color: AppColors.chuPhu,
              )
            : AppTheme.font(size: 12, color: AppColors.chuPhu),
      ),
    );
  }
}
