using System.ComponentModel.DataAnnotations;

namespace PhotocopyShared.Dtos.MayInAo;

public sealed class HangDoiWindowsDto
{
    [Required, StringLength(256)] public string Ten { get; set; } = "";
    [StringLength(256)] public string Driver { get; set; } = "";
    [StringLength(512)] public string Cong { get; set; } = "";
    public uint TrangThaiDriver { get; set; }
    public int SoJob { get; set; }
    public bool LaMayAo { get; set; }
    // null = driver không cung cấp; không được hiểu là không hỗ trợ.
    public bool? InMau { get; set; }
    public bool? InHaiMat { get; set; }
}

public sealed class BaoCaoPhatHienRequest
{
    [Required, StringLength(100)] public string TenMayTinh { get; set; } = "";
    [StringLength(500)] public string? Loi { get; set; }
    public bool BoInPdfSanSang { get; set; }
    [MaxLength(100)] public List<int> MaMayCauHinh { get; set; } = new();
    [MaxLength(100)] public List<LienKetMayInDto> KetNoiCauHinh { get; set; } = new();
    [MaxLength(100)] public List<HangDoiWindowsDto> HangDoi { get; set; } = new();
}

public sealed class ConnectorMayInDto
{
    public string ConnectorId { get; set; } = "";
    public string TenMayTinh { get; set; } = "";
    public DateTimeOffset LanCuoiPhatHien { get; set; }
    public bool TrucTuyen { get; set; }
    public int? MaChiNhanh { get; set; }
    public string? Loi { get; set; }
    public bool BoInPdfSanSang { get; set; }
    public List<int> MaMayCauHinh { get; set; } = new();
    public List<LienKetMayInDto> KetNoiCauHinh { get; set; } = new();
    public List<HangDoiWindowsDto> HangDoi { get; set; } = new();
    public List<LienKetMayInDto> LienKet { get; set; } = new();
}

public sealed class LienKetMayInDto
{
    public int MaMayIn { get; set; }
    public int MaChiNhanh { get; set; }
    public string ConnectorId { get; set; } = "";
    public string TenHangDoi { get; set; } = "";
    public string CheDo { get; set; } = "AO";
    public string PhienBan { get; set; } = "";
}

public sealed class LienKetMayInRequest
{
    [Required, RegularExpression("^[a-fA-F0-9]{32}$")] public string ConnectorId { get; set; } = "";
    [Required, RegularExpression("^(AO|THAT)$")] public string CheDo { get; set; } = "AO";
    [StringLength(256)] public string TenHangDoi { get; set; } = "";
    public bool DaKiemTraThietBi { get; set; }
}
