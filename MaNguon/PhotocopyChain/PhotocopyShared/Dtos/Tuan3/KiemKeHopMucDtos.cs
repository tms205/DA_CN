using System.ComponentModel.DataAnnotations;

namespace PhotocopyShared.Dtos.Tuan3;

// ===================================================================== KIỂM KÊ KHO (28/11, CSDL 034)

public sealed class PhieuKiemKeDto
{
    public int MaPhieuKiemKe { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public int MaVatTu { get; set; }
    public string TenVatTu { get; set; } = "";
    public string? DonViTinh { get; set; }
    public decimal DonGia { get; set; }             // giá nhập tham khảo của vật tư, để quy hao hụt ra tiền
    public int TonTheoSo { get; set; }
    public int SoLuongThucDem { get; set; }
    public int ChenhLech { get; set; }
    public string? LyDo { get; set; }
    public string? TenNguoiLap { get; set; }
    public string? TenNguoiDuyet { get; set; }
    public string TrangThai { get; set; } = "";
    public DateTime NgayLap { get; set; }
    public DateTime? NgayDuyet { get; set; }
}

public sealed class TaoPhieuKiemKeRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn vật tư.")]
    public int MaVatTu { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Số đếm thực tế không được âm.")]
    public int SoLuongThucDem { get; set; }
    [MaxLength(255, ErrorMessage = "Lý do tối đa 255 ký tự.")]
    public string? LyDo { get; set; }
}

public sealed class DuyetPhieuKiemKeRequest
{
    public bool DongY { get; set; }
}

public static class QuyTacKiemKe
{
    // Chênh lệch = số đếm thực tế - tồn theo sổ. Âm là hao hụt, dương là thừa so với sổ.
    public static int ChenhLech(int tonTheoSo, int soDem) => soDem - tonTheoSo;

    // Lệch thì bắt ghi lý do (giống đóng ca lệch két).
    public static string? LoiLyDo(int chenhLech, string? lyDo) =>
        chenhLech != 0 && (lyDo ?? "").Trim().Length < 5
            ? "Số đếm lệch với tồn trên máy. Vui lòng ghi lý do (ít nhất 5 ký tự), ví dụ: kẹt giấy, ẩm hỏng."
            : null;

    // Lúc duyệt: CỘNG chênh lệch vào tồn HIỆN TẠI, không ghi đè bằng số đã đếm,
    // vì từ lúc đếm tới lúc duyệt vẫn có đơn in làm tồn giảm.
    // Ví dụ: đếm 500, sổ 520 (lệch -20); sau đó in 100 tờ, sổ còn 420 → sau duyệt 420 - 20 = 400.
    public static int TonSauDuyet(int tonHienTai, int chenhLech) => tonHienTai + chenhLech;

    // Tiền hao hụt = phần thiếu × giá nhập tham khảo (phần thừa không tính là hao hụt).
    public static decimal TienHaoHut(int chenhLech, decimal donGia) => chenhLech < 0 ? -chenhLech * donGia : 0;
}

// ===================================================================== HỘP MỰC MÁY IN (28/11, CSDL 035)

public sealed class HopMucMayInDto
{
    public int MaMayIn { get; set; }
    public string TenMay { get; set; } = "";
    public int MaChiNhanh { get; set; }
    public int? MaVatTuMuc { get; set; }
    public string? TenVatTuMuc { get; set; }
    public int NangSuatHopMuc { get; set; }
    public int SoTrangDaIn { get; set; }
    public int SoTrangLucThayMuc { get; set; }
    public int TonMucTrongKho { get; set; }          // số hộp mực loại này còn trong kho chi nhánh
    public int SoTrangHopHienTai => Math.Max(0, SoTrangDaIn - SoTrangLucThayMuc);
    public int PhanTramDaDung => QuyTacHopMuc.PhanTramDaDung(SoTrangHopHienTai, NangSuatHopMuc);
    public bool SapHet => QuyTacHopMuc.SapHet(SoTrangHopHienTai, NangSuatHopMuc);
    public List<LanThayMucDto> LichSu { get; set; } = new();
    public List<VatTuChonDto> DsVatTu { get; set; } = new();   // để quản lý chọn loại mực khi cấu hình
}

public sealed class LanThayMucDto
{
    public DateTime ThoiGian { get; set; }
    public string TenVatTu { get; set; } = "";
    public string TenNguoiThay { get; set; } = "";
    public int SoTrangHopCu { get; set; }
    public int NangSuatHopMuc { get; set; }
    public bool NghiVan { get; set; }
}

public sealed class VatTuChonDto
{
    public int MaVatTu { get; set; }
    public string TenVatTu { get; set; } = "";
    public string? DonViTinh { get; set; }
}

public sealed class CauHinhHopMucRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại hộp mực trong kho.")]
    public int MaVatTuMuc { get; set; }
    [Range(QuyTacHopMuc.NangSuatToiThieu, QuyTacHopMuc.NangSuatToiDa, ErrorMessage = "Năng suất hộp mực phải từ 100 đến 1.000.000 trang.")]
    public int NangSuatHopMuc { get; set; } = 2000;
}

public static class QuyTacHopMuc
{
    public const int NangSuatToiThieu = 100;
    public const int NangSuatToiDa = 1_000_000;
    public const int PhanTramCanhBao = 90;    // in được 90% năng suất thì báo sắp hết mực
    public const int PhanTramNghiVan = 50;    // hộp cũ hết dưới 50% năng suất thì đánh dấu nghi vấn

    public static int PhanTramDaDung(int soTrang, int nangSuat) =>
        nangSuat <= 0 ? 0 : (int)Math.Min(999, (long)soTrang * 100 / nangSuat);

    public static bool SapHet(int soTrang, int nangSuat) => PhanTramDaDung(soTrang, nangSuat) >= PhanTramCanhBao;

    // Hộp cũ mới in được chưa tới một nửa năng suất đã phải thay → có thể hộp bị lấy ra ngoài hoặc in ngoài hệ thống.
    public static bool NghiVan(int soTrangHopCu, int nangSuat) => PhanTramDaDung(soTrangHopCu, nangSuat) < PhanTramNghiVan;
}
