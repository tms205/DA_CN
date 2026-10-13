namespace PhotocopyShared.QuyCachIn;

public static class QuyTacIn
{
    public const int SoToKimGocToiDa = 50;

    public static int SoToMoiBan(int soTrang, string matIn)
        => matIn == "2_MAT" ? soTrang / 2 + soTrang % 2 : soTrang;

    public static bool HuongGiayHopLe(string? huongGiay)
        => huongGiay is "DUNG" or "NGANG";

    public static bool LaKimGoc(string ten)
        => string.Equals(ten.Trim(), "Bấm kim góc", StringComparison.OrdinalIgnoreCase);

    private static bool LaLoXo(string ten)
        => string.Equals(ten.Trim(), "Đóng lò xo", StringComparison.OrdinalIgnoreCase);

    private static bool LaBiaKieng(string ten)
        => string.Equals(ten.Trim(), "Đóng bìa kiếng", StringComparison.OrdinalIgnoreCase);

    public static bool LoaiTruNhau(string tenA, string? nhomA, string tenB, string? nhomB)
    {
        // Bìa bảo vệ và lò xo là hai công đoạn có thể đi kèm, kể cả dữ liệu cũ cùng nhóm.
        if ((LaBiaKieng(tenA) && LaLoXo(tenB)) || (LaLoXo(tenA) && LaBiaKieng(tenB))) return false;
        if (!string.IsNullOrWhiteSpace(nhomA) &&
            string.Equals(nhomA, nhomB, StringComparison.OrdinalIgnoreCase)) return true;

        return (LaLoXo(tenA) && LaKimGoc(tenB)) || (LaKimGoc(tenA) && LaLoXo(tenB));
    }
}
