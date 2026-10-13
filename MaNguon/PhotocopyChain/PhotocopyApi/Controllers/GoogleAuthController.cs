using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using PhotocopyApi.Auth;
using PhotocopyApi.Models;
using PhotocopyShared.Dtos.Auth;

namespace PhotocopyApi.Controllers;

public partial class AuthController
{
    [HttpGet("google/config")]
    public IActionResult CauHinhGoogle()
    {
        bool daBat = GoogleDaBat();
        return Ok(new GoogleCongKhaiDto { DaBat = daBat, WebClientId = daBat ? _config["GoogleAuth:WebClientId"] : "" });
    }

    [HttpPost("admin/google")]
    [EnableRateLimiting("google-auth")]
    public Task<IActionResult> DangNhapGoogleWeb(DangNhapGoogleRequest req) => DangNhapGoogleAsync(req, true);

    [HttpPost("google")]
    [EnableRateLimiting("google-auth")]
    public Task<IActionResult> DangNhapGoogleApp(DangNhapGoogleRequest req) => DangNhapGoogleAsync(req, false);

    private bool GoogleDaBat() => _config.GetValue<bool>("GoogleAuth:Enabled")
        && Regex.IsMatch(_config["GoogleAuth:WebClientId"] ?? "", @"^[A-Za-z0-9-]+\.apps\.googleusercontent\.com$");

