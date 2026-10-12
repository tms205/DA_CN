using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyShared.KiemTraDuLieu;

public static class QuyTacNhap
{
    public const string SoDienThoai = @"^0[0-9]{9}$";
    public const string LoiSoDienThoai = "Số điện thoại phải gồm 10 chữ số, bắt đầu bằng 0.";
    public const string MaKhuyenMai = @"^[A-Za-z0-9_-]{1,30}$";
    public const string LoiMaKhuyenMai = "Mã khuyến mãi tối đa 30 ký tự, chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.";

    // Web dùng cùng annotations với API, gồm cả các dòng con trong danh sách file.
    public static string? LayLoi(object model)
    {
        if (model == null) return "Vui lòng nhập dữ liệu.";
        var loi = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), loi, true);
        return loi.FirstOrDefault()?.ErrorMessage;
    }

    public static IEnumerable<ValidationResult> KiemTraGiaoHang(string hinhThuc,
        string? diaChi, string? sdt, decimal? viDo, decimal? kinhDo, bool canSdt)
    {
        if (hinhThuc == "GIAO_HANG" && string.IsNullOrWhiteSpace(diaChi))
            yield return new ValidationResult("Vui lòng nhập địa chỉ giao hàng.", ["DiaChiGiao"]);
        if (hinhThuc == "GIAO_HANG" && canSdt && string.IsNullOrWhiteSpace(sdt))
            yield return new ValidationResult("Vui lòng nhập số điện thoại người nhận.", ["SdtNhan"]);
        if (viDo.HasValue != kinhDo.HasValue)
            yield return new ValidationResult("Tọa độ giao hàng phải có đủ vĩ độ và kinh độ.", ["ViDoGiao", "KinhDoGiao"]);
    }
}

// Chỉ áp dụng khi tạo tài khoản, không áp dụng cho đăng nhập/khôi phục tài khoản cũ.
public sealed class GmailAttribute : ValidationAttribute
{
    public GmailAttribute() : base("Email nhập mới phải là địa chỉ @gmail.com hợp lệ.") { }

    public override bool IsValid(object? value)
    {
        if (value == null || value is string { Length: 0 }) return true; // Required kiểm tra riêng.
        if (value is not string email || email.Length > 100) return false;
        return Regex.IsMatch(email, @"\A[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)*(?:\+[A-Za-z0-9._-]+)?@gmail\.com\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

public sealed class TaiKhoanAttribute : ValidationAttribute
{
    public TaiKhoanAttribute() : base("Vui lòng nhập email hoặc số điện thoại hợp lệ.") { }

    public override bool IsValid(object? value)
    {
        if (value == null || value is string { Length: 0 }) return true;
        return value is string tk && tk.Length <= 100 &&
            (Regex.IsMatch(tk, @"\A0[0-9]{9}\z") || Regex.IsMatch(tk, @"\A[^@\s]+@[^@\s]+\.[^@\s]+\z"));
    }
}

public sealed class MatKhauMoiAttribute : ValidationAttribute
{
    public MatKhauMoiAttribute() : base("Mật khẩu cần ít nhất 8 ký tự, có chữ hoa và số, tối đa 72 byte UTF-8 và không chứa ký tự điều khiển.") { }

    public override bool IsValid(object? value)
    {
        if (value == null || value is string { Length: 0 }) return true;
        return value is string mk && mk.Length >= 8 && Encoding.UTF8.GetByteCount(mk) <= 72
            && mk.Any(c => c is >= 'A' and <= 'Z') && mk.Any(c => c is >= '0' and <= '9')
            && !mk.Any(char.IsControl);
    }
}

public sealed class SoTienAttribute : ValidationAttribute
{
    public SoTienAttribute() : base("Số tiền phải là số nguyên từ 0 đến 999.999.999.999 đồng.") { }

    public override bool IsValid(object? value) => value == null ||
        value is decimal so && so >= 0 && so <= 999999999999m && decimal.Truncate(so) == so;
}

public sealed class DanhSachMaAttribute : ValidationAttribute
{
    public bool BatBuoc { get; set; }
    public DanhSachMaAttribute() : base("Danh sách mã phải gồm các số nguyên dương, không trùng nhau.") { }

    public override bool IsValid(object? value) => value is List<int> ds
        && (!BatBuoc || ds.Count > 0) && ds.All(x => x > 0) && ds.Distinct().Count() == ds.Count;
}

public sealed class DanhSachFileAttribute : ValidationAttribute
{
    public DanhSachFileAttribute() : base("Vui lòng chọn file và quy cách in hợp lệ, không chọn trùng file.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        string[]? truong = context.MemberName == null ? null : [context.MemberName];
        if (value is not List<DongInRequest> ds || ds.Count == 0)
            return new ValidationResult("Vui lòng chọn ít nhất một file in.", truong);
        var daChon = new HashSet<int>();
        for (int i = 0; i < ds.Count; i++)
        {
            string? loi = ds[i] == null ? "Dòng in không được để trống." : QuyTacNhap.LayLoi(ds[i]);
            if (loi != null) return new ValidationResult($"File thứ {i + 1}: {loi}", truong);
            if (!daChon.Add(ds[i].MaFileIn))
                return new ValidationResult("Không chọn trùng file in trong cùng đơn.", truong);
        }
        return ValidationResult.Success;
    }
}
