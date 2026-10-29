using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PhotocopyShared.Dtos.Tuan3;

// Lưu ba dòng trong DonHang.GhiChu (NVARCHAR(255)), không thêm bảng/cột.
public sealed class ThongTinHoanTienRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do hoàn tiền.")]
    [MaxLength(100, ErrorMessage = "Lý do hoàn tiền tối đa 100 ký tự.")]
    public string LyDo { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập ngân hàng nhận hoàn tiền.")]
    [MaxLength(60, ErrorMessage = "Tên ngân hàng tối đa 60 ký tự.")]
    public string NganHang { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số tài khoản nhận hoàn tiền.")]
    [RegularExpression("^[0-9]{6,30}$", ErrorMessage = "Số tài khoản phải gồm 6–30 chữ số.")]
    public string SoTaiKhoan { get; set; } = "";

    public string? KiemTra()
    {
        var loi = new List<ValidationResult>();
        return Validator.TryValidateObject(this, new ValidationContext(this), loi, true)
            ? null : loi[0].ErrorMessage;
    }

    public string TaoGhiChu()
    {
        Validator.ValidateObject(this, new ValidationContext(this), true);
        return $"Lý do hoàn tiền: {MotDong(LyDo)}\nNgân hàng: {MotDong(NganHang)}\nSố tài khoản: {SoTaiKhoan}";
    }

    public static ThongTinHoanTienRequest? DocGhiChu(string? ghiChu)
    {
        string[] dong = (ghiChu ?? "").Replace("\r\n", "\n").Split('\n');
        string[] nhan = ["Lý do hoàn tiền: ", "Ngân hàng: ", "Số tài khoản: "];
        if (dong.Length != 3 || nhan.Where((prefix, i) => !dong[i].StartsWith(prefix, StringComparison.Ordinal)).Any())
            return null;
        var result = new ThongTinHoanTienRequest {
            LyDo = dong[0][nhan[0].Length..], NganHang = dong[1][nhan[1].Length..], SoTaiKhoan = dong[2][nhan[2].Length..]
        };
        return result.KiemTra() == null ? result : null;
    }

    private static string MotDong(string text) => Regex.Replace(text.Trim(), @"\s+", " ");
}
