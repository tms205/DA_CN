namespace PhotocopyShared.Dtos.Tuan3;

// Báo cáo "in lại do lỗi quầy" theo nhân viên trong một tháng.
// Dùng để quản lý/CEO thấy ai in lại nhiều bất thường (in lại 0đ là cửa hàng chịu chi phí giấy mực).
public sealed class BaoCaoInLaiDto
{
    public string Thang { get; set; } = "";                 // dạng yyyy-MM
    public List<InLaiTheoNhanVienDto> TheoNhanVien { get; set; } = new();
    public List<LanInLaiDto> ChiTiet { get; set; } = new();
}

// Một dòng tổng hợp cho một nhân viên.
public sealed class InLaiTheoNhanVienDto
{
    public int MaNhanVien { get; set; }
    public string HoTen { get; set; } = "";
    public string TenChiNhanh { get; set; } = "";
    public int SoDonDaXuLy { get; set; }      // đơn thường (không phải in lại) người này tiếp nhận trong tháng
    public int SoLanInLai { get; set; }       // số lần người này tạo yêu cầu in lại (không tính lần đã hủy)
    public int SoDonGocBiInLai { get; set; }  // in lại cho bao nhiêu đơn gốc khác nhau
    public int SoTrangInLai { get; set; }     // tổng trang in lại = số trang × số bản
}

// Một lần in lại cụ thể, để quản lý xem lý do.
public sealed class LanInLaiDto
{
    public int MaDonHang { get; set; }
    public int MaDonHangGoc { get; set; }
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public DateTime NgayDat { get; set; }
    public int SoTrang { get; set; }
    public string LyDo { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public int SoAnh { get; set; }            // số ảnh bản in lỗi đã chụp (lần in lại trước 28/11 không có ảnh)
}

// Ảnh bằng chứng của một lần in lại, để quản lý bấm xem.
public sealed class BangChungInLaiDto
{
    public int MaDonInLai { get; set; }
    public int MaDonHangGoc { get; set; }
    public DateTimeOffset GuiLuc { get; set; }
    public List<string> MaAnh { get; set; } = new();
}

public sealed class TaiBangChungInLaiResponse
{
    public string MaBangChung { get; set; } = "";
    public int SoAnh { get; set; }
}