    private async Task<IActionResult> DangNhapGoogleAsync(DangNhapGoogleRequest req, bool laWeb)
    {
        if (!GoogleDaBat()) return StatusCode(503, new { message = "Chưa cấu hình đăng nhập Google. Vui lòng dùng tài khoản và mật khẩu." });
        GoogleDanhTinh google;
        try { google = await _google.XacMinhAsync(req.IdToken, HttpContext.RequestAborted); }
        catch (SecurityTokenException) { return Unauthorized(new { message = "Google không xác thực được tài khoản Gmail. Vui lòng chọn lại tài khoản và thử lại." }); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return StatusCode(502, new { message = "Không kết nối được dịch vụ xác thực Google. Vui lòng thử lại." });
        }

        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable);
        var nd = await conn.QueryFirstOrDefaultAsync<NguoiDung>(
            @"SELECT nd.* FROM LienKetTaiKhoanGoogle g WITH (UPDLOCK,HOLDLOCK)
              JOIN NguoiDung nd WITH (UPDLOCK,HOLDLOCK) ON nd.MaNguoiDung=g.MaNguoiDung
              WHERE g.GoogleSubject=@Subject", new { google.Subject }, tx);
        if (nd == null)
            nd = await conn.QueryFirstOrDefaultAsync<NguoiDung>(
                "SELECT * FROM NguoiDung WITH (UPDLOCK,HOLDLOCK) WHERE Email=@Email", new { google.Email }, tx);
        if (nd == null)
        {
            if (laWeb) return StatusCode(403, new { message = "Email Google này chưa được cấp tài khoản nhân sự. Liên hệ CEO hoặc quản lý." });
            string ve = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _cache.Set("google-dang-ky:" + ve, new GoogleDangKyTam(google), TimeSpan.FromMinutes(10));
            return Ok(new BoSungGoogleDto { VeDangKy = ve, Email = google.Email, HoTen = google.HoTen.Length <= 100 ? google.HoTen : google.HoTen[..100] });
        }
        string loi = QuyTacDangNhap.KiemTraQuyen(nd.VaiTro, nd.TrangThai, laWeb);
        if (loi != null) return StatusCode(403, new { message = loi });
        string subjectCu = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT GoogleSubject FROM LienKetTaiKhoanGoogle WITH (UPDLOCK,HOLDLOCK) WHERE MaNguoiDung=@MaNguoiDung",
            new { nd.MaNguoiDung }, tx);
        if (subjectCu != null && subjectCu != google.Subject)
            return Conflict(new { message = "Tài khoản đã liên kết với một tài khoản Google khác. Vui lòng liên hệ quản trị viên." });
        if (subjectCu == null)
            await conn.ExecuteAsync("INSERT INTO LienKetTaiKhoanGoogle(GoogleSubject,MaNguoiDung) VALUES(@Subject,@MaNguoiDung)",
                new { google.Subject, nd.MaNguoiDung }, tx);
        await tx.CommitAsync();
        return await HoanTatDangNhapAsync(conn, nd, laWeb, req.GhiNho);
    }

    [HttpPost("google/register")]
    [EnableRateLimiting("google-auth")]
    public async Task<IActionResult> DangKyGoogle(DangKyGoogleRequest req)
    {
        if (!GoogleDaBat()) return StatusCode(503, new { message = "Chưa cấu hình đăng nhập Google." });
        if (!_cache.TryGetValue("google-dang-ky:" + req.VeDangKy, out GoogleDangKyTam tam))
            return BadRequest(new { message = "Phiên đăng ký Google đã hết hạn hoặc đã dùng. Vui lòng chọn Google lại." });
        lock (tam)
        {
            if (tam.DangXuLy) return Conflict(new { message = "Đăng ký đang được xử lý. Vui lòng đợi." });
            tam.DangXuLy = true;
        }
        try
        {
            using var conn = new SqlConnection(_chuoiKetNoi);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable);
            // Giữ cùng thứ tự khóa với đăng nhập: liên kết Google rồi đến tài khoản.
            int? lienKetCu = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT MaNguoiDung FROM LienKetTaiKhoanGoogle WITH (UPDLOCK,HOLDLOCK) WHERE GoogleSubject=@Subject",
                new { tam.Google.Subject }, tx);
            int? taiKhoanCu = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT MaNguoiDung FROM NguoiDung WITH (UPDLOCK,HOLDLOCK) WHERE Email=@Email", new { tam.Google.Email }, tx);
            if (lienKetCu != null || taiKhoanCu != null)
                return Conflict(new { message = "Tài khoản đã tồn tại. Vui lòng quay lại đăng nhập Google." });
            bool sdtTrung = await conn.ExecuteScalarAsync<bool>(
                "SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM NguoiDung WITH (UPDLOCK,HOLDLOCK) WHERE SoDienThoai=@SoDienThoai) THEN 1 ELSE 0 END AS BIT)",
                new { req.SoDienThoai }, tx);
            if (sdtTrung) return Conflict(new { message = "Số điện thoại này đã được đăng ký. Hãy nhập số khác hoặc dùng tài khoản cũ." });
            int ma = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO NguoiDung(HoTen,Email,SoDienThoai,MatKhau,VaiTro,TrangThai)
                  VALUES(@HoTen,@Email,@SoDienThoai,NULL,'CUSTOMER',N'HOAT_DONG');
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { HoTen = req.HoTen.Trim(), tam.Google.Email, req.SoDienThoai }, tx);
            await conn.ExecuteAsync("INSERT INTO LienKetTaiKhoanGoogle(GoogleSubject,MaNguoiDung) VALUES(@Subject,@MaNguoiDung)",
                new { tam.Google.Subject, MaNguoiDung = ma }, tx);
            await tx.CommitAsync();
            _cache.Remove("google-dang-ky:" + req.VeDangKy);
            return Ok(TaoToken(await DocHoSo(conn, ma), false, req.GhiNho));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new { message = "Tài khoản vừa được tạo ở yêu cầu khác. Vui lòng quay lại đăng nhập." });
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return Conflict(new { message = "Có yêu cầu đăng ký đồng thời. Vui lòng thử lại; chưa tạo tài khoản từ yêu cầu này." });
        }
        finally { lock (tam) tam.DangXuLy = false; }
    }

    private sealed class GoogleDangKyTam(GoogleDanhTinh google)
    {
        public GoogleDanhTinh Google { get; } = google;
        public bool DangXuLy { get; set; }
    }
}
