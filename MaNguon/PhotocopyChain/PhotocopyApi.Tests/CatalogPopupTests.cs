using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using TrangDichVu = PhotocopyAdmin.Pages.DichVu;
using PhotocopyShared.Dtos.Auth;
using Xunit;

namespace PhotocopyApi.Tests;

public class CatalogPopupTests
{
    [Theory]
    [InlineData(typeof(TrangDichVu))]
    [InlineData(typeof(BangGia))]
    [InlineData(typeof(KhuyenMai))]
    public async Task DanhMuc_KhongHienFormThemSuaNgayTrenTrang(Type trang)
    {
        string html = await Render(trang);
        Assert.Contains("<dialog", html);
        Assert.DoesNotContain("<form", html);
        if (trang == typeof(KhuyenMai)) Assert.Contains("Thêm khuyến mãi", html);
    }

    [Fact]
    public async Task ThemDichVu_FormVaNutLuuNamTrongPopup()
    {
        string html = await Render(typeof(DichVuDangThem));
        int dau = html.IndexOf("<dialog", StringComparison.Ordinal);
        int cuoi = html.IndexOf("</dialog>", dau, StringComparison.Ordinal);
        string popup = html[dau..cuoi];
        Assert.Contains("Tên dịch vụ", popup);
        Assert.Contains("id=\"dich-vu-form\"", popup);
        Assert.Contains("form=\"dich-vu-form\"", popup);
        Assert.Contains("Lưu dịch vụ", popup);
        Assert.Contains("Riêng bìa kiếng và lò xo được phép đi cùng", popup);
        Assert.Contains("<option value=\"DONG_GAY\">Đóng tập tài liệu</option>", popup);
        Assert.DoesNotContain("đóng gáy", popup, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> Render(Type type)
    {
        using var http = new HttpClient(new ApiDanhMuc()) { BaseAddress = new Uri("http://test.local/") };
        var js = new JsThu();
        var phamVi = new PhamViChiNhanh(js, http);
        var phien = new PhienDangNhap(js, http, phamVi);
        await phien.DangNhap(new DangNhapResponse {
            AccessToken = "chi-de-render-test", HetHanLuc = DateTime.UtcNow.AddHours(1),
            NguoiDung = new NguoiDungDto { MaNguoiDung = 1, HoTen = "CEO kiểm thử", TenVaiTro = "SUPER_ADMIN" }
        }, false);
        await phamVi.ChonAsync(1);
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(http); services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton(phamVi); services.AddSingleton(phien);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => {
            var root = await renderer.RenderComponentAsync(type, ParameterView.Empty);
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
    }

    public class DichVuDangThem : TrangDichVu
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            typeof(TrangDichVu).GetMethod("ThemMoi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, null);
        }
    }

    private sealed class ApiDanhMuc : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains(req.RequestUri!.AbsolutePath, new[] {
                "/api/quan-tri/danh-muc-dich-vu", "/api/quan-tri/danh-muc-vat-tu",
                "/api/quan-tri/dich-vu", "/api/quan-tri/khuyen-mai"
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) });
        }
    }

    private sealed class JsThu : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string id, object[] args) => ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }
}
