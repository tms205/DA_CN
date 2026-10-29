using System.ComponentModel.DataAnnotations;

namespace PhotocopyShared.Dtos.MayInAo;

public sealed class YeuCauDungChuyenMayRequest
{
    [Range(1, int.MaxValue)] public int MaMayCu { get; set; }
    [Required, StringLength(255, MinimumLength = 3)] public string LyDo { get; set; } = "";
}

public sealed class DongChuyenMayRequest
{
    [Range(1, int.MaxValue)] public int MaChiTiet { get; set; }
    [Range(1, int.MaxValue)] public int TuTrang { get; set; }
    [Range(1, int.MaxValue)] public int DenTrang { get; set; }
    [Range(1, 10000)] public int SoBan { get; set; } = 1;
}

public sealed class HaoHutChuyenMayRequest
{
    [Range(1, int.MaxValue)] public int MaChiTiet { get; set; }
    [Range(0, 1000000)] public int SoToHong { get; set; }
}

public sealed class ApDungChuyenMayRequest
{
    [Required, StringLength(32, MinimumLength = 32)] public string MaYeuCau { get; set; } = "";
    [Range(1, int.MaxValue)] public int MaMayMoi { get; set; }
    public bool DaKiemTraMayCu { get; set; }
    public bool DaDoiChieuBanGiay { get; set; }
    public List<DongChuyenMayRequest> DongIn { get; set; } = new();
    public List<HaoHutChuyenMayRequest> HaoHut { get; set; } = new();
}

// DTO web không chứa token claim hay danh tính bí mật của connector.
public sealed class ChuyenMayInDto
{
    public string MaYeuCau { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public int MaMayCu { get; set; }
    public int? MaMayMoi { get; set; }
    public string LyDo { get; set; } = "";
    public int MaNguoiYeuCau { get; set; }
    public int? MaNguoiChuyen { get; set; }
    public DateTimeOffset LucYeuCau { get; set; }
    public DateTimeOffset? LucDaDung { get; set; }
    public DateTimeOffset? LucChuyen { get; set; }
    public List<DongLenhInDto> PhanConLai { get; set; } = new();
    public List<HaoHutChuyenMayRequest> HaoHut { get; set; } = new();
}

public sealed class TrangThaiChuyenMayDto
{
    public int MaDonHang { get; set; }
    public int? MaMayHienTai { get; set; }
    public string TrangThaiDon { get; set; } = "";
    public List<DongLenhInDto> PhanDangIn { get; set; } = new();
    public List<ChuyenMayInDto> LichSu { get; set; } = new();
}
