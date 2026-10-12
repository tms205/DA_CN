using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyPrinterSimulator;

public sealed class CauHinhSimulator
{
    public string? ThuMucTrangThai { get; set; }
    public string ApiBaseUrl { get; set; } = "http://localhost:5268/";
    public int SoGiayKiemTra { get; set; } = 2;
    public List<CauHinhTocDoMayIn> MayIn { get; set; } = new();
}

public sealed class TienTrinhInDaLuu
{
    public string MaLenh { get; set; } = "";
    public LenhInAoDto? Lenh { get; set; }
    public bool DaBatDau { get; set; }
    public int MaDonHang { get; set; }
    public DateTime DuKienXong { get; set; }
}
