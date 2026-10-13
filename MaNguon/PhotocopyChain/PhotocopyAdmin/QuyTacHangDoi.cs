using PhotocopyShared.Dtos.DonHang;

namespace PhotocopyAdmin;

// Nhãn hiển thị, không thay đổi trạng thái hoặc quyền thao tác của đơn.
public static class QuyTacHangDoi
{
    public static string NhanCho(HangDoiDonDto don)
    {
        if (don.TrangThaiDonHang != "CHO_XAC_NHAN") return "";
        if (don.PhuongThucThanhToan != "VIETQR") return "Cần tiếp nhận";
        return don.TrangThaiThanhToan == "DA_THANH_TOAN"
            ? "Chờ hệ thống tiếp nhận" : "Chờ thanh toán QR";
    }

    public static string LopCho(HangDoiDonDto don)
    {
        if (don.TrangThaiDonHang != "CHO_XAC_NHAN") return "";
        if (don.PhuongThucThanhToan != "VIETQR") return "hd-cho-tiep-nhan";
        return don.TrangThaiThanhToan == "DA_THANH_TOAN"
            ? "hd-cho-tu-dong" : "hd-cho-thanh-toan";
    }
}
