namespace PhotocopyShared.Dtos.DonHang;

public static class QuyTacThongBaoXuLy
{
    public static string TrangThai(string tieuDe) => tieuDe switch
    {
        "CEO đã xử lý yêu cầu hoàn tiền" or "Phản hồi giao hàng đã được đối chiếu" => "DA_XU_LY",
        "Đã tiếp nhận yêu cầu hoàn tiền" or "Yêu cầu hoàn tiền do sản phẩm" or "Hoàn tiền do in lỗi" or "Hoàn tiền do in lỗi 3 lần" or "Khách gửi bằng chứng đối chiếu" => "CHO_XU_LY",
        _ => ""
    };
}
