using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin.Pages;
using Xunit;

namespace PhotocopyApi.Tests;

public class PopupQuanTriTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PopupChiRenderFormKhiMo_VaHienLoiBusyTrongPopup(bool mo)
    {
        var type = typeof(MauDon).Assembly.GetType("PhotocopyAdmin.Components.HopThoaiQuanTri");
        Assert.NotNull(type);
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IJSRuntime, JsKhongChay>();
        await using var sp = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(sp, sp.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var root = await renderer.RenderComponentAsync(type, ParameterView.FromDictionary(new Dictionary<string, object> {
                ["Mo"] = mo, ["TieuDe"] = "Thêm & sửa bản ghi", ["Loi"] = "Lỗi kiểm thử", ["DangLuu"] = true,
                ["NoiDung"] = (RenderFragment)(b => b.AddContent(0, "NOI_DUNG_FORM")),
                ["Chan"] = (RenderFragment)(b => b.AddContent(0, "CAC_NUT_FORM"))
            }));
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("<dialog", html);
        Assert.Equal(mo, html.Contains("NOI_DUNG_FORM"));
        Assert.Equal(mo, html.Contains("CAC_NUT_FORM"));
        Assert.Equal(mo, html.Contains("Lỗi kiểm thử"));
        if (mo) { Assert.Contains("role=\"alert\"", html); Assert.Contains("disabled", html); Assert.Contains("Thêm & sửa bản ghi", html); }
    }

    private sealed class JsKhongChay : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string name, object[] args) => ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }
}
