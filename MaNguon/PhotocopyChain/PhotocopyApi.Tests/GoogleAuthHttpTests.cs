using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public class GoogleAuthHttpTests
{
    [Fact]
    public async Task GoogleCauHinhVaTokenSai_KhongGoiSql()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddMemoryCache();
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("google-auth",
            limiter => { limiter.PermitLimit = 30; limiter.Window = TimeSpan.FromMinutes(10); }));
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();
        string url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(url) };
        using var cfg = await http.GetAsync("/api/auth/google/config");
        Assert.Equal(HttpStatusCode.OK, cfg.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await cfg.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("daBat").GetBoolean());
        foreach (string path in new[] { "/api/auth/google", "/api/auth/admin/google" })
        {
            using var missing = await http.PostAsJsonAsync(path, new { idToken = "" });
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            using var disabled = await http.PostAsJsonAsync(path, new { idToken = "fixture-token-not-google", ghiNho = true });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, disabled.StatusCode);
        }
        using var invalidRegister = await http.PostAsJsonAsync("/api/auth/google/register",
            new { veDangKy = "ticket", hoTen = "A", soDienThoai = "09123", dongYDieuKhoan = false });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRegister.StatusCode);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string> {
            ["GoogleAuth:Enabled"] = "true",
            ["GoogleAuth:WebClientId"] = "fixture-client.apps.googleusercontent.com"
        });
        using var enabled = await http.GetFromJsonAsync<System.Text.Json.JsonDocument>("/api/auth/google/config");
        Assert.True(enabled.RootElement.GetProperty("daBat").GetBoolean());
        foreach (string path in new[] { "/api/auth/google", "/api/auth/admin/google" })
        {
            using var invalidToken = await http.PostAsJsonAsync(path, new { idToken = "fixture-invalid-google-token", vaiTro = "SUPER_ADMIN", email = "forged@gmail.com" });
            Assert.Equal(HttpStatusCode.Unauthorized, invalidToken.StatusCode);
        }
        using var missingTicket = await http.PostAsJsonAsync("/api/auth/google/register", new {
            veDangKy = new string('A', 64), hoTen = "Khách thử", soDienThoai = "0912345678", dongYDieuKhoan = true,
            vaiTro = "SUPER_ADMIN", email = "forged@gmail.com"
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingTicket.StatusCode);
        using var missingJson = System.Text.Json.JsonDocument.Parse(await missingTicket.Content.ReadAsStringAsync());
        Assert.Contains("hết hạn hoặc đã dùng", missingJson.RootElement.GetProperty("message").GetString());
        await app.StopAsync();
    }
}
