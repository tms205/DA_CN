using System.ComponentModel.DataAnnotations;
using PhotocopyShared.KiemTraDuLieu;

namespace PhotocopyShared.Dtos.Tuan3;

public sealed class VatTuDanhMucDto
{
    public int MaVatTu { get; set; }
    public string TenVatTu { get; set; } = "";
    public string DonViTinh { get; set; } = "";
}

public sealed class TaoMayInRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn chi nhánh.")]
    public int MaChiNhanh { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên máy in.")]
    [MaxLength(100, ErrorMessage = "Tên máy tối đa 100 ký tự.")]
    public string TenMay { get; set; } = "";
    [Required]
    [RegularExpression("^(A4|A3)$", ErrorMessage = "Khổ giấy lớn nhất phải là A4 hoặc A3.")]
    public string KhoGiay { get; set; } = "A4";
    public bool InMau { get; set; }
    public bool In2Mat { get; set; }
    [Range(1, 1000, ErrorMessage = "Tốc độ phải từ 1 đến 1.000 trang/phút.")]
    public int TocDo { get; set; } = 20;
    [Range(0, int.MaxValue, ErrorMessage = "Counter phải là số nguyên không âm.")]
    public int SoTrangDaIn { get; set; }
    [Required]
    [RegularExpression("^(HOAT_DONG|BAO_TRI|HONG)$", ErrorMessage = "Trạng thái máy in không hợp lệ.")]
    public string TrangThai { get; set; } = "HOAT_DONG";
}

public sealed class CapNhatGiaChiNhanhRequest
{
    [SoTien]
    public decimal DonGia { get; set; }
    [Required]
    [RegularExpression("^(HIEN|AN)$", ErrorMessage = "Trạng thái bán không hợp lệ.")]
    public string TrangThai { get; set; } = "HIEN";
}
