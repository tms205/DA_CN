namespace PhotocopyApi.Models;

// Giống hệt các cột của bảng ChiNhanh trong CSDL.
public class ChiNhanh
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; }
    public string DiaChi { get; set; }
    public string SoDienThoai { get; set; }
    public decimal? ViDo { get; set; }        // tọa độ trên bản đồ, dùng tính quãng đường giao hàng
    public decimal? KinhDo { get; set; }
    public TimeSpan GioMoCua { get; set; }
    public TimeSpan GioDongCua { get; set; }
    public decimal BanKinhGiao { get; set; }  // km
    public string TrangThai { get; set; }     // HOAT_DONG / KHOA
}
