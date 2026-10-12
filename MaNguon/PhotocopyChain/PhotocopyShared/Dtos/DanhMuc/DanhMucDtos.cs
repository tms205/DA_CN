namespace PhotocopyShared.Dtos.DanhMuc;

public sealed class ChiNhanhDto
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public TimeSpan GioMoCua { get; set; }
    public TimeSpan GioDongCua { get; set; }
    /// <summary>km</summary>
    public decimal BanKinhGiao { get; set; }
    public double DiemDanhGiaTB { get; set; }
    public int SoLuotDanhGia { get; set; }

    /// <summary>Chỉ có giá trị ở bước 3 (POST /api/don-hang/chi-nhanh-phu-hop):
    /// chi nhánh có máy in đáp ứng được khổ giấy / in màu / in 2 mặt khách đã chọn không.
    /// Các chỗ khác luôn trả về true.</summary>
    public bool DapUngDuoc { get; set; } = true;

    /// <summary>Lý do làm mờ chi nhánh, vd "Cơ sở 2 chưa có máy in màu".</summary>
    public string? LyDoKhongDapUng { get; set; }
}

/// <summary>Một dòng bảng giá: tài liệu / trang, ảnh thẻ / ảnh hoặc gia công / cuốn.</summary>
public sealed class DichVuDto
{
    public int MaDichVu { get; set; }
    public string TenDichVu { get; set; } = "";
    /// <summary>IN_AN hoặc GIA_CONG.</summary>
    public string LoaiDichVu { get; set; } = "";
    /// <summary>A4 / A3 — NULL nếu là gia công.</summary>
    public string? KhoGiay { get; set; }
    public string? LoaiGiay { get; set; }
    /// <summary>1_MAT / 2_MAT.</summary>
    public string? MatIn { get; set; }
    /// <summary>MAU / DEN_TRANG.</summary>
    public string? MauSac { get; set; }
    /// <summary>Nhóm gia công loại trừ nhau — DONG_GAY: lò xo hoặc bìa kiếng, chỉ chọn 1. NULL = chọn thoải mái.</summary>
    public string? NhomGiaCong { get; set; }
    public decimal DonGia { get; set; }
    /// <summary>NULL: tài liệu; mã kích thước: giá tính theo từng ảnh thẻ.</summary>
    public string? KichThuocAnh { get; set; }
}
