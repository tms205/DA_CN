using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

public partial class QuanTriController
{
    [HttpGet("lich-su-don-huy")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> LichSuDonHuy([FromQuery, Range(1, 1000000)] int trang = 1,
        [FromQuery, MaxLength(100)] string tim = "", [FromQuery] string thanhToan = "")
    {
        if (thanhToan is not ("" or "CHUA_THANH_TOAN" or "DA_THANH_TOAN" or "DA_HOAN_TIEN"))
            return BadRequest(new { message = "Trạng thái thanh toán không hợp lệ." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        const string nguon = @"
            FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
            JOIN NguoiDung kh ON kh.MaNguoiDung=dh.MaKhachHang
            JOIN ChiNhanh cn ON cn.MaChiNhanh=dh.MaChiNhanh
            WHERE dh.TrangThai='DA_HUY' AND (@ChiNhanh IS NULL OR dh.MaChiNhanh=@ChiNhanh)
              AND (@ThanhToan='' OR tt.TrangThai=@ThanhToan)
              AND (@Tim='' OR CHARINDEX(@Tim,CONCAT('DH',RIGHT('00000'+CAST(dh.MaDonHang AS VARCHAR(10)),5),' ',kh.HoTen,' ',kh.SoDienThoai,' ',cn.TenChiNhanh))>0)";
        var args = new { ChiNhanh = LayChiNhanhTuToken(), ThanhToan = thanhToan, Tim = tim?.Trim() ?? "", BoQua = (trang - 1) * 8 };
        using var data = await conn.QueryMultipleAsync(@"SELECT COUNT(*) " + nguon + @";
            SELECT dh.MaDonHang,'DH'+RIGHT('00000'+CAST(dh.MaDonHang AS VARCHAR(10)),5) AS MaDonHangHienThi,
                   kh.HoTen AS TenKhachHang,kh.SoDienThoai,cn.TenChiNhanh,tt.SoTien,
                   tt.TrangThai AS TrangThaiThanhToan,COALESCE(JSON_VALUE(dh.BangChungHuyJson,'$.LyDo'),dh.GhiChu) AS GhiChu,dh.NgayDat,dh.KenhDatDon,
                   TRY_CONVERT(DATETIMEOFFSET,JSON_VALUE(dh.BangChungHuyJson,'$.GuiLuc')) AS GuiLuc
            " + nguon + @" ORDER BY COALESCE(TRY_CONVERT(DATETIMEOFFSET,JSON_VALUE(dh.BangChungHuyJson,'$.GuiLuc')),TODATETIMEOFFSET(dh.NgayDat,'+07:00')) DESC,dh.MaDonHang DESC
            OFFSET @BoQua ROWS FETCH NEXT 8 ROWS ONLY;", args);
        return Ok(new LichSuDonHuyDto { Total = await data.ReadSingleAsync<int>(), Items = (await data.ReadAsync<HoanTienChoDuyetDto>()).ToList() });
    }
}
