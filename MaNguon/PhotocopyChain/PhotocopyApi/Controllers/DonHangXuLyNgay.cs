using System.ComponentModel.DataAnnotations;
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;

namespace PhotocopyApi.Controllers;

public partial class DonHangController
{
    [HttpPost("{maDonHang:int}/xu-ly-ngay")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> XacNhanXuLyNgay([Range(1, int.MaxValue)] int maDonHang)
    {
        int maKhachHang = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        try
        {
            await conn.ExecuteAsync("sp_XacNhanXuLyNgay", new { MaDonHang = maDonHang, MaKhachHang = maKhachHang },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex) when (ex.Number is >= 50000 and <= 50999)
        {
            return Conflict(new { message = ex.Message });
        }
        await BaoDonDoiTrangThai(conn, maDonHang, "Khách xác nhận xử lý ngay; nhân viên có thể tiếp nhận đơn.");
        return Ok(new KetQuaDto { Message = "Đã bỏ qua thời gian chờ hủy/hoàn. Đơn chờ nhân viên tiếp nhận." });
    }
}
