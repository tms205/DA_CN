using System.ComponentModel.DataAnnotations;

namespace PhotocopyShared.Dtos.Auth;

public sealed class DangNhapRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại.")]
    public string TaiKhoan { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string MatKhau { get; set; } = "";

    /// <summary>Web: "Duy trì phiên trong 8 giờ". Mobile: "Ghi nhớ đăng nhập".</summary>
    public bool GhiNho { get; set; }
}

public sealed class DangNhapResponse
{
    public string AccessToken { get; set; } = "";
    public DateTime HetHanLuc { get; set; }
    public NguoiDungDto NguoiDung { get; set; } = new();
}

public sealed class NguoiDungDto
{
    public int MaNguoiDung { get; set; }
    public string HoTen { get; set; } = "";
    public string Email { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string? AnhDaiDien { get; set; }
    public string TenVaiTro { get; set; } = "";
    public int? MaChiNhanh { get; set; }
    public string? TenChiNhanh { get; set; }
    public string? DiaChiChiNhanh { get; set; }
    public string? DiaChiMacDinh { get; set; }
}

/// <summary>Quy tắc mật khẩu theo Figma DangKy: tối thiểu 8 ký tự, có chữ hoa và số.</summary>
public static class QuyTacMatKhau
{
    public const string BieuThuc = @"^(?=.*[A-Z])(?=.*\d).{8,}$";
    public const string ThongBao = "Mật khẩu tối thiểu 8 ký tự, có chữ hoa và số.";
}

/// <summary>F-01 bước 1/2: thông tin đăng ký — API giữ tạm và gửi OTP, CHƯA tạo tài khoản.</summary>
public sealed class DangKyRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string HoTen { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(100, ErrorMessage = "Email tối đa 100 ký tự.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại phải gồm 10 chữ số, bắt đầu bằng 0.")]
    public string SoDienThoai { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [RegularExpression(QuyTacMatKhau.BieuThuc, ErrorMessage = QuyTacMatKhau.ThongBao)]
    public string MatKhau { get; set; } = "";
}

/// <summary>F-01 bước 2/2: nhập đúng OTP thì tài khoản mới được tạo.</summary>
public sealed class XacNhanDangKyRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP gồm 6 chữ số.")]
    public string MaOtp { get; set; } = "";
}

/// <summary>Kết quả gửi OTP — App dùng 2 mốc giây để chạy đồng hồ đếm ngược như Figma.</summary>
public sealed class OtpDaGuiDto
{
    public string Message { get; set; } = "";
    public int HetHanSauGiay { get; set; }
    public int GuiLaiSauGiay { get; set; }

    /// <summary>
    /// true: tài khoản nhân viên (STAFF / SHIPPER / BRANCH_ADMIN) — KHÔNG gửi OTP, đã ghi yêu cầu để
    /// Quản lý chi nhánh / Super Admin cấp mật khẩu tạm. Client hiện <see cref="Message"/>, không sang bước nhập OTP.
    /// </summary>
    public bool CanCapTrenCapLai { get; set; }
}

public sealed class CapNhatHoSoRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string HoTen { get; set; } = "";

    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại phải gồm 10 chữ số, bắt đầu bằng 0.")]
    public string? SoDienThoai { get; set; }

    /// <summary>Chỉ áp dụng cho khách hàng.</summary>
    [MaxLength(255)]
    public string? DiaChiMacDinh { get; set; }
}

public sealed class DoiMatKhauRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    public string MatKhauCu { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [RegularExpression(QuyTacMatKhau.BieuThuc, ErrorMessage = QuyTacMatKhau.ThongBao)]
    public string MatKhauMoi { get; set; } = "";
}

public sealed class QuenMatKhauRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = "";
}

public sealed class XacMinhOtpDatLaiRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Mã OTP gồm 6 chữ số.")]
    public string MaOtp { get; set; } = "";
}

public sealed class DatLaiMatKhauRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP gồm 6 chữ số.")]
    public string MaOtp { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [RegularExpression(QuyTacMatKhau.BieuThuc, ErrorMessage = QuyTacMatKhau.ThongBao)]
    public string MatKhauMoi { get; set; } = "";
}
