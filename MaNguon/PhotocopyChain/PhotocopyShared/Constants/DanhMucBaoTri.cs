namespace PhotocopyShared.Constants;

// Các nhóm chức năng thực có trên từng nền tảng; dùng chung cho cấu hình và kiểm tra API.
public static class DanhMucBaoTri
{
    public static readonly IReadOnlyDictionary<string, string> Web = new Dictionary<string, string>
    {
        ["DANG_NHAP"] = "Đăng nhập / tài khoản", ["DON_HANG"] = "Đơn hàng / in ấn",
        ["FILE_IN"] = "Tải file / mẫu đơn", ["THANH_TOAN"] = "Thanh toán",
        ["GIAO_HANG"] = "Giao hàng nội bộ", ["OCR"] = "Nhận dạng chữ OCR",
        ["QUAN_LY"] = "Quản lý danh mục / nhân sự", ["BAO_CAO"] = "Dashboard / báo cáo"
    };
    public static readonly IReadOnlyDictionary<string, string> App = Web
        .Where(x => x.Key is not ("QUAN_LY" or "BAO_CAO"))
        .ToDictionary(x => x.Key, x => x.Key == "GIAO_HANG" ? "Giao hàng / đối chiếu" : x.Value);
}
