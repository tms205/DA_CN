using System.ComponentModel.DataAnnotations;
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.DuLieu;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

// F-24B: ca làm việc và két tiền.
// Tiệm chạy 3 ca liền, mỗi ca 4 tiếng (09:00–13:00, 13:00–17:00, 17:00–21:00) nhưng hệ thống KHÔNG ép giờ:
// STAFF đăng nhập tự mở ca riêng 200.000đ; quản lý có thể mở ca thủ công. Hết ca thì kiểm đếm và đóng ca.
// Phải có ca đang mở thì nhân viên mới được tiếp nhận, xử lý đơn và thu COD của khách.
// Quản lý xác nhận ca (sp_XacNhanCa, xử lý lệch két) làm ở Tuần 3.
[ApiController]
[Route("api/ca")]
[Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
public class CaController : ControllerBase
{
    private readonly string _chuoiKetNoi;

    public CaController(IConfiguration config)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    // GET api/ca/dang-mo — ca của chính tôi đang mở (trả 204 nếu chưa mở ca nào)
    [HttpGet("dang-mo")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> CaDangMo()
    {
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int? maChiNhanh = User.FindFirst(TenClaim.MaChiNhanh) == null
            ? null
            : int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);
        CaLamViecDto ca = await conn.QueryFirstOrDefaultAsync<CaLamViecDto>(
            TruyVanSql.CaLamViec.DangMoCuaNhanVien,
            new { MaNhanVien = maNhanVien, MaChiNhanh = maChiNhanh });

        if (ca == null)
            return NoContent();   // chưa mở ca — Web hiện nút "Mở ca"
        return Ok(ca);
    }

    // GET api/ca/chua-ket-thuc — giữ endpoint cũ, trả ca đang mở của chính người đăng nhập.
    // Chỉ xem ca đang mở của chính mình; không khóa bởi ca của nhân viên khác.
    [HttpGet("chua-ket-thuc")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> CaChuaKetThucCuaChiNhanh()
    {
        var claimChiNhanh = User.FindFirst(TenClaim.MaChiNhanh);
        if (claimChiNhanh == null)
            return BadRequest(new { message = "Vui lòng chọn một chi nhánh trước khi xem ca đang trực." });
        int maChiNhanh = int.Parse(claimChiNhanh.Value);
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        CaLamViecDto ca = await conn.QueryFirstOrDefaultAsync<CaLamViecDto>(
            TruyVanSql.CaLamViec.ChuaKetThucCuaChiNhanh,
            new { MaChiNhanh = maChiNhanh, MaNhanVien = maNhanVien });

        if (ca == null)
            return NoContent();
        return Ok(ca);
    }

    // GET api/ca — STAFF chỉ xem ca của mình; BRANCH_ADMIN xem toàn chi nhánh.
    [HttpGet]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> DanhSach()
    {
        int? maChiNhanh = User.FindFirst(TenClaim.MaChiNhanh) == null
            ? null
            : int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int? maNhanVien = vaiTro == "STAFF" ? maNguoiDung : null;

        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<CaLamViecDto>(
            TruyVanSql.CaLamViec.GanDayCuaChiNhanh,
            new { MaChiNhanh = maChiNhanh, MaNhanVien = maNhanVien });

        return Ok(danhSach);
    }

    // GET api/ca/5/don-hang — tách từng đơn đã thanh toán trong ca, không gộp thành một cục.
    // Web tự chia 5 dòng mỗi trang để code dễ đọc và dữ liệu của một ca cũng không quá lớn.
    [HttpGet("{maCa}/don-hang")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> DonHangTrongCa([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maCa)
    {
        int? maChiNhanh = User.FindFirst(TenClaim.MaChiNhanh) == null
            ? null
            : int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int? maNhanVien = vaiTro == "STAFF"
            ? int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value)
            : null;

        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<DonHangTrongCaDto>(
            TruyVanSql.CaLamViec.DonHangTrongCa,
            new { MaCa = maCa, MaChiNhanh = maChiNhanh, MaNhanVien = maNhanVien });
        return Ok(danhSach);
    }

    // GET api/ca/don-hang/lich-su?tuNgay=2026-09-21&denNgay=2026-09-22
    // Mặc định lấy 30 ngày gần nhất để màn này đúng vai trò lịch sử tra cứu.
    [HttpGet("don-hang/lich-su")]
    public async Task<IActionResult> LichSuDonHangTrongCa(
        [FromQuery] DateTime? tuNgay,
        [FromQuery] DateTime? denNgay)
    {
        DateTime ngayBatDau = (tuNgay ?? DateTime.Today.AddDays(-30)).Date;
        DateTime ngayKetThuc = (denNgay ?? DateTime.Today).Date;

        if (ngayBatDau > ngayKetThuc)
            return BadRequest(new { message = "Từ ngày không được lớn hơn đến ngày." });
        if (ngayBatDau.Year < 1753 || ngayKetThuc >= new DateTime(9999, 12, 31))
            return BadRequest(new { message = "Khoảng lịch sử phải từ 01/01/1753 đến 30/12/9999." });

        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);

        // Super Admin không có MaChiNhanh trong JWT nên xem được toàn chuỗi.
        // Quản lý bị giới hạn trong chi nhánh. STAFF còn bị giới hạn theo MaNhanVien
        // ở chính câu SQL để không thể sửa request rồi xem đơn của người khác.
        int? maChiNhanh = vaiTro == "SUPER_ADMIN"
            ? null
            : int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        int? maNhanVien = vaiTro == "STAFF" ? maNguoiDung : null;

        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<DonHangTrongCaDto>(
            TruyVanSql.CaLamViec.LichSuDonHangTrongCa,
            new
            {
                MaChiNhanh = maChiNhanh,
                MaNhanVien = maNhanVien,
                TuNgay = ngayBatDau,
                DenNgay = ngayKetThuc
            });

        return Ok(danhSach);
    }

    // POST api/ca/mo — vào quầy, khai tiền lẻ có sẵn trong két
    [HttpPost("mo")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> MoCa(MoCaRequest req)
    {
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        var claimChiNhanh = User.FindFirst(TenClaim.MaChiNhanh);
        if (claimChiNhanh == null)
            return BadRequest(new { message = "Vui lòng chọn một chi nhánh trước khi mở ca." });
        int maChiNhanh = int.Parse(claimChiNhanh.Value);

        using var conn = new SqlConnection(_chuoiKetNoi);

        // Mỗi người chỉ có một ca đang mở. Mở lại dùng ca cũ, không đổi tiền.
        CaLamViecDto caDangMo = await conn.QueryFirstOrDefaultAsync<CaLamViecDto>(
            TruyVanSql.CaLamViec.DangMoCuaNhanVien,
            new { MaChiNhanh = maChiNhanh, MaNhanVien = maNhanVien });
        if (caDangMo != null) return Ok(caDangMo);

        // Thủ tục trả mã ca vừa mở qua tham số OUTPUT
        var thamSo = new DynamicParameters();
        thamSo.Add("@MaNhanVien", maNhanVien);
        thamSo.Add("@TienDauCa", req.TienDauCa);
        thamSo.Add("@MaChiNhanhThayThe", User.IsInRole(VaiTro.SuperAdmin) ? maChiNhanh : null);
        thamSo.Add("@MaCa", dbType: DbType.Int32, direction: ParameterDirection.Output);

        try
        {
            // Thủ tục khóa theo nhân viên để đăng nhập đồng thời không tạo trùng.
            await conn.ExecuteAsync("sp_MoCa", thamSo, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        int maCa = thamSo.Get<int>("@MaCa");
        CaLamViecDto ca = await conn.QueryFirstAsync<CaLamViecDto>(
            TruyVanSql.CaLamViec.TheoMa,
            new { MaCa = maCa });
        return Ok(ca);
    }

    // POST api/ca/dong — STAFF chờ xác nhận; BRANCH_ADMIN đóng ca xong ngay.
    [HttpPost("dong")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> DongCa(DongCaRequest req)
    {
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;

        if (req.TongDoanhThuCuoiCa < 0)
            return BadRequest(new { message = "Số tiền đếm được không hợp lệ." });

        using var conn = new SqlConnection(_chuoiKetNoi);

        int? maCa = await conn.ExecuteScalarAsync<int?>(
            @"SELECT MaCa FROM CaLamViec WHERE MaNhanVien=@MaNhanVien AND TrangThai='DANG_MO'
              AND (@MaChiNhanh IS NULL OR MaChiNhanh=@MaChiNhanh)",
            new { MaNhanVien = maNhanVien,
                MaChiNhanh = User.FindFirst(TenClaim.MaChiNhanh)?.Value });
        if (maCa == null)
            return BadRequest(new { message = "Bạn chưa mở ca nào." });

        try
        {
            await conn.ExecuteAsync("sp_DongCa",
                new { MaCa = maCa.Value, TongDoanhThuCuoiCa = req.TongDoanhThuCuoiCa },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new KetQuaDto
        {
            Message = vaiTro == "BRANCH_ADMIN"
                ? "Đã đóng ca quản lý. Ca đã hoàn tất, không cần xác nhận thêm."
                : "Đã đóng ca, chờ quản lý kiểm đếm và xác nhận."
        });
    }

    // GET api/ca/cho-xac-nhan — quản lý chi nhánh xem ca của chi nhánh mình; Super Admin xem toàn chuỗi.
    [HttpGet("cho-xac-nhan")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> CaChoXacNhan()
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int? maChiNhanh = User.FindFirst(TenClaim.MaChiNhanh) == null
            ? null : int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<CaLamViecDto>(
            TruyVanSql.CaLamViec.ChoXacNhan + @"
              AND ca.MaNhanVien <> @MaNguoiDung
              AND (@MaChiNhanh IS NULL OR ca.MaChiNhanh = @MaChiNhanh)
              ORDER BY ca.GioDongCa DESC",
            new { MaNguoiDung = maNguoiDung, MaChiNhanh = maChiNhanh });
        return Ok(danhSach);
    }

    // POST api/ca/5/xac-nhan — ghi nhận khoản chi và số tiền nhân viên bù khi két thiếu.
    [HttpPost("{maCa}/xac-nhan")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> XacNhanCa([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maCa, XacNhanCaRequest req)
    {
        int maQuanLy = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        if (req.TienQuanLyDemCuoiCa == null || req.TienQuanLyDemCuoiCa < 0)
            return BadRequest(new { message = "Quản lý cần nhập số tiền thực tế đã kiểm đếm." });
        if (req.TienChi < 0 || req.TienBu < 0)
            return BadRequest(new { message = "Tiền chi và tiền bù không được âm." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        var claimChiNhanh = User.FindFirst(TenClaim.MaChiNhanh);
        if (claimChiNhanh != null)
        {
            int? maChiNhanhCa = await conn.ExecuteScalarAsync<int?>(
                "SELECT MaChiNhanh FROM CaLamViec WHERE MaCa=@MaCa", new { MaCa = maCa });
            if (maChiNhanhCa == null) return NotFound(new { message = "Không tìm thấy ca làm việc." });
            if (maChiNhanhCa != int.Parse(claimChiNhanh.Value))
                return StatusCode(403, new { message = "Ca không thuộc phạm vi chi nhánh đang chọn." });
        }
        try
        {
            await conn.ExecuteAsync("sp_XacNhanCa", new
            {
                MaCa = maCa,
                MaQuanLy = maQuanLy,
                req.TienQuanLyDemCuoiCa,
                req.TienChi,
                req.TienBu,
                GhiChu = string.IsNullOrWhiteSpace(req.GhiChu) ? null : req.GhiChu.Trim()
            }, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        CaLamViecDto ca = await conn.QueryFirstAsync<CaLamViecDto>(
            TruyVanSql.CaLamViec.TheoMa, new { MaCa = maCa });
        return Ok(new { ca, message = "Đã xác nhận ca làm việc." });
    }
}
