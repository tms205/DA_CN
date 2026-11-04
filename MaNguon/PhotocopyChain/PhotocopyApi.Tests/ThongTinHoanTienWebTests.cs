using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class ThongTinHoanTienWebTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CeoChiXacNhanHoanKhiDaCoDuThongTinNhanTien(bool coThongTin)
    {
        using var http = new HttpClient(new ApiGia(coThongTin)) { BaseAddress = new Uri("http://fixture.local/") };
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(http);
        services.AddSingleton<IJSRuntime>(new JsGia());
        services.AddSingleton(new ThongBaoPheDuyet(http));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string raw = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<PheDuyet>()).ToHtmlString());
        string html = WebUtility.HtmlDecode(raw);
        Assert.Single(Regex.Matches(html, "<h2[^>]*>Hoàn tiền</h2>"));
        Assert.DoesNotContain("Đơn hàng chờ xác nhận hoàn tiền", html);
        Assert.Contains("Loại yêu cầu hoàn tiền", html);
        Assert.Matches("Đơn đã hủy <span[^>]*>1</span>", html);
        Assert.Matches("Sản phẩm <span[^>]*>0</span>", html);
        Assert.Matches("id=\"phe-san-pham\"[^>]*hidden", html);
        var button = Regex.Match(html, @"<button[^>]*>"+(coThongTin?"Xử lý hoàn tiền":"Xem hồ sơ")+"</button>");
        Assert.True(button.Success);
        Assert.DoesNotContain("disabled", button.Value);
        if (coThongTin) {
            Assert.Contains("Vietcombank", html);
            Assert.Contains("Chờ chuyển hoàn", html);
        }
        else Assert.Contains("Chờ khách bổ sung", html);
        Assert.DoesNotContain("<script>", raw);
    }

    private sealed class ApiGia(bool du) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            object data = request.RequestUri!.AbsolutePath.EndsWith("/hoan-tien")
                ? new[] { new HoanTienChoDuyetDto {
                    MaDonHang = 1, MaDonHangHienThi = "DH00001", TenKhachHang = "Khách test", SoTien = 10000,
                    GhiChu = du ? new ThongTinHoanTienRequest {
                        LyDo = "Không cần in <script>", NganHang = "Vietcombank", SoTaiKhoan = "001234567890"
                    }.TaoGhiChu() : "Lý do hủy cũ"
                } } : Array.Empty<object>();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data, data.GetType()) });
        }
    }

    private sealed class JsGia : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string id, object[] args) => ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }
}
