namespace PhotocopyShared.Dtos.MayInAo;

/// <summary>Một dòng file và quy cách mà connector cần xử lý.</summary>
public sealed class DongLenhInDto
{
    public int MaChiTiet { get; set; }
    public int MaFile { get; set; }
    public string TenFile { get; set; } = "";
    public string KhoGiay { get; set; } = "A4";
    public string MauSac { get; set; } = "DEN_TRANG";
    public string MatIn { get; set; } = "1_MAT";
    public int SoTrang { get; set; }
    public int? TuTrang { get; set; }
    public int? DenTrang { get; set; }
    public int SoBan { get; set; }
    public string DuongDanTaiFile { get; set; } = "";
}

/// <summary>Lệnh in được API dựng từ dữ liệu đơn hàng hiện có.</summary>
public sealed class LenhInAoDto
{
    public string MaLenh { get; set; } = "";
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    public int MaMayIn { get; set; }
    public string TenMay { get; set; } = "";
    public List<DongLenhInDto> DsDongIn { get; set; } = new();
}

/// <summary>Thông số tốc độ của một mẫu máy trong chương trình mô phỏng.</summary>
public sealed class CauHinhTocDoMayIn
{
    public int MaMayIn { get; set; }
    public string TenMay { get; set; } = "";
    public double TocDoA4 { get; set; }
    public double TocDoA3 { get; set; }
    public double TocDoHaiMat { get; set; }
    public double SoGiayRaTrangDau { get; set; }
}

/// <summary>Nội dung connector gửi khi hoàn thành hoặc gặp lỗi.</summary>
public sealed class BaoCaoMayInRequest
{
    public int? MaDonHang { get; set; }
    public string MaLenh { get; set; } = "";
    public int MaMayIn { get; set; }
    public string TrangThai { get; set; } = "SAN_SANG";
    public string? Loi { get; set; }
}

public sealed class TrangThaiKetNoiMayInDto
{
    public int MaMayIn { get; set; }
    public DateTime LanCuoiPhanHoi { get; set; }
    public string TrangThai { get; set; } = "SAN_SANG";
    public int? MaDonHangDangIn { get; set; }
    public string? Loi { get; set; }
}
