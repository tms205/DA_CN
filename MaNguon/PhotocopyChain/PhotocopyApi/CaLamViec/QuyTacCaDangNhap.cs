using PhotocopyShared.Constants;

namespace PhotocopyApi.CaLamViec;

public static class QuyTacCaDangNhap
{
    public const decimal TienDauCaMacDinh = 200000m;

    public static bool CanTuDongMoCa(bool laWeb, string vaiTro)
    {
        return laWeb && vaiTro == VaiTro.Staff;
    }
}
