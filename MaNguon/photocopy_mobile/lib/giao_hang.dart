// Điểm giao khách đã xác nhận. Chi tiết căn hộ/ghi chú không dùng để tìm tuyến đường.
class DiaChiNhan {
  const DiaChiNhan({
    this.diaChi = '',
    this.viDo,
    this.kinhDo,
    this.chiTiet = '',
    this.ghiChu = '',
    this.maViTriGiao,
  });
  final String diaChi;
  final double? viDo;
  final double? kinhDo;
  final String chiTiet;
  final String ghiChu;
  final String? maViTriGiao;
  bool get daChon =>
      viDo != null &&
      kinhDo != null &&
      diaChi.trim().isNotEmpty &&
      (maViTriGiao?.isNotEmpty ?? false);
  Map<String, dynamic> get duLieu => {
    'diaChiGiao': diaChi.trim(),
    'viDoGiao': viDo,
    'kinhDoGiao': kinhDo,
    'diaChiChiTiet': chiTiet.trim(),
    'ghiChuGiao': ghiChu.trim(),
    'maViTriGiao': maViTriGiao,
  };
  factory DiaChiNhan.fromJson(Map<String, dynamic> json) => DiaChiNhan(
    diaChi: json['diaChi'] ?? '',
    viDo: (json['viDo'] as num).toDouble(),
    kinhDo: (json['kinhDo'] as num).toDouble(),
    maViTriGiao: json['maViTriGiao'],
  );
}

class DonGiao {
  DonGiao.fromJson(Map<String, dynamic> json)
    : maDonHang = json['maDonHang'],
      maHienThi = json['maDonHangHienThi'] ?? '',
      maGiaoHang = json['maGiaoHang'],
      diaChi = json['diaChiGiao'] ?? '',
      diaChiChiNhanh = json['diaChiChiNhanh'] ?? '',
      tenKhach = json['tenKhachHang'] ?? '',
      sdt = json['sdtNhan'] ?? '',
      trangThai = json['trangThaiDonHang'] ?? '',
      trangThaiGiao = json['trangThaiGiao'],
      lyDo = json['lyDo'],
      soLan = json['soLanGiao'] ?? 0,
      lanGiao = json['lanGiao'],
      tongTien = (json['tongTien'] as num? ?? 0).toDouble(),
      phiShip = (json['phiShip'] as num? ?? 0).toDouble();
  final int maDonHang;
  final String maHienThi;
  final int? maGiaoHang;
  final String diaChi;
  final String diaChiChiNhanh;
  final String tenKhach;
  final String sdt;
  final String trangThai;
  final String? trangThaiGiao;
  final String? lyDo;
  final int soLan;
  final int? lanGiao;
  final double tongTien;
  final double phiShip;
}
