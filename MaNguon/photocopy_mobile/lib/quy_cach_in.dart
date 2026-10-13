import 'models.dart';

// Giống QuyTacIn và sp_TinhTien trên API: giới hạn từng bản, không nhân số bản.
int soToMoiBan(int soTrang, String matIn) =>
    matIn == '2_MAT' ? soTrang ~/ 2 + soTrang % 2 : soTrang;

bool laKimGoc(DichVu dv) => dv.tenDichVu.trim().toLowerCase() == 'bấm kim góc';
bool _laLoXo(DichVu dv) => dv.tenDichVu.trim().toLowerCase() == 'đóng lò xo';
bool _laBiaKieng(DichVu dv) => dv.tenDichVu.trim().toLowerCase() == 'đóng bìa kiếng';

bool giaCongLoaiTru(DichVu a, DichVu b) {
  if ((_laBiaKieng(a) && _laLoXo(b)) || (_laLoXo(a) && _laBiaKieng(b))) {
    return false;
  }
  if (a.nhomGiaCong != null &&
      a.nhomGiaCong!.trim().isNotEmpty &&
      a.nhomGiaCong!.toLowerCase() == b.nhomGiaCong?.toLowerCase()) {
    return true;
  }
  return (_laLoXo(a) && laKimGoc(b)) || (laKimGoc(a) && _laLoXo(b));
}
