// Các class dữ liệu — tên thuộc tính trùng JSON mà API trả về (DTO trong PhotocopyShared).
// Mỗi class có hàm fromJson: đổi Map (JSON đã giải mã) thành đối tượng.

// JSON có thể trả số nguyên (15000) hoặc số thực (15000.5) → đổi hết về double
double soThuc(dynamic giaTri) {
  if (giaTri == null) return 0;
  return (giaTri as num).toDouble();
}

// ============================================================ Người dùng (NguoiDungDto)
class NguoiDung {
  int maNguoiDung = 0;
  String hoTen = '';
  String email = '';
  String? soDienThoai;
  String tenVaiTro =
      ''; // CUSTOMER hoặc SHIPPER (App chỉ cho 2 vai trò này đăng nhập)
  int? maChiNhanh;
  String? tenChiNhanh;
  String? diaChiMacDinh; // địa chỉ nhận hàng mặc định của khách

  NguoiDung.fromJson(Map<String, dynamic> json) {
    maNguoiDung = json['maNguoiDung'];
    hoTen = json['hoTen'];
    email = json['email'];
    soDienThoai = json['soDienThoai'];
    tenVaiTro = json['tenVaiTro'];
    maChiNhanh = json['maChiNhanh'];
    tenChiNhanh = json['tenChiNhanh'];
    diaChiMacDinh = json['diaChiMacDinh'];
  }

  // Dùng khi lưu phiên đăng nhập vào máy
  Map<String, dynamic> toJson() {
    return {
      'maNguoiDung': maNguoiDung,
      'hoTen': hoTen,
      'email': email,
      'soDienThoai': soDienThoai,
      'tenVaiTro': tenVaiTro,
      'maChiNhanh': maChiNhanh,
      'tenChiNhanh': tenChiNhanh,
      'diaChiMacDinh': diaChiMacDinh,
    };
  }

  bool get laShipper => tenVaiTro == 'SHIPPER';

  String get tenVaiTroHienThi =>
      laShipper ? 'Nhân viên giao hàng' : 'Khách hàng';

  // "Nguyễn Thị Sinh Viên" → "SV" (chữ trong ô ảnh đại diện)
  String get vietTat {
    List<String> tu = hoTen
        .trim()
        .split(' ')
        .where((t) => t.isNotEmpty)
        .toList();
    if (tu.isEmpty) return '?';
    if (tu.length == 1) return tu[0].substring(0, 1).toUpperCase();
    return (tu[tu.length - 2].substring(0, 1) +
            tu[tu.length - 1].substring(0, 1))
        .toUpperCase();
  }
}

// ============================================================ File in (FileInDto)
class FileIn {
  int maFileIn = 0;
  String tenFileGoc = '';
  String? dinhDangGoc; // pdf, docx, png...
  int? dungLuong; // byte
  int? soTrang; // máy chủ đếm sau khi đổi sang PDF
  String trangThaiFile = ''; // SAN_SANG / DA_XOA

  FileIn.fromJson(Map<String, dynamic> json) {
    maFileIn = json['maFileIn'];
    tenFileGoc = json['tenFileGoc'];
    dinhDangGoc = json['dinhDangGoc'];
    dungLuong = json['dungLuong'];
    soTrang = json['soTrang'];
    trangThaiFile = json['trangThaiFile'];
  }

  // File Word / Excel / ảnh... đã được máy chủ đổi sang PDF
  bool get daChuyenPdf => dinhDangGoc != null && dinhDangGoc != 'pdf';
}

// ============================================================ Một dòng bảng giá (bảng DichVu)
class DichVu {
  int maDichVu = 0;
  String tenDichVu = '';
  String loaiDichVu = ''; // IN_AN (giá / trang) hoặc GIA_CONG (giá / cuốn)
  String? khoGiay; // A4 / A3
  String? loaiGiay; // 70gsm, 80gsm...
  String? matIn; // 1_MAT / 2_MAT
  String? mauSac; // DEN_TRANG / MAU
  String?
  nhomGiaCong; // DONG_GAY: lò xo / bìa kiếng chỉ chọn 1; null = chọn thoải mái
  double donGia = 0;

