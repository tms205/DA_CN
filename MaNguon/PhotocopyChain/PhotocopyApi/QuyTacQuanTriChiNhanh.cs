using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi;

public static class QuyTacQuanTriChiNhanh
{
    public static string KiemTraDichVu(LuuDichVuRequest req, int? maChiNhanh)
    {
        if (maChiNhanh == null) return "Hãy chọn một chi nhánh trước khi cập nhật bảng giá.";
        if (string.IsNullOrWhiteSpace(req.TenDichVu)) return "Tên dịch vụ không được để trống.";
        if (req.LoaiDichVu != "IN_AN" && req.LoaiDichVu != "GIA_CONG") return "Loại dịch vụ không hợp lệ.";
        if (req.DonGia < 0) return "Đơn giá không được âm.";
        if (req.TrangThai != "HIEN" && req.TrangThai != "AN") return "Trạng thái dịch vụ không hợp lệ.";
        return null;
    }

    public static string KiemTraKhuyenMai(LuuKhuyenMaiRequest req, int? maChiNhanh)
    {
        if (maChiNhanh == null) return "Hãy chọn một chi nhánh trước khi cập nhật khuyến mãi.";
        if (string.IsNullOrWhiteSpace(req.MaCode)) return "Mã khuyến mãi không được để trống.";
        if (req.PhanTramGiam < 1 || req.PhanTramGiam > 100) return "Phần trăm giảm phải từ 1 đến 100.";
        if (req.SoLuong < 0 || req.DonToiThieu < 0 || req.GiamToiDa < 0) return "Giá trị khuyến mãi không hợp lệ.";
        if (req.NgayKetThuc.Date < req.NgayBatDau.Date) return "Ngày kết thúc phải sau ngày bắt đầu.";
        if (req.TrangThai != "HOAT_DONG" && req.TrangThai != "TAM_DUNG") return "Trạng thái khuyến mãi không hợp lệ.";
        return null;
    }
}
