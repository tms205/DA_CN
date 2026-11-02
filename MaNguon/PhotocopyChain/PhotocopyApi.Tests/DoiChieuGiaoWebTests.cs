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

public class DoiChieuGiaoWebTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhanHoiKhach_DungLuot_AnToanHtml_KetLuanChiDoc(bool done)
    {
        var services = new ServiceCollection(); services.AddLogging();
        using var http = new HttpClient(new Fixture(done)) { BaseAddress = new Uri("http://fixture.invalid/") }; services.AddSingleton(http);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string raw = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<DoiChieuKhachHang>(
            ParameterView.FromDictionary(new Dictionary<string, object> { { "MaGiaoHang", 77 } }))).ToHtmlString());
        string html = WebUtility.HtmlDecode(raw);
        Assert.Contains("DH00015", html); Assert.Contains("Khách <script>alert(1)</script>", html); Assert.DoesNotContain("<script>", raw);
        Assert.Contains("Chưa truy cập được ảnh", html);
        Assert.Contains(done ? "Kết luận thử" : "Ghi kết luận xử lý", html);
        Assert.Equal(!done, raw.Contains("<textarea")); Assert.DoesNotContain("Bearer", html);
    }
    private sealed class Fixture(bool done) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri.AbsolutePath.Contains("/anh/")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            Assert.Equal("/api/giao-hang/luot/77/doi-chieu", request.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LuotDoiChieuGiaoDto
                {
                    MaGiaoHang = 77,
                    MaDonHang = 15,
                    LanGiao = 2,
                    PhanHoi = new()
                    {
                        MaKhachHang = 5,
                        NoiDung = "Khách <script>alert(1)</script>",
                        GuiLuc = DateTimeOffset.UtcNow,
                        TrangThai = done ? "DA_XU_LY" : "CHO_DOI_CHIEU",
                        KetLuan = done ? "Kết luận thử" : null,
                        TenNguoiXuLy = "Quản lý thử",
                        XuLyLuc = DateTimeOffset.UtcNow,
                        Anh = new() { new() { MaAnh = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", LoaiNoiDung = "image/png" } }
                    }
                })
            });
        }
    }
}
