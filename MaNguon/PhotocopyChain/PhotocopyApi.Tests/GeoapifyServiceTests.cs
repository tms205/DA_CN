using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyApi.Models;
using Xunit;

namespace PhotocopyApi.Tests;

public class GeoapifyServiceTests
{
    private sealed class BanDoGiaLap : HttpMessageHandler
    {
        public int SoLanGoi;
        public HttpStatusCode TrangThai = HttpStatusCode.OK;
        public string Route = "{\"features\":[{\"properties\":{\"distance\":3500,\"time\":600}}]}";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            SoLanGoi++;
            Assert.Equal("api.geoapify.com", request.RequestUri.Host);
            string json = request.RequestUri.AbsolutePath.Contains("routing") ? Route :
                "{\"features\":[{\"properties\":{\"lat\":10.7,\"lon\":106.7,\"formatted\":\"140 Lê Trọng Tấn, TP Hồ Chí Minh\",\"country_code\":\"vn\"}}]}";
            return Task.FromResult(new HttpResponseMessage(TrangThai) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private static object Tao(IMemoryCache cache, HttpClient http, bool coKey = true)
    {
        Type type = typeof(DonHangController).Assembly.GetType("PhotocopyApi.GiaoHang.GeoapifyService");
        Assert.NotNull(type);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> {
            ["GiaoHang:GeoapifyApiKey"] = coKey ? "fixture-not-real-key" : ""
        }).Build();
        return Activator.CreateInstance(type, config, cache, http);
    }

    private static async Task<KetQuaPhiGiaoHang> Tinh(object service, ChiNhanh cn, decimal? lat = 10.7m, decimal? lon = 106.7m)
    {
        var task = (Task)service.GetType().GetMethod("TinhPhi").Invoke(service,
            new object[] { cn, "140 Lê Trọng Tấn, TP Hồ Chí Minh", lat, lon, CancellationToken.None });
        await task;
        return (KetQuaPhiGiaoHang)task.GetType().GetProperty("Result").GetValue(task);
    }

    private static ChiNhanh ChiNhanh() => new() { MaChiNhanh = 1, ViDo = 10.8m, KinhDo = 106.6m, BanKinhGiao = 10 };

    [Fact]
    public async Task TuyenThuc_QuangDuongPhiVaCacheKhongPhuThuocKeyTrenApp()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new BanDoGiaLap();
        using var http = new HttpClient(handler);
        var service = Tao(cache, http);
        var kq = await Tinh(service, ChiNhanh());
        Assert.Equal(3.5m, kq.KhoangCachKm);
        Assert.Equal(21000m, kq.PhiShip);
        Assert.Equal(600, kq.ThoiGianDuKienGiay);
        Assert.Equal(kq, await Tinh(service, ChiNhanh()));
        Assert.Equal(1, handler.SoLanGoi);
    }

    [Fact]
    public async Task ThieuKey_KhongTuGanQuangDuongHoacPhiGiaLap()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new BanDoGiaLap();
        using var http = new HttpClient(handler);
        var service = Tao(cache, http, false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Tinh(service, ChiNhanh()));
        Assert.Contains("Geoapify", ex.Message);
        Assert.Equal(0, handler.SoLanGoi);
    }

    [Fact]
    public async Task NgoaiBanKinh_KhongLamTronXuốngDeChoGiao()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new BanDoGiaLap { Route = "{\"features\":[{\"properties\":{\"distance\":10000.1,\"time\":600}}]}" };
        using var http = new HttpClient(handler);
        var service = Tao(cache, http);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Tinh(service, ChiNhanh()));
        Assert.Contains("bán kính", ex.Message);
    }

    [Theory]
    [InlineData("{\"features\":[{}]}")]
    [InlineData("{\"features\":[{\"properties\":null}]}")]
    [InlineData("{\"features\":[{\"properties\":{\"distance\":-1,\"time\":1}}]}")]
    [InlineData("{\"features\":[{\"properties\":{\"distance\":\"bad\",\"time\":1}}]}")]
    public async Task DuLieuTuyenSai_TraLoiDeHieuThayViLoi500(string json)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var http = new HttpClient(new BanDoGiaLap { Route = json });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Tinh(Tao(cache, http), ChiNhanh()));
        Assert.Contains("Bản đồ", ex.Message);
        Assert.DoesNotContain("fixture-not-real-key", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task LoiNhaCungCap_KhongLoKeyVaKhongTraPhiBangKhong(HttpStatusCode status)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var http = new HttpClient(new BanDoGiaLap { TrangThai = status });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Tinh(Tao(cache, http), ChiNhanh()));
        Assert.DoesNotContain("fixture-not-real-key", ex.Message);
        Assert.DoesNotContain("http", ex.Message);
    }
}
