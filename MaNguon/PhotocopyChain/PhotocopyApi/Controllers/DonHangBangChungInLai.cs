using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.InLai;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

// =====================================================================================
// ẢNH BẰNG CHỨNG IN LẠI DO LỖI QUẦY (thêm 28/11)
//   POST api/don-hang/{maDonGoc}/bang-chung-in-lai       — nhân viên tải 1–3 ảnh bản in lỗi, nhận MaBangChung
//   GET  api/don-hang/{maDonInLai}/bang-chung-in-lai     — quản lý xem danh sách ảnh của một lần in lại
//   GET  api/don-hang/{maDonInLai}/bang-chung-in-lai/anh/{maAnh} — tải một ảnh
// Sau đó TaoInLaiTaiQuay bắt buộc gửi kèm MaBangChung (xem DonHangController.cs).
// =====================================================================================
public partial class DonHangController
{
    [HttpPost("{maDonHang:int:min(1)}/bang-chung-in-lai")]
    [Authorize(Roles = "BRANCH_ADMIN,STAFF")]
    [RequestSizeLimit(16 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> TaiBangChungInLai(int maDonHang, [FromForm] AnhInLaiRequest req)
    {
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        var don = await conn.QueryFirstOrDefaultAsync(
            "SELECT MaChiNhanh, TrangThai, HinhThucNhan, MaDonHangGoc FROM DonHang WHERE MaDonHang = @maDonHang",
            new { maDonHang });
        if (don == null || !DuocLamViecVoiChiNhanh((int)don.MaChiNhanh))
            return NotFound(new { message = "Không tìm thấy đơn trong chi nhánh của bạn." });
        // Chỉ chụp cho đơn gốc đang chờ bàn giao tại quầy (đúng lúc được phép in lại).
        if (don.MaDonHangGoc != null || (string)don.TrangThai != "DA_IN" || (string)don.HinhThucNhan != "TAI_QUAY")
            return Conflict(new { message = "Chỉ chụp bằng chứng in lại cho đơn gốc đã in xong, đang chờ bàn giao tại quầy." });

        try
        {
            HoSoInLai hoSo = await _bangChungInLai.Luu(maDonHang, maNhanVien, req.Anh, HttpContext.RequestAborted);
            return Ok(new TaiBangChungInLaiResponse { MaBangChung = hoSo.MaBangChung, SoAnh = hoSo.Anh.Count });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (IOException) { return StatusCode(503, new { message = "Chưa lưu được ảnh bằng chứng. Hãy thử lại." }); }
    }

    // Đọc hồ sơ ảnh của một đơn in lại, có kiểm chi nhánh. null = không thấy / ngoài phạm vi.
    private async Task<HoSoInLai> DocHoSoInLaiTrongPhamVi(int maDonInLai)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? maChiNhanh = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaChiNhanh FROM DonHang WHERE MaDonHang = @maDonInLai AND LoaiPhatSinh = 'IN_LAI_LOI_QUAY'",
            new { maDonInLai });
        if (maChiNhanh == null || !DuocLamViecVoiChiNhanh(maChiNhanh.Value)) return null;
        return _bangChungInLai.DocTheoDonInLai(maDonInLai);
    }

    [HttpGet("{maDonHang:int:min(1)}/bang-chung-in-lai")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> XemBangChungInLai(int maDonHang)
    {
        Response.Headers.CacheControl = "no-store";
        HoSoInLai hoSo = await DocHoSoInLaiTrongPhamVi(maDonHang);
        if (hoSo == null) return NotFound(new { message = "Lần in lại này không có ảnh bằng chứng hoặc ngoài phạm vi của bạn." });
        return Ok(new BangChungInLaiDto
        {
            MaDonInLai = maDonHang,
            MaDonHangGoc = hoSo.MaDonHangGoc,
            GuiLuc = hoSo.GuiLuc,
            MaAnh = hoSo.Anh.Select(a => a.MaAnh).ToList()
        });
    }

    [HttpGet("{maDonHang:int:min(1)}/bang-chung-in-lai/anh/{maAnh}")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> XemAnhInLai(int maDonHang, string maAnh)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (!Guid.TryParseExact(maAnh, "N", out _)) return BadRequest();
        HoSoInLai hoSo = await DocHoSoInLaiTrongPhamVi(maDonHang);
        var anh = hoSo?.Anh.SingleOrDefault(a => a.MaAnh == maAnh);
        if (anh == null) return NotFound();
        try
        {
            byte[] data = await System.IO.File.ReadAllBytesAsync(_bangChungInLai.DuongDanAnh(hoSo.MaDonHangGoc, anh), HttpContext.RequestAborted);
            // Ảnh bị sửa trên ổ đĩa thì mã băm không khớp → không trả, để bằng chứng không bị tráo.
            if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)) != anh.Sha256)
                return Conflict(new { message = "Ảnh không khớp mã kiểm tra." });
            return File(data, anh.LoaiNoiDung);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return NotFound(); }
    }
}

public sealed class AnhInLaiRequest
{
    [MinLength(1, ErrorMessage = "Cần ít nhất một ảnh bản in lỗi."), MaxLength(3, ErrorMessage = "Tối đa 3 ảnh.")]
    public List<IFormFile> Anh { get; set; } = new();
}
