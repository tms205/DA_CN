namespace PhotocopyShared.Dtos.DanhMuc;

// Dữ liệu công khai không bao gồm mã đơn, tài khoản, email hoặc số điện thoại.
public sealed class DanhGiaCongKhaiDto
{
    public string TenKhachHang { get; set; } = "";
    public int SoSao { get; set; }
    public string? NhanXet { get; set; }
    public DateTime NgayDanhGia { get; set; }
}
public sealed class DanhGiaChiNhanhDto
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public double DiemTrungBinh { get; set; }
    public int SoLuotDanhGia { get; set; }
    public int Trang { get; set; }
    public bool CoTrangSau { get; set; }
    public List<DanhGiaCongKhaiDto> DanhGia { get; set; } = new();
}
