using PhotocopyShared.Constants;

namespace PhotocopyApi.CaLamViec;

public static class QuyTacCaDangNhap
{
    public const decimal TienDauCaMacDinh = 200000m;

    public static bool CanTuDongMoCa(bool laWeb, string vaiTro)
    {
        // CEO mở ca thủ công; chỉ nhân viên và quản lý được tự mở khi đăng nhập Web.
        return laWeb && (vaiTro == VaiTro.Staff || vaiTro == VaiTro.BranchAdmin);
    }
}
