using System.ComponentModel.DataAnnotations;
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using PhotocopyApi.DuLieu;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.NhanVien;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

// F-24: danh sách nhân viên + F-04: nhân viên quên mật khẩu thì cấp trên cấp mật khẩu tạm.
// Chỉ Super Admin và Quản lý chi nhánh được vào.
[ApiController]
[Route("api/nhan-vien")]
[Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
public class NhanVienController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly IMemoryCache _cache;

    public NhanVienController(IConfiguration config, IMemoryCache cache)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
        _cache = cache;
    }

    // GET api/nhan-vien — Super Admin thấy toàn chuỗi, Quản lý chi nhánh chỉ thấy nhân viên chi nhánh mình
    [HttpGet]
    public async Task<IActionResult> DanhSach()
    {
        int maNguoiXem = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int? maChiNhanh = null;
        if (User.FindFirst(TenClaim.MaChiNhanh) != null)
            maChiNhanh = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);
        // Truy vấn tự tính cờ DuocCapLai để Web chỉ việc hiện/ẩn nút.
        var danhSach = await conn.QueryAsync<NhanVienDto>(TruyVanSql.NguoiDung.DanhSachNhanVien, new
        {
            MaNguoiXem = maNguoiXem,
            VaiTro = vaiTro,
            MaChiNhanh = maChiNhanh
        });
        return Ok(danhSach);
    }

    // POST api/nhan-vien/5/cap-lai-mat-khau — cấp mật khẩu tạm cho nhân viên quên mật khẩu.
    // Mật khẩu tạm chỉ trả về đúng 1 lần cho người cấp đọc cho nhân viên, CSDL chỉ lưu bản đã băm.
    [HttpPost("{maNhanVien}/cap-lai-mat-khau")]
    public async Task<IActionResult> CapLaiMatKhau([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maNhanVien)
    {
        int maNguoiCap = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string matKhauTam = TaoMatKhauTam();

        using var conn = new SqlConnection(_chuoiKetNoi);
        var claimChiNhanh = User.FindFirst(TenClaim.MaChiNhanh);
        if (claimChiNhanh != null)
        {
            int? maChiNhanhNhanVien = await conn.ExecuteScalarAsync<int?>(
                "SELECT MaChiNhanh FROM NguoiDung WHERE MaNguoiDung=@MaNhanVien", new { MaNhanVien = maNhanVien });
            if (maChiNhanhNhanVien != int.Parse(claimChiNhanh.Value))
                return StatusCode(403, new { message = "Nhân viên không thuộc phạm vi chi nhánh đang chọn." });
        }
        try
        {
            // Thủ tục sp_CapLaiMatKhau tự kiểm tra quyền (Quản lý CN chỉ cấp cho nhân viên chi nhánh mình...)
            await conn.ExecuteAsync("sp_CapLaiMatKhau", new
            {
                MaNhanVien = maNhanVien,
                MatKhauMoi = BCrypt.Net.BCrypt.HashPassword(matKhauTam),
                MaNguoiCap = maNguoiCap
            }, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            // Thủ tục báo lỗi bằng câu tiếng Việt (THROW) → trả nguyên câu đó cho Web
            return BadRequest(new { message = ex.Message });
        }

        NguoiDung nv = await conn.QueryFirstAsync<NguoiDung>(
            "SELECT * FROM NguoiDung WHERE MaNguoiDung = @MaNguoiDung", new { MaNguoiDung = maNhanVien });

        // Nhân viên có thể đang bị khóa vì nhập sai mật khẩu nhiều lần → mở khóa luôn
        // (tên khóa phải giống bên AuthController)
        _cache.Remove("khoa-dang-nhap:" + nv.Email.ToLower());
        _cache.Remove("sai-mat-khau:" + nv.Email.ToLower());
        if (nv.SoDienThoai != null)
        {
            _cache.Remove("khoa-dang-nhap:" + nv.SoDienThoai);
            _cache.Remove("sai-mat-khau:" + nv.SoDienThoai);
        }

        return Ok(new CapLaiMatKhauKetQuaDto
        {
            MatKhauTam = matKhauTam,
            Message = $"Đã cấp mật khẩu tạm cho {nv.HoTen}. Đọc trực tiếp cho nhân viên (mật khẩu chỉ hiện một lần) " +
                      "và nhắc nhân viên đổi mật khẩu ngay sau khi đăng nhập."
        });
    }

    // POST api/nhan-vien — tạo nhân viên mới; thủ tục tự kiểm phạm vi quyền.
    [HttpPost]
    public async Task<IActionResult> TaoNhanVien(TaoNhanVienRequest req)
    {
        int maNguoiTao = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string vaiTroNguoiTao = User.FindFirst(TenClaim.VaiTro).Value;
        int? maChiNhanh = req.MaChiNhanh;
        if (vaiTroNguoiTao == VaiTro.BranchAdmin)
            maChiNhanh = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);

        if (string.IsNullOrWhiteSpace(req.HoTen) || string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Vui lòng nhập họ tên và email nhân viên." });
        if (maChiNhanh == null)
            return BadRequest(new { message = "Vui lòng chọn chi nhánh cho nhân viên." });
        if (req.MatKhau.Length < 8 || !req.MatKhau.Any(char.IsUpper) || !req.MatKhau.Any(char.IsDigit))
            return BadRequest(new { message = "Mật khẩu tối thiểu 8 ký tự, có chữ hoa và số." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        if (await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM NguoiDung WHERE Email = @Email OR (@SoDienThoai IS NOT NULL AND SoDienThoai = @SoDienThoai)",
                new { Email = req.Email.Trim().ToLower(), SoDienThoai = string.IsNullOrWhiteSpace(req.SoDienThoai) ? null : req.SoDienThoai.Trim() }) > 0)
            return BadRequest(new { message = "Email hoặc số điện thoại đã được sử dụng." });

        try
        {
            await conn.ExecuteAsync("sp_TaoNhanVien", new
            {
                HoTen = req.HoTen.Trim(),
                Email = req.Email.Trim().ToLower(),
                SoDienThoai = string.IsNullOrWhiteSpace(req.SoDienThoai) ? null : req.SoDienThoai.Trim(),
                MatKhau = BCrypt.Net.BCrypt.HashPassword(req.MatKhau),
                VaiTro = req.VaiTro,
                MaChiNhanh = maChiNhanh.Value,
                MaNguoiTao = maNguoiTao
            }, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new { message = "Đã tạo tài khoản nhân viên." });
    }

    // PUT api/nhan-vien/5/trang-thai — khóa hoặc mở lại tài khoản trong đúng phạm vi quản lý.
    [HttpPut("{maNhanVien}/trang-thai")]
    public async Task<IActionResult> CapNhatTrangThai([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maNhanVien, CapNhatTrangThaiRequest req)
    {
        int maNguoiCap = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int? maChiNhanh = User.FindFirst(TenClaim.MaChiNhanh) == null
            ? null : int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        if (req.TrangThai != "HOAT_DONG" && req.TrangThai != "KHOA")
            return BadRequest(new { message = "Trạng thái tài khoản không hợp lệ." });
        if (maNhanVien == maNguoiCap)
            return BadRequest(new { message = "Bạn không thể tự khóa tài khoản của mình." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        int soDong = await conn.ExecuteAsync(
            @"UPDATE NguoiDung SET TrangThai = @TrangThai
              WHERE MaNguoiDung = @MaNhanVien AND VaiTro NOT IN ('CUSTOMER','SUPER_ADMIN')
                AND (@VaiTro = 'SUPER_ADMIN'
                     OR (VaiTro IN ('STAFF','SHIPPER') AND MaChiNhanh = @MaChiNhanh))",
            new { req.TrangThai, MaNhanVien = maNhanVien, VaiTro = vaiTro, MaChiNhanh = maChiNhanh });
        if (soDong == 0)
            return BadRequest(new { message = "Không thể đổi trạng thái tài khoản này." });
        return Ok(new { message = req.TrangThai == "KHOA" ? "Đã khóa tài khoản." : "Đã mở lại tài khoản." });
    }

    // Mật khẩu tạm 8 ký tự đúng quy tắc (có chữ hoa và số): 1 chữ hoa + 3 chữ thường + 4 số, vd "Kmxq4827".
    // Bỏ các ký tự dễ nhầm (I, l, O, 0, 1) vì quản lý đọc miệng cho nhân viên.
    private string TaoMatKhauTam()
    {
        string chuHoa = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        string chuThuong = "abcdefghijkmnpqrstuvwxyz";
        string chuSo = "23456789";

        string ketQua = "";
        ketQua += chuHoa[Random.Shared.Next(chuHoa.Length)];
        for (int i = 0; i < 3; i++)
            ketQua += chuThuong[Random.Shared.Next(chuThuong.Length)];
        for (int i = 0; i < 4; i++)
            ketQua += chuSo[Random.Shared.Next(chuSo.Length)];
        return ketQua;
    }
}
