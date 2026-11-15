using System.ComponentModel.DataAnnotations;
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

public partial class QuanTriController
{
    [HttpPost("phieu-nhap/{maPhieuNhap:int}/huy")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> HuyPhieuNhap([Range(1, int.MaxValue)] int maPhieuNhap, HuyPhieuNhapRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LyDoHuy)) return BadRequest(new { message = "Vui lòng nhập lý do hủy phiếu nhập." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? chiNhanh = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaChiNhanh FROM PhieuNhap WHERE MaPhieuNhap=@maPhieuNhap", new { maPhieuNhap });
        if (chiNhanh == null) return NotFound(new { message = "Không tìm thấy phiếu nhập." });
        if (!DuocLamViecVoiChiNhanh(chiNhanh.Value)) return StatusCode(403);
        try
        {
            await conn.ExecuteAsync("sp_HuyPhieuNhap", new { MaPhieuNhap = maPhieuNhap,
                MaNguoiHuy = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value), LyDoHuy = req.LyDoHuy.Trim() },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex) when (ex.Number is >= 50000 and <= 50999)
        {
            return Conflict(new { message = ex.Message });
        }
        return Ok(new KetQuaDto { Message = "Đã hủy phiếu nhập; tồn kho không thay đổi." });
    }
}
