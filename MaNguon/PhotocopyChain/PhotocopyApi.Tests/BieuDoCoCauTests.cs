using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin.Components;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class BieuDoCoCauTests
{
    [Fact]
    public async Task BaPhanBangNhau_LamTronVanDu100PhanTram()
    {
        var html = await Render(new() { new() { Ten = "In màu", SoTien = 100 }, new() { Ten = "In đen trắng", SoTien = 100 }, new() { Ten = "Gia công", SoTien = 100 } });
        var pct = Regex.Matches(html, "data-phan-tram=\"([0-9.]+)\"").Select(x => decimal.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(3, pct.Count);
        Assert.Equal(100, pct.Sum());
        Assert.Contains(33.4m, pct);
        Assert.DoesNotContain("NaN", html);
        Assert.Contains("300₫", html);
    }

    [Fact]
    public async Task MotDichVu_DuVong100PhanTram_KhongVeDichVuBang0()
    {
        var html = await Render(new() { new() { Ten = "In màu", SoTien = 15000 }, new() { Ten = "Không phát sinh", SoTien = 0 } });
        Assert.Single(Regex.Matches(html, "data-phan-tram="));
        Assert.Contains("data-phan-tram=\"100\"", html);
        Assert.DoesNotContain("Không phát sinh", html);
        Assert.Contains("15.000₫", html);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SoLieuKhongVeDuoc_KhongGiaVong100PhanTram(decimal amount)
    {
        var html = await Render(new() { new() { Ten = "Dịch vụ thử", SoTien = amount } });
        Assert.DoesNotContain("<svg", html);
        Assert.DoesNotContain("100%", html);
        Assert.Contains(amount < 0 ? "giá trị dịch vụ âm" : "Chưa có dữ liệu", html);
    }

    private static async Task<string> Render(List<CoCauDoanhThuDto> data)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode((await renderer.RenderComponentAsync<BieuDoCoCauDichVu>(
            ParameterView.FromDictionary(new Dictionary<string, object> { ["DuLieu"] = data }))).ToHtmlString()));
    }
}
