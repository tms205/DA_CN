using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class DashboardHoanThienTests
{
    [Theory]
    [InlineData(126000, 50000)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(900000, 250000)]
    public void TrucBieuDo_BuocTronVaBaoPhuGiaTri(decimal max, decimal buoc)
    {
        MethodInfo ham = typeof(TongQuanBaoCao).GetMethod("BuocTruc");
        Assert.NotNull(ham);
        Assert.Equal(buoc, (decimal)ham.Invoke(null, new object[] { max }));
        Assert.True(buoc * 4 >= max);
    }

    [Fact]
    public async Task BieuDo_KyTruocBangKhong_KhongVeDuongGia()
    {
        string html = await BieuDo(0);
        Assert.DoesNotContain("class=\"tq-duong-truoc\"", html);
        Assert.Contains("Kỳ đối chiếu chưa có doanh thu", html);
        Assert.Contains("50.000", html);
        Assert.DoesNotContain(">31.5k<", html);
    }

    [Fact]
    public async Task BieuDoNgay_DungCotVaVanCoKyDoiChieu()
    {
        string html = await BieuDo(10000);
        Assert.Contains("class=\"tq-cot-nay\"", html);
        Assert.Contains("class=\"tq-cot-truoc\"", html);
    }

    [Fact]
    public void ApiBaoCao_MoiCoPhanQuyenCEOQuanLy()
    {
        Type lop = typeof(PhotocopyApi.Controllers.QuanTriController).Assembly.GetType("PhotocopyApi.Controllers.BaoCaoTongQuanController");
        Assert.NotNull(lop);
        var quyen = lop.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.Equal("SUPER_ADMIN,BRANCH_ADMIN", quyen.Roles);
    }

    [Fact]
    public async Task VanHanhRong_KhongCoCanhBaoCamGia()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(new HttpClient(new ApiRong()) { BaseAddress = new Uri("http://test/") });
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var kq = await renderer.RenderComponentAsync<PhotocopyAdmin.Components.TongQuanVanHanh>(ParameterView.Empty);
            return WebUtility.HtmlDecode(kq.ToHtmlString());
        });
        Assert.DoesNotContain("class=\"tq-viec-can-lam\"", html);
        Assert.DoesNotContain("tq-so-vh tq-so-canh-bao", html);
        Assert.Contains("Không có đơn cần ưu tiên", html);
        Assert.Contains("Cập nhật lúc", html);
    }

    private sealed class ApiRong : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") });
    }

    private static async Task<string> BieuDo(decimal truoc)
    {
        var services = new ServiceCollection().AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => {
            var p = ParameterView.FromDictionary(new Dictionary<string, object> {
                ["HienTai"] = new List<BaoCaoDoanhThuDto> { new() { Ngay = DateTime.Today, DoanhThu = 126000 } },
                ["KyTruoc"] = new List<BaoCaoDoanhThuDto> { new() { Ngay = DateTime.Today, DoanhThu = truoc } },
                ["NhomTheo"] = "ngay"
            });
            var result = await renderer.RenderComponentAsync<PhotocopyAdmin.Components.BieuDoDoanhThu>(p);
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }
}
