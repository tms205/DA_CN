using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.GiaoHang;

public static class QuyTacDoiChieu
{
    public static void KiemTraNoiDung(string text, int min = 5)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < min || text.Trim().Length > 1000)
            throw new ArgumentException($"Nội dung cần từ {min} đến 1.000 ký tự.");
    }

    public static PhanHoiGiaoHangDto Tao(int khach, string text, List<AnhBangChungDto> anh, DateTimeOffset now)
    {
        KiemTraNoiDung(text);
        if (khach <= 0 || anh == null || anh.Count is < 1 or > 3) throw new ArgumentException("Cần 1–3 ảnh nhật ký cuộc gọi.");
        return new() { MaKhachHang = khach, NoiDung = text.Trim(), Anh = anh, GuiLuc = now, TrangThai = "CHO_DOI_CHIEU" };
    }

    public static void XuLy(PhanHoiGiaoHangDto report, string ketLuan, int id, string ten, DateTimeOffset now)
    {
        KiemTraNoiDung(ketLuan, 10);
        if (report == null || report.TrangThai != "CHO_DOI_CHIEU") throw new InvalidOperationException("Phản hồi không còn chờ đối chiếu.");
        if (id <= 0) throw new ArgumentException("Người xử lý không hợp lệ.");
        report.TrangThai = "DA_XU_LY"; report.KetLuan = ketLuan.Trim(); report.MaNguoiXuLy = id;
        report.TenNguoiXuLy = ten; report.XuLyLuc = now;
    }
}
