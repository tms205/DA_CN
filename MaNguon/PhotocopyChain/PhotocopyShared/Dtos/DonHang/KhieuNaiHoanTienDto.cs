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
    // KHACH_GUI: khách tự gửi từ App; IN_LOI_3_LAN: quầy lập khi đã in lại 3 lần vẫn lỗi.
    public string Nguon { get; set; } = "KHACH_GUI";
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

// Quầy in lại đủ 3 lần mà vẫn lỗi: nhân viên lập hồ sơ hoàn tiền cho đơn đã chuyển khoản VietQR.
public sealed class HoanTienInLoiRequest
{
    [Required, StringLength(60, MinimumLength = 2)] public string NganHang { get; set; } = "";
    [Required, RegularExpression("^[0-9]{6,30}$")] public string SoTaiKhoan { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 2)] public string ChuTaiKhoan { get; set; } = "";
    [StringLength(300)] public string? GhiChu { get; set; }
}

public static class QuyTacHoanTienInLoi
{
    public const string Nguon = "IN_LOI_3_LAN";

    // Hiện nút ở trang Bàn giao: đơn gốc, trả VietQR, đã chuyển khoản, đã in lại đủ số lần tối đa.
    public static bool HienNut(int? maDonHangGoc, string phuongThuc, string thanhToan, int soLanInLai) =>
        maDonHangGoc == null && phuongThuc == "VIETQR" && thanhToan == "DA_THANH_TOAN"
        && soLanInLai >= PhotocopyShared.QuyCachIn.QuyTacIn.SoLanInLaiToiDa;

    // Trả câu lỗi nếu chưa được lập hồ sơ; null là hợp lệ.
    // soInLaiDangChay: lần in lại còn ở hàng đợi / đang in (chưa in xong, chưa hủy).
    public static string? LoiKhongChoHoan(string trangThaiDon, string hinhThucNhan, int? maDonHangGoc,
        string phuongThuc, string thanhToan, bool daCoHoSo, int soLanInLai, int soInLaiDangChay)
    {
        if (maDonHangGoc != null) return "Chỉ lập hoàn tiền trên đơn gốc, không lập trên đơn in lại.";
        if (trangThaiDon != "DA_IN" || hinhThucNhan != "TAI_QUAY") return "Đơn không còn ở bước chờ bàn giao tại quầy.";
        if (phuongThuc != "VIETQR") return "Chỉ hoàn tiền cho đơn đã chuyển khoản VietQR.";
        if (thanhToan != "DA_THANH_TOAN") return "Đơn chưa thanh toán hoặc đã hoàn tiền.";
        if (daCoHoSo) return "Đơn đã có hồ sơ hoàn tiền, không lập trùng.";
        if (soLanInLai < PhotocopyShared.QuyCachIn.QuyTacIn.SoLanInLaiToiDa)
            return $"Đơn mới in lại {soLanInLai}/{PhotocopyShared.QuyCachIn.QuyTacIn.SoLanInLaiToiDa} lần; chỉ hoàn tiền khi đã in lại đủ 3 lần vẫn lỗi.";
        if (soInLaiDangChay > 0) return "Lần in lại gần nhất chưa in xong. Chờ in xong rồi mới lập hoàn tiền.";
        return null;
    }
}
