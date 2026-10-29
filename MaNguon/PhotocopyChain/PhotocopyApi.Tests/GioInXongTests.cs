using PhotocopyApi.MayInAo;
using PhotocopyPrinterSimulator;
using PhotocopyShared.Dtos.MayInAo;
using Xunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PhotocopyApi.Tests;

public class GioInXongTests
{
    [Fact]
    public async Task Web_HienDuGioPhutGiayVaNgay_CongDuTruDungMotLan()
    {
        var state = new TrangThaiKetNoiMayInDto { TrangThai = "DANG_IN", MaDonHangDangIn = 57,
            LanCuoiPhanHoi = new DateTime(2026, 10, 28, 15, 30, 0), SoGiayConLai = 45 };
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<PhotocopyAdmin.Components.TienDoInDon>(
            ParameterView.FromDictionary(new Dictionary<string, object> { ["TrangThai"] = state }))).ToHtmlString());
        Assert.Contains("15:31:00 28/10/2026", html);
        Assert.Contains("15 gi", System.Net.WebUtility.HtmlDecode(html));
    }

    [Fact]
    public void DuKien_TheoPhanHoiDungMayDungDon_Cong15Giay_KhongTruotTheoMoiLanDoc()
    {
        var clock = new Clock();
        var store = new TrangThaiMayInAoStore(clock);
        store.CapNhat(2, "DANG_IN", 57, baoCao: new() { SoGiayConLai = 60 });
        var expected = clock.GetLocalNow().DateTime.AddSeconds(75);
        Assert.Equal(expected, store.LayDuKienInXong(57, 2));
        clock.Now = clock.Now.AddSeconds(10);
        Assert.Equal(expected, store.LayDuKienInXong(57, 2));
        Assert.Null(store.LayDuKienInXong(58, 2));
        Assert.Null(store.LayDuKienInXong(57, 3));
        Assert.Null(store.LayDuKienInXong(57, null));
        clock.Now = clock.Now.AddSeconds(21);
        Assert.Null(store.LayDuKienInXong(57, 2));
    }

    [Theory]
    [InlineData("SAN_SANG", false, 10)]
    [InlineData("TAM_DUNG", true, 10)]
    [InlineData("HET_GIAY", false, 10)]
    [InlineData("KET_GIAY", false, 10)]
    [InlineData("LOI", false, 10)]
    [InlineData("CHO_XAC_NHAN", false, 10)]
    [InlineData("DANG_IN", true, 10)]
    [InlineData("DANG_IN", false, -1)]
    [InlineData("DANG_IN", false, null)]
    public void KhongBaoGioKhiMayKhongDangChayHoacThieuUocTinh(string state, bool paused, int? remaining)
    {
        var store = new TrangThaiMayInAoStore(new Clock());
        store.CapNhat(1, state, 57, baoCao: new() { SoGiayConLai = remaining, TamDung = paused });
        Assert.Null(store.LayDuKienInXong(57, 1));
    }

    [Fact]
    public void MayThat_DungDuBaoTheoTocDo_KhongBienThoiGianThanhGiayDaIn()
    {
        var now = new DateTime(2026, 10, 28, 15, 30, 0);
        var p = new TienTrinhInDaLuu { CheDo = "THAT", DaBatDau = true, DuKienXong = now.AddSeconds(60) };
        Assert.Equal(60, p.UocTinhSoGiayConLai(now));
        Assert.Equal(50, p.UocTinhSoGiayConLai(now.AddSeconds(10)));
        Assert.Null(p.UocTinhSoGiayConLai(now.AddSeconds(61)));
        Assert.Equal(0, p.SoGiayDaIn);
        p.DaBatDau = false;
        Assert.Null(p.UocTinhSoGiayConLai(now));
    }

    [Fact]
    public void MayThat_TamDungKhongLamTieuHaoThoiGianUocTinh()
    {
        var now = new DateTime(2026, 10, 28, 15, 30, 0);
        var p = new TienTrinhInDaLuu { CheDo = "THAT", DaBatDau = true, DuKienXong = now.AddSeconds(60) };
        Assert.True(p.CapNhatTamDungDuKien(true, now.AddSeconds(10)));
        Assert.False(p.CapNhatTamDungDuKien(true, now.AddSeconds(30)));
        Assert.True(p.CapNhatTamDungDuKien(false, now.AddSeconds(40)));
        Assert.Equal(50, p.UocTinhSoGiayConLai(now.AddSeconds(40)));
        Assert.False(p.CapNhatTamDungDuKien(false, now.AddSeconds(42)));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 28, 8, 30, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
