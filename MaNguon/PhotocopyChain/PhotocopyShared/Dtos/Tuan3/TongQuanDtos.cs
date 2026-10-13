namespace PhotocopyShared.Dtos.Tuan3;

public sealed class CoCauDoanhThuDto
{
    public string Ten { get; set; } = "";
    public decimal SoTien { get; set; }
    public int SoDon { get; set; }
}

public sealed class GioCaoDiemDto
{
    public int Thu { get; set; } // 0 = thứ Hai, 6 = Chủ nhật.
    public int Gio { get; set; }
    public int SoDon { get; set; }
}

public sealed class HieuQuaDonDto
{
    public int SoDon { get; set; }
    public int SoDonCoMoc { get; set; }
    public decimal? PhutXuLyTrungBinh { get; set; }
    public int SoDonCoHen { get; set; }
    public int SoDonDungHen { get; set; }
}

public sealed class DanhGiaTongQuanDto
{
    public int SoLuot { get; set; }
    public decimal? DiemTrungBinh { get; set; }
}

public sealed class TongQuanPhanTichDto
{
    public decimal TienHang { get; set; }
    public decimal PhiShip { get; set; }
    public decimal GiamGia { get; set; }
    public decimal DoanhThu { get; set; }
    public List<CoCauDoanhThuDto> DichVu { get; set; } = new();
    public List<CoCauDoanhThuDto> ThanhToan { get; set; } = new();
    public List<GioCaoDiemDto> GioCaoDiem { get; set; } = new();
    public HieuQuaDonDto HieuQua { get; set; } = new();
    public DanhGiaTongQuanDto DanhGia { get; set; } = new();
}

public sealed class TongQuanHienTaiDto
{
    public DateTime DocLuc { get; set; }
    public DateTime TuNgayKiemDem { get; set; }
    public int SoCaDangMo { get; set; }
    public int SoCaChoXacNhan { get; set; }
    public int SoCaKiemDem { get; set; }
    public int SoCaLech { get; set; }
    public decimal TienLech { get; set; }
    public int SoPhieuChoDuyet { get; set; }
    public int SoHoanTienChoDuyet { get; set; }
}
