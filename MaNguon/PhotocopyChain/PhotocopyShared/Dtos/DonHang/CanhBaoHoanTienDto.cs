namespace PhotocopyShared.Dtos.DonHang;

// Chỉ thông tin vận hành, không chứa ngân hàng/ảnh. Quyết định hoàn vẫn dành riêng CEO.
public sealed class CanhBaoHoanTienDto
{
    public int MaDonHang { get; set; }
    public string TenKhachHang { get; set; } = "";
    public string TenChiNhanh { get; set; } = "";
    public decimal SoTien { get; set; }
    public string Loai { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public DateTimeOffset GuiLuc { get; set; }
}
