using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class TongQuanWebTests
{
    [Theory]
    [InlineData("du-lieu")]
    [InlineData("trong")]
    [InlineData("loi")]
    public async Task Dashboard_RenderSoLieuHoacLoi_KhongGiaSoKhongKhiApiLoi(string tinhHuong)
    {
        Type lop = typeof(PhamViChiNhanh).Assembly.GetType("PhotocopyAdmin.Pages.TongQuan");
        Assert.NotNull(lop);
        using var http = new HttpClient(new ApiBaoCao(tinhHuong)) { BaseAddress = new Uri("http://test.local/") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(http);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var ketQua = await renderer.RenderComponentAsync(lop, Microsoft.AspNetCore.Components.ParameterView.Empty);
            return WebUtility.HtmlDecode(ketQua.ToHtmlString());
        });
        Assert.True(html.Contains(tinhHuong == "loi" ? "Không tải được tổng quan" : "Giá trị trung bình/đơn"), html);
        if (tinhHuong == "loi")
        {
            Assert.Contains("Không tải được tổng quan", html);
            Assert.DoesNotContain("Doanh thu kỳ đã chọn", html);
            Assert.DoesNotContain("<svg", html);
        }
        else
        {
            Assert.Contains("role=\"tablist\"", html);
            Assert.Contains("Giá trị trung bình/đơn", html);
            Assert.Contains("Doanh thu kỳ đã chọn", html);
            Assert.Contains("Hôm nay", html);
            Assert.Contains("Tuần này", html);
            Assert.Contains("Tháng này", html);
            Assert.DoesNotContain("Chi tiết doanh thu", html);
            Assert.DoesNotContain("Số dòng báo cáo", html);
            Assert.Contains("Xu hướng doanh thu", html);
            Assert.Contains("href=\"bao-cao\"", html);
            Assert.Contains("<svg", html);
            Assert.Contains("role=\"img\"", html);
            Assert.DoesNotContain("NaN", html);
            Assert.DoesNotContain("Infinity", html);
            Assert.Contains(tinhHuong == "du-lieu" ? "150.000₫" : "Chưa có đơn hoàn tất", html);
        }
    }

    [Fact]
    public async Task Dashboard_ChiCeoVaQuanLy_StaffKhongDuocVao()
    {
        Type lop = typeof(PhamViChiNhanh).Assembly.GetType("PhotocopyAdmin.Pages.TongQuan");
        Assert.NotNull(lop);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        await using var provider = services.BuildServiceProvider();
        // Các AuthorizeAttribute được kết hợp bằng AND, gồm cả quyền mặc định của Pages.
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), lop.GetCustomAttributes<AuthorizeAttribute>());
        var auth = provider.GetRequiredService<IAuthorizationService>();
        foreach (string vaiTro in new[] { "SUPER_ADMIN", "BRANCH_ADMIN", "STAFF", "CUSTOMER" })
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, vaiTro) }, "test"));
            var ketQua = await auth.AuthorizeAsync(user, null, policy);
            Assert.Equal(vaiTro == "SUPER_ADMIN" || vaiTro == "BRANCH_ADMIN", ketQua.Succeeded);
        }
    }

    private sealed class ApiBaoCao(string tinhHuong) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            if (req.RequestUri.AbsolutePath != "/api/quan-tri/bao-cao/doanh-thu")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(Array.Empty<object>()) });
            Assert.Equal("/api/quan-tri/bao-cao/doanh-thu", req.RequestUri.AbsolutePath);
            Assert.Contains("nhomTheo=ngay", req.RequestUri.Query);
            if (tinhHuong == "loi") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var ds = new List<BaoCaoDoanhThuDto>();
            if (tinhHuong == "du-lieu") ds.Add(new() { Ngay = DateTime.Today, MaChiNhanh = 1, TenChiNhanh = "Chi nhánh thử", SoDon = 3, DoanhThu = 150000 });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(ds) });
        }
    }

    [Fact]
    public async Task BieuDoMotNgay_HienCaHaiKy_VaToaDoKhongPhuThuocDauPhayThapPhan()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var cu = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
                var p = Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object> {
                    ["HienTai"] = new List<BaoCaoDoanhThuDto> { new() { Ngay = DateTime.Today, DoanhThu = 3 } },
                    ["KyTruoc"] = new List<BaoCaoDoanhThuDto> { new() { Ngay = DateTime.Today, DoanhThu = 1 } },
                    ["NhomTheo"] = "tuan"
                });
                var ketQua = await renderer.RenderComponentAsync<PhotocopyAdmin.Components.BieuDoDoanhThu>(p);
                return WebUtility.HtmlDecode(ketQua.ToHtmlString());
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = cu; }
        });
        Assert.Contains("class=\"tq-diem-truoc\"", html);
        Assert.Contains("points=\"452,175\"", html);
        Assert.Contains("class=\"tq-diem\"", html);
    }
}
