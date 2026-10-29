using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using PhotocopyShared.Dtos.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using Xunit;
namespace PhotocopyApi.Tests;
public class DangNhapBaoTriWebTests
{
    [Fact]
    public async Task BaoTri_KhongHienLoiKhiVuaMoTrang()
    {
        using var http = new HttpClient(new Handler()) { BaseAddress = new Uri("http://fixture.local/") };
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IJSRuntime, FakeJs>(); services.AddSingleton<NavigationManager, FakeNav>(); services.AddSingleton(http);
        services.AddSingleton<PhamViChiNhanh>(); services.AddSingleton<PhienDangNhap>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<PhotocopyAdmin.Pages.DangNhap>()).ToHtmlString()));
        Assert.DoesNotContain("Tin nhắn bảo trì của CEO", html);
        Assert.DoesNotContain("Không tải được cấu hình Google", html);
        Assert.Contains("Đăng nhập", html);
        Assert.Contains("Tiếp tục với Google", html);
    }
    [Fact]
    public async Task BaoTriBatTrongLucTaiGoogle_KhongTuHienLoiDo()
    {
        using var http = new HttpClient(new RaceHandler()) { BaseAddress = new Uri("http://fixture.local/") };
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IJSRuntime, FakeJs>(); services.AddSingleton<NavigationManager, FakeNav>(); services.AddSingleton(http);
        services.AddSingleton<PhamViChiNhanh>(); services.AddSingleton<PhienDangNhap>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<PhotocopyAdmin.Pages.DangNhap>()).ToHtmlString()));
        Assert.DoesNotContain("Không tải được cấu hình Google", html);
        Assert.DoesNotContain("Tin nhắn bảo trì của CEO", html);
        Assert.Contains("Tiếp tục với Google", html);
    }
    private sealed class RaceHandler : HttpMessageHandler
    {
        private int _statusCalls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/api/bao-tri/trang-thai")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { dangBaoTri = ++_statusCalls > 1, loiNhan = "Tin nhắn bảo trì của CEO" }) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { message = "Bảo trì" }) });
        }
    }
    [Theory]
    [InlineData("QuenMatKhauAsync")]
    [InlineData("GoogleTrongBaoTriAsync")]
    [InlineData("DangNhapKhongHopLe")]
    public async Task ThaoTacBiChan_MoThongBaoLoiNhan(string method)
    {
        using var http = new HttpClient(new Handler()) { BaseAddress = new Uri("http://fixture.local/") };
        var page = new PhotocopyAdmin.Pages.DangNhap();
        typeof(PhotocopyAdmin.Pages.DangNhap).GetProperty("Http", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, http);
        await (Task)typeof(PhotocopyAdmin.Pages.DangNhap).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
        Assert.True((bool)typeof(PhotocopyAdmin.Pages.DangNhap).GetField("_moThongBaoBaoTri", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!);
    }

    [Fact]
    public async Task DangNhapCEO_VanThanhCongKhiBaoTri()
    {
        using var http = new HttpClient(new LoginHandler()) { BaseAddress = new Uri("http://fixture.local/") };
        var js = new FakeJs(); var nav = new FakeNav(); var scope = new PhamViChiNhanh(js, http);
        var session = new PhienDangNhap(js, http, scope);
        var page = new PhotocopyAdmin.Pages.DangNhap();
        var type = page.GetType();
        type.GetProperty("Http", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, http);
        type.GetProperty("Phien", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, session);
        type.GetProperty("Nav", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, nav);
        var form = (DangNhapRequest)type.GetField("_form", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
        form.TaiKhoan = "ceo@example.com"; form.MatKhau = "password";
        await (Task)type.GetMethod("TaiTrangThaiBaoTriAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
        await (Task)type.GetMethod("DangNhapAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
        Assert.Equal("SUPER_ADMIN", session.NguoiDung.TenVaiTro);
        Assert.False((bool)type.GetField("_moThongBaoBaoTri", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!);
    }
    [Fact]
    public async Task DangNhapKhongDuocPhep_HienThongBaoThayViLoiDo()
    {
        using var http = new HttpClient(new BlockedLoginHandler()) { BaseAddress = new Uri("http://fixture.local/") };
        var page = new PhotocopyAdmin.Pages.DangNhap(); var type = page.GetType();
        type.GetProperty("Http", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, http);
        var form = (DangNhapRequest)type.GetField("_form", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
        form.TaiKhoan = "staff@example.com"; form.MatKhau = "password";
        await (Task)type.GetMethod("DangNhapAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
        Assert.True((bool)type.GetField("_moThongBaoBaoTri", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!);
        Assert.Null(type.GetField("_loi", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page));
    }
    private sealed class BlockedLoginHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/api/bao-tri/trang-thai")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { dangBaoTri = true, loiNhan = "Tin nhắn bảo trì của CEO" }) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { message = "Bảo trì" }) });
        }
    }
    private sealed class LoginHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/api/bao-tri/trang-thai")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { dangBaoTri = true, loiNhan = "Tin nhắn bảo trì của CEO" }) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { accessToken = "test-token", hetHanLuc = "2099-01-01T00:00:00Z", nguoiDung = new { maNguoiDung = 1, hoTen = "CEO", tenVaiTro = "SUPER_ADMIN", email = "ceo@example.com" } }) });
        }
    }
    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { dangBaoTri = true, loiNhan = "Tin nhắn bảo trì của CEO" }) });
    }
    private sealed class FakeJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object[] args) => new(default(T));
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object[] args) => new(default(T));
    }
    private sealed class FakeNav : NavigationManager
    {
        public FakeNav() => Initialize("http://fixture.local/", "http://fixture.local/dang-nhap");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
