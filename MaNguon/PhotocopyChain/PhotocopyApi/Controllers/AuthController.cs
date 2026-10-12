using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.RateLimiting;
using PhotocopyApi.Auth;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using PhotocopyApi.CaLamViec;
using PhotocopyApi.DuLieu;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.Auth;

namespace PhotocopyApi.Controllers;

// F-01 → F-05: đăng nhập, đăng ký (có OTP), quên mật khẩu, hồ sơ cá nhân, đổi mật khẩu
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;             // bộ nhớ tạm: giữ OTP, đếm số lần sai mật khẩu
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AuthController> _logger;
    private readonly OtpDatLaiStore _otpDatLai;

    public AuthController(IConfiguration config, IMemoryCache cache, IWebHostEnvironment env, ILogger<AuthController> logger, OtpDatLaiStore otpDatLai = null)
    {
        _config = config;
        _cache = cache;
        _env = env;
        _logger = logger;
        _otpDatLai = otpDatLai ?? new OtpDatLaiStore(cache, TimeProvider.System);
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    // ===================================================================== ĐĂNG NHẬP (F-02)

    // POST api/auth/login — đăng nhập trên App: chỉ Khách hàng và Shipper
    [HttpPost("login")]
    public async Task<IActionResult> DangNhapApp(DangNhapRequest req)
    {
        return await DangNhap(req, false);
    }

    // POST api/auth/admin/login — đăng nhập Web quản trị: chỉ Super Admin, Quản lý chi nhánh, Nhân viên in
    [HttpPost("admin/login")]
    public async Task<IActionResult> DangNhapWeb(DangNhapRequest req)
    {
        return await DangNhap(req, true);
    }

    // Phần xử lý chung của 2 hàm đăng nhập ở trên
    private async Task<IActionResult> DangNhap(DangNhapRequest req, bool laWeb)
    {
        string taiKhoan = req.TaiKhoan.Trim();

        // 1. Sai mật khẩu 5 lần liên tiếp thì bị khóa 15 phút
        if (_cache.TryGetValue("khoa-dang-nhap:" + taiKhoan.ToLower(), out DateTime khoaDen))
        {
            int soPhut = (int)Math.Ceiling((khoaDen - DateTime.Now).TotalMinutes);
            return StatusCode(403, new { message = $"Tài khoản tạm khóa do nhập sai mật khẩu 5 lần. Thử lại sau {soPhut} phút." });
        }

        // 2. Tìm tài khoản theo email hoặc số điện thoại
        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = (await conn.QueryAsync<NguoiDung>(
            "SELECT * FROM NguoiDung WHERE Email = @TaiKhoan OR SoDienThoai = @TaiKhoan",
            new { TaiKhoan = taiKhoan })).ToList();

        // Cột SoDienThoai không bắt buộc duy nhất nên 1 số có thể ra nhiều tài khoản
        if (danhSach.Count > 1)
            return BadRequest(new { message = "Số điện thoại này gắn với nhiều tài khoản, vui lòng đăng nhập bằng email." });

        // 3. Kiểm tra mật khẩu
        NguoiDung nd = danhSach.FirstOrDefault();
        if (nd == null || !KiemTraMatKhau(req.MatKhau, nd.MatKhau))
        {
            GhiNhanSaiMatKhau(taiKhoan.ToLower());
            return Unauthorized(new { message = "Thông tin đăng nhập không chính xác" });
        }
        _cache.Remove("sai-mat-khau:" + taiKhoan.ToLower());   // đúng rồi thì đếm lại từ đầu

        // 4. Kiểm tra tài khoản có bị khóa không, có đăng nhập đúng nơi không
        //    (kiểm SAU khi đúng mật khẩu để người đoán mò không biết tài khoản thuộc vai trò nào)
        if (nd.TrangThai != "HOAT_DONG")
            return StatusCode(403, new { message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." });

        bool laKhachHoacShipper = nd.VaiTro == "CUSTOMER" || nd.VaiTro == "SHIPPER";
        if (laWeb && laKhachHoacShipper)
            return StatusCode(403, new { message = "Khách hàng và Shipper vui lòng đăng nhập trên ứng dụng di động PhotocopyChain App." });
        if (!laWeb && !laKhachHoacShipper)
            return StatusCode(403, new { message = "Tài khoản nhân viên vui lòng đăng nhập tại Web quản trị." });

        // 5. Nhân viên đăng nhập Web được tự mở ca với tiền đầu ca mặc định 200.000đ.
        // sp_MoCa trả lại ca hiện có nếu nhân viên đăng nhập lại, nên không tạo trùng.
        if (QuyTacCaDangNhap.CanTuDongMoCa(laWeb, nd.VaiTro))
        {
            var thamSo = new DynamicParameters();
            thamSo.Add("@MaNhanVien", nd.MaNguoiDung);
            thamSo.Add("@TienDauCa", QuyTacCaDangNhap.TienDauCaMacDinh);
            thamSo.Add("@MaChiNhanhThayThe", null);
            thamSo.Add("@MaCa", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

            try
            {
                await conn.ExecuteAsync("sp_MoCa", thamSo, commandType: System.Data.CommandType.StoredProcedure);
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "Không thể tự mở ca khi nhân viên {MaNhanVien} đăng nhập.", nd.MaNguoiDung);
                return Conflict(new { message = "Không thể tự mở ca làm việc: " + ex.Message });
            }
        }

        // 6. Tạo token trả về cho client
        NguoiDungDto hoSo = await DocHoSo(conn, nd.MaNguoiDung);
        return Ok(TaoToken(hoSo, laWeb, req.GhiNho));
    }

    // ===================================================================== ĐĂNG KÝ (F-01)

    // POST api/auth/register/otp — Đăng ký bước 1: kiểm tra trùng, gửi mã OTP qua email (CHƯA tạo tài khoản)
    [HttpPost("register/otp")]
    public async Task<IActionResult> GuiOtpDangKy(DangKyRequest req)
    {
        string email = req.Email.Trim().ToLower();

        // Nhập sai mã 5 lần thì bị khóa đăng ký 15 phút
        if (_cache.TryGetValue("khoa-dang-ky:" + email, out DateTime khoaDen))
        {
            int soPhut = (int)Math.Ceiling((khoaDen - DateTime.Now).TotalMinutes);
            return StatusCode(403, new { message = $"Nhập sai mã quá 5 lần. Thử đăng ký lại sau {soPhut} phút." });
        }

        // Vừa gửi mã xong thì phải đợi 60 giây mới được gửi lại
        if (_cache.TryGetValue("dang-ky:" + email, out DangKyTam cu) && cu.GuiLuc.AddSeconds(60) > DateTime.Now)
        {
            int soGiay = (int)Math.Ceiling((cu.GuiLuc.AddSeconds(60) - DateTime.Now).TotalSeconds);
            return StatusCode(429, new { message = $"Vui lòng đợi {soGiay} giây rồi gửi lại mã." });
        }

        // Báo trùng ngay từ bước này, khỏi bắt khách nhập mã xong mới biết
        using var conn = new SqlConnection(_chuoiKetNoi);
        string loiTrung = await KiemTraTrung(conn, req.Email.Trim(), req.SoDienThoai.Trim());
        if (loiTrung != null)
            return Conflict(new { message = loiTrung });

        // Giữ tạm thông tin đăng ký trong bộ nhớ 5 phút
        var dangKy = new DangKyTam
        {
            HoTen = req.HoTen.Trim(),
            Email = req.Email.Trim(),
            SoDienThoai = req.SoDienThoai.Trim(),
            MatKhauDaBam = BCrypt.Net.BCrypt.HashPassword(req.MatKhau),
            MaOtp = TaoMaOtp(),
            GuiLuc = DateTime.Now,
            SoLanNhapSai = 0
        };
        _cache.Set("dang-ky:" + email, dangKy, TimeSpan.FromMinutes(5));

        bool daGui = await GuiEmailOtp(dangKy.Email, dangKy.HoTen, dangKy.MaOtp, "kích hoạt tài khoản", 5);
        if (!daGui)
        {
            _cache.Remove("dang-ky:" + email);   // gửi lỗi thì cho bấm gửi lại ngay, không bắt đợi 60 giây
            return StatusCode(502, new { message = "Không gửi được email chứa mã OTP. Vui lòng thử lại sau." });
        }

        return Ok(new OtpDaGuiDto
        {
            Message = $"Đã gửi mã OTP 6 số tới {dangKy.Email}.",
            HetHanSauGiay = 5 * 60,    // App dùng 2 con số này để chạy đồng hồ đếm ngược
            GuiLaiSauGiay = 60
        });
    }

    // POST api/auth/register — Đăng ký bước 2: nhập đúng mã OTP thì mới tạo tài khoản khách hàng và đăng nhập luôn
    [HttpPost("register")]
    public async Task<IActionResult> XacNhanDangKy(XacNhanDangKyRequest req)
    {
        string email = req.Email.Trim().ToLower();

        // Quá 5 phút thì bộ nhớ tự xóa thông tin đăng ký
        if (!_cache.TryGetValue("dang-ky:" + email, out DangKyTam dangKy))
            return BadRequest(new { message = "Mã OTP đã hết hạn. Vui lòng bấm gửi lại mã." });

        if (req.MaOtp != dangKy.MaOtp)
        {
            dangKy.SoLanNhapSai++;
            if (dangKy.SoLanNhapSai >= 5)
            {
                _cache.Remove("dang-ky:" + email);
                _cache.Set("khoa-dang-ky:" + email, DateTime.Now.AddMinutes(15), TimeSpan.FromMinutes(15));
                return StatusCode(403, new { message = "Nhập sai mã quá 5 lần. Chức năng đăng ký bị khóa tạm 15 phút." });
            }
            return BadRequest(new { message = $"Mã OTP không đúng. Còn {5 - dangKy.SoLanNhapSai} lần thử." });
        }

        // Đúng mã → xóa khỏi bộ nhớ, kiểm tra trùng lần nữa (trong lúc chờ có thể người khác đã đăng ký) rồi thêm vào CSDL
        _cache.Remove("dang-ky:" + email);

        using var conn = new SqlConnection(_chuoiKetNoi);
        string loiTrung = await KiemTraTrung(conn, dangKy.Email, dangKy.SoDienThoai);
        if (loiTrung != null)
            return Conflict(new { message = loiTrung });

        string sql = @"INSERT INTO NguoiDung (HoTen, Email, SoDienThoai, MatKhau, VaiTro)
                       VALUES (@HoTen, @Email, @SoDienThoai, @MatKhau, 'CUSTOMER');
                       SELECT CAST(SCOPE_IDENTITY() AS INT);";
        int maMoi = await conn.ExecuteScalarAsync<int>(sql, new
        {
            HoTen = dangKy.HoTen,
            Email = dangKy.Email,
            SoDienThoai = dangKy.SoDienThoai,
            MatKhau = dangKy.MatKhauDaBam
        });

        NguoiDungDto hoSo = await DocHoSo(conn, maMoi);
        return Ok(TaoToken(hoSo, false, true));
    }

    // ===================================================================== QUÊN MẬT KHẨU (F-04)

    // POST api/auth/forgot-password — tài khoản đang hoạt động nhận OTP qua email.
    // Trả thông báo chung để không tiết lộ email có tồn tại hoặc đang bị khóa.
    [HttpPost("forgot-password")]
    [EnableRateLimiting("khoi-phuc-mat-khau")]
    public async Task<IActionResult> QuenMatKhau(QuenMatKhauRequest req)
    {
        string email = req.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(_config["Email:Host"])
            || string.IsNullOrWhiteSpace(_config["Email:TenDangNhap"])
            || string.IsNullOrWhiteSpace(_config["Email:MatKhau"]))
            return StatusCode(503, new { message = "Chưa cấu hình Gmail gửi mã xác thực. Vui lòng liên hệ quản trị viên." });

        int choGiay = _otpDatLai.ThuBatDauGui(email);
        if (choGiay > 0)
            return StatusCode(429, new { message = $"Vui lòng đợi {choGiay} giây rồi gửi lại mã." });

        bool daGui = false;
        try
        {
            using var conn = new SqlConnection(_chuoiKetNoi);
            NguoiDung nd = await conn.QueryFirstOrDefaultAsync<NguoiDung>(
                "SELECT * FROM NguoiDung WHERE Email = @Email AND TrangThai = 'HOAT_DONG'",
                new { Email = email });

            if (nd != null)
            {
                string maOtp = TaoMaOtp();
                daGui = await GuiEmailOtp(email, nd.HoTen, maOtp, "đặt lại mật khẩu", 10);
                if (!daGui)
                    return StatusCode(502, new { message = "Không gửi được email chứa mã OTP. Vui lòng thử lại sau." });
                _otpDatLai.LuuMa(email, maOtp);
            }

            return Ok(new OtpDaGuiDto
            {
                Message = "Nếu email thuộc tài khoản đang hoạt động, mã OTP đã được gửi. Hãy kiểm tra cả mục thư rác. Mã có hiệu lực 10 phút.",
                HetHanSauGiay = 10 * 60,
                GuiLaiSauGiay = 60
            });
        }
        finally
        {
            if (!daGui) _otpDatLai.HuyMa(email);
        }
    }

    // POST api/auth/forgot-password/verify-otp — xác minh trước khi hiện form mật khẩu.
    // Chưa tiêu thụ mã; reset-password vẫn kiểm tra lại hạn và đánh dấu đã dùng.
    [HttpPost("forgot-password/verify-otp")]
    [EnableRateLimiting("khoi-phuc-mat-khau")]
    public IActionResult XacMinhOtpDatLai(XacMinhOtpDatLaiRequest req)
    {
        string email = req.Email.Trim().ToLowerInvariant();
        if (!_otpDatLai.KiemTraMa(email, req.MaOtp))
            return BadRequest(new { message = "Mã OTP không đúng, đã sử dụng, hết hạn hoặc tạm khóa do nhập sai quá 5 lần." });

        return Ok(new KetQuaDto { Message = "Xác minh thành công. Bạn có thể đặt mật khẩu mới." });
    }

    // POST api/auth/reset-password — mã đúng chỉ được dùng một lần.
    [HttpPost("reset-password")]
    [EnableRateLimiting("khoi-phuc-mat-khau")]
    public async Task<IActionResult> DatLaiMatKhau(DatLaiMatKhauRequest req)
    {
        string email = req.Email.Trim().ToLowerInvariant();
        if (!_otpDatLai.KiemTraMa(email, req.MaOtp))
            return BadRequest(new { message = "Mã OTP không đúng, đã sử dụng, hết hạn hoặc tạm khóa do nhập sai quá 5 lần." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        NguoiDung nd = await conn.QueryFirstOrDefaultAsync<NguoiDung>(
            "SELECT * FROM NguoiDung WHERE Email = @Email AND TrangThai = 'HOAT_DONG'",
            new { Email = email });
        if (nd == null)
            return BadRequest(new { message = "Không thể đặt lại mật khẩu cho tài khoản này." });

        string loiMatKhau = KiemTraMatKhauMoi(req.MatKhauMoi, nd.MatKhau);
        if (loiMatKhau != null)
            return Conflict(new { message = loiMatKhau });

        // Chỉ dùng mã sau khi mật khẩu hợp lệ; vẫn chặn hai request dùng cùng OTP.
        if (!_otpDatLai.DungMa(email, req.MaOtp))
            return BadRequest(new { message = "Mã OTP không đúng, đã sử dụng, hết hạn hoặc tạm khóa do nhập sai quá 5 lần." });

        int soDong = await conn.ExecuteAsync(
            "UPDATE NguoiDung SET MatKhau = @MatKhau WHERE MaNguoiDung = @MaNguoiDung AND TrangThai = 'HOAT_DONG'",
            new { MatKhau = BCrypt.Net.BCrypt.HashPassword(req.MatKhauMoi), MaNguoiDung = nd.MaNguoiDung });
        if (soDong != 1)
            return BadRequest(new { message = "Tài khoản không còn hoạt động. Vui lòng liên hệ quản trị viên." });

        _cache.Remove("khoa-dang-nhap:" + email);
        _cache.Remove("sai-mat-khau:" + email);
        if (!string.IsNullOrWhiteSpace(nd.SoDienThoai))
        {
            _cache.Remove("khoa-dang-nhap:" + nd.SoDienThoai);
            _cache.Remove("sai-mat-khau:" + nd.SoDienThoai);
        }

        return Ok(new KetQuaDto { Message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới." });
    }

    // ===================================================================== HỒ SƠ CÁ NHÂN (F-03)

    // GET api/auth/me — xem hồ sơ của người đang đăng nhập
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> XemHoSo()
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);
        NguoiDungDto hoSo = await DocHoSo(conn, maNguoiDung);
        if (hoSo == null)
            return NotFound(new { message = "Không tìm thấy tài khoản." });

        return Ok(hoSo);
    }

    // PUT api/auth/me — sửa họ tên, số điện thoại; khách hàng sửa thêm địa chỉ mặc định
    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> CapNhatHoSo(CapNhatHoSoRequest req)
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string soDienThoai = string.IsNullOrWhiteSpace(req.SoDienThoai) ? null : req.SoDienThoai.Trim();
        string diaChi = string.IsNullOrWhiteSpace(req.DiaChiMacDinh) ? null : req.DiaChiMacDinh.Trim();

        using var conn = new SqlConnection(_chuoiKetNoi);

        // Số điện thoại không được trùng với tài khoản khác
        if (soDienThoai != null)
        {
            int soLuong = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM NguoiDung WHERE SoDienThoai = @SoDienThoai AND MaNguoiDung <> @MaNguoiDung",
                new { SoDienThoai = soDienThoai, MaNguoiDung = maNguoiDung });
            if (soLuong > 0)
                return BadRequest(new { message = "Số điện thoại này đã được tài khoản khác sử dụng." });
        }

        // Chỉ khách hàng mới có địa chỉ mặc định, nhân viên thì giữ nguyên cột DiaChi
        string sql = @"UPDATE NguoiDung
                       SET HoTen = @HoTen, SoDienThoai = @SoDienThoai,
                           DiaChi = CASE WHEN VaiTro = 'CUSTOMER' THEN @DiaChi ELSE DiaChi END
                       WHERE MaNguoiDung = @MaNguoiDung";
        await conn.ExecuteAsync(sql, new
        {
            HoTen = req.HoTen.Trim(),
            SoDienThoai = soDienThoai,
            DiaChi = diaChi,
            MaNguoiDung = maNguoiDung
        });

        return Ok(await DocHoSo(conn, maNguoiDung));
    }

    // PUT api/auth/me/password — đổi mật khẩu (phải nhập đúng mật khẩu hiện tại)
    [Authorize]
    [HttpPut("me/password")]
    public async Task<IActionResult> DoiMatKhau(DoiMatKhauRequest req)
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);
        string matKhauHienTai = await conn.ExecuteScalarAsync<string>(
            "SELECT MatKhau FROM NguoiDung WHERE MaNguoiDung = @MaNguoiDung", new { MaNguoiDung = maNguoiDung });

        if (!KiemTraMatKhau(req.MatKhauCu, matKhauHienTai))
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
        string loiMatKhau = KiemTraMatKhauMoi(req.MatKhauMoi, matKhauHienTai);
        if (loiMatKhau != null)
            return Conflict(new { message = loiMatKhau });

        await conn.ExecuteAsync("UPDATE NguoiDung SET MatKhau = @MatKhau WHERE MaNguoiDung = @MaNguoiDung",
            new { MatKhau = BCrypt.Net.BCrypt.HashPassword(req.MatKhauMoi), MaNguoiDung = maNguoiDung });

        return Ok(new KetQuaDto { Message = "Đã đổi mật khẩu." });
    }

    // ===================================================================== CÁC HÀM PHỤ

    // Đọc hồ sơ để trả về cho Web / App (kèm tên và địa chỉ chi nhánh công tác)
    private async Task<NguoiDungDto> DocHoSo(SqlConnection conn, int maNguoiDung)
    {
        return await conn.QueryFirstOrDefaultAsync<NguoiDungDto>(
            TruyVanSql.NguoiDung.HoSo,
            new { MaNguoiDung = maNguoiDung });
    }

    // Tạo JWT: trong token có mã người dùng, họ tên, email, vai trò, chi nhánh (đặc tả F-02)
    private DangNhapResponse TaoToken(NguoiDungDto nd, bool laWeb, bool ghiNho)
    {
        // Thời hạn: Web 8 giờ (1 ca làm); App 12 giờ, bấm "Ghi nhớ đăng nhập" thì 30 ngày
        DateTime hetHan;
        if (laWeb)
            hetHan = DateTime.UtcNow.AddHours(_config.GetValue<int>("Jwt:SoGioWeb"));
        else if (ghiNho)
            hetHan = DateTime.UtcNow.AddDays(_config.GetValue<int>("Jwt:SoNgayMobileGhiNho"));
        else
            hetHan = DateTime.UtcNow.AddHours(_config.GetValue<int>("Jwt:SoGioMobile"));

        var claims = new List<Claim>();
        claims.Add(new Claim(TenClaim.MaNguoiDung, nd.MaNguoiDung.ToString()));
        claims.Add(new Claim(TenClaim.HoTen, nd.HoTen));
        claims.Add(new Claim(TenClaim.Email, nd.Email));
        claims.Add(new Claim(TenClaim.VaiTro, nd.TenVaiTro));
        if (nd.MaChiNhanh != null)
            claims.Add(new Claim(TenClaim.MaChiNhanh, nd.MaChiNhanh.ToString()));

        var khoa = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: hetHan,
            signingCredentials: new SigningCredentials(khoa, SecurityAlgorithms.HmacSha256));

        return new DangNhapResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            HetHanLuc = hetHan,
            NguoiDung = nd
        };
    }

    // Dùng chung cho đổi mật khẩu và quên mật khẩu; không so sánh hai hash có salt khác nhau.
    private string KiemTraMatKhauMoi(string matKhauMoi, string matKhauDaBam)
    {
        if (KiemTraMatKhau(matKhauMoi, matKhauDaBam))
            return "Mật khẩu mới trùng với mật khẩu hiện tại. Vui lòng chọn mật khẩu khác.";
        return null;
    }

    // So mật khẩu người dùng nhập với mật khẩu đã băm trong CSDL
    private bool KiemTraMatKhau(string matKhau, string matKhauDaBam)
    {
        if (string.IsNullOrEmpty(matKhauDaBam))
            return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(matKhau, matKhauDaBam);
        }
        catch
        {
            return false;   // chuỗi trong CSDL không phải dạng BCrypt → coi như sai
        }
    }

    // Đếm số lần nhập sai mật khẩu, đủ 5 lần thì khóa 15 phút
    private void GhiNhanSaiMatKhau(string taiKhoan)
    {
        _cache.TryGetValue("sai-mat-khau:" + taiKhoan, out int soLanSai);
        soLanSai++;

        if (soLanSai >= 5)
        {
            _cache.Set("khoa-dang-nhap:" + taiKhoan, DateTime.Now.AddMinutes(15), TimeSpan.FromMinutes(15));
            _cache.Remove("sai-mat-khau:" + taiKhoan);
        }
        else
        {
            _cache.Set("sai-mat-khau:" + taiKhoan, soLanSai, TimeSpan.FromMinutes(15));
        }
    }

    // Trả về câu báo lỗi nếu email / số điện thoại đã có người dùng, không trùng thì trả về null
    private async Task<string> KiemTraTrung(SqlConnection conn, string email, string soDienThoai)
    {
        int trungEmail = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM NguoiDung WHERE Email = @Email", new { Email = email });
        if (trungEmail > 0)
            return "Email này đã được đăng ký.";

        int trungSdt = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM NguoiDung WHERE SoDienThoai = @SoDienThoai", new { SoDienThoai = soDienThoai });
        if (trungSdt > 0)
            return "Số điện thoại này đã được đăng ký.";

        return null;
    }

    // Mã OTP gồm 6 chữ số ngẫu nhiên
    private string TaoMaOtp()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    // Gửi mã OTP qua Gmail. Cấu hình mục "Email" được nạp từ file .env riêng lúc khởi động.
    // Chưa cấu hình thì báo thất bại; không in OTP ra log và không giả báo đã gửi.
    private async Task<bool> GuiEmailOtp(string email, string hoTen, string maOtp, string viec, int soPhut)
    {
        string host = _config["Email:Host"];
        string tenDangNhap = _config["Email:TenDangNhap"];
        string matKhau = _config["Email:MatKhau"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(tenDangNhap) || string.IsNullOrWhiteSpace(matKhau))
            return false;

        using var thu = new MailMessage();
        thu.From = new MailAddress(tenDangNhap, _config["Email:TenNguoiGui"]);
        thu.To.Add(email);
        thu.Subject = $"{maOtp} là mã {viec} PhotocopyChain";
        thu.SubjectEncoding = Encoding.UTF8;
        thu.BodyEncoding = Encoding.UTF8;
        thu.IsBodyHtml = true;
        thu.Body = $@"
            <div style=""font-family:Arial,sans-serif;background:#FBF9F5;padding:24px"">
              <div style=""max-width:440px;margin:auto;background:#fff;border:1px solid #E6E0D8;border-radius:12px;padding:24px"">
                <h2 style=""color:#22437F;margin:0"">PhotocopyChain</h2>
                <p>Xin chào <b>{WebUtility.HtmlEncode(hoTen)}</b>,</p>
                <p>Mã OTP {viec} của bạn là:</p>
                <p style=""font-size:32px;font-weight:bold;letter-spacing:8px;text-align:center;background:#EDF0F5;padding:12px;border-radius:8px"">{maOtp}</p>
                <p style=""color:#837B72;font-size:13px"">Mã có hiệu lực trong {soPhut} phút. Không chia sẻ mã này cho bất kỳ ai.</p>
              </div>
            </div>";

        using var smtp = new SmtpClient(host, _config.GetValue<int>("Email:Port", 587));
        smtp.EnableSsl = true;
        smtp.UseDefaultCredentials = false;
        smtp.Credentials = new NetworkCredential(tenDangNhap, matKhau.Replace(" ", ""));

        try
        {
            using var hetHan = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await smtp.SendMailAsync(thu, hetHan.Token);
            return true;
        }
        catch (Exception ex)
        {
            // Hay gặp nhất: sai mật khẩu ứng dụng Gmail hoặc Gmail chưa bật xác minh 2 bước
            _logger.LogError(ex, "Gửi email OTP tới {Email} thất bại", email);
            return false;
        }
    }
}
