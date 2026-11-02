using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.DuLieu;
using PhotocopyShared.Dtos.DanhMuc;

namespace PhotocopyApi.Controllers;

// Danh mục cho App đặt in: danh sách chi nhánh, bảng giá. Ai cũng xem được, không cần đăng nhập.
[ApiController]
[Route("api/danh-muc")]
public class DanhMucController : ControllerBase
{
    private readonly string _chuoiKetNoi;

    public DanhMucController(IConfiguration config)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    // đọc danh sách chi nhánh đang hoạt động
    [HttpGet("chi-nhanh")]
    public async Task<IActionResult> DanhSachChiNhanh()
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<ChiNhanhDto>(
            TruyVanSql.DanhMuc.ChiNhanhDangHoatDong);
        return Ok(danhSach);
    }

    // các dòng giá in ấn + gia công đang hiển thị 
    [HttpGet("chi-nhanh/{maChiNhanh:int}/danh-gia")]
    public async Task<IActionResult> DanhGiaChiNhanh([Range(1, int.MaxValue)] int maChiNhanh, [FromQuery, Range(1, 10000)] int trang = 1)
    {
        Response.Headers.CacheControl = "no-store";
        if (maChiNhanh <= 0 || trang is < 1 or > 10000) return BadRequest(new { message = "Chi nhánh hoặc trang đánh giá không hợp lệ." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        var result = await conn.QuerySingleOrDefaultAsync<DanhGiaChiNhanhDto>(
            @"SELECT cn.MaChiNhanh, cn.TenChiNhanh, ISNULL(dg.DiemTrungBinh,0) AS DiemTrungBinh,
                     ISNULL(dg.SoLuotDanhGia,0) AS SoLuotDanhGia
              FROM ChiNhanh cn LEFT JOIN vw_DanhGiaChiNhanh dg ON dg.MaChiNhanh=cn.MaChiNhanh
              WHERE cn.MaChiNhanh=@maChiNhanh AND cn.TrangThai=N'HOAT_DONG'", new { maChiNhanh });
        if (result == null) return NotFound(new { message = "Chi nhánh không tồn tại hoặc đang ngừng hoạt động." });
        var reviews = (await conn.QueryAsync<DanhGiaCongKhaiDto>(
            @"SELECT CASE WHEN LEN(LTRIM(RTRIM(kh.HoTen)))>0 THEN LEFT(LTRIM(kh.HoTen),1)+N'***' ELSE N'Khách hàng' END AS TenKhachHang,
                     dg.SoSao,dg.NhanXet,dg.NgayDanhGia
              FROM DanhGia dg JOIN DonHang dh ON dh.MaDonHang=dg.MaDonHang
              JOIN NguoiDung kh ON kh.MaNguoiDung=dh.MaKhachHang
              WHERE dh.MaChiNhanh=@maChiNhanh AND dg.TrangThai=N'HIEN'
              ORDER BY dg.NgayDanhGia DESC,dg.MaDanhGia DESC
              OFFSET @boQua ROWS FETCH NEXT 21 ROWS ONLY", new { maChiNhanh, boQua = (trang - 1) * 20 })).ToList();
        result.Trang = trang; result.CoTrangSau = reviews.Count > 20; result.DanhGia = reviews.Take(20).ToList();
        return Ok(result);
    }

    // các dòng giá in ấn + gia công đang hiển thị
    [HttpGet("bang-gia")]
    public async Task<IActionResult> BangGia([FromQuery] [Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maChiNhanh)
    {
        if (maChiNhanh <= 0)
            return BadRequest(new { message = "Hãy chọn chi nhánh để xem bảng giá." });

        string sql = @"SELECT dv.MaDichVu, dv.TenDichVu, dv.LoaiDichVu, dv.KhoGiay, dv.LoaiGiay,
                              dv.MatIn, dv.MauSac, dv.NhomGiaCong, bg.DonGia, dv.KichThuocAnh,
                              dv.MaVatTu, vt.TenVatTu
                       FROM BangGiaChiNhanh bg
                       JOIN DichVu dv ON dv.MaDichVu = bg.MaDichVu
                       JOIN ChiNhanh cn ON cn.MaChiNhanh = bg.MaChiNhanh
                       LEFT JOIN VatTu vt ON vt.MaVatTu = dv.MaVatTu
                       WHERE bg.MaChiNhanh = @MaChiNhanh AND bg.TrangThai = N'HIEN'
                         AND cn.TrangThai = N'HOAT_DONG'
                       ORDER BY dv.LoaiDichVu DESC, dv.KhoGiay, dv.LoaiGiay, dv.MatIn, dv.MauSac";
        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<DichVuDto>(sql, new { MaChiNhanh = maChiNhanh });
        return Ok(danhSach);
    }
}
