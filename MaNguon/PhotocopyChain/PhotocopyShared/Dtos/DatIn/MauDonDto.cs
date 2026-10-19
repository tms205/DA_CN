namespace PhotocopyShared.Dtos.DatIn;

public sealed record MauDonDto(string MaMau, string TenMau, string MoTa)
{
    public string Nhom { get; init; } = "";
    public string Nguon { get; init; } = "";
    public string UrlNguon { get; init; } = "";
    public string UrlTaiGoc { get; init; } = "";
    public string PhamVi { get; init; } = "";
    public int SoTrang { get; init; }
    public string TenFile { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string TrangThai { get; init; } = "HIEN";
    public long PhienBan { get; init; } = 1;
    public DateTimeOffset? CapNhatLuc { get; init; }
}

public sealed class LuuMauDonRequest
{
    public string TenMau { get; set; } = "";
    public string Nhom { get; set; } = "";
    public string MoTa { get; set; } = "";
    public long PhienBan { get; set; }
}

public sealed class HienMauDonRequest
{
    public bool Hien { get; set; }
    public long PhienBan { get; set; }
}
