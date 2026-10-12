using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.DonHang;

namespace PhotocopyApi.Controllers;

// F-13: trung tâm thông báo. Các thủ tục nghiệp vụ (tiếp nhận đơn, in xong, giao hàng...) tự ghi vào bảng ThongBao,
// ở đây chỉ đọc ra và đánh dấu đã đọc. Ai đăng nhập cũng chỉ thấy thông báo của chính mình.
[ApiController]
[Route("api/thong-bao")]
[Authorize]
public class ThongBaoController : ControllerBase
{
    private readonly string _chuoiKetNoi;

    public ThongBaoController(IConfiguration config)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    // GET api/thong-bao?soLuong=50 — thông báo mới nhất + số thông báo chưa đọc
    [HttpGet]
    public async Task<IActionResult> DanhSach(int soLuong = 50)
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        if (soLuong < 1) soLuong = 1;
        if (soLuong > 200) soLuong = 200;

        string sql = @"SELECT TOP (@SoLuong) MaThongBao, MaDonHang,
                              'DH' + RIGHT('00000' + CAST(MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                              TieuDe, NoiDung,
                              CASE WHEN MaDonHang IS NULL THEN 'HE_THONG' ELSE 'DON_HANG' END AS LoaiThongBao,
                              DaDoc, NgayTao
                       FROM ThongBao
                       WHERE MaNguoiDung = @MaNguoiDung
                       ORDER BY NgayTao DESC, MaThongBao DESC";

        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<ThongBaoDto>(sql, new { SoLuong = soLuong, MaNguoiDung = maNguoiDung });
        int soChuaDoc = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ThongBao WHERE MaNguoiDung = @MaNguoiDung AND DaDoc = 0",
            new { MaNguoiDung = maNguoiDung });

        return Ok(new DanhSachThongBaoDto
        {
            DanhSach = danhSach.ToList(),
            SoChuaDoc = soChuaDoc
        });
    }

    // PUT api/thong-bao/5/da-doc — đánh dấu 1 thông báo là đã đọc
    [HttpPut("{maThongBao}/da-doc")]
    public async Task<IActionResult> DanhDauDaDoc([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maThongBao)
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);
        // Điều kiện MaNguoiDung để không sửa được thông báo của người khác
        int soDong = await conn.ExecuteAsync(
            "UPDATE ThongBao SET DaDoc = 1 WHERE MaThongBao = @MaThongBao AND MaNguoiDung = @MaNguoiDung",
            new { MaThongBao = maThongBao, MaNguoiDung = maNguoiDung });

        if (soDong == 0)
            return NotFound(new { message = "Không tìm thấy thông báo." });

        return Ok(new KetQuaDto { Message = "Đã đánh dấu đã đọc." });
    }

    // PUT api/thong-bao/da-doc-tat-ca — đánh dấu tất cả là đã đọc
    [HttpPut("da-doc-tat-ca")]
    public async Task<IActionResult> DanhDauTatCaDaDoc()
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.ExecuteAsync(
            "UPDATE ThongBao SET DaDoc = 1 WHERE MaNguoiDung = @MaNguoiDung AND DaDoc = 0",
            new { MaNguoiDung = maNguoiDung });

        return Ok(new KetQuaDto { Message = "Đã đánh dấu tất cả là đã đọc." });
    }
}
