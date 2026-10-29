using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin.Pages;
using Xunit;
namespace PhotocopyApi.Tests;
public class BaoTriWebTests
{
    [Fact]
    public async Task KhongGiaTrangThaiHoatDongKhiKhongTaiDuoc()
    {
        using var http = new HttpClient(new ErrorHandler()) { BaseAddress = new Uri("http://test.local/") };
        var services = new ServiceCollection().AddLogging().AddSingleton(http).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<PhotocopyAdmin.Pages.BaoTri>()).ToHtmlString());
        Assert.DoesNotContain("Hệ thống đang hoạt động", WebUtility.HtmlDecode(html));
        Assert.Contains("Không lấy được trạng thái bảo trì", WebUtility.HtmlDecode(html));
    }
    [Theory]
    [InlineData(false, "Đang hoạt động", "Bật bảo trì")]
    [InlineData(true, "Đang bảo trì", "Tắt bảo trì")]
    public async Task HienDungTrangThaiVaNutThaoTac(bool active, string status, string action)
    {
        using var http = new HttpClient(new StateHandler(active)) { BaseAddress = new Uri("http://test.local/") };
        var services = new ServiceCollection().AddLogging().AddSingleton(http).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<PhotocopyAdmin.Pages.BaoTri>()).ToHtmlString()));
        Assert.Contains(status, html);
        Assert.Contains(action, html);
        Assert.Contains("Lời nhắn cho người dùng", html);
        Assert.Contains("Hệ thống đang bảo trì", html);
    }
    private sealed class StateHandler(bool active) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { dangBaoTri = active, loiNhan = "Chúng tôi đang nâng cấp." }) });
    }
    private sealed class ErrorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}