  DichVu.fromJson(Map<String, dynamic> json) {
    maDichVu = json['maDichVu'];
    tenDichVu = json['tenDichVu'];
    loaiDichVu = json['loaiDichVu'];
    khoGiay = json['khoGiay'];
    loaiGiay = json['loaiGiay'];
    matIn = json['matIn'];
    mauSac = json['mauSac'];
    nhomGiaCong = json['nhomGiaCong'];
    donGia = soThuc(json['donGia']);
  }

  bool get laDongIn => loaiDichVu == 'IN_AN';

  // "A4 70gsm" — dùng làm nhãn chip "Khổ giấy & loại giấy"
  String get tenGiay => '${khoGiay ?? ''} ${loaiGiay ?? ''}'.trim();
}

// ============================================================ Chi nhánh (ChiNhanhDto)
class ChiNhanh {
  int maChiNhanh = 0;
  String tenChiNhanh = '';
  String diaChi = '';
  String? soDienThoai;
  String? gioMoCua; // "09:00" (API trả "09:00:00", chỉ lấy 5 ký tự đầu)
  String? gioDongCua;
  double banKinhGiao = 0; // km
  double diemDanhGiaTB = 0;
  int soLuotDanhGia = 0;

  // Bước 3 (chọn chi nhánh): chi nhánh có máy in được cấu hình khách chọn không.
  // Các màn khác API luôn trả true.
  bool dapUngDuoc = true;
  String? lyDoKhongDapUng;

  ChiNhanh.fromJson(Map<String, dynamic> json) {
    maChiNhanh = json['maChiNhanh'];
    dapUngDuoc = json['dapUngDuoc'] ?? true;
    lyDoKhongDapUng = json['lyDoKhongDapUng'];
    tenChiNhanh = json['tenChiNhanh'];
    diaChi = json['diaChi'];
    soDienThoai = json['soDienThoai'];
    if (json['gioMoCua'] != null) {
      gioMoCua = json['gioMoCua'].toString().substring(0, 5);
    }
    if (json['gioDongCua'] != null) {
      gioDongCua = json['gioDongCua'].toString().substring(0, 5);
    }
    banKinhGiao = soThuc(json['banKinhGiao']);
    diemDanhGiaTB = soThuc(json['diemDanhGiaTB']);
    soLuotDanhGia = json['soLuotDanhGia'] ?? 0;
  }
}

// ============================================================ Kết quả báo giá (sp_BaoGia)
class BaoGia {
  double tienIn = 0;
  double tienGiaCong = 0;
  double phuPhiGap = 0;
  double phiShip = 0;
  double giamGia = 0;
  double tongTien = 0;
  int tongSoTrang = 0;
  bool? khuyenMaiHopLe; // null khi khách không nhập mã
  String? lyDoKhuyenMai;
  DateTime? ngayHenTra; // giờ in xong dự kiến

  BaoGia.fromJson(Map<String, dynamic> json) {
    tienIn = soThuc(json['tienIn']);
    tienGiaCong = soThuc(json['tienGiaCong']);
    phuPhiGap = soThuc(json['phuPhiGap']);
    phiShip = soThuc(json['phiShip']);
    giamGia = soThuc(json['giamGia']);
    tongTien = soThuc(json['tongTien']);
    tongSoTrang = json['tongSoTrang'] ?? 0;
    khuyenMaiHopLe = json['khuyenMaiHopLe'];
    lyDoKhuyenMai = json['lyDoKhuyenMai'];
    if (json['ngayHenTra'] != null) {
      ngayHenTra = DateTime.parse(json['ngayHenTra']);
    }
  }
}

// ============================================================ Đơn hàng (DonHangTomTatDto)
class DonHang {
  int maDonHang = 0;
  String? maDonHangHienThi; // "DH00012"
  String tenChiNhanh = '';
  String hinhThucNhan = ''; // TAI_QUAY / GIAO_HANG
  String? diaChiGiao;
  bool inHoaToc = false;
  String trangThaiDonHang = ''; // CHO_XAC_NHAN, DANG_IN, HOAN_THANH...
  String phuongThucThanhToan = ''; // VIETQR / TIEN_MAT
  String trangThaiThanhToan =
      ''; // CHUA_THANH_TOAN / DA_THANH_TOAN / DA_HOAN_TIEN
  double tongTien = 0;
  DateTime ngayDat = DateTime.now();
  DateTime? ngayHoanTat;
  DateTime? thoiGianDuKienHoanThanh;
  int soFile = 0;
  int tongSoTrang = 0;
  String? tenFileDau;

