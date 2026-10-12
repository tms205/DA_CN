import 'dart:convert';

// Cùng quy tắc với PhotocopyShared; API vẫn kiểm tra lại, không tin dữ liệu từ app.
bool emailGmailHopLe(String email) =>
    email.length <= 100 &&
    RegExp(
      r'^[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)*(?:\+[A-Za-z0-9._-]+)?@gmail\.com$',
      caseSensitive: false,
    ).hasMatch(email);

bool emailHopLe(String email) =>
    email.length <= 100 &&
    RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(email);

bool sdtHopLe(String sdt) => RegExp(r'^0[0-9]{9}$').hasMatch(sdt);

bool matKhauMoiHopLe(String mk) =>
    mk.length >= 8 &&
    utf8.encode(mk).length <= 72 &&
    RegExp(r'[A-Z]').hasMatch(mk) &&
    RegExp(r'[0-9]').hasMatch(mk) &&
    !mk.codeUnits.any((c) => c < 32 || (c >= 127 && c <= 159));

const loiSdt = 'Số điện thoại phải gồm 10 chữ số, bắt đầu bằng 0.';
const loiMatKhau =
    'Mật khẩu cần ít nhất 8 ký tự, có chữ hoa và số, tối đa 72 byte UTF-8 và không chứa ký tự điều khiển.';

String? _chuoi(dynamic giaTri, String nhan, int toiDa, {bool batBuoc = false}) {
  if (giaTri == null || giaTri == '') {
    return batBuoc ? 'Vui lòng nhập $nhan.' : null;
  }
  if (giaTri is! String) return '$nhan không hợp lệ.';
  if (batBuoc && giaTri.trim().isEmpty) return 'Vui lòng nhập $nhan.';
  if (giaTri.length > toiDa) return '$nhan tối đa $toiDa ký tự.';
  return null;
}

bool _maHopLe(dynamic ma) => ma is int && ma > 0 && ma <= 2147483647;
bool _dsMaHopLe(dynamic ds, {bool batBuoc = false}) =>
    ds is List &&
    (!batBuoc || ds.isNotEmpty) &&
    ds.every(_maHopLe) &&
    ds.toSet().length == ds.length;

