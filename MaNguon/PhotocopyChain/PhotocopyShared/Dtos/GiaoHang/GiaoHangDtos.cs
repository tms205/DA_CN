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
    public string? TrangThaiDoiChieu { get; set; }
    public int SoPhanHoiChoDoiChieu { get; set; }
}

public sealed class LuotLichSuGiaoDto
{
    public int MaGiaoHang { get; set; }
    public int MaDonHang { get; set; }
    public int LanGiao { get; set; }
    public string TenShipper { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public string? LyDo { get; set; }
    public DateTime? NgayNhan { get; set; }
    public DateTime? NgayGiao { get; set; }
    public string? TrangThaiDoiChieu { get; set; }
}

public sealed class ShipperDto
{
    public int MaShipper { get; set; }
    public int MaChiNhanh { get; set; }
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
}

public sealed class PhanBoGiaoHangDto
{
    public string Nhom { get; set; } = "";
    public int SoDon { get; set; }
}

public class ViTriShipperRequest
{
    [Required(ErrorMessage = "Cần lấy vị trí GPS."), Range(typeof(decimal), "-90", "90", ErrorMessage = "Vĩ độ không hợp lệ.")] public decimal? ViDo { get; set; }
    [Required(ErrorMessage = "Cần lấy vị trí GPS."), Range(typeof(decimal), "-180", "180", ErrorMessage = "Kinh độ không hợp lệ.")] public decimal? KinhDo { get; set; }
    [Required(ErrorMessage = "Thiếu độ chính xác GPS."), Range(0, 100000, ErrorMessage = "Độ chính xác GPS không hợp lệ.")] public double? DoChinhXac { get; set; }
    [Required(ErrorMessage = "Thiếu thời điểm lấy GPS.")] public DateTimeOffset? ThoiDiemGps { get; set; }
}

public sealed class AnhBangChungDto
{
    public string MaAnh { get; set; } = "";
    public string LoaiNoiDung { get; set; } = "";
    public long KichThuoc { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class BangChungGiaoDto
{
    public int MaGiaoHang { get; set; }
    public int MaDonHang { get; set; }
    public int LanGiao { get; set; }
    public string TenShipper { get; set; } = "";
    public string? LyDo { get; set; }
    public decimal? ViDo { get; set; }
    public decimal? KinhDo { get; set; }
    public double? DoChinhXac { get; set; }
    public DateTimeOffset? ThoiDiemGps { get; set; }
    public DateTimeOffset? ThoiDiemBaoCao { get; set; }
    public string? DiaChi { get; set; }
    public List<AnhBangChungDto> Anh { get; set; } = new();
}

public sealed class PhanHoiGiaoHangDto
{
    public int MaKhachHang { get; set; }
    public string NoiDung { get; set; } = "";
    public DateTimeOffset GuiLuc { get; set; }
    public List<AnhBangChungDto> Anh { get; set; } = new();
    public string TrangThai { get; set; } = "CHO_DOI_CHIEU";
    public string? KetLuan { get; set; }
    public int? MaNguoiXuLy { get; set; }
    public string? TenNguoiXuLy { get; set; }
    public DateTimeOffset? XuLyLuc { get; set; }
}

public sealed class LuotDoiChieuGiaoDto
{
    public int MaGiaoHang { get; set; }
    public int MaDonHang { get; set; }
    public int LanGiao { get; set; }
    public string? LyDoGiaoThatBai { get; set; }
    public DateTime? NgayGiao { get; set; }
    public PhanHoiGiaoHangDto? PhanHoi { get; set; }
}

public sealed class XuLyDoiChieuRequest
{
    [Required(ErrorMessage = "Vui lòng ghi kết luận đối chiếu.")]
    [StringLength(1000,MinimumLength = 10,ErrorMessage = "Kết luận cần từ 10 đến 1.000 ký tự.")]
    public string KetLuan { get; set; } = "";
}