  DonHang.fromJson(Map<String, dynamic> json) {
    maDonHang = json['maDonHang'];
    maDonHangHienThi = json['maDonHangHienThi'];
    tenChiNhanh = json['tenChiNhanh'];
    hinhThucNhan = json['hinhThucNhan'];
    diaChiGiao = json['diaChiGiao'];
    inHoaToc = json['inHoaToc'] ?? false;
    trangThaiDonHang = json['trangThaiDonHang'];
    phuongThucThanhToan = json['phuongThucThanhToan'] ?? '';
    trangThaiThanhToan = json['trangThaiThanhToan'] ?? '';
    tongTien = soThuc(json['tongTien']);
    ngayDat = DateTime.parse(json['ngayDat']);
    if (json['ngayHoanTat'] != null) {
      ngayHoanTat = DateTime.parse(json['ngayHoanTat']);
    }
    if (json['thoiGianDuKienHoanThanh'] != null) {
      thoiGianDuKienHoanThanh = DateTime.parse(json['thoiGianDuKienHoanThanh']);
    }
    soFile = json['soFile'] ?? 0;
    tongSoTrang = json['tongSoTrang'] ?? 0;
    tenFileDau = json['tenFileDau'];
  }

  String get maHienThi => maDonHangHienThi ?? '#$maDonHang';

  bool get laGiaoTanNoi => hinhThucNhan == 'GIAO_HANG';

  // Chỉ VietQR chưa được ghi nhận tiền và đơn chưa bị hủy mới được quay lại màn QR.
  bool get dangChoChuyenKhoan =>
      phuongThucThanhToan == 'VIETQR' &&
      trangThaiThanhToan == 'CHUA_THANH_TOAN' &&
      trangThaiDonHang != 'DA_HUY';

  // Nhóm lọc trên tab Đơn hàng: DANG_XU_LY / DANG_GIAO / HOAN_TAT / DA_HUY
  String get nhom {
    if (trangThaiDonHang == 'DANG_GIAO' ||
        trangThaiDonHang == 'GIAO_THAT_BAI') {
      return 'DANG_GIAO';
    }
    if (trangThaiDonHang == 'DA_GIAO' || trangThaiDonHang == 'HOAN_THANH') {
      return 'HOAN_TAT';
    }
    if (trangThaiDonHang == 'DA_HUY') return 'DA_HUY';
    return 'DANG_XU_LY'; // CHO_XAC_NHAN, DA_XAC_NHAN, DANG_IN, DA_IN
  }
}

// Tên hiển thị của nhóm lọc đơn hàng
String tenNhomDon(String nhom) {
  if (nhom == 'DANG_XU_LY') return 'Đang xử lý';
  if (nhom == 'DANG_GIAO') return 'Đang giao';
  if (nhom == 'HOAN_TAT') return 'Hoàn tất';
  if (nhom == 'DA_HUY') return 'Đã hủy';
  return 'Tất cả';
}

// ============================================================ Thông báo (ThongBaoDto)
class ThongBao {
  int maThongBao = 0;
  int? maDonHang;
  String? maDonHangHienThi;
  String tieuDe = '';
  String noiDung = '';
  String loaiThongBao = ''; // DON_HANG / HE_THONG
  bool daDoc = false;
  DateTime ngayTao = DateTime.now();

  ThongBao.fromJson(Map<String, dynamic> json) {
    maThongBao = json['maThongBao'];
    maDonHang = json['maDonHang'];
    maDonHangHienThi = json['maDonHangHienThi'];
    tieuDe = json['tieuDe'];
    noiDung = json['noiDung'];
    loaiThongBao = json['loaiThongBao'];
    daDoc = json['daDoc'] ?? false;
    ngayTao = DateTime.parse(json['ngayTao']);
  }
}

// ============================================================ Một dòng in trong đơn (DongInChiTietDto)
class DongInChiTiet {
  int maChiTiet = 0;
  int maFile = 0;
  String tenFile = '';
  int? dungLuong;
  String trangThaiFile = ''; // SAN_SANG / DA_XOA (in xong là file gốc bị xóa)
  String tenDichVu = '';
  String? khoGiay;
  String? loaiGiay;
  String? matIn;
  String huongGiay = 'DUNG';
  String? mauSac;
  int soTrang = 0;
  int soBan = 1;
  double donGia = 0;
  double thanhTien = 0;
  String? tenGiaCong; // "Đóng lò xo, Ép plastic" — null nếu không gia công

