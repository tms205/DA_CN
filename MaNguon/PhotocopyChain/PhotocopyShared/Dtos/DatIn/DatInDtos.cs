namespace PhotocopyShared.Dtos.DatIn;

public sealed class FileInDto
{
    public int MaFileIn { get; set; }
    public string TenFileGoc { get; set; } = "";
    public string? DinhDangGoc { get; set; }
    public int? DungLuong { get; set; }
    public int? SoTrang { get; set; }
    /// <summary>SAN_SANG / DA_XOA.</summary>
    public string TrangThaiFile { get; set; } = "";
    public DateTime NgayTaiLen { get; set; }
}

public sealed class BaoGiaRequest
{
    public int MaChiNhanh { get; set; }
    /// <summary>TAI_QUAY hoặc GIAO_HANG.</summary>
    public string HinhThucNhan { get; set; } = "TAI_QUAY";
    /// <summary>Bắt buộc khi GIAO_HANG — Backend gọi Ahamove lấy quãng đường và phí giao dự kiến.</summary>
    public string? DiaChiGiao { get; set; }

    /// <summary>Tọa độ GPS khi khách bấm "Dùng vị trí hiện tại" — gửi cho Ahamove định vị chính xác hơn.</summary>
    public decimal? ViDoGiao { get; set; }
    public decimal? KinhDoGiao { get; set; }
    public bool InGap { get; set; }
    public string? MaCodeKhuyenMai { get; set; }
    /// <summary>Danh sách file cùng cấu hình in mà khách đã chọn.</summary>
    public List<DongInRequest> FileIn { get; set; } = [];
}

public sealed class DongInRequest
{
    public int MaFileIn { get; set; }
    /// <summary>Dòng giá IN_AN khách chọn (khổ giấy + loại giấy + mặt in + màu).</summary>
    public int MaDichVu { get; set; }
    public int SoBanIn { get; set; } = 1;
    /// <summary>Các dịch vụ gia công đi kèm (chọn được nhiều), mỗi cái tính theo số bản in.
    /// Lò xo và bìa kiếng cùng nhóm đóng gáy nên chỉ được chọn 1 trong 2.</summary>
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
