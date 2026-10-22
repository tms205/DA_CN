using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyPrinterSimulator;

public sealed class CauHinhSimulator
{
    public string? ThuMucTrangThai { get; set; }
    public string ApiBaseUrl { get; set; } = "http://localhost:5268/";
    public int SoGiayKiemTra { get; set; } = 2;
    public string SumatraPdfPath { get; set; } = "";
    public bool TuPhatHienWindows { get; set; }
    public List<CauHinhTocDoMayIn> MayIn { get; set; } = new();

    public void KiemTraDanhSachMay()
    {
        if (MayIn.Count == 0 || MayIn.Any(x => x.MaMayIn <= 0 || x.CheDo is not ("AO" or "THAT")))
            throw new InvalidOperationException("Cấu hình mã máy hoặc chế độ AO/THAT không hợp lệ.");
        if (MayIn.GroupBy(x => x.MaMayIn).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Không cấu hình trùng mã máy in trong connector.");
        if (MayIn.Where(x => x.CheDo == "THAT").GroupBy(x => x.TenHangDoiWindows.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Mỗi máy logic phải gắn một queue Windows riêng; không gắn trùng queue.");
    }
}

public sealed class TienTrinhInDaLuu
{
    public string MaLenh { get; set; } = "";
    public LenhInAoDto? Lenh { get; set; }
    public bool DaBatDau { get; set; }
    public int MaDonHang { get; set; }
    public DateTime DuKienXong { get; set; }
    public double TongSoGiay { get; set; }
    public double SoGiayDaIn { get; set; }
    public string CheDo { get; set; } = "AO";
    public string TenHangDoi { get; set; } = "";
    public List<string> TepDaGui { get; set; } = new();
    public bool ChoXacNhan { get; set; }
    public bool DaGuiDuFile { get; set; }
    public string? CanhBao { get; set; }

    public void TangTienDoAo(double soGiay, bool biChan)
    {
        if (!biChan) SoGiayDaIn = Math.Clamp(SoGiayDaIn + Math.Max(0, soGiay), 0, TongSoGiay);
    }
}
