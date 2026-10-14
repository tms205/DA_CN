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

// Host riêng, token giả chỉ có trong test. Không kết nối CSDL hay dịch vụ bên ngoài.
public class GiaoHangHttpTests
{
    [Theory]
    [InlineData(null, "GET", "/api/giao-hang", HttpStatusCode.Unauthorized)]
    [InlineData("CUSTOMER", "GET", "/api/giao-hang", HttpStatusCode.Forbidden)]
    [InlineData("STAFF", "POST", "/api/giao-hang/luot/1/ket-qua", HttpStatusCode.Forbidden)]
    [InlineData("SHIPPER", "POST", "/api/giao-hang/1/phan-cong", HttpStatusCode.Forbidden)]
    [InlineData("STAFF", "POST", "/api/giao-hang/dia-chi", HttpStatusCode.Forbidden)]
    [InlineData("BRANCH_ADMIN", "GET", "/api/giao-hang", HttpStatusCode.Forbidden)]
    [InlineData("CUSTOMER", "POST", "/api/giao-hang/dia-chi", HttpStatusCode.BadRequest)]
    [InlineData("SHIPPER", "POST", "/api/giao-hang/luot/1/ket-qua", HttpStatusCode.BadRequest)]
    public async Task Middleware_ChanSaiQuyenVaDuLieuTruocKhiGoiDatabase(string role, string method, string path, HttpStatusCode status)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(GiaoHangController).Assembly);
        builder.Services.AddMemoryCache();
        builder.Services.AddSignalR();
        builder.Services.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions, XacThucFixture>("fixture", _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try
        {
            string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
            using var http = new HttpClient { BaseAddress = new Uri(address) };
            if (role != null) http.DefaultRequestHeaders.Add("X-Fixture-Role", role);
            using var req = new HttpRequestMessage(new HttpMethod(method), path);
            if (method == "POST") req.Content = JsonContent.Create(new { });
            using var res = await http.SendAsync(req);
            Assert.Equal(status, res.StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    private sealed class XacThucFixture : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public XacThucFixture(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string role = Request.Headers["X-Fixture-Role"];
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(TenClaim.MaNguoiDung, "1"), new Claim(TenClaim.VaiTro, role) },
                Scheme.Name, ClaimTypes.Name, TenClaim.VaiTro); // Không có chi nhánh: API phải từ chối.
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
