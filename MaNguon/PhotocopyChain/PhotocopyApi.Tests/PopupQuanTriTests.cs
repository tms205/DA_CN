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
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task PopupChiRenderFormKhiMo_VaLoiCoHopThoaiRieng(bool mo, bool dangLuu)
    {
        var type = typeof(MauDon).Assembly.GetType("PhotocopyAdmin.Components.HopThoaiQuanTri");
        Assert.NotNull(type);
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IJSRuntime, JsKhongChay>();
        await using var sp = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(sp, sp.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var root = await renderer.RenderComponentAsync(type, ParameterView.FromDictionary(new Dictionary<string, object> {
                ["Mo"] = mo, ["TieuDe"] = "Thêm & sửa bản ghi", ["Loi"] = "Lỗi kiểm thử", ["DangLuu"] = dangLuu,
                ["NoiDung"] = (RenderFragment)(b => b.AddContent(0, "NOI_DUNG_FORM")),
                ["Chan"] = (RenderFragment)(b => b.AddContent(0, "CAC_NUT_FORM"))
            }));
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("<dialog", html);
        Assert.Equal(mo, html.Contains("NOI_DUNG_FORM"));
        Assert.Equal(mo, html.Contains("CAC_NUT_FORM"));
        if (mo) Assert.Contains("Thêm & sửa bản ghi", html);
        if (dangLuu) Assert.Contains("disabled", html);
        Assert.Equal(mo && !dangLuu, html.Contains("Lỗi kiểm thử"));
        if (mo && !dangLuu)
        {
            int sauForm = html.IndexOf("</dialog>", StringComparison.Ordinal);
            Assert.DoesNotContain("Lỗi kiểm thử", html[..sauForm]);
            Assert.Contains("role=\"alertdialog\"", html[sauForm..]);
            Assert.Contains("Đã hiểu", html[sauForm..]);
        }
    }

    private sealed class JsKhongChay : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string name, object[] args) => ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }
}
