using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyShared.Dtos.Tuan3;
using PhotocopyShared.KiemTraDuLieu;
using Xunit;

namespace PhotocopyApi.Tests;

public class LapDonTaiQuayTests
{
    [Theory]
    [InlineData("Nguyễn Văn An", "")]
    [InlineData("Nguyễn Văn An", null)]
    [InlineData("", "0912345678")]
    [InlineData(null, "0912345678")]
    [InlineData("   ", "0912345678")]
    [InlineData("Nguyễn Văn An", "0912345678")]
    public void Khach_CoTenHoacSdt_LaHopLe(string ten, string sdt)
    {
        Assert.Null(QuyTacNhap.LayLoi(new KhachTaiQuayRequest { HoTen = ten, SoDienThoai = sdt }));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("  ", "")]
    [InlineData("  ", "  ")]
    public void Khach_ThieuCaHai_ThongBaoCanMotTrongHai(string ten, string sdt)
    {
        Assert.Equal("Vui lòng nhập họ tên hoặc số điện thoại khách hàng.",
            QuyTacNhap.LayLoi(new KhachTaiQuayRequest { HoTen = ten, SoDienThoai = sdt }));
    }

    [Theory]
    [InlineData("Nguyễn Văn An", "091234567")]
    [InlineData("", "09123456789")]
    [InlineData("Nguyễn Văn An", "1234567890")]
    [InlineData("Nguyễn Văn An", "0١٢٣٤٥٦٧٨٩")]
    [InlineData("A", "")]
    public void Khach_NhapThemThongTinSai_VanBiChan(string ten, string sdt)
    {
        Assert.NotNull(QuyTacNhap.LayLoi(new KhachTaiQuayRequest { HoTen = ten, SoDienThoai = sdt }));
    }

    [Theory]
    [InlineData("Nguyễn Văn An", "")]
    [InlineData("", "0912345678")]
    public async Task Controller_ChoPhepThieuMotTruong_VaDiToiBuocDatabase(string ten, string sdt)
    {
        // DB không cấu hình: nếu request hợp lệ phải đi tới DB, không trả lỗi bắt buộc cả hai.
        var controller = new DonHangController(new ConfigurationBuilder().Build(), null, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.ChonKhachTaiQuay(
            new KhachTaiQuayRequest { HoTen = ten, SoDienThoai = sdt }));
    }

    [Fact]
    public async Task Controller_ThieuCaHai_ChanTruocDatabase()
    {
        var controller = new DonHangController(new ConfigurationBuilder().Build(), null, null);
        var kq = Assert.IsType<BadRequestObjectResult>(await controller.ChonKhachTaiQuay(new()));
        Assert.Contains("họ tên hoặc số điện thoại", kq.Value.ToString());
    }
}
