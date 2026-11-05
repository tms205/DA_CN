using System.Text.RegularExpressions;

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

    // Các cách viết để tìm địa chỉ, từ đúng chữ khách gõ tới gọn nhất:
    //   1. nguyên văn;  2. bỏ phần hẻm "/42" của số nhà, thêm dấu phẩy trước quận/huyện/phường/thành phố…;
    //   3. bỏ hẳn số nhà, chỉ còn tên đường và khu vực.
    // Ví dụ "320/42 Ấp Chiến Lược quận Bình Tân Thành Phố Hồ Chí Minh"
    //   → "320 Ấp Chiến Lược, quận Bình Tân, Thành Phố Hồ Chí Minh" → "Ấp Chiến Lược, quận Bình Tân, Thành Phố Hồ Chí Minh".
    public static List<string> CachVietDiaChi(string diaChi)
    {
        var ds = new List<string>();
        string goc = Regex.Replace((diaChi ?? "").Trim(), @"\s+", " ");
        if (goc.Length == 0) return ds;
        ds.Add(goc);

        string gon = Regex.Replace(goc, @"^(số\s*)?(\d+[a-zA-Z]?)(/[0-9a-zA-Z]+)+", "$2", RegexOptions.IgnoreCase);
        gon = Regex.Replace(gon, @"\s*,?\s+(?=(phường|xã|quận|huyện|thị xã|thị trấn|thành phố|tp\.?|tỉnh)(\s|$))", ", ", RegexOptions.IgnoreCase);
        gon = Regex.Replace(gon, @"\s*,\s*(,\s*)+", ", ").Trim(' ', ',');
        if (!ds.Contains(gon)) ds.Add(gon);

        string khongSoNha = Regex.Replace(gon, @"^(số\s*)?\d+[a-zA-Z]?\s*,?\s*", "", RegexOptions.IgnoreCase).Trim(' ', ',');
        if (khongSoNha.Length >= 5 && !ds.Contains(khongSoNha)) ds.Add(khongSoNha);
        return ds;
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
