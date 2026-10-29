using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos.MayInAo;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

public sealed partial class MayInAoController
{
    [HttpPost("don/{maDonHang:int}/da-dung")]
    [AllowAnonymous]
    public async Task<IActionResult> DaDungDeChuyen([Range(1,int.MaxValue)] int maDonHang, BaoCaoMayInRequest req)
    {
        var loi = KiemTraKhoa();
        if (loi != null) return loi;
        if (_chuyenMay == null) return StatusCode(503);
        var phien = _chuyenMay.LichSu(maDonHang).LastOrDefault(x => x.MaMayCu == req.MaMayIn
            && x.MaLenhCu == req.MaLenh && x.ConnectorCu == ConnectorId());
        if (phien == null) return StatusCode(403);
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var don = await conn.QuerySingleOrDefaultAsync<DonDangIn>(
            "SELECT MaDonHang,MaChiNhanh,MaMayIn,TrangThai FROM DonHang WITH (UPDLOCK,HOLDLOCK) WHERE MaDonHang=@maDonHang",
            new {maDonHang}, tx);
        if (don == null) return NotFound();
        if (don.MaChiNhanh != phien.MaChiNhanh) return StatusCode(403);
        if (phien.TrangThai == "DANG_DUNG" && (don.TrangThai != "DANG_IN" || don.MaMayIn != req.MaMayIn
            || !_quyenNhanLenh.CoQuyen(req.MaMayIn,maDonHang,ConnectorId(),req.MaLenh)))
            return Conflict(new {message="Trạng thái đơn/lệnh cũ đã thay đổi; không xác nhận dừng nhầm lệnh."});
        bool vuaDung=phien.TrangThai=="DANG_DUNG";
        if (!_chuyenMay.XacNhanDaDung(maDonHang,req.MaMayIn,req.MaLenh,ConnectorId())) return Conflict();
        _quyenNhanLenh.HoanTat(req.MaMayIn,maDonHang,ConnectorId(),req.MaLenh);
        await tx.CommitAsync();
        // Máy vừa lỗi cần được nhân sự khôi phục rõ ràng; không tự nhận thêm việc.
        if (vuaDung)
        {
            _trangThai.DieuKhien(req.MaMayIn,new() {ThaoTac="TAM_DUNG"});
            _trangThai.CapNhat(req.MaMayIn,"TAM_DUNG");
        }
        return Ok(new KetQuaDto {Message="Đã dừng lệnh cũ; đối chiếu máy và bản giấy trước khi chuyển."});
    }
}
