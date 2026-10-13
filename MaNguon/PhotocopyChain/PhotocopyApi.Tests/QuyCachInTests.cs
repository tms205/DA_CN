using PhotocopyShared.Dtos.DatIn;
using PhotocopyShared.QuyCachIn;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuyCachInTests
{
    [Theory]
    [InlineData(50, "1_MAT", 50)]
    [InlineData(51, "1_MAT", 51)]
    [InlineData(100, "2_MAT", 50)]
    [InlineData(101, "2_MAT", 51)]
    [InlineData(1, "2_MAT", 1)]
    public void SoToMoiBan_TinhTheoMatIn(int trang, string mat, int to)
        => Assert.Equal(to, QuyTacIn.SoToMoiBan(trang, mat));

    [Theory]
    [InlineData("Đóng lò xo", "DONG_GAY", "Ép plastic", null, false)]
    [InlineData("Đóng lò xo", "DONG_GAY", "Bấm kim góc", null, true)]
    [InlineData("Bấm kim góc", null, "Đóng lò xo", "DONG_GAY", true)]
    [InlineData("Đóng lò xo", "DONG_GAY", "Đóng bìa kiếng", "DONG_GAY", false)]
    [InlineData("Đóng bìa kiếng", "DONG_GAY", "Đóng lò xo", "DONG_GAY", false)]
    [InlineData(" ĐÓNG BÌA KIẾNG ", "DONG_GAY", "đóng lò xo", "DONG_GAY", false)]
    [InlineData("Kiểu đóng gáy A", "DONG_GAY", "Kiểu đóng gáy B", "DONG_GAY", true)]
    [InlineData("Đóng bìa kiếng", "DONG_GAY", "Bấm kim góc", null, false)]
    public void GiaCong_BiaKiengDiCungLoXo_NhungVanChanKimGocVaNhomKhac(string tenA, string nhomA,
        string tenB, string nhomB, bool loaiTru)
        => Assert.Equal(loaiTru, QuyTacIn.LoaiTruNhau(tenA, nhomA, tenB, nhomB));

    [Theory]
    [InlineData("DUNG", true)]
    [InlineData("NGANG", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("TUY_Y", false)]
    [InlineData("DUNG ", false)]
    public void HuongGiay_ChiNhanHaiGiaTri(string huong, bool hopLe)
        => Assert.Equal(hopLe, QuyTacIn.HuongGiayHopLe(huong));

    [Fact]
    public void ClientCu_KhongGuiHuongGiay_ThiMacDinhGiayDung()
    {
        var dong = System.Text.Json.JsonSerializer.Deserialize<DongInRequest>("{\"MaFileIn\":1}");
        Assert.Equal("DUNG", dong.HuongGiay);
    }

    [Theory]
    [InlineData("TUY_Y")]
    [InlineData("DUNG ")]
    [InlineData(null)]
    public async Task API_ChanHuongSaiTruocKhiTruyCapCSDL(string huong)
    {
        var context = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(TenClaim.MaNguoiDung, "1"), new Claim(TenClaim.MaChiNhanh, "1"),
                new Claim(TenClaim.VaiTro, "CUSTOMER")
            }, "KiemThu"))
        }};
        var config = new ConfigurationBuilder().Build(); // Không cấu hình DB: phải bị chặn trước SQL.
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var baoGia = new BaoGiaController(config, cache) { ControllerContext = context };
        var don = new DonHangController(config, cache, null) { ControllerContext = context };
        var dong = new List<DongInRequest> { new() { MaFileIn = 1, HuongGiay = huong } };
        Assert.IsType<BadRequestObjectResult>(await baoGia.TinhGia(new BaoGiaRequest { FileIn = dong }, null));
        Assert.IsType<BadRequestObjectResult>(await don.DatHang(new DatDonHangRequest { FileIn = dong }));
        Assert.IsType<BadRequestObjectResult>(await don.DatDonTaiQuay(new DatDonTaiQuayRequest { FileIn = dong, PhuongThucThanhToan = "COD" }));
    }
}
