using System.ComponentModel.DataAnnotations;
using PhotocopyShared.KiemTraDuLieu;

namespace PhotocopyShared.Dtos.DatIn;

public sealed partial class BaoGiaRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        QuyTacNhap.KiemTraGiaoHang(HinhThucNhan, DiaChiGiao, null, ViDoGiao, KinhDoGiao, false);
}
