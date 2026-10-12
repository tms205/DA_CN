namespace PhotocopyShared.Dtos.NhanVien;

/// <summary>Một dòng màn Nhân viên (F-24).</summary>
public sealed class NhanVienDto
{
    public int MaNguoiDung { get; set; }
    public string HoTen { get; set; } = "";
    public string Email { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string TenVaiTro { get; set; } = "";
    public int? MaChiNhanh { get; set; }
    public string? TenChiNhanh { get; set; }
    /// <summary>HOAT_DONG / KHOA.</summary>
    public string TrangThai { get; set; } = "";
    public DateTime NgayTao { get; set; }
    /// <summary>Người đang xem có được cấp lại mật khẩu cho dòng này không (thủ tục vẫn kiểm lại).</summary>
    public bool DuocCapLai { get; set; }
}

public sealed class CapLaiMatKhauKetQuaDto
{
    /// <summary>Chỉ trả về MỘT lần — CSDL chỉ lưu bản băm.</summary>
    public string MatKhauTam { get; set; } = "";
    public string Message { get; set; } = "";
}
