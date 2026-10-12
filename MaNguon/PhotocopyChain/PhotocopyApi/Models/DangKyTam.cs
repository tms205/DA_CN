namespace PhotocopyApi.Models;

// Thông tin khách vừa điền ở màn Đăng ký, đang chờ nhập mã OTP.
// Chỉ giữ trong bộ nhớ của API 5 phút — nhập đúng mã thì mới thêm vào bảng NguoiDung.
public class DangKyTam
{
    public string HoTen { get; set; }
    public string Email { get; set; }
    public string SoDienThoai { get; set; }
    public string MatKhauDaBam { get; set; }
    public string MaOtp { get; set; }
    public DateTime GuiLuc { get; set; }      // để bắt đợi 60 giây mới được gửi lại mã
    public int SoLanNhapSai { get; set; }
}
