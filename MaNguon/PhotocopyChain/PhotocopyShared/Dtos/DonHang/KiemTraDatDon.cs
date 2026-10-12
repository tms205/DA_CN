using System.ComponentModel.DataAnnotations;
using PhotocopyShared.KiemTraDuLieu;

namespace PhotocopyShared.Dtos.DonHang;

public sealed partial class DatDonHangRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        QuyTacNhap.KiemTraGiaoHang(HinhThucNhan, DiaChiGiao, SdtNhan, ViDoGiao, KinhDoGiao, true);
}
