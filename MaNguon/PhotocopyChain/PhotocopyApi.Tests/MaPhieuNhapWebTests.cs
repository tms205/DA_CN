using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class MaPhieuNhapWebTests
{
    [Theory]
    [InlineData(false, 1, "PN0001", "Tờ")]
    [InlineData(false, 42, "PN0042", "Hộp")]
    [InlineData(false, 12345, "PN12345", "Cái")]
    [InlineData(true, 1, "PN0001", "Tờ")]
    [InlineData(true, 42, "PN0042", "Hộp")]
    [InlineData(true, 12345, "PN12345", "Cái")]
    public async Task MaPhieu_HienTienToVaSo_KhongInNguyenCode(bool pheDuyet, int ma, string mongDoi, string donViTinh)
    {
        using var http = new HttpClient(new ApiPhieuNhap(ma, donViTinh)) { BaseAddress = new Uri("http://test.local/") };
        var phamVi = new PhamViChiNhanh(null, http);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, JsKhongChay>();
        services.AddSingleton(http);
        services.AddSingleton(new ThongBaoPheDuyet(http));
        services.AddSingleton(phamVi);
        services.AddSingleton(new PhienDangNhap(null, http, phamVi));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var trang = pheDuyet
                ? await renderer.RenderComponentAsync<PheDuyet>()
                : await renderer.RenderComponentAsync<KhoVatTu>();
            return WebUtility.HtmlDecode(trang.ToHtmlString());
        });
        var oMa = Regex.Match(html, "<td[^>]*data-nhan=\"Mã phiếu\"[^>]*><b>(.*?)</b></td>");
        Assert.True(oMa.Success, "Phải có ô mã phiếu nhập trong bảng.");
        Assert.Equal(mongDoi, oMa.Groups[1].Value);
        Assert.DoesNotContain("MaPhieuNhap.ToString", html);
        Assert.Contains("<b>20</b> " + donViTinh, html);
        Assert.DoesNotContain("bản/cuộn", html);
    }

    private sealed class JsKhongChay : Microsoft.JSInterop.IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string name, object[] args) => ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }

    private sealed class ApiPhieuNhap(int ma, string donViTinh) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            object data = req.RequestUri.AbsolutePath switch {
                "/api/quan-tri/phieu-nhap" => new[] { new {
                    MaPhieuNhap = ma, TrangThai = "CHO_DUYET", MaChiNhanh = 1,
                    TenChiNhanh = "Chi nhánh thử", MaVatTu = 1, TenVatTu = "Giấy A4",
                    DonViTinh = donViTinh, NhaCungCap = "Nhà cung cấp thử", SoLuong = 20, DonGia = 100000,
                    TenNguoiLap = "Người lập thử", NgayLap = new DateTime(2026, 10, 12),
                } },
                "/api/quan-tri/kho" => Array.Empty<TonKhoDto>(),
                "/api/quan-tri/hoan-tien" => Array.Empty<HoanTienChoDuyetDto>(),
                _ => throw new InvalidOperationException("API không được dự kiến trong kiểm thử: " + req.RequestUri.AbsolutePath),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data) });
        }
    }
}
