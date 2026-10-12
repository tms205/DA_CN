namespace PhotocopyShared.QuyCachIn;

public sealed record KichThuocAnhThe(string Ma, string Ten, double RongCm, double CaoCm);

public static class QuyCachAnhThe
{
    public static IReadOnlyList<KichThuocAnhThe> DanhSach { get; } = Array.AsReadOnly(new[]
    {
        new KichThuocAnhThe("2X3", "2 × 3 cm", 2, 3),
        new KichThuocAnhThe("3X4", "3 × 4 cm", 3, 4),
        new KichThuocAnhThe("4X6", "4 × 6 cm", 4, 6),
        new KichThuocAnhThe("35X45", "3,5 × 4,5 cm", 3.5, 4.5),
        new KichThuocAnhThe("5X5", "5 × 5 cm", 5, 5)
    });

    public static KichThuocAnhThe? Lay(string? ma) => DanhSach.FirstOrDefault(x => x.Ma == ma);
}
