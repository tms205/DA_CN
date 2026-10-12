namespace PhotocopyApi.Models;

// Giống hệt các cột của bảng MayIn trong CSDL.
// Dùng để biết chi nhánh có in được cấu hình khách chọn không (khổ giấy / in màu / in 2 mặt).
public class MayIn
{
    public int MaMayIn { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenMay { get; set; }
    public string KhoGiay { get; set; }       // khổ giấy lớn nhất in được: A4 hoặc A3
    public bool InMau { get; set; }
    public bool In2Mat { get; set; }
    public int TocDo { get; set; }            // trang / phút
    public int SoTrangDaIn { get; set; }
    public string TrangThai { get; set; }     // HOAT_DONG / BAO_TRI / HONG
}
