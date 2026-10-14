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
}
