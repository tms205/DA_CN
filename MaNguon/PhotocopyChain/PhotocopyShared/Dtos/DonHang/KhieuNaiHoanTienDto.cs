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

// Quầy đã in lại (ít nhất 1 lần) mà vẫn lỗi: nhân viên lập hồ sơ hoàn tiền cho đơn đã chuyển khoản VietQR.
public sealed class HoanTienInLoiRequest
{
    [Required, StringLength(60, MinimumLength = 2)] public string NganHang { get; set; } = "";
    [Required, RegularExpression("^[0-9]{6,30}$")] public string SoTaiKhoan { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 2)] public string ChuTaiKhoan { get; set; } = "";
    [StringLength(300)] public string? GhiChu { get; set; }
}

public static class QuyTacHoanTienInLoi
{
    // Giữ nguyên mã nguồn "IN_LOI_3_LAN" vì hồ sơ cũ trong CSDL và App đang nhận theo mã này.
    public const string Nguon = "IN_LOI_3_LAN";

    // 28/11: không bắt in lại đủ 3 lần nữa. In lại 1 lần mà vẫn lỗi là quầy được đề nghị hoàn tiền luôn,
    // tránh tốn thêm giấy mực cho lần 2, lần 3. (Vẫn có thể in lại tiếp tới tối đa 3 lần nếu khách muốn chờ.)
    public const int SoLanInLaiToiThieu = 1;

    // Hiện nút ở trang Bàn giao: đơn gốc, trả VietQR, đã chuyển khoản, đã in lại ít nhất 1 lần.
    public static bool HienNut(int? maDonHangGoc, string phuongThuc, string thanhToan, int soLanInLai) =>
        maDonHangGoc == null && phuongThuc == "VIETQR" && thanhToan == "DA_THANH_TOAN"
        && soLanInLai >= SoLanInLaiToiThieu;

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
        if (soLanInLai < SoLanInLaiToiThieu)
            return "Đơn chưa in lại lần nào; chỉ hoàn tiền khi quầy đã in lại mà tài liệu vẫn lỗi.";
        if (soInLaiDangChay > 0) return "Lần in lại gần nhất chưa in xong. Chờ in xong rồi mới lập hoàn tiền.";
        return null;
    }
}