String? kiemTraDuLieu(String duongDan, Map<String, dynamic> duLieu) {
  final path = Uri.parse(duongDan).path;
  final dangKy = path == '/api/auth/register/otp';
  final xacNhanDangKy = path == '/api/auth/register';
  final hoSo = path == '/api/auth/me';
  final don = path == '/api/don-hang';
  final baoGia = path == '/api/bao-gia';

  if (dangKy || hoSo) {
    final loi = _chuoi(duLieu['hoTen'], 'họ tên', 100, batBuoc: true);
    if (loi != null) return loi;
  }
  if (dangKy ||
      xacNhanDangKy ||
      path == '/api/auth/forgot-password' ||
      path == '/api/auth/reset-password' ||
      path == '/api/auth/forgot-password/verify-otp') {
    final email = duLieu['email'];
    if (email is! String ||
        !(dangKy || xacNhanDangKy
            ? emailGmailHopLe(email)
            : emailHopLe(email))) {
      return dangKy || xacNhanDangKy
          ? 'Email nhập mới phải là địa chỉ @gmail.com hợp lệ.'
          : 'Email không hợp lệ.';
    }
  }
  if (path == '/api/auth/login' || path == '/api/auth/admin/login') {
    final tk = duLieu['taiKhoan'];
    if (tk is! String || !(emailHopLe(tk) || sdtHopLe(tk))) {
      return 'Vui lòng nhập email hoặc số điện thoại hợp lệ.';
    }
    final loi = _chuoi(duLieu['matKhau'], 'mật khẩu', 1024, batBuoc: true);
    if (loi != null) return loi;
  }
  if (dangKy || hoSo) {
    final sdt = duLieu['soDienThoai'];
    if ((dangKy || (sdt != null && sdt != '')) &&
        (sdt is! String || !sdtHopLe(sdt))) {
      return loiSdt;
    }
  }
  if (hoSo) {
    final loi = _chuoi(duLieu['diaChiMacDinh'], 'địa chỉ', 255);
    if (loi != null) return loi;
  }
  if (dangKy ||
      path == '/api/auth/reset-password' ||
      path == '/api/auth/me/password') {
    final mk = duLieu[dangKy ? 'matKhau' : 'matKhauMoi'];
    if (mk is! String || !matKhauMoiHopLe(mk)) return loiMatKhau;
    if (path == '/api/auth/me/password') {
      final loi = _chuoi(
        duLieu['matKhauCu'],
        'mật khẩu hiện tại',
        1024,
        batBuoc: true,
      );
      if (loi != null) return loi;
    }
  }
  if (xacNhanDangKy ||
      path == '/api/auth/reset-password' ||
      path == '/api/auth/forgot-password/verify-otp') {
    final otp = duLieu['maOtp'];
    if (otp is! String || !RegExp(r'^[0-9]{6}$').hasMatch(otp)) {
      return 'Mã OTP gồm 6 chữ số.';
    }
  }
  if (path == '/api/don-hang/chi-nhanh-phu-hop' &&
      !_dsMaHopLe(duLieu['dsMaDichVu'], batBuoc: true)) {
    return 'Vui lòng chọn dịch vụ hợp lệ, không trùng nhau.';
  }

  if (don || baoGia) {
    if (!_maHopLe(duLieu['maChiNhanh'])) return 'Vui lòng chọn chi nhánh.';
    final hinhThuc = duLieu.containsKey('hinhThucNhan')
        ? duLieu['hinhThucNhan']
        : 'TAI_QUAY';
    if (hinhThuc != 'TAI_QUAY' && hinhThuc != 'GIAO_HANG') {
      return 'Hình thức nhận không hợp lệ.';
    }
    final loiDiaChi = _chuoi(
      duLieu['diaChiGiao'],
      'địa chỉ giao hàng',
      255,
      batBuoc: hinhThuc == 'GIAO_HANG',
    );
    if (loiDiaChi != null) return loiDiaChi;
    final sdt = duLieu['sdtNhan'];
    if (don &&
        ((hinhThuc == 'GIAO_HANG' || (sdt != null && sdt != '')) &&
            (sdt is! String || !sdtHopLe(sdt)))) {
      return loiSdt;
    }
    final viDo = duLieu['viDoGiao'];
    final kinhDo = duLieu['kinhDoGiao'];
    if ((viDo == null) != (kinhDo == null)) {
      return 'Tọa độ giao hàng phải có đủ vĩ độ và kinh độ.';
    }
    if (viDo != null &&
        (viDo is! num ||
            !viDo.isFinite ||
            viDo < -90 ||
            viDo > 90 ||
            kinhDo is! num ||
            !kinhDo.isFinite ||
            kinhDo < -180 ||
            kinhDo > 180)) {
      return 'Tọa độ giao hàng không hợp lệ.';
    }
    final loiGhiChu = _chuoi(duLieu['ghiChu'], 'ghi chú', 255);
    if (loiGhiChu != null) return loiGhiChu;
    final code = duLieu['maCodeKhuyenMai'];
    if (code != null &&
        code != '' &&
        (code is! String || !RegExp(r'^[A-Za-z0-9_-]{1,30}$').hasMatch(code))) {
      return 'Mã khuyến mãi tối đa 30 ký tự, chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.';
    }
    final files = duLieu['fileIn'];
    if (files is! List || files.isEmpty) return 'Vui lòng chọn file in.';
    final maDaChon = <int>{};
    for (final file in files) {
      if (file is! Map ||
          !_maHopLe(file['maFileIn']) ||
          !_maHopLe(file['maDichVu']) ||
          !_maHopLe(file.containsKey('soBanIn') ? file['soBanIn'] : 1)) {
        return 'File, dịch vụ và số bản in phải hợp lệ.';
      }
      if (!maDaChon.add(file['maFileIn'] as int)) {
        return 'Không chọn trùng file in.';
      }
      final huong = file.containsKey('huongGiay') ? file['huongGiay'] : 'DUNG';
      if (huong != 'DUNG' && huong != 'NGANG') {
        return 'Hướng giấy không hợp lệ.';
      }
      if (file.containsKey('dsGiaCong') && !_dsMaHopLe(file['dsGiaCong'])) {
        return 'Gia công phải gồm các mã hợp lệ, không trùng nhau.';
      }
    }
  }
  if (RegExp(r'^/api/don-hang/[0-9]+/huy$').hasMatch(path)) {
    return _chuoi(duLieu['lyDo'], 'lý do hủy', 255, batBuoc: true);
  }
  if (RegExp(r'^/api/don-hang/[0-9]+/danh-gia$').hasMatch(path)) {
    final sao = duLieu['soSao'];
    if (sao is! int || sao < 1 || sao > 5) {
      return 'Đánh giá phải từ 1 đến 5 sao.';
    }
    return _chuoi(duLieu['nhanXet'], 'nhận xét', 500);
  }
  return null;
}
