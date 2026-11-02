using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin.Components;
using PhotocopyShared.Dtos.GiaoHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class BangChungGiaoWebTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Popup_BangChungTheoLuot_KhongChayHtmlKhachNhap_VaXuLyAnhMat(bool coAnh)
    {
        var services = new ServiceCollection(); services.AddLogging();
        using var http = new HttpClient(new MayChuBangChung(coAnh)) { BaseAddress=new Uri("http://fixture.invalid/") };
        services.AddSingleton(http);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string raw = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<BangChungGiaoHang>(
            ParameterView.FromDictionary(new Dictionary<string,object> { ["MaGiaoHang"] = 77 }))).ToHtmlString());
        string html = WebUtility.HtmlDecode(raw);
        Assert.Contains("DH00015",html); Assert.Contains("Lượt 2/3",html);
        Assert.Contains("Khách <script>alert(1)</script>",html);
        Assert.DoesNotContain("<script>",raw);
        Assert.Contains("Sai số khoảng",html);
        Assert.Contains("query=",html);
        Assert.Contains(coAnh ? "Chưa tải được ảnh" : "Lượt giao cũ chưa có ảnh",html);
        Assert.DoesNotContain("Bearer",html);
    }

    private sealed class MayChuBangChung(bool coAnh) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri.AbsolutePath.Contains("/anh/"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            Assert.Equal("/api/giao-hang/luot/77/bang-chung",request.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new BangChungGiaoDto {
                MaGiaoHang=77,MaDonHang=15,LanGiao=2,TenShipper="Shipper thử",LyDo="Khách <script>alert(1)</script>",
                ViDo=10.7m,KinhDo=106.7m,DoChinhXac=12,ThoiDiemGps=DateTimeOffset.UtcNow,ThoiDiemBaoCao=DateTimeOffset.UtcNow,
                Anh=coAnh ? new() { new() { MaAnh=Guid.NewGuid().ToString("N"),LoaiNoiDung="image/png" } } : new()
            }) });
        }
    }
}
