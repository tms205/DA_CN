namespace PhotocopyShared.Constants;

/// <summary>
/// Tên các claim trong JWT. API ghi, Admin đọc — để chung một chỗ cho khỏi lệch.
/// </summary>
public static class TenClaim
{
    public const string MaNguoiDung = "ma_nguoi_dung";
    public const string HoTen = "ho_ten";
    public const string Email = "email";
    public const string VaiTro = "vai_tro";
    public const string MaChiNhanh = "ma_chi_nhanh";
}
