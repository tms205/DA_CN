using System.ComponentModel.DataAnnotations;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyShared.Dtos.DonHang;

public sealed class KhieuNaiHoanTienRequest
{
    [Required, StringLength(2000, MinimumLength = 11)]
    public string MoTa { get; set; } = "";
    [Required, StringLength(60)] public string NganHang { get; set; } = "";
    [Required, RegularExpression("^[0-9]{6,30}$")] public string SoTaiKhoan { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 2)] public string ChuTaiKhoan { get; set; } = "";
    [Required] public string MaBangChung { get; set; } = "";
}

public sealed class QuyetDinhHoanTienRequest
{
    [Required, RegularExpression("^(CHAP_THUAN|TU_CHOI|DA_HOAN_TIEN)$")]
    public string TrangThai { get; set; } = "";
    [Required, StringLength(500, MinimumLength = 11)] public string LyDo { get; set; } = "";
    [StringLength(100)] public string? MaChuyenKhoan { get; set; }
    [RegularExpression("^[a-f0-9]{32}$")] public string? MaBienLai { get; set; }
}

public sealed class HoSoKhieuNaiDto
{
    public int MaDonHang { get; set; }
    public int MaKhachHang { get; set; }
    public string TenKhachHang { get; set; } = "";
    public string TenChiNhanh { get; set; } = "";
    public string TrangThai { get; set; } = "CHO_DUYET";
    public string MoTa { get; set; } = "";
    public string NganHang { get; set; } = "";
    public string SoTaiKhoan { get; set; } = "";
    public string ChuTaiKhoan { get; set; } = "";
    public decimal SoTien { get; set; }
    public DateTimeOffset GuiLuc { get; set; }
    public List<AnhBangChungDto> Anh { get; set; } = [];
    public List<QuyetDinhHoanTienDto> LichSu { get; set; } = [];

    public bool ChoQuyetDinh(string moi) => (TrangThai, moi) is
        ("CHO_DUYET", "CHAP_THUAN") or ("CHO_DUYET", "TU_CHOI") or ("CHAP_THUAN", "DA_HOAN_TIEN");
}

public sealed class QuyetDinhHoanTienDto
{
    public int MaCeo { get; set; }
    public string TrangThai { get; set; } = "";
    public string LyDo { get; set; } = "";
    public string? MaChuyenKhoan { get; set; }
    public DateTimeOffset Luc { get; set; }
    public BienLaiHoanTienDto? BienLai { get; set; }
}

public sealed class XacNhanHoanTienRequest
{
    [Required, StringLength(500, MinimumLength = 11)] public string LyDo { get; set; } = "";
    [Required, StringLength(100)] public string MaChuyenKhoan { get; set; } = "";
    [Required, RegularExpression("^[a-f0-9]{32}$")] public string MaBienLai { get; set; } = "";
}

public sealed class BienLaiHoanTienDto
{
    public AnhBangChungDto Anh { get; set; } = new();
    public int MaCeo { get; set; }
    public DateTimeOffset Luc { get; set; }
    public string MaChuyenKhoan { get; set; } = "";
    public string LyDo { get; set; } = "";
}
