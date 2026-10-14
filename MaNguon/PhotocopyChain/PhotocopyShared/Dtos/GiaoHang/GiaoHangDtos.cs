using System.ComponentModel.DataAnnotations;

namespace PhotocopyShared.Dtos.GiaoHang;

public sealed class DiaChiGiaoRequest : IValidatableObject
{
    [MaxLength(255)] public string? DiaChi { get; set; }
    [Range(typeof(decimal), "-90", "90")] public decimal? ViDo { get; set; }
    [Range(typeof(decimal), "-180", "180")] public decimal? KinhDo { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if ((ViDo == null) != (KinhDo == null)) yield return new ValidationResult("Cần cả vĩ độ và kinh độ.");
        if (ViDo == null && (string.IsNullOrWhiteSpace(DiaChi) || DiaChi.Trim().Length < 10))
            yield return new ValidationResult("Vui lòng nhập đầy đủ địa chỉ để tìm vị trí.");
    }
}

public sealed class DiaChiGiaoDto
{
    public string MaViTriGiao { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public decimal ViDo { get; set; }
    public decimal KinhDo { get; set; }
}

public sealed class PhanCongGiaoRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn shipper.")]
    public int MaShipper { get; set; }
}

public sealed class KetQuaGiaoRequest : IValidatableObject
{
    public bool ThanhCong { get; set; }
    [MaxLength(255, ErrorMessage = "Lý do tối đa 255 ký tự.")] public string? LyDo { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!ThanhCong && (string.IsNullOrWhiteSpace(LyDo) || LyDo.Trim().Length < 5))
            yield return new ValidationResult("Vui lòng ghi lý do giao thất bại (ít nhất 5 ký tự).");
    }
}

public sealed class DonGiaoDto
{
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public string DiaChiChiNhanh { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string SdtNhan { get; set; } = "";
    public string DiaChiGiao { get; set; } = "";
    public decimal PhiShip { get; set; }
    public decimal TongTien { get; set; }
    public string TrangThaiDonHang { get; set; } = "";
    public string TrangThaiThanhToan { get; set; } = "";
    public int? MaGiaoHang { get; set; }
    public int? MaShipper { get; set; }
    public string TenShipper { get; set; } = "";
    public int SoLanGiao { get; set; }
    public int? LanGiao { get; set; }
    public string? TrangThaiGiao { get; set; }
    public string? LyDo { get; set; }
    public DateTime NgayDat { get; set; }
    public DateTime? NgayNhan { get; set; }
    public DateTime? NgayGiao { get; set; }
}

public sealed class ShipperDto
{
    public int MaShipper { get; set; }
    public int MaChiNhanh { get; set; }
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
}
