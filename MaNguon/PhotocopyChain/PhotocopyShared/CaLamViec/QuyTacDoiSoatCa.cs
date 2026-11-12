using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyShared.CaLamViec;

public static class QuyTacDoiSoatCa
{
    public static string? KiemTra(CaLamViecDto ca, XacNhanCaRequest req)
    {
        if (req.TienQuanLyDemCuoiCa is not decimal dem || dem < 0)
            return "Vui lòng nhập số tiền quản lý trực tiếp kiểm đếm.";
        if (ca.TongDoanhThuCuoiCa == null)
            return "Ca chưa có số tiền nhân viên tổng kết, chưa thể xác nhận.";
        decimal phaiCo = ca.TienDauCa + ca.DoanhThuTrongCa;
        decimal thieu = Math.Max(0, phaiCo - dem);
        if (req.TienChi < 0 || req.TienBu < 0 || req.TienChi > thieu || req.TienBu != thieu - req.TienChi)
            return "Tiền chi và tiền bù phải bằng đúng số tiền còn thiếu; ca khớp hoặc thừa không được nhập khoản bù.";
        if (ca.TongDoanhThuCuoiCa != phaiCo && string.IsNullOrWhiteSpace(ca.GhiChu))
            return "Ca có chênh lệch nhưng thiếu lý do của người đóng ca. Chưa thể xác nhận.";
        if ((dem != phaiCo || dem != ca.TongDoanhThuCuoiCa) && string.IsNullOrWhiteSpace(req.GhiChu))
            return "Vui lòng ghi kết quả đối soát và lý do chênh lệch.";
        return null;
    }
}
