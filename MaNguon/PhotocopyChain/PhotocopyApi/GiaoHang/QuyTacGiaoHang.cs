namespace PhotocopyApi.GiaoHang;

public static class QuyTacGiaoHang
{
    // Bảng phí của cửa hàng tham khảo ngày 13/10/2026; không phải cước hãng ngoài.
    public static decimal TinhPhi(decimal km, IConfiguration config)
    {
        decimal coBan = config.GetValue<decimal?>("GiaoHang:PhiCoBan") ?? 15000;
        decimal kmCoBan = config.GetValue<decimal?>("GiaoHang:KmCoBan") ?? 2;
        decimal moiKm = config.GetValue<decimal?>("GiaoHang:PhiMoiKm") ?? 4000;
        decimal lamTron = config.GetValue<decimal?>("GiaoHang:LamTronDen") ?? 1000;
        if (km < 0 || coBan <= 0 || kmCoBan < 0 || moiKm <= 0 || lamTron <= 0)
            throw new InvalidOperationException("Cấu hình phí giao hàng không hợp lệ.");
        return Math.Ceiling((coBan + Math.Max(0, km - kmCoBan) * moiKm) / lamTron) * lamTron;
    }

    public static string LoiNhanDon(string trangThai, string thanhToan, int soLan)
    {
        if (thanhToan != "DA_THANH_TOAN") return "Đơn phải được thanh toán đầy đủ trước khi giao.";
        if (trangThai != "DA_IN" && trangThai != "GIAO_THAT_BAI") return "Đơn chưa sẵn sàng để giao hoặc đã có người nhận.";
        if (soLan >= 3) return "Đã giao thất bại 3 lần. Cửa hàng cần liên hệ khách để xử lý.";
        return null;
    }

    public static string GhepDiaChi(string diaChi, string chiTiet, string ghiChu)
    {
        string ketQua = (diaChi ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(chiTiet)) ketQua += " · Chi tiết: " + chiTiet.Trim();
        if (!string.IsNullOrWhiteSpace(ghiChu)) ketQua += " · Ghi chú giao: " + ghiChu.Trim();
        if (ketQua.Length < 10 || ketQua.Length > 255)
            throw new InvalidOperationException("Địa chỉ giao cần đầy đủ và tổng địa chỉ, chi tiết, ghi chú tối đa 255 ký tự.");
        return ketQua;
    }
}
