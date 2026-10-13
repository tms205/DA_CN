using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class TongQuanPhanTichTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhanTich_RenderCoCauHeatmapHoacLoi_KhongGiaSoKhong(bool loi)
    {
        Type lop = typeof(PhamViChiNhanh).Assembly.GetType("PhotocopyAdmin.Components.TongQuanPhanTich");
        Assert.NotNull(lop);
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(new HttpClient(new ApiPhanTich(loi)) { BaseAddress = new Uri("http://test/") });
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var p = ParameterView.FromDictionary(new Dictionary<string, object> { ["TuNgay"] = DateTime.Today, ["DenNgay"] = DateTime.Today });
            var kq = await renderer.RenderComponentAsync(lop, p);
            return WebUtility.HtmlDecode(kq.ToHtmlString());
        });
        Assert.Contains(loi ? "Không tải được phân tích" : "Cơ cấu dịch vụ", html);
        if (loi) Assert.DoesNotContain("Giờ cao điểm", html);
        else
        {
            Assert.Contains("Giờ cao điểm", html);
            Assert.Contains("In màu", html);
            Assert.Contains("Đóng lò xo", html);
            Assert.Contains("COD", html);
            Assert.Contains("Chưa có dữ liệu", html);
            Assert.DoesNotContain("NaN", html);
            Assert.DoesNotContain("Infinity", html);
        }
    }

    [Fact]
    public async Task QuanLyThieuChiNhanh_ApiMoiTuChoiTruocKhiKetNoiDb()
    {
        var c = Controller(VaiTro.BranchAdmin);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await c.HienTai()).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await c.PhanTich(DateTime.Today, DateTime.Today)).StatusCode);
    }

    [Theory]
    [InlineData(367)]
    [InlineData(0)]
    public async Task KyNgoaiGioiHan_ApiTra400(int soNgay)
    {
        var c = Controller(VaiTro.SuperAdmin);
        Assert.IsType<BadRequestObjectResult>(await c.PhanTich(DateTime.Today, DateTime.Today.AddDays(soNgay - 1)));
    }

    private static BaoCaoTongQuanController Controller(string role) => new(new ConfigurationBuilder().Build()) {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(TenClaim.VaiTro, role) }, "test", ClaimTypes.Name, TenClaim.VaiTro))
        } }
    };

    private sealed class ApiPhanTich(bool loi) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("/api/quan-tri/bao-cao/phan-tich", req.RequestUri.AbsolutePath);
            var dto = new TongQuanPhanTichDto {
                TienHang = 110000, PhiShip = 5000, GiamGia = 10000, DoanhThu = 105000,
                DichVu = new() { new() { Ten = "In màu", SoTien = 100000 }, new() { Ten = "Đóng lò xo", SoTien = 10000 } },
                ThanhToan = new() { new() { Ten = "COD", SoTien = 105000, SoDon = 1 } },
                GioCaoDiem = new() { new() { Thu = 0, Gio = 9, SoDon = 1 } }
            };
            return Task.FromResult(new HttpResponseMessage(loi ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = JsonContent.Create(dto) });
        }
    }
}