  DongInChiTiet.fromJson(Map<String, dynamic> json) {
    maChiTiet = json['maChiTiet'];
    maFile = json['maFile'];
    tenFile = json['tenFile'];
    dungLuong = json['dungLuong'];
    trangThaiFile = json['trangThaiFile'];
    tenDichVu = json['tenDichVu'];
    khoGiay = json['khoGiay'];
    loaiGiay = json['loaiGiay'];
    matIn = json['matIn'];
    huongGiay = json['huongGiay'] ?? 'DUNG';
    mauSac = json['mauSac'];
    soTrang = json['soTrang'] ?? 0;
    soBan = json['soBan'] ?? 1;
    donGia = soThuc(json['donGia']);
    thanhTien = soThuc(json['thanhTien']);
    tenGiaCong = json['tenGiaCong'];
  }

  // "A4 70gsm · 2 mặt · Đen trắng"
  String get quyCach {
    String mat = matIn == '2_MAT' ? '2 mặt' : '1 mặt';
    String mau = mauSac == 'MAU' ? 'In màu' : 'Đen trắng';
    return '${khoGiay ?? ''} ${loaiGiay ?? ''} · $mat · $mau · ${huongGiay == 'NGANG' ? 'Giấy ngang' : 'Giấy đứng'}';
  }
}

// ============================================================ Một mốc trên thanh tiến độ (MocTienDoDto)
class MocTienDo {
  String ma = ''; // CHO_XAC_NHAN, DANG_IN, HOAN_THANH...
  String tieuDe = '';
  String? moTa;
  DateTime? thoiDiem;
  bool daXong = false; // mốc đã đi qua
  bool dangO = false; // mốc đơn đang đứng

  MocTienDo.fromJson(Map<String, dynamic> json) {
    ma = json['ma'];
    tieuDe = json['tieuDe'];
    moTa = json['moTa'];
    if (json['thoiDiem'] != null) thoiDiem = DateTime.parse(json['thoiDiem']);
    daXong = json['daXong'] ?? false;
    dangO = json['dangO'] ?? false;
  }
}

// ============================================================ Chi tiết 1 đơn (DonHangChiTietDto)
class DonHangChiTiet {
  int maDonHang = 0;
  String maDonHangHienThi = '';
  int maChiNhanh = 0;
  String tenChiNhanh = '';
  String diaChiChiNhanh = '';
  String? soDienThoaiChiNhanh;

  String hinhThucNhan = ''; // TAI_QUAY / GIAO_HANG
  String? diaChiGiao;
  String? sdtNhan;
  bool inHoaToc = false;
  String trangThaiDonHang = '';
  String? ghiChu;

  double tienHang = 0;
  double phiShip = 0;
  double giamGia = 0;
  double tongTien = 0;

  String phuongThucThanhToan = ''; // VIETQR / TIEN_MAT
  String trangThaiThanhToan =
      ''; // CHUA_THANH_TOAN / DA_THANH_TOAN / DA_HOAN_TIEN

  String? tenNhanVien;
  String? tenMayIn;
  String? tenShipper;
  String? soDienThoaiShipper;

  DateTime ngayDat = DateTime.now();
  DateTime? ngayHenTra;
  DateTime? ngayHoanThanh;

  int tongSoTrang = 0;
  bool daDanhGia = false;

  List<DongInChiTiet> dongIn = [];
  List<MocTienDo> tienDo = [];

