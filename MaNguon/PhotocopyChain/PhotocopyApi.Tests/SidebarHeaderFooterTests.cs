using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using PhotocopyAdmin.Components;
using PhotocopyAdmin.Layout;
using PhotocopyShared.Dtos.Auth;
using PhotocopyShared.Dtos.DanhMuc;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class SidebarHeaderFooterTests
{
    [Theory]
    [InlineData("BangDieuKhien")]
    [InlineData("DongHoDo")]
    [InlineData("QuyTrinh")]
    [InlineData("SoDoQuanLy")]
    [InlineData("CaiDat")]
    [InlineData("ViTien")]
    [InlineData("LichSu")]
    [InlineData("NgoiSao")]
    [InlineData("NhanGia")]
    [InlineData("VeKhuyenMai")]
    public void IconMoi_SvgHopLeVaCungKhung24(string ten)
    {
        var ham = typeof(Svg).GetMethod(ten);
        Assert.NotNull(ham);
        string svg = Assert.IsType<string>(ham.Invoke(null, new object[] { 18 }));
        var hinh = XElement.Parse(svg);
        Assert.Equal("0 0 24 24", hinh.Attribute("viewBox")?.Value);
        Assert.Equal("18", hinh.Attribute("width")?.Value);
        Assert.Equal("currentColor", hinh.Attribute("stroke")?.Value);
        Assert.True(hinh.HasElements);
    }

    [Fact]
    public async Task Sidebar_MoiMucVaNhomCoHinhRieng()
    {
        string html = await Render("SUPER_ADMIN", "tong-quan");
        string menu = Regex.Match(html, "<nav class=\"tb-menu\">(.*?)</nav>", RegexOptions.Singleline).Groups[1].Value;
        var hinh = Regex.Matches(menu, "<svg[^>]*>(.*?)</svg>", RegexOptions.Singleline)
            .Select(x => x.Groups[1].Value).ToList();
        Assert.True(hinh.Count >= 15);
        Assert.Equal(hinh.Count, hinh.Distinct().Count());
    }

    [Theory]
    [InlineData("SUPER_ADMIN", "tong-quan", "Tổng quan", true)]
    [InlineData("SUPER_ADMIN", "bang-gia", "Bảng giá in ấn", true)]
    [InlineData("BRANCH_ADMIN", "ca-lam-viec", "Ca làm việc & két tiền", true)]
    [InlineData("STAFF", "", "Hàng đợi đơn hàng", false)]
    public async Task Layout_HeaderTheoTrang_FooterSauNoiDung_GiuQuyen(string vaiTro, string trang, string tenTrang, bool coDashboard)
    {
        string html = await Render(vaiTro, trang);
        Assert.Equal(coDashboard, html.Contains("<span>Dashboard</span>"));
        string header = Regex.Match(html, "<header[^>]*>(.*?)</header>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Contains(tenTrang, header);
        Assert.Contains("aria-expanded=\"true\"", header);
        Assert.Contains("aria-expanded=\"false\"", header);
        Assert.Contains("role=\"banner\"", html);
        Assert.Contains("role=\"contentinfo\"", html);
        Assert.DoesNotContain("CNTT-KLCN031", html);
        Assert.Contains("href=\"mailto:manh27062005@gmail.com\"", html);
        Assert.Contains("href=\"tel:0769479813\"", html);
        Assert.Contains("Email liên hệ", html);
        Assert.Contains("Điện thoại", html);
        Assert.Contains("In rõ nét — Nhận đúng hẹn.", html);
        Assert.True(html.IndexOf("<footer", StringComparison.Ordinal) > html.IndexOf("</main>", StringComparison.Ordinal));
        Assert.True(html.IndexOf("<footer", StringComparison.Ordinal) > html.IndexOf("noi-dung-kiem-thu", StringComparison.Ordinal));
        Assert.Equal(vaiTro == "SUPER_ADMIN", header.Contains("<select"));
        Assert.DoesNotContain("<span>Báo cáo</span>", html);
    }

    private static async Task<string> Render(string vaiTro, string trang)
    {
        using var http = new HttpClient(new ApiLayout()) { BaseAddress = new Uri("http://test.local/") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(http);
        services.AddSingleton<IJSRuntime, JsLayout>();
        services.AddSingleton<NavigationManager>(new DieuHuong(trang));
        services.AddSingleton<PhamViChiNhanh>();
        services.AddSingleton<PhienDangNhap>();
        services.AddSingleton<ThongBaoPheDuyet>();
        await using var provider = services.BuildServiceProvider();
        var phien = provider.GetRequiredService<PhienDangNhap>();
        await phien.DangNhap(new DangNhapResponse {
            AccessToken = "layout-test", HetHanLuc = DateTime.UtcNow.AddHours(1),
            NguoiDung = new NguoiDungDto { MaNguoiDung = 1, HoTen = "Người kiểm thử", TenVaiTro = vaiTro,
                MaChiNhanh = vaiTro == "SUPER_ADMIN" ? null : 1, TenChiNhanh = "Chi nhánh thử" }
        }, false);
        RenderFragment noiDung = b => b.AddMarkupContent(0, "<p>noi-dung-kiem-thu</p>");
        RenderFragment layout = b => {
            b.OpenComponent<MainLayout>(0);
            b.AddAttribute(1, "Body", noiDung);
            b.CloseComponent();
        };
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => {
            var ketQua = await renderer.RenderComponentAsync<CascadingValue<Task<AuthenticationState>>>(
                ParameterView.FromDictionary(new Dictionary<string, object> {
                    ["Value"] = phien.GetAuthenticationStateAsync(), ["ChildContent"] = layout
                }));
            return WebUtility.HtmlDecode(ketQua.ToHtmlString());
        });
    }

    private sealed class ApiLayout : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            object ds = req.RequestUri.AbsolutePath switch {
                "/api/danh-muc/chi-nhanh" => new List<ChiNhanhDto>(),
                "/api/quan-tri/bao-cao/hien-tai" => new TongQuanHienTaiDto(),
                _ => throw new InvalidOperationException("Layout không cần gọi API khác.")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(ds) });
        }
    }

    private sealed class JsLayout : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args) => new(default(TValue));
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object[] args) => new(default(TValue));
    }

    private sealed class DieuHuong : NavigationManager
    {
        public DieuHuong(string trang) => Initialize("http://test.local/", "http://test.local/" + trang);
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }
}
