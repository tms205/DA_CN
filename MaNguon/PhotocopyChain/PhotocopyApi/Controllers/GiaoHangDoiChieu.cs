using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.GiaoHang;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.Controllers;

public partial class GiaoHangController
{
    [HttpGet("don/{maDonHang:int:min(1)}/lich-su")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> LichSuGiaoCuaDon(int maDonHang)
    {
        Response.Headers.CacheControl = "no-store";
        if (KhongCoPhamVi) return Forbid();
        using var conn = new SqlConnection(_chuoiKetNoi);
        bool trongPhamVi = await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM DonHang
            WHERE MaDonHang=@maDonHang AND HinhThucNhan='GIAO_HANG'
              AND (@ChiNhanh IS NULL OR MaChiNhanh=@ChiNhanh)", new { maDonHang, ChiNhanh }) == 1;
        if (!trongPhamVi) return NotFound(new { message = "Không tìm thấy đơn giao hàng trong phạm vi của bạn." });
        var rows = await conn.QueryAsync<LuotLichSuGiaoDto>(@"SELECT gh.MaGiaoHang,gh.MaDonHang,gh.LanGiao,
            sp.HoTen AS TenShipper,gh.TrangThai,gh.LyDo,gh.NgayNhan,gh.NgayGiao,
            JSON_VALUE(gh.DoiChieuKhachHangJson,'$.TrangThai') AS TrangThaiDoiChieu
            FROM GiaoHang gh JOIN DonHang dh ON dh.MaDonHang=gh.MaDonHang
            LEFT JOIN NguoiDung sp ON sp.MaNguoiDung=gh.MaShipper
            WHERE gh.MaDonHang=@maDonHang AND dh.HinhThucNhan='GIAO_HANG'
              AND (@ChiNhanh IS NULL OR dh.MaChiNhanh=@ChiNhanh)
            ORDER BY gh.LanGiao,gh.MaGiaoHang", new { maDonHang, ChiNhanh });
        return Ok(rows);
    }

    [HttpGet("don/{maDonHang:int:min(1)}/doi-chieu")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> LuotDoiChieuCuaKhach(int maDonHang)
    {
        Response.Headers.CacheControl = "no-store";
        using var conn = new SqlConnection(_chuoiKetNoi);
        bool own = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DonHang WHERE MaDonHang=@maDonHang AND MaKhachHang=@MaNguoiDung AND HinhThucNhan='GIAO_HANG'", new { maDonHang, MaNguoiDung }) == 1;
        if (!own) return NotFound(new { message = "Không tìm thấy đơn giao hàng của bạn." });
        var rows = await conn.QueryAsync<DongDoiChieu>(@"SELECT gh.MaGiaoHang,gh.MaDonHang,gh.LanGiao,gh.LyDo,gh.NgayGiao,gh.DoiChieuKhachHangJson
            FROM GiaoHang gh JOIN DonHang dh ON dh.MaDonHang=gh.MaDonHang
            WHERE gh.MaDonHang=@maDonHang AND dh.MaKhachHang=@MaNguoiDung AND gh.TrangThai='THAT_BAI' ORDER BY gh.LanGiao,gh.MaGiaoHang", new { maDonHang, MaNguoiDung });
        return Ok(rows.Select(DtoDoiChieu).ToList());
    }

    [HttpGet("luot/{maGiaoHang:int:min(1)}/doi-chieu")]
    [Authorize(Roles = "CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> XemDoiChieu(int maGiaoHang)
    {
        Response.Headers.CacheControl = "no-store";
        if (!User.IsInRole(VaiTro.Customer) && KhongCoPhamVi) return Forbid();
        var row = await DocLuotDoiChieu(maGiaoHang);
        return row == null ? NotFound(new { message = "Không tìm thấy lượt giao trong phạm vi của bạn." }) : Ok(DtoDoiChieu(row));
    }

    [HttpGet("luot/{maGiaoHang:int:min(1)}/doi-chieu/anh/{maAnh}")]
    [Authorize(Roles = "CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> XemAnhDoiChieu(int maGiaoHang, string maAnh)
    {
        Response.Headers.CacheControl = "no-store";
        if (!User.IsInRole(VaiTro.Customer) && KhongCoPhamVi) return Forbid();
        if (!Guid.TryParseExact(maAnh, "N", out _)) return BadRequest(new { message = "Mã ảnh không hợp lệ." });
        var row = await DocLuotDoiChieu(maGiaoHang);
        if (row == null) return NotFound();
        return await TraAnhBangChung(maGiaoHang, maAnh, DocPhanHoi(row.DoiChieuKhachHangJson)?.Anh ?? new());
    }

    [HttpPost("luot/{maGiaoHang:int:min(1)}/doi-chieu")]
    [Authorize(Roles = "CUSTOMER")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> GuiDoiChieu(int maGiaoHang, [FromForm] GuiDoiChieuRequest req)
    {
        List<AnhBangChungDto> files = new(); bool committing = false, saved = false;
        try
        {
            var row = await DocLuotDoiChieu(maGiaoHang);
            if (row == null) return NotFound(new { message = "Không tìm thấy lượt giao thất bại của bạn." });
            if (row.DoiChieuKhachHangJson != null) return Ok(new { message = "Phản hồi lượt này đã được ghi nhận; bằng chứng cũ được giữ nguyên." });
            QuyTacDoiChieu.KiemTraNoiDung(req.NoiDung);
            files = await _bangChung.Luu(maGiaoHang, req.Anh, HttpContext.RequestAborted);
            // File/ảnh được xử lý trước khi khóa SQL; khóa theo thứ tự Đơn → Lượt như nghiệp vụ giao hàng.
            using var conn = new SqlConnection(_chuoiKetNoi); await conn.OpenAsync();
            await using var tran = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
            var locked = await KhoaLuotDoiChieu(conn, tran, row);
            if (locked == null) return NotFound(new { message = "Lượt giao không còn thuộc đơn của bạn." });
            if (locked.DoiChieuKhachHangJson != null) return Ok(new { message = "Phản hồi đã được ghi nhận; không tạo trùng." });
            var report = QuyTacDoiChieu.Tao(MaNguoiDung, req.NoiDung, files, DateTimeOffset.UtcNow);
            await conn.ExecuteAsync("UPDATE GiaoHang SET DoiChieuKhachHangJson=@json WHERE MaGiaoHang=@maGiaoHang", new { maGiaoHang, json = JsonSerializer.Serialize(report) }, tran);
            await conn.ExecuteAsync(@"INSERT ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
                SELECT MaNguoiDung,@MaDonHang,N'Khách gửi bằng chứng đối chiếu',@NoiDung FROM NguoiDung
                WHERE TrangThai=N'HOAT_DONG' AND (VaiTro='SUPER_ADMIN' OR (VaiTro='BRANCH_ADMIN' AND MaChiNhanh=@MaChiNhanh))",
                new { row.MaDonHang, row.MaChiNhanh, NoiDung = $"DH{row.MaDonHang:D5} · Lượt {row.LanGiao}: có phản hồi khách hàng chờ đối chiếu." }, tran);
            committing = true; await tran.CommitAsync(); saved = true;
            await GuiTin(DonDoiChieu(row), row.TrangThaiDon, "Khách gửi bằng chứng đối chiếu giao hàng.", row.MaShipper);
            return Ok(new { message = "Đã gửi bằng chứng. Cửa hàng sẽ đối chiếu và trả lời trong đơn hàng." });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { return new EmptyResult(); }
        catch (Exception ex) when (ex is SqlException or IOException or UnauthorizedAccessException)
        {
            _log.LogWarning("Chưa xác nhận lưu phản hồi lượt giao {MaGiaoHang}; loại lỗi {Loai}.", maGiaoHang, ex.GetType().Name);
            return StatusCode(503, new { message = "Chưa xác nhận được phản hồi. Giữ ảnh và kiểm tra lại đơn trước khi gửi lại." });
        }
        finally { if (!saved && !committing) _bangChung.XoaTepCuaYeuCau(maGiaoHang, files); }
    }

    [HttpPut("luot/{maGiaoHang:int:min(1)}/doi-chieu/xu-ly")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> XuLyDoiChieu(int maGiaoHang, XuLyDoiChieuRequest req)
    {
        if (KhongCoPhamVi) return Forbid();
        try
        {
            QuyTacDoiChieu.KiemTraNoiDung(req.KetLuan, 10);
            var row = await DocLuotDoiChieu(maGiaoHang);
            if (row == null) return NotFound(new { message = "Không tìm thấy lượt giao trong phạm vi." });
            using var conn = new SqlConnection(_chuoiKetNoi); await conn.OpenAsync();
            await using var tran = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
            var locked = await KhoaLuotDoiChieu(conn, tran, row);
            if (locked == null) return NotFound();
            var report = DocPhanHoi(locked.DoiChieuKhachHangJson);
            if (report == null) return Conflict(new { message = "Khách chưa gửi phản hồi đối chiếu." });
            if (report.TrangThai == "DA_XU_LY") return report.KetLuan == req.KetLuan.Trim() && report.MaNguoiXuLy == MaNguoiDung
                ? Ok(new { message = "Kết luận này đã được ghi nhận." }) : Conflict(new { message = "Phản hồi đã được xử lý. Tải lại để xem kết luận, không ghi đè." });
            QuyTacDoiChieu.XuLy(report, req.KetLuan, MaNguoiDung, User.FindFirst(TenClaim.HoTen)?.Value ?? "Quản lý", DateTimeOffset.UtcNow);
            await conn.ExecuteAsync("UPDATE GiaoHang SET DoiChieuKhachHangJson=@json WHERE MaGiaoHang=@maGiaoHang", new { maGiaoHang, json = JsonSerializer.Serialize(report) }, tran);
            await ThongBao(conn, tran, row.MaKhachHang, row.MaDonHang, "Phản hồi giao hàng đã được đối chiếu", $"DH{row.MaDonHang:D5} · Lượt {row.LanGiao}: cửa hàng đã trả lời. Mở đơn để xem kết quả.");
            await tran.CommitAsync();
            await GuiTin(DonDoiChieu(row), row.TrangThaiDon, "Cửa hàng đã đối chiếu phản hồi giao hàng.", row.MaShipper);
            return Ok(new { message = "Đã lưu kết luận đối chiếu và thông báo cho khách." });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private async Task<DongDoiChieu> DocLuotDoiChieu(int id)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? khach = User.IsInRole(VaiTro.Customer) ? MaNguoiDung : null;
        return await conn.QueryFirstOrDefaultAsync<DongDoiChieu>(@"SELECT gh.MaGiaoHang,gh.MaDonHang,gh.MaShipper,gh.LanGiao,gh.LyDo,gh.NgayGiao,
            gh.DoiChieuKhachHangJson,dh.MaKhachHang,dh.MaChiNhanh,dh.TrangThai AS TrangThaiDon
            FROM GiaoHang gh JOIN DonHang dh ON dh.MaDonHang=gh.MaDonHang
            WHERE gh.MaGiaoHang=@id AND gh.TrangThai='THAT_BAI' AND dh.HinhThucNhan='GIAO_HANG'
                AND (@Khach IS NULL OR dh.MaKhachHang=@Khach)
                AND (@Khach IS NOT NULL OR @ChiNhanh IS NULL OR dh.MaChiNhanh=@ChiNhanh)", new { id, Khach = khach, ChiNhanh = ChiNhanh });
    }

    private static async Task<DongDoiChieu> KhoaLuotDoiChieu(SqlConnection conn, SqlTransaction tran, DongDoiChieu row)
    {
        bool own = await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM DonHang WITH(UPDLOCK,HOLDLOCK)
            WHERE MaDonHang=@MaDonHang AND MaKhachHang=@MaKhachHang AND MaChiNhanh=@MaChiNhanh AND HinhThucNhan='GIAO_HANG'", row, tran) == 1;
        if (!own) return null;
        return await conn.QueryFirstOrDefaultAsync<DongDoiChieu>(@"SELECT MaGiaoHang,DoiChieuKhachHangJson FROM GiaoHang WITH(UPDLOCK,HOLDLOCK)
            WHERE MaGiaoHang=@MaGiaoHang AND MaDonHang=@MaDonHang AND TrangThai='THAT_BAI'", row, tran);
    }
    private static PhanHoiGiaoHangDto DocPhanHoi(string json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PhanHoiGiaoHangDto>(json);
    private static LuotDoiChieuGiaoDto DtoDoiChieu(DongDoiChieu row) => new()
    {
        MaGiaoHang = row.MaGiaoHang,
        MaDonHang = row.MaDonHang,
        LanGiao = row.LanGiao,
        LyDoGiaoThatBai = row.LyDo,
        NgayGiao = row.NgayGiao,
        PhanHoi = DocPhanHoi(row.DoiChieuKhachHangJson)
    };
    private static DonDeGiao DonDoiChieu(DongDoiChieu row) => new() { MaDonHang = row.MaDonHang, MaKhachHang = row.MaKhachHang, MaChiNhanh = row.MaChiNhanh, TrangThai = row.TrangThaiDon };
    private sealed class DongDoiChieu
    {
        public int MaGiaoHang { get; set; }
        public int MaDonHang { get; set; }
        public int MaShipper { get; set; }
        public int MaKhachHang { get; set; }
        public int MaChiNhanh { get; set; }
        public int LanGiao { get; set; }
        public string LyDo { get; set; }
        public DateTime? NgayGiao { get; set; }
        public string DoiChieuKhachHangJson { get; set; }
        public string TrangThaiDon { get; set; }
    }
}

public sealed class GuiDoiChieuRequest
{
    [Required(ErrorMessage = "Vui lòng nhập nội dung đối chiếu."), StringLength(1000, MinimumLength = 5, ErrorMessage = "Nội dung cần từ 5 đến 1.000 ký tự.")]
    public string NoiDung { get; set; }
    public List<IFormFile> Anh { get; set; } = new();
}
