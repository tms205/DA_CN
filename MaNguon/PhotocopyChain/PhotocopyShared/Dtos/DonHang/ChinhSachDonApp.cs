namespace PhotocopyShared.Dtos.DonHang;

public static class ChinhSachDonApp
{
    public const int SoGiayCho = 120;
    public const string ThongBao = "Bạn chỉ có thể hủy và yêu cầu hoàn tiền khi đơn chưa được tiếp nhận và còn trong 2 phút sau thanh toán. Sau thời hạn này không hỗ trợ hủy/hoàn theo luồng thông thường. Nếu sản phẩm đã nhận bị lỗi hoặc không hài lòng, hãy gửi bằng chứng tại chi tiết đơn để CEO xem xét.";

    public static bool ChoHuy(string kenh, string trangThai, string thanhToan, DateTime? ngayTra, DateTime? ngayHen, DateTime now)
        => trangThai == "CHO_XAC_NHAN" && (thanhToan == "CHUA_THANH_TOAN" || (kenh == "APP" && thanhToan == "DA_THANH_TOAN"
            ? ngayTra.HasValue && now < ngayTra.Value.AddSeconds(SoGiayCho)
            : thanhToan != "DA_HOAN_TIEN" && (!ngayHen.HasValue || ngayHen > now.AddMinutes(15))));

    // Alias dh/tt dùng chung cho chi tiết, bằng chứng và lệnh hủy. Đồng hồ database là thẩm quyền.
    public const string SqlChoHuy = @"CAST(CASE WHEN dh.TrangThai='CHO_XAC_NHAN' AND (tt.TrangThai='CHUA_THANH_TOAN' OR 
        (dh.KenhDatDon='APP' AND tt.TrangThai='DA_THANH_TOAN' AND tt.NgayThanhToan IS NOT NULL AND GETDATE()<DATEADD(SECOND,120,tt.NgayThanhToan))
        OR ((dh.KenhDatDon<>'APP' OR tt.TrangThai<>'DA_THANH_TOAN') AND tt.TrangThai<>'DA_HOAN_TIEN'
            AND (dh.NgayHenTra IS NULL OR dh.NgayHenTra>DATEADD(MINUTE,15,GETDATE())))) THEN 1 ELSE 0 END AS BIT)";
}
