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

// Kestrel chạy cổng tạm. Không cấu hình CSDL, Gmail hay payOS: dữ liệu sai phải bị chặn trước khi gọi chúng.
public class KiemTraHttpNhapTests
{
    [Fact]
    public async Task RequestSai_BiMvcChanTruocControllerVaKhongCanDatabase()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddMemoryCache();
        builder.Services.AddSignalR();
        builder.Services.AddAuthentication("kiem-thu").AddScheme<AuthenticationSchemeOptions, DangNhapKiemThu>("kiem-thu", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(url) };

        var cases = new (string Path, object Body)[]
        {
            ("api/auth/register/otp", new { hoTen = "Nguyễn Văn A", email = "a@huit.edu.vn", soDienThoai = "0912345678", matKhau = "Abcd1234" }),
            ("api/auth/register/otp", new { hoTen = "Nguyễn Văn A", email = "a@gmail.com", soDienThoai = "091234567", matKhau = "Abcd1234" }),
            ("api/nhan-vien", new { hoTen = "Nguyễn Văn A", email = "a@huit.edu.vn", matKhau = "Abcd1234", maChiNhanh = 1 }),
            ("api/don-hang/khach-tai-quay", new { hoTen = "Nguyễn Văn A", soDienThoai = "09123456789" }),
            ("api/don-hang/tai-quay", new { maKhachHang = 1, fileIn = new object[] { null } }),
            ("api/bao-gia", new { maChiNhanh = 1, fileIn = new[] { new { maFileIn = 1, maDichVu = 1, soBanIn = 1.5 } } }),
            ("api/bao-gia", new { maChiNhanh = 1, fileIn = new[] { new { maFileIn = 1, maDichVu = 1, soBanIn = 0 } } }),
            ("api/don-hang", new { maChiNhanh = 1, hinhThucNhan = "GIAO_HANG", fileIn = new[] { new { maFileIn = 1, maDichVu = 1 } } }),
            ("api/quan-tri/khuyen-mai", new { maCode = "ABC", phanTramGiam = 101 }),
            ("api/quan-tri/phieu-nhap", new { maVatTu = 1, soLuong = 1, donGia = 1.5 }),
            ("api/ca/mo", new { tienDauCa = -1 }),
            ("api/ca/dong", new { tongDoanhThuCuoiCa = 1000000000000m }),
        };
        foreach (var item in cases)
        {
            using var res = await http.PostAsJsonAsync(item.Path, item.Body);
            Assert.True(res.StatusCode == HttpStatusCode.BadRequest,
                $"{item.Path}: mong đợi 400, nhận {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        }
        using var invalidBranch = await http.PutAsJsonAsync("api/quan-tri/chi-nhanh/1", new {
            tenChiNhanh = "Cơ sở 1", diaChi = "140 Lê Trọng Tấn", soDienThoai = "091234567",
            gioMoCua = "08:00:00", gioDongCua = "18:00:00", trangThai = "HOAT_DONG" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidBranch.StatusCode);
        using var invalidId = await http.GetAsync("api/don-hang/-1");
        Assert.Equal(HttpStatusCode.BadRequest, invalidId.StatusCode);
        await app.StopAsync();
    }

    public sealed class DangNhapKiemThu : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public DangNhapKiemThu(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim> { new(TenClaim.MaNguoiDung, "1"), new(TenClaim.MaChiNhanh, "1"), new(TenClaim.VaiTro, "SUPER_ADMIN") };
            foreach (var role in new[] { "SUPER_ADMIN", "BRANCH_ADMIN", "STAFF", "CUSTOMER" })
                claims.Add(new Claim(ClaimTypes.Role, role));
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
