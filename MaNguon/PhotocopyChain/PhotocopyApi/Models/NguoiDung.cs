namespace PhotocopyApi.Models;

// Giống hệt các cột của bảng NguoiDung trong CSDL.
// Chỉ dùng bên trong API (có cả mật khẩu đã băm) — trả về cho Web / App thì dùng NguoiDungDto.
public class NguoiDung
{
    public int MaNguoiDung { get; set; }
    public string HoTen { get; set; }
    public string Email { get; set; }
    public string SoDienThoai { get; set; }
    public string MatKhau { get; set; }       // mật khẩu đã băm bằng BCrypt
    public string VaiTro { get; set; }        // SUPER_ADMIN / BRANCH_ADMIN / STAFF / SHIPPER / CUSTOMER
    public int? MaChiNhanh { get; set; }      // null với khách hàng và Super Admin
    public string DiaChi { get; set; }
    public string AnhDaiDien { get; set; }
    public string TrangThai { get; set; }     // HOAT_DONG / KHOA
    public DateTime NgayTao { get; set; }
}
