using System.Net;
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
using PhotocopyApi.DichVu;
using Xunit;

namespace PhotocopyApi.Tests;

// API HTTP thật, xác thực fixture riêng; chỉ đọc mẫu, không cấu hình/kết nối database.
public class MauDonHttpTests
{
    [Fact]
    public async Task XemPdfAnhTrangDungFileVaDungQuyen()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication("mau-test").AddScheme<AuthenticationSchemeOptions, PhienMau>("mau-test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(FileInController).Assembly);
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(url) };
        foreach (string role in new[] { "", "SHIPPER", "CUSTOMER", "STAFF", "BRANCH_ADMIN", "SUPER_ADMIN" })
        {
            http.DefaultRequestHeaders.Remove("X-Test-Role");
            if (role != "") http.DefaultRequestHeaders.Add("X-Test-Role", role);
            var status = role == "" ? HttpStatusCode.Unauthorized : role == "SHIPPER" ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
            foreach (string path in new[] { "", "/DON_XIN_VIEC/pdf", "/DON_XIN_VIEC/trang/1" })
            {
                using var res = await http.GetAsync("api/file-in/mau" + path);
                Assert.Equal(status, res.StatusCode);
            }
            if (status != HttpStatusCode.OK)
            {
                using var res = await http.PostAsync("api/file-in/mau/DON_XIN_VIEC", null);
                Assert.Equal(status, res.StatusCode);
            }
        }
        foreach (var mau in MauDonPdf.DanhSach)
        {
            using var res = await http.GetAsync("api/file-in/mau/" + mau.MaMau + "/pdf");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal(MauDonPdf.Tao(mau.MaMau), await res.Content.ReadAsByteArrayAsync());
            for (int page = 1; page <= mau.SoTrang; page++)
            {
                using var image = await http.GetAsync($"api/file-in/mau/{mau.MaMau}/trang/{page}");
                Assert.Equal(HttpStatusCode.OK, image.StatusCode);
                byte[] bytes = await image.Content.ReadAsByteArrayAsync();
                Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8));
                Assert.InRange(bytes.Length, 100, 4 * 1024 * 1024);
            }
        }
        await app.StopAsync();
    }

    public sealed class PhienMau : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public PhienMau(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string role = Request.Headers["X-Test-Role"];
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name)));
        }
    }
}
