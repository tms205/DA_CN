using PhotocopyShared.KiemTraDuLieu;
using System.ComponentModel.DataAnnotations;
namespace PhotocopyShared.Dtos.DatIn;

public sealed class FileInDto
{
    public int MaFileIn { get; set; }
    public string TenFileGoc { get; set; } = "";
    public string? DinhDangGoc { get; set; }
    public int? DungLuong { get; set; }
    public int? SoTrang { get; set; }
    public string? KichThuocAnh { get; set; }
    public int? SoLuongAnh { get; set; }
    /// <summary>SAN_SANG / DA_XOA.</summary>
    public string TrangThaiFile { get; set; } = "";
    public DateTime NgayTaiLen { get; set; }
}

public sealed partial class BaoGiaRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã chi nhánh phải là số nguyên dương.")]
    public int MaChiNhanh { get; set; }
    /// <summary>TAI_QUAY hoặc GIAO_HANG.</summary>
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(TAI_QUAY|GIAO_HANG)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
    public string HinhThucNhan { get; set; } = "TAI_QUAY";
    /// <summary>Bắt buộc khi GIAO_HANG — Backend gọi Ahamove lấy quãng đường và phí giao dự kiến.</summary>
    [MaxLength(255, ErrorMessage = "Địa chỉ giao tối đa 255 ký tự.")]
    public string? DiaChiGiao { get; set; }

    /// <summary>Tọa độ GPS khi khách bấm "Dùng vị trí hiện tại" — gửi cho Ahamove định vị chính xác hơn.</summary>
    [Range(typeof(decimal), "-90", "90", ErrorMessage = "Vĩ độ phải từ -90 đến 90.")]
    public decimal? ViDoGiao { get; set; }
    [Range(typeof(decimal), "-180", "180", ErrorMessage = "Kinh độ phải từ -180 đến 180.")]
    public decimal? KinhDoGiao { get; set; }
    public bool InGap { get; set; }
    [RegularExpression(@"^[A-Za-z0-9_-]{1,30}$", ErrorMessage = "Mã khuyến mãi tối đa 30 ký tự, chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.")]
    public string? MaCodeKhuyenMai { get; set; }
    /// <summary>Danh sách file cùng cấu hình in mà khách đã chọn.</summary>
    [DanhSachFile]
    public List<DongInRequest> FileIn { get; set; } = [];
}

public sealed class DongInRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã file phải là số nguyên dương.")]
    public int MaFileIn { get; set; }
    /// <summary>Dòng giá IN_AN khách chọn (khổ giấy + loại giấy + mặt in + màu).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Mã dịch vụ phải là số nguyên dương.")]
    public int MaDichVu { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Số bản in phải là số nguyên dương.")]
    public int SoBanIn { get; set; } = 1;
    /// <summary>DUNG / NGANG. Hướng giấy không làm thay đổi giá.</summary>
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(DUNG|NGANG)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
    public string HuongGiay { get; set; } = "DUNG";
    /// <summary>Các dịch vụ gia công đi kèm (chọn được nhiều), mỗi cái tính theo số bản in.
    /// Lò xo và bìa kiếng cùng nhóm đóng gáy nên chỉ được chọn 1 trong 2.</summary>
    [DanhSachMa]
    public List<int> DsGiaCong { get; set; } = [];
}

/// <summary>Kết quả sp_BaoGia — cùng công thức với sp_TaoDonHang.</summary>
public sealed class BaoGiaResponse
{
    public decimal TienIn { get; set; }
    public decimal TienGiaCong { get; set; }
    public decimal PhuPhiGap { get; set; }
    public decimal PhiShip { get; set; }
    public decimal GiamGia { get; set; }
    public decimal TongTien { get; set; }
    public int TongSoTrang { get; set; }
    /// <summary>NULL khi khách không nhập mã.</summary>
    public bool? KhuyenMaiHopLe { get; set; }
    public string? LyDoKhuyenMai { get; set; }
    public DateTime? NgayHenTra { get; set; }
}
