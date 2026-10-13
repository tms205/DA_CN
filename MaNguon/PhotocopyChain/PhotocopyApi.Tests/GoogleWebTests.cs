using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using Xunit;

namespace PhotocopyApi.Tests;

public class GoogleWebTests
{
    [Theory]
    [InlineData(false, false, "Chưa cấu hình Google")]
    [InlineData(true, false, "Đăng nhập bằng Google")]
    [InlineData(false, true, "Không tải được cấu hình")]
    public async Task NutGoogle_ChoBietCauHinhKhongGiaThanhCong(bool enabled, bool error, string text)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, Js>();
        services.AddSingleton(new HttpClient(new Config(enabled, error)) { BaseAddress = new Uri("http://fixture.local") });
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var type = typeof(PhienDangNhap).Assembly.GetType("PhotocopyAdmin.Components.DangNhapGoogle");
        Assert.NotNull(type);
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var result = await renderer.RenderComponentAsync(type, ParameterView.Empty);
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
        Assert.Contains(text, html);
        if (!enabled && !error) Assert.Contains("disabled", html);
        Assert.DoesNotContain("chưa được bật ở phiên bản này", html);
    }
    private sealed class Config(bool enabled, bool error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Assert.EndsWith("/api/auth/google/config", req.RequestUri.ToString());
            return Task.FromResult(new HttpResponseMessage(error ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) {
                Content = JsonContent.Create(new { daBat = enabled, webClientId = "123456-test.apps.googleusercontent.com" })
            });
        }
    }
    private sealed class Js : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string name, object[] args) => new(default(T));
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken ct, object[] args) => new(default(T));
    }
}
