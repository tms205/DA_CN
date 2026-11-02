using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin.Components;
using Xunit;

namespace PhotocopyApi.Tests;
public class KhungBanNhapTacVuTests
{
    [Theory]
    [InlineData("CUSTOMER", "lap-don-tai-quay")]
    [InlineData("SHIPPER", "nhan-dang-chu")]
    [InlineData(null, "lap-don-tai-quay")]
    [InlineData("STAFF", "nhan-vien")]
    public async Task KhongTuKhoiTaoTacVuKhiChuaTruyCapHoacKhongCoQuyen(string role, string path)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<KhungBanNhapTacVu>(ParameterView.FromDictionary(new Dictionary<string,object> {
            ["VaiTro"] = role, ["DuongDan"] = path, ["NoiDung"] = (RenderFragment)(b => b.AddContent(0,"Nội dung route gốc"))
        }))).ToHtmlString());
        Assert.Contains("Nội dung route gốc", System.Net.WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Bản nháp lập đơn", html);
        Assert.DoesNotContain("Bản nháp nhận dạng", html);
    }
    [Fact]
    public async Task BaoTriKhongRenderNoiDungRouteHoacKhoiTaoTacVuMoi()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<KhungBanNhapTacVu>(ParameterView.FromDictionary(new Dictionary<string,object> {
            ["VaiTro"]="SUPER_ADMIN",["DuongDan"]="lap-don-tai-quay",["BiKhoa"]=true,["NoiDung"]=(RenderFragment)(b=>b.AddContent(0,"Không được hiện"))
        }))).ToHtmlString());
        Assert.DoesNotContain("Không được hiện",System.Net.WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Bản nháp",System.Net.WebUtility.HtmlDecode(html));
    }
}
