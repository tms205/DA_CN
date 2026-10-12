using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using PhotocopyShared.KiemTraDuLieu;

namespace PhotocopyShared.Dtos.Tuan3;

public sealed partial class KhachTaiQuayRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        string ten = (HoTen ?? "").Trim();
        string sdt = (SoDienThoai ?? "").Trim();
        if (ten.Length == 0 && sdt.Length == 0)
            yield return new ValidationResult("Vui lòng nhập họ tên hoặc số điện thoại khách hàng.",
                [nameof(HoTen), nameof(SoDienThoai)]);
        if (ten.Length > 0 && ten.Length < 2)
            yield return new ValidationResult("Họ tên khách hàng cần ít nhất 2 ký tự.", [nameof(HoTen)]);
        if (sdt.Length > 0 && !Regex.IsMatch(sdt, QuyTacNhap.SoDienThoai))
            yield return new ValidationResult(QuyTacNhap.LoiSoDienThoai, [nameof(SoDienThoai)]);
    }
}

public sealed partial class TaoInLaiTaiQuayRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DenTrang < TuTrang)
            yield return new ValidationResult("Trang kết thúc không được nhỏ hơn trang bắt đầu.", [nameof(DenTrang)]);
    }
}

public sealed partial class TaoDonBoSungTaiQuayRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DenTrang < TuTrang)
            yield return new ValidationResult("Trang kết thúc không được nhỏ hơn trang bắt đầu.", [nameof(DenTrang)]);
    }
}

public sealed partial class LuuDichVuRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (LoaiDichVu == "IN_AN" && (string.IsNullOrWhiteSpace(KhoGiay) ||
            string.IsNullOrWhiteSpace(LoaiGiay) || string.IsNullOrWhiteSpace(MatIn) || string.IsNullOrWhiteSpace(MauSac)))
            yield return new ValidationResult("Dịch vụ in cần đủ khổ giấy, loại giấy, mặt in và màu sắc.",
                [nameof(KhoGiay), nameof(LoaiGiay), nameof(MatIn), nameof(MauSac)]);
    }
}

public sealed partial class LuuKhuyenMaiRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (NgayBatDau == default || NgayKetThuc == default || NgayBatDau.Year < 1753 || NgayKetThuc.Year < 1753)
            yield return new ValidationResult("Vui lòng chọn ngày bắt đầu và kết thúc hợp lệ.", [nameof(NgayBatDau), nameof(NgayKetThuc)]);
        else if (NgayKetThuc < NgayBatDau)
            yield return new ValidationResult("Ngày kết thúc không được trước ngày bắt đầu.", [nameof(NgayKetThuc)]);
    }
}

public sealed partial class ChiNhanhQuanTriDto : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ViDo.HasValue != KinhDo.HasValue)
            yield return new ValidationResult("Chi nhánh cần đủ vĩ độ và kinh độ.", [nameof(ViDo), nameof(KinhDo)]);
        if (GioMoCua < TimeSpan.Zero || GioDongCua >= TimeSpan.FromDays(1) || GioMoCua >= GioDongCua)
            yield return new ValidationResult("Giờ mở/đóng cửa phải trong ngày, giờ đóng sau giờ mở.", [nameof(GioMoCua), nameof(GioDongCua)]);
        if (decimal.Round(BanKinhGiao, 2) != BanKinhGiao)
            yield return new ValidationResult("Bán kính giao hàng tối đa 2 chữ số thập phân.", [nameof(BanKinhGiao)]);
        if (ViDo.HasValue && decimal.Round(ViDo.Value, 6) != ViDo ||
            KinhDo.HasValue && decimal.Round(KinhDo.Value, 6) != KinhDo)
            yield return new ValidationResult("Tọa độ tối đa 6 chữ số thập phân.", [nameof(ViDo), nameof(KinhDo)]);
    }
}