  DonHangChiTiet.fromJson(Map<String, dynamic> json) {
    maDonHang = json['maDonHang'];
    maDonHangHienThi = json['maDonHangHienThi'] ?? '';
    maChiNhanh = json['maChiNhanh'];
    tenChiNhanh = json['tenChiNhanh'];
    diaChiChiNhanh = json['diaChiChiNhanh'] ?? '';
    soDienThoaiChiNhanh = json['soDienThoaiChiNhanh'];

    hinhThucNhan = json['hinhThucNhan'];
    diaChiGiao = json['diaChiGiao'];
    sdtNhan = json['sdtNhan'];
    inHoaToc = json['inHoaToc'] ?? false;
    trangThaiDonHang = json['trangThaiDonHang'];
    ghiChu = json['ghiChu'];

    tienHang = soThuc(json['tienHang']);
    phiShip = soThuc(json['phiShip']);
    giamGia = soThuc(json['giamGia']);
    tongTien = soThuc(json['tongTien']);

    phuongThucThanhToan = json['phuongThucThanhToan'] ?? '';
    trangThaiThanhToan = json['trangThaiThanhToan'] ?? '';

    tenNhanVien = json['tenNhanVien'];
    tenMayIn = json['tenMayIn'];
    tenShipper = json['tenShipper'];
    soDienThoaiShipper = json['soDienThoaiShipper'];

    ngayDat = DateTime.parse(json['ngayDat']);
    if (json['ngayHenTra'] != null) {
      ngayHenTra = DateTime.parse(json['ngayHenTra']);
    }
    if (json['ngayHoanThanh'] != null) {
      ngayHoanThanh = DateTime.parse(json['ngayHoanThanh']);
    }

    tongSoTrang = json['tongSoTrang'] ?? 0;
    daDanhGia = json['daDanhGia'] ?? false;

    for (var dong in (json['dongIn'] ?? [])) {
      dongIn.add(DongInChiTiet.fromJson(dong));
    }
    for (var moc in (json['tienDo'] ?? [])) {
      tienDo.add(MocTienDo.fromJson(moc));
    }
  }

  bool get laGiaoTanNoi => hinhThucNhan == 'GIAO_HANG';

  bool get daTra => trangThaiThanhToan == 'DA_THANH_TOAN';
}

// ============================================================ Thanh toán (ThanhToanDto)
class ThanhToan {
  int maDonHang = 0;
  String maDonHangHienThi = '';
  String phuongThuc = ''; // VIETQR / TIEN_MAT
  String trangThai = ''; // CHUA_THANH_TOAN / DA_THANH_TOAN / DA_HOAN_TIEN
  double soTien = 0;
  String trangThaiDonHang = '';

  String? tenNganHang;
  String? soTaiKhoan;
  String? chuTaiKhoan;
  String? noiDungChuyenKhoan; // "PCCDH00012"
  String? anhQr; // đường dẫn ảnh QR do img.vietqr.io vẽ
  DateTime? hanThanhToan; // quá giờ này chưa chuyển khoản thì đơn tự hủy
  bool choPhepGiaLap =
      false; // máy chủ có bật nút demo "Tôi đã chuyển khoản" không

  ThanhToan.fromJson(Map<String, dynamic> json) {
    maDonHang = json['maDonHang'];
    maDonHangHienThi = json['maDonHangHienThi'] ?? '';
    phuongThuc = json['phuongThuc'] ?? '';
    trangThai = json['trangThai'] ?? '';
    soTien = soThuc(json['soTien']);
    trangThaiDonHang = json['trangThaiDonHang'] ?? '';
    tenNganHang = json['tenNganHang'];
    soTaiKhoan = json['soTaiKhoan'];
    chuTaiKhoan = json['chuTaiKhoan'];
    noiDungChuyenKhoan = json['noiDungChuyenKhoan'];
    anhQr = json['anhQr'];
    if (json['hanThanhToan'] != null) {
      hanThanhToan = DateTime.parse(json['hanThanhToan']);
    }
    choPhepGiaLap = json['choPhepGiaLap'] ?? false;
  }

  bool get daTra => trangThai == 'DA_THANH_TOAN';
}

// QR và link do API lấy từ payOS. App không giữ khóa bí mật hay tự xác nhận tiền.
class LienKetPayOS {
  String checkoutUrl = '';
  DateTime? hanThanhToan;
  int maDonHang = 0;
  double soTien = 0;
  String qrCode = '';
  String maNganHang = '';
  String soTaiKhoan = '';
  String chuTaiKhoan = '';
  String noiDungChuyenKhoan = '';

  LienKetPayOS.fromJson(Map<String, dynamic> json) {
    checkoutUrl = json['checkoutUrl'] ?? '';
    maDonHang = json['maDonHang'] ?? 0;
    soTien = soThuc(json['soTien']);
    qrCode = json['qrCode'] ?? '';
    maNganHang = json['maNganHang'] ?? '';
    soTaiKhoan = json['soTaiKhoan'] ?? '';
    chuTaiKhoan = json['chuTaiKhoan'] ?? '';
    noiDungChuyenKhoan = json['noiDungChuyenKhoan'] ?? '';
    if (json['hanThanhToan'] != null) {
      hanThanhToan = DateTime.parse(json['hanThanhToan']);
    }
  }
}
