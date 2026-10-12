using System.ComponentModel.DataAnnotations;
using PhotocopyShared.Dtos.Auth;

namespace PhotocopyAdmin;

// Form web cần nhập mật khẩu hai lần; không thay hợp đồng API của app mobile.
public sealed class DatLaiMatKhauForm
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [RegularExpression(QuyTacMatKhau.BieuThuc, ErrorMessage = QuyTacMatKhau.ThongBao)]
    public string MatKhauMoi { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [Compare(nameof(MatKhauMoi), ErrorMessage = "Hai mật khẩu chưa trùng nhau. Vui lòng kiểm tra lại.")]
    public string MatKhauXacNhan { get; set; } = "";
}
