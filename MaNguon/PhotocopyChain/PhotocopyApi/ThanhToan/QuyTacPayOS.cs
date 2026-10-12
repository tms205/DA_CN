namespace PhotocopyApi.ThanhToan;

// Các quy tắc nhỏ của payOS được tách riêng để dễ đọc và kiểm thử.
// Lớp này không gọi mạng, không đọc CSDL và không chứa khóa bí mật.
public static class QuyTacPayOS
{
    // Nội dung chuyển khoản phải ngắn, không dấu và nhận ra được mã đơn.
    public static string TaoMoTaDon(int maDonHang)
    {
        if (maDonHang <= 0)
            throw new ArgumentOutOfRangeException(nameof(maDonHang));

        return "DH" + maDonHang.ToString("D5");
    }

    // Chỉ chấp nhận địa chỉ HTTPS công khai để payOS có thể chuyển hướng về an toàn.
    public static string ChuanHoaDiaChiGoc(string diaChi)
    {
        if (!Uri.TryCreate((diaChi ?? "").Trim(), UriKind.Absolute, out Uri uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host))
            return null;

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    // API lấy link cũ chỉ trả paymentLinkId; URL checkout của payOS có dạng cố định này.
    public static string TaoCheckoutUrl(string paymentLinkId)
    {
        string maLink = (paymentLinkId ?? "").Trim();
        if (maLink == "") return null;

        return "https://pay.payos.vn/web/" + Uri.EscapeDataString(maLink);
    }

    // Trình duyệt thanh toán của quầy quay thẳng về trang chủ Web Admin.
    // Không đi qua Quick Tunnel vì URL tunnel có thể hết hiệu lực sau khi tạo link.
    public static string TaoDiaChiTrangChuWeb(string diaChiWeb)
    {
        if (!Uri.TryCreate((diaChiWeb ?? "").Trim(), UriKind.Absolute, out Uri uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/";
    }

    public static bool SoTienKhop(decimal soTienCanTra, long soTienThucNhan)
    {
        return soTienCanTra == soTienThucNhan;
    }

    // payOS có thể trả HTTP 200 kèm mã nghiệp vụ 101 khi orderCode chưa có link.
    public static bool ChuaCoLienKet(int? statusCode, string errorCode)
    {
        return statusCode == 404 || errorCode == "101";
    }
}
