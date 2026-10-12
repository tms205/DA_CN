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
    [HttpGet("bang-gia")]
    public async Task<IActionResult> BangGia([FromQuery] [Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maChiNhanh)
    {
        if (maChiNhanh <= 0)
            return BadRequest(new { message = "Hãy chọn chi nhánh để xem bảng giá." });

        string sql = @"SELECT dv.MaDichVu, dv.TenDichVu, dv.LoaiDichVu, dv.KhoGiay, dv.LoaiGiay,
                              dv.MatIn, dv.MauSac, dv.NhomGiaCong, bg.DonGia, dv.KichThuocAnh
                       FROM BangGiaChiNhanh bg
                       JOIN DichVu dv ON dv.MaDichVu = bg.MaDichVu
                       JOIN ChiNhanh cn ON cn.MaChiNhanh = bg.MaChiNhanh
                       WHERE bg.MaChiNhanh = @MaChiNhanh AND bg.TrangThai = N'HIEN'
                         AND cn.TrangThai = N'HOAT_DONG'
                       ORDER BY dv.LoaiDichVu DESC, dv.KhoGiay, dv.LoaiGiay, dv.MatIn, dv.MauSac";
        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<DichVuDto>(sql, new { MaChiNhanh = maChiNhanh });
        return Ok(danhSach);
    }
}
