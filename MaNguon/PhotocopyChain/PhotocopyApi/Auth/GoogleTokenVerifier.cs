using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PhotocopyShared.KiemTraDuLieu;

namespace PhotocopyApi.Auth;

public sealed record GoogleDanhTinh(string Subject, string Email, string HoTen);

public interface IGoogleTokenVerifier
{
    Task<GoogleDanhTinh> XacMinhAsync(string idToken, CancellationToken dung = default);
}

// Dùng thư viện JWT/OIDC đang có trong API, không tự viết thuật toán xác minh chữ ký.
public sealed class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private readonly IConfiguration _config;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _khoaGoogle;

    public GoogleTokenVerifier(IConfiguration config, IConfigurationManager<OpenIdConnectConfiguration> khoaGoogle = null)
    {
        _config = config;
        _khoaGoogle = khoaGoogle ?? new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://accounts.google.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = true });
    }

    public async Task<GoogleDanhTinh> XacMinhAsync(string idToken, CancellationToken dung = default)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 8192 };
        if (!handler.CanReadToken(idToken)) throw new SecurityTokenValidationException("Invalid Google token.");
        for (int lan = 0; lan < 2; lan++)
        {
            var metadata = await _khoaGoogle.GetConfigurationAsync(dung);
            var quyTac = new TokenValidationParameters {
                ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                IssuerSigningKeys = metadata.SigningKeys,
                ValidateIssuer = true, ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                ValidateAudience = true, RequireAudience = true, ValidAudience = _config["GoogleAuth:WebClientId"],
                ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            };
            try
            {
                var claims = handler.ValidateToken(idToken, quyTac, out _);
                string subject = claims.FindFirst("sub")?.Value;
                string email = claims.FindFirst("email")?.Value;
                if (!bool.TryParse(claims.FindFirst("email_verified")?.Value, out bool verified) || !verified
                    || string.IsNullOrWhiteSpace(subject) || subject.Length > 255 || subject.Any(char.IsControl)
                    || string.IsNullOrWhiteSpace(email) || !new GmailAttribute().IsValid(email))
                    throw new SecurityTokenValidationException("Verified Gmail and Google subject are required.");
                return new(subject, email.ToLowerInvariant(), claims.FindFirst("name")?.Value ?? "");
            }
            catch (SecurityTokenSignatureKeyNotFoundException) when (lan == 0)
            {
                // Khóa Google có thể vừa xoay. ConfigurationManager giới hạn tần suất refresh.
                _khoaGoogle.RequestRefresh();
            }
        }
        throw new SecurityTokenValidationException("Invalid Google signing key.");
    }
}
