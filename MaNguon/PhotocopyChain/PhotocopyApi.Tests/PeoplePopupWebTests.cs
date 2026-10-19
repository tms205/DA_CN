using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.Auth;
using Xunit;

namespace PhotocopyApi.Tests;

public class PeoplePopupWebTests
{
    [Theory]
    [InlineData(typeof(KhachHang))]
    [InlineData(typeof(PheDuyet))]
    [InlineData(typeof(DanhGia))]
    public async Task TrangQuanLy_CoPopupXacNhan_KhongGuiRequestThayDoiKhiMoTrang(Type type)
    {
        using var http = new HttpClient(new ApiDanhSach()) { BaseAddress = new Uri("http://test.local/") };
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(http);
        services.AddSingleton<IJSRuntime, JsKhongChay>();
        services.AddSingleton(new ThongBaoPheDuyet(http));
        await using var sp = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(sp, sp.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var root = await renderer.RenderComponentAsync(type);
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("<dialog", html);
        Assert.DoesNotContain("aria-busy=\"true\"", html);
    }

    [Fact]
    public async Task NhanVien_ChiMoFormTaoBangPopup_KhongDeMatKhauTrongTrangBanDau()
    {
        using var http = new HttpClient(new ApiDanhSach()) { BaseAddress = new Uri("http://test.local/") };
        var js = new JsKhongChay();
        var phamVi = new PhamViChiNhanh(js, http);
        var phien = new PhienDangNhap(js, http, phamVi);
        await phien.DangNhap(new DangNhapResponse {
            AccessToken = "kiem-thu", HetHanLuc = DateTime.UtcNow.AddHours(1),
            NguoiDung = new NguoiDungDto { MaNguoiDung = 1, HoTen = "CEO kiểm thử", TenVaiTro = "SUPER_ADMIN" }
        }, false);
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(http); services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton(phamVi); services.AddSingleton(phien);
        await using var sp = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(sp, sp.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var root = await renderer.RenderComponentAsync<NhanVien>();
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("Thêm nhân viên", html);
        Assert.Contains("<dialog", html);
        Assert.DoesNotContain("type=\"password\"", html);
        Assert.DoesNotContain("<form", html);
    }

    private sealed class ApiDanhSach : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains(request.RequestUri.AbsolutePath, new[] {
                "/api/nhan-vien", "/api/danh-muc/chi-nhanh", "/api/quan-tri/khach-hang",
                "/api/quan-tri/phieu-nhap", "/api/quan-tri/hoan-tien", "/api/don-hang/danh-gia"
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) });
        }
    }

    private sealed class JsKhongChay : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string name, object[] args) => ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }
}
