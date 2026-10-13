using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;

// Request sai / ngoài quyền phải bị chặn trước SQL. Không kết nối CSDL hay tạo dữ liệu thật.
public class QuanLyDanhMucHttpTests
{
    [Fact]
    public async Task DanhMuc_KiemTraQuyenDuLieuVaPhamViTruocSql()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, DangNhap>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(QuanTriController).Assembly);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(url) };
        http.DefaultRequestHeaders.Add("VaiTroTest", "SUPER_ADMIN");
        var cases = new (string Path, object Body)[] {
            ("chi-nhanh", new { tenChiNhanh = "", diaChi = "Địa chỉ", gioMoCua = "08:00:00", gioDongCua = "20:00:00", trangThai = "HOAT_DONG" }),
            ("chi-nhanh", new { tenChiNhanh = "Cơ sở", diaChi = "Địa chỉ", soDienThoai = "09123", gioMoCua = "08:00:00", gioDongCua = "20:00:00", trangThai = "HOAT_DONG" }),
            ("chi-nhanh", new { tenChiNhanh = "Cơ sở", diaChi = "Địa chỉ", gioMoCua = "20:00:00", gioDongCua = "08:00:00", trangThai = "HOAT_DONG" }),
            ("chi-nhanh", new { tenChiNhanh = "Cơ sở", diaChi = "Địa chỉ", viDo = 10, gioMoCua = "08:00:00", gioDongCua = "20:00:00", trangThai = "HOAT_DONG" }),
            ("may-in", new { maChiNhanh = 1, tenMay = "Ricoh", tocDo = 0, khoGiay = "A4" }),
            ("may-in", new { maChiNhanh = 1, tenMay = "Ricoh", tocDo = 20, khoGiay = "A0" }),
            ("may-in", new { maChiNhanh = 0, tenMay = "Ricoh", tocDo = 20, khoGiay = "A4" }),
            ("may-in", new { maChiNhanh = 1, tenMay = "Ricoh", tocDo = 20, khoGiay = "A4", soTrangDaIn = -1 }),
        };
        foreach (var item in cases)
        {
            using var r = await http.PostAsJsonAsync("api/quan-tri/" + item.Path, item.Body);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        using var price = await http.PutAsJsonAsync("api/quan-tri/bang-gia/1", new { donGia = -1, trangThai = "HIEN" });
        Assert.Equal(HttpStatusCode.BadRequest, price.StatusCode);
        using var catalog = await http.PutAsJsonAsync("api/quan-tri/danh-muc-dich-vu/1", new { tenDichVu = "" });
        Assert.Equal(HttpStatusCode.BadRequest, catalog.StatusCode);
        // CEO đang chọn chi nhánh 1 không thêm máy vào chi nhánh 2.
        http.DefaultRequestHeaders.Add("ChiNhanhTest", "1");
        using var outside = await http.PostAsJsonAsync("api/quan-tri/may-in", new { maChiNhanh = 2, tenMay = "Ricoh", tocDo = 20, khoGiay = "A4" });
        Assert.Equal(HttpStatusCode.Forbidden, outside.StatusCode);
        foreach (var role in new[] { "BRANCH_ADMIN", "STAFF" })
        {
            http.DefaultRequestHeaders.Remove("VaiTroTest");
            http.DefaultRequestHeaders.Add("VaiTroTest", role);
            foreach (string path in new[] { "chi-nhanh", "may-in", "dich-vu" })
            {
                using var r = await http.PostAsJsonAsync("api/quan-tri/" + path, new { });
                Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
            }
            using var rPrice = await http.PutAsJsonAsync("api/quan-tri/bang-gia/1", new { });
            Assert.Equal(HttpStatusCode.Forbidden, rPrice.StatusCode);
            using var rCustomer = await http.GetAsync("api/quan-tri/khach-hang");
            Assert.Equal(HttpStatusCode.Forbidden, rCustomer.StatusCode);
        }
        await app.StopAsync();
    }

    private sealed class DangNhap : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public DangNhap(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string role = Request.Headers["VaiTroTest"].FirstOrDefault() ?? "STAFF";
            var claims = new List<Claim> { new(ClaimTypes.Role, role), new(TenClaim.VaiTro, role), new(TenClaim.MaNguoiDung, "1") };
            string branch = Request.Headers["ChiNhanhTest"].FirstOrDefault();
            if (branch != null) claims.Add(new(TenClaim.MaChiNhanh, branch));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
