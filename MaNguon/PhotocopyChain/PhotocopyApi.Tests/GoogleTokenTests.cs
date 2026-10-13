using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public class GoogleTokenTests
{
    private const string Client = "123456-test.apps.googleusercontent.com";
    [Theory]
    [InlineData("hop-le")]
    [InlineData("sai-audience")]
    [InlineData("sai-issuer")]
    [InlineData("het-han")]
    [InlineData("sai-chu-ky")]
    [InlineData("khong-chu-ky")]
    [InlineData("email-chua-xac-minh")]
    [InlineData("khong-gmail")]
    [InlineData("thieu-sub")]
    public async Task TokenGoogle_KiemTraChuKyVaClaimsTruocKhiTinDanhTinh(string truongHop)
    {
        using var rsa = RSA.Create(2048);
        using var khac = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "google-fixture" };
        var signingKey = truongHop == "sai-chu-ky" ? new RsaSecurityKey(khac) { KeyId = key.KeyId } : key;
        var claims = new List<Claim> {
            new("email", truongHop == "khong-gmail" ? "someone@example.org" : "tester@gmail.com"),
            new("email_verified", truongHop == "email-chua-xac-minh" ? "false" : "true", ClaimValueTypes.Boolean),
            new("name", "Người dùng kiểm thử")
        };
        if (truongHop != "thieu-sub") claims.Add(new("sub", "google-subject-123"));
        var jwt = new JwtSecurityToken(
            issuer: truongHop == "sai-issuer" ? "https://attacker.example" : "https://accounts.google.com",
            audience: truongHop == "sai-audience" ? "other.apps.googleusercontent.com" : Client,
            claims: claims, notBefore: DateTime.UtcNow.AddHours(-2),
            expires: truongHop == "het-han" ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(10),
            signingCredentials: truongHop == "khong-chu-ky" ? null : new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));
        string token = new JwtSecurityTokenHandler().WriteToken(jwt);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> {
            ["GoogleAuth:WebClientId"] = Client
        }).Build();
        var type = typeof(AuthController).Assembly.GetType("PhotocopyApi.Auth.GoogleTokenVerifier");
        Assert.NotNull(type);
        var manager = new KhoaGoogle(key);
        var verifier = Activator.CreateInstance(type, config, manager);
        var method = type.GetMethod("XacMinhAsync");
        Assert.NotNull(method);
        async Task<object> Verify()
        {
            var task = (Task)method.Invoke(verifier, new object[] { token, CancellationToken.None });
            await task;
            return task.GetType().GetProperty("Result").GetValue(task);
        }
        if (truongHop != "hop-le") { await Assert.ThrowsAnyAsync<SecurityTokenException>(Verify); return; }
        object result = await Verify();
        Assert.Equal("google-subject-123", result.GetType().GetProperty("Subject").GetValue(result));
        Assert.Equal("tester@gmail.com", result.GetType().GetProperty("Email").GetValue(result));
    }

    [Theory]
    [InlineData("SUPER_ADMIN", "HOAT_DONG", true, false)]
    [InlineData("BRANCH_ADMIN", "HOAT_DONG", true, false)]
    [InlineData("STAFF", "HOAT_DONG", true, false)]
    [InlineData("CUSTOMER", "HOAT_DONG", true, true)]
    [InlineData("SHIPPER", "HOAT_DONG", true, true)]
    [InlineData("STAFF", "HOAT_DONG", false, true)]
    [InlineData("CUSTOMER", "HOAT_DONG", false, false)]
    [InlineData("SHIPPER", "HOAT_DONG", false, false)]
    [InlineData("SUPER_ADMIN", "KHOA", true, true)]
    [InlineData("CUSTOMER", "KHOA", false, true)]
    [InlineData("UNKNOWN", "HOAT_DONG", true, true)]
    public void Google_KhongMoRongQuyenVaiTroCu(string role, string status, bool web, bool error)
    {
        var type = typeof(AuthController).Assembly.GetType("PhotocopyApi.Auth.QuyTacDangNhap");
        Assert.NotNull(type);
        Assert.Equal(error, type.GetMethod("KiemTraQuyen").Invoke(null, new object[] { role, status, web }) != null);
    }

    private sealed class KhoaGoogle : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private readonly OpenIdConnectConfiguration _config = new();
        public KhoaGoogle(SecurityKey key) => _config.SigningKeys.Add(key);
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) => Task.FromResult(_config);
        public void RequestRefresh() { }
    }
}
