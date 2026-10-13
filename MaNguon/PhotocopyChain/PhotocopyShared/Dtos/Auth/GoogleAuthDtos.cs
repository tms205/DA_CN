using System.ComponentModel.DataAnnotations;
using PhotocopyShared.KiemTraDuLieu;

namespace PhotocopyShared.Dtos.Auth;

public sealed class GoogleCongKhaiDto
{
    public bool DaBat { get; set; }
    public string WebClientId { get; set; } = "";
}

public sealed class DangNhapGoogleRequest
{
    [Required(ErrorMessage = "Thiếu token đăng nhập Google.")]
    [MinLength(20)]
    [MaxLength(8192)]
    public string IdToken { get; set; } = "";
    public bool GhiNho { get; set; } = true;
}

public sealed class BoSungGoogleDto
{
    public bool CanBoSungThongTin { get; set; } = true;
    public string VeDangKy { get; set; } = "";
    public string Email { get; set; } = "";
    public string HoTen { get; set; } = "";
}

public sealed class DangKyGoogleRequest : IValidatableObject
{
    [Required(ErrorMessage = "Phiên đăng ký Google không hợp lệ.")]
    [MinLength(40)]
    [MaxLength(128)]
    public string VeDangKy { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MinLength(2, ErrorMessage = "Họ tên cần ít nhất 2 ký tự.")]
    [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string HoTen { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(QuyTacNhap.SoDienThoai, ErrorMessage = QuyTacNhap.LoiSoDienThoai)]
    public string SoDienThoai { get; set; } = "";
    public bool DongYDieuKhoan { get; set; }
    public bool GhiNho { get; set; } = true;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!DongYDieuKhoan)
            yield return new ValidationResult("Vui lòng đồng ý điều khoản và chính sách bảo mật.", [nameof(DongYDieuKhoan)]);
    }
}
