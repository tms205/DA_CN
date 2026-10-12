namespace PhotocopyShared.Constants;

/// <summary>Trạng thái đơn — trùng CHECK của cột <c>DonHang.TrangThai</c>.</summary>
public static class TrangThaiDonHang
{
    public const string ChoXacNhan = "CHO_XAC_NHAN";
    public const string DaXacNhan = "DA_XAC_NHAN";
    public const string DangIn = "DANG_IN";
    public const string DaIn = "DA_IN";
    public const string DangGiao = "DANG_GIAO";
    public const string DaGiao = "DA_GIAO";
    public const string GiaoThatBai = "GIAO_THAT_BAI";
    public const string HoanThanh = "HOAN_THANH";
    public const string DaHuy = "DA_HUY";

    public static string TenHienThi(string trangThai) => trangThai switch
    {
        ChoXacNhan => "Chờ tiếp nhận",
        DaXacNhan => "Đã tiếp nhận",
        DangIn => "Đang in ấn",
        DaIn => "Đã in xong",
        DangGiao => "Đang giao",
        DaGiao => "Đã giao",
        GiaoThatBai => "Giao thất bại",
        HoanThanh => "Hoàn tất",
        DaHuy => "Đã hủy",
        _ => trangThai
    };
}

/// <summary>Trùng CHECK của cột <c>DonHang.HinhThucNhan</c>.</summary>
public static class HinhThucNhan
{
    public const string TaiQuay = "TAI_QUAY";
    public const string GiaoHang = "GIAO_HANG";
}
