using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

public partial class QuanTriController
{
    [HttpGet("danh-muc-vat-tu")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> DanhMucVatTu()
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        return Ok(await conn.QueryAsync<VatTuDanhMucDto>("SELECT MaVatTu,TenVatTu,DonViTinh FROM VatTu ORDER BY TenVatTu"));
    }

    [HttpGet("danh-muc-dich-vu")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> DanhMucDichVu()
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        return Ok(await conn.QueryAsync<DichVuQuanTriDto>(
            @"SELECT dv.*, vt.TenVatTu FROM DichVu dv
              LEFT JOIN VatTu vt ON vt.MaVatTu=dv.MaVatTu ORDER BY dv.LoaiDichVu, dv.MaDichVu"));
    }

    [HttpPut("danh-muc-dich-vu/{maDichVu}")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CapNhatDanhMucDichVu([Range(1, int.MaxValue)] int maDichVu, LuuDichVuRequest req)
        => await LuuDichVuAsync(maDichVu, req, true);

    // Sửa giá không được tác động định nghĩa dùng chung hay giá của chi nhánh khác.
    [HttpPut("bang-gia/{maDichVu}")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CapNhatGiaChiNhanh([Range(1, int.MaxValue)] int maDichVu, CapNhatGiaChiNhanhRequest req)
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        if (maChiNhanh == null) return BadRequest(new { message = "Hãy chọn chi nhánh trước khi sửa giá." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        int soDong = await conn.ExecuteAsync(
            @"UPDATE BangGiaChiNhanh SET DonGia=@DonGia, TrangThai=@TrangThai
              WHERE MaChiNhanh=@MaChiNhanh AND MaDichVu=@MaDichVu",
            new { MaChiNhanh = maChiNhanh.Value, MaDichVu = maDichVu, req.DonGia, req.TrangThai });
        if (soDong == 0) return NotFound(new { message = "Không tìm thấy giá dịch vụ tại chi nhánh." });
        return Ok(new KetQuaDto { Message = "Đã cập nhật giá và trạng thái bán tại chi nhánh." });
    }

    [HttpPost("chi-nhanh")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> TaoChiNhanh(ChiNhanhQuanTriDto req)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        int ma = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO ChiNhanh (TenChiNhanh,DiaChi,SoDienThoai,ViDo,KinhDo,GioMoCua,GioDongCua,BanKinhGiao,TrangThai)
              VALUES (@TenChiNhanh,@DiaChi,@SoDienThoai,@ViDo,@KinhDo,@GioMoCua,@GioDongCua,@BanKinhGiao,@TrangThai);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", req, giaoDich);
        await conn.ExecuteAsync(
            @"INSERT INTO BangGiaChiNhanh (MaChiNhanh,MaDichVu,DonGia,TrangThai)
              SELECT @MaChiNhanh,MaDichVu,DonGia,TrangThai FROM DichVu;
              INSERT INTO TonKho (MaChiNhanh,MaVatTu,SoLuong,MucToiThieu)
              SELECT @MaChiNhanh,MaVatTu,0,0 FROM VatTu;",
            new { MaChiNhanh = ma }, giaoDich);
        await giaoDich.CommitAsync();
        return Ok(new { maChiNhanh = ma, message = "Đã thêm chi nhánh, giá mặc định và kho trống." });
    }

    [HttpPost("may-in")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> TaoMayIn(TaoMayInRequest req)
    {
        if (!DuocLamViecVoiChiNhanh(req.MaChiNhanh))
            return StatusCode(403, new { message = "Máy in không thuộc phạm vi chi nhánh đang chọn." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        bool tonTai = await conn.ExecuteScalarAsync<bool>(
            "SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM ChiNhanh WITH (UPDLOCK,HOLDLOCK) WHERE MaChiNhanh=@MaChiNhanh) THEN 1 ELSE 0 END AS BIT)",
            new { req.MaChiNhanh }, giaoDich);
        if (!tonTai) return NotFound(new { message = "Không tìm thấy chi nhánh." });
        int ma = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO MayIn (MaChiNhanh,TenMay,KhoGiay,InMau,In2Mat,TocDo,SoTrangDaIn,TrangThai)
              VALUES (@MaChiNhanh,@TenMay,@KhoGiay,@InMau,@In2Mat,@TocDo,@SoTrangDaIn,@TrangThai);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", req, giaoDich);
        await giaoDich.CommitAsync();
        return Ok(new { maMayIn = ma, message = "Đã đăng ký máy in. Cần kết nối gateway/máy in ảo để nhận lệnh in." });
    }
}
