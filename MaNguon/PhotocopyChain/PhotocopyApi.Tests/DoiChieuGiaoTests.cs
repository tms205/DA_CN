using PhotocopyApi.GiaoHang;
using PhotocopyShared.Dtos.GiaoHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class DoiChieuGiaoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("1234")]
    public void PhanHoi_PhaiCoNoiDungVaAnh(string text)
    {
        Assert.Throws<ArgumentException>(() => QuyTacDoiChieu.Tao(1, text, new() { new() }, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => QuyTacDoiChieu.Tao(1, "Nội dung hợp lệ", new(), DateTimeOffset.UtcNow));
    }
    [Fact]
    public void XuLy_GiuNguyenBangChungVaNguoiGui_KhongChoGhiDeKetLuan()
    {
        var time = DateTimeOffset.UtcNow; var anh = new AnhBangChungDto { MaAnh = Guid.NewGuid().ToString("N"), Sha256 = "test" };
        var report = QuyTacDoiChieu.Tao(3, "  Tôi không nhận được cuộc gọi  ", new() { anh }, time);
        Assert.Equal("CHO_DOI_CHIEU", report.TrangThai); Assert.Null(report.KetLuan);
        Assert.Throws<ArgumentException>(() => QuyTacDoiChieu.XuLy(report, "ngắn", 7, "Quản lý", time));
        QuyTacDoiChieu.XuLy(report, "Đã liên hệ khách để thống nhất giao lại.", 7, "Quản lý", time.AddMinutes(2));
        Assert.Equal("DA_XU_LY", report.TrangThai); Assert.Equal(7, report.MaNguoiXuLy);
        Assert.Equal(3, report.MaKhachHang); Assert.Equal(time, report.GuiLuc); Assert.Same(anh, report.Anh.Single());
        Assert.Equal("Tôi không nhận được cuộc gọi", report.NoiDung);
        Assert.Throws<InvalidOperationException>(() => QuyTacDoiChieu.XuLy(report, "Muốn ghi đè kết luận trước.", 8, "Người khác", time));
        Assert.Equal(7, report.MaNguoiXuLy);
    }
    [Fact]
    public void NoiDungQuaDai_KhongDuocNhan() => Assert.Throws<ArgumentException>(() => QuyTacDoiChieu.KiemTraNoiDung(new string('a', 1001)));
}
