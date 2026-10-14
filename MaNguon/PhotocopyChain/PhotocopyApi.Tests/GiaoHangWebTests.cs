using Xunit;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.Tests;

public class GiaoHangWebTests
{
    private static string Goc([System.Runtime.CompilerServices.CallerFilePath] string file = "")
    {
        string dir = Path.GetDirectoryName(file);
        while (!Directory.Exists(Path.Combine(dir, "PhotocopyAdmin")))
            dir = Directory.GetParent(dir)?.FullName ?? throw new InvalidOperationException("Không tìm thấy solution.");
        return dir;
    }

    [Fact]
    public void WebGiaoHang_CoTrangVaMenuTheoPhongCachCu()
    {
        string goc = Goc();
        Assert.True(File.Exists(Path.Combine(goc, "PhotocopyAdmin/Pages/GiaoHang.razor")));
        string page = File.ReadAllText(Path.Combine(goc, "PhotocopyAdmin/Pages/GiaoHang.razor"));
        Assert.Contains("SUPER_ADMIN,BRANCH_ADMIN,STAFF", page);
        Assert.Contains("/phan-cong", page);
        Assert.Contains("Chờ giao", page);
        Assert.Contains("Đang giao", page);
        Assert.Contains("Lịch sử", page);
        Assert.Contains("the-bang", page);
        Assert.Contains("giao-hang", File.ReadAllText(Path.Combine(goc, "PhotocopyAdmin/Layout/MainLayout.razor")));
        Assert.Contains("PhotocopyAdmin.styles.css", File.ReadAllText(Path.Combine(goc, "PhotocopyAdmin/wwwroot/index.html")));
    }

    [Fact]
    public async Task RenderTrangGiao_CoDiaChiKhachPhiShipperVaKhongInCodeRazor()
    {
        using var http = new HttpClient(new MayChuGiao()) { BaseAddress = new Uri("http://fixture.local/") };
        var phamVi = new PhamViChiNhanh(null, http);
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton(http);
        services.AddSingleton(new PhienDangNhap(null, http, phamVi));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string raw = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<PhotocopyAdmin.Pages.GiaoHang>()).ToHtmlString());
        string html = WebUtility.HtmlDecode(raw);
        Assert.Contains("DH00015", html);
        Assert.Contains("Khách <script>", html);
        Assert.Contains("Tầng 2", html);
        Assert.Contains("19.000₫", html);
        Assert.Contains("Shipper chi nhánh này", html);
        Assert.Contains("Nguyễn Văn Giao", html);
        Assert.Contains("Không thu COD", html);
        Assert.DoesNotContain("<script>", raw); // Nội dung khách nhập phải được HTML-encode.
        Assert.DoesNotContain("don.MaDonHang", html);
    }

    private sealed class MayChuGiao : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            object data = req.RequestUri.AbsolutePath.EndsWith("/shipper")
                ? new[] { new ShipperDto { MaShipper = 7, MaChiNhanh = 1, HoTen = "Nguyễn Văn Giao" } }
                : new[] { new DonGiaoDto { MaDonHang = 15, MaDonHangHienThi = "DH00015", MaChiNhanh = 1,
                    TenChiNhanh = "Cơ sở 1", DiaChiChiNhanh = "140 Lê Trọng Tấn", TenKhachHang = "Khách <script>",
                    DiaChiGiao = "123 Lê Lợi · Chi tiết: Tầng 2", TongTien = 39000, PhiShip = 19000,
                    TrangThaiDonHang = "DA_IN", TrangThaiThanhToan = "DA_THANH_TOAN" } };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data) });
        }
    }
}
