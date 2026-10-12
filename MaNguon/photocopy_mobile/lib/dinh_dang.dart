import 'package:intl/intl.dart';

// 57600 → "57.600₫"
String tien(num soTien) {
  return '${NumberFormat('#,##0', 'vi_VN').format(soTien)}₫';
}

// 2516582 byte → "2,4 MB"
String dungLuong(int? soByte) {
  if (soByte == null) return '';
  NumberFormat dinhDang = NumberFormat('#,##0.#', 'vi_VN');
  if (soByte < 1024 * 1024) return '${dinhDang.format(soByte / 1024)} KB';
  return '${dinhDang.format(soByte / (1024 * 1024))} MB';
}

// "14:05"
String gioPhut(DateTime t) {
  return DateFormat('HH:mm').format(t);
}

// "14:05 15/09/2026"
String ngayGio(DateTime t) {
  return DateFormat('HH:mm dd/MM/yyyy').format(t);
}

// Thời gian hiển thị trong danh sách thông báo / đơn hàng: "Vừa xong", "5 phút trước", "Hôm qua 14:05"...
// bayGio chỉ truyền khi viết test, bình thường để trống = giờ hiện tại
String thoiGianTuongDoi(DateTime t, [DateTime? bayGio]) {
  DateTime hienTai = bayGio ?? DateTime.now();
  Duration khoang = hienTai.difference(t);
  DateTime dauHomNay = DateTime(hienTai.year, hienTai.month, hienTai.day);
  DateTime dauHomQua = dauHomNay.subtract(const Duration(days: 1));

  if (khoang.inMinutes < 1) return 'Vừa xong';
  if (khoang.inMinutes < 60) return '${khoang.inMinutes} phút trước';
  if (!t.isBefore(dauHomNay)) return '${khoang.inHours} giờ trước';
  if (!t.isBefore(dauHomQua)) return 'Hôm qua ${gioPhut(t)}';
  if (khoang.inDays < 7) return '${khoang.inDays} ngày trước';
  return DateFormat('dd/MM/yyyy').format(t);
}

// 252 giây → "04:12" (đồng hồ đếm ngược màn OTP)
String phutGiay(Duration d) {
  int giay = d.isNegative ? 0 : d.inSeconds;
  String phut = (giay ~/ 60).toString().padLeft(2, '0');
  String con = (giay % 60).toString().padLeft(2, '0');
  return '$phut:$con';
}
