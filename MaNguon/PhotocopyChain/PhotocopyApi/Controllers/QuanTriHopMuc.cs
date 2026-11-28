using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

// =====================================================================================
// HỘP MỰC MÁY IN (28/11, CSDL 035)
//   GET  api/quan-tri/may-in/{id}/hop-muc   — số trang hộp hiện tại đã in, % năng suất, tồn mực, lịch sử thay
//   PUT  api/quan-tri/may-in/{id}/hop-muc   — quản lý/CEO khai loại hộp mực và năng suất (trang/hộp)
//   POST api/quan-tri/may-in/{id}/thay-muc  — nhân viên báo vừa thay hộp mực mới
//
// Mực KHÔNG trừ theo trang (độ phủ mực mỗi trang khác nhau). Trừ 1 hộp trong kho đúng lúc thay hộp,
// vì đó là lúc hộp mực thật sự rời kho. Bộ đếm MayIn.SoTrangDaIn (cộng sau mỗi đơn in xong) so với
// năng suất hãng công bố để: báo sắp hết mực (≥ 90%) và đánh dấu nghi vấn khi hộp cũ hết dưới 50%.
// =====================================================================================
public partial class QuanTriController
{
    private sealed class MayInMuc
    {
        public int MaMayIn { get; set; }
        public int MaChiNhanh { get; set; }
        public int? MaVatTuMuc { get; set; }
        public int NangSuatHopMuc { get; set; }
        public int SoTrangDaIn { get; set; }
        public int SoTrangLucThayMuc { get; set; }
    }

    [HttpGet("may-in/{maMayIn}/hop-muc")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> XemHopMuc([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maMayIn)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        var hop = await conn.QueryFirstOrDefaultAsync<HopMucMayInDto>(
            @"SELECT m.MaMayIn, m.TenMay, m.MaChiNhanh, m.MaVatTuMuc, vt.TenVatTu AS TenVatTuMuc, m.NangSuatHopMuc,
                     ISNULL(m.SoTrangDaIn, 0) AS SoTrangDaIn, m.SoTrangLucThayMuc,
                     ISNULL((SELECT tk.SoLuong FROM TonKho tk WHERE tk.MaChiNhanh = m.MaChiNhanh AND tk.MaVatTu = m.MaVatTuMuc), 0) AS TonMucTrongKho
              FROM MayIn m LEFT JOIN VatTu vt ON vt.MaVatTu = m.MaVatTuMuc
              WHERE m.MaMayIn = @maMayIn", new { maMayIn });
        if (hop == null || !DuocLamViecVoiChiNhanh(hop.MaChiNhanh))
            return NotFound(new { message = "Không tìm thấy máy in trong phạm vi của bạn." });

        hop.LichSu = (await conn.QueryAsync<LanThayMucDto>(
            @"SELECT TOP 10 ls.ThoiGian, vt.TenVatTu, nd.HoTen AS TenNguoiThay, ls.SoTrangHopCu, ls.NangSuatHopMuc, ls.NghiVan
              FROM LichSuThayMuc ls JOIN VatTu vt ON vt.MaVatTu = ls.MaVatTu JOIN NguoiDung nd ON nd.MaNguoiDung = ls.MaNguoiThay
              WHERE ls.MaMayIn = @maMayIn ORDER BY ls.ThoiGian DESC, ls.MaThayMuc DESC", new { maMayIn })).ToList();
        // Vật tư có trong kho chi nhánh của máy, để quản lý chọn đúng loại hộp mực.
        hop.DsVatTu = (await conn.QueryAsync<VatTuChonDto>(
            @"SELECT vt.MaVatTu, vt.TenVatTu, vt.DonViTinh FROM TonKho tk JOIN VatTu vt ON vt.MaVatTu = tk.MaVatTu
              WHERE tk.MaChiNhanh = @MaChiNhanh ORDER BY vt.TenVatTu", new { hop.MaChiNhanh })).ToList();
        return Ok(hop);
    }

    [HttpPut("may-in/{maMayIn}/hop-muc")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> CauHinhHopMuc([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maMayIn, CauHinhHopMucRequest req)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? maChiNhanh = await conn.ExecuteScalarAsync<int?>("SELECT MaChiNhanh FROM MayIn WHERE MaMayIn = @maMayIn", new { maMayIn });
        if (maChiNhanh == null || !DuocLamViecVoiChiNhanh(maChiNhanh.Value))
            return NotFound(new { message = "Không tìm thấy máy in trong phạm vi của bạn." });
        bool coTrongKho = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM TonKho WHERE MaChiNhanh = @MaChiNhanh AND MaVatTu = @MaVatTu",
            new { MaChiNhanh = maChiNhanh, MaVatTu = req.MaVatTuMuc }) == 1;
        if (!coTrongKho) return BadRequest(new { message = "Loại hộp mực phải là vật tư có trong kho của chi nhánh." });
        await conn.ExecuteAsync("UPDATE MayIn SET MaVatTuMuc = @MaVatTuMuc, NangSuatHopMuc = @NangSuatHopMuc WHERE MaMayIn = @maMayIn",
            new { req.MaVatTuMuc, req.NangSuatHopMuc, maMayIn });
        return Ok(new KetQuaDto { Message = "Đã lưu loại hộp mực và năng suất của máy." });
    }

    [HttpPost("may-in/{maMayIn}/thay-muc")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> ThayMuc([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maMayIn)
    {
        int maNguoiThay = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        // Khóa dòng máy: bấm "Thay mực" 2 lần cùng lúc thì lần sau phải chờ, rồi thấy số trang hộp mới = 0.
        var may = await conn.QueryFirstOrDefaultAsync<MayInMuc>(
            @"SELECT MaMayIn, MaChiNhanh, MaVatTuMuc, NangSuatHopMuc, ISNULL(SoTrangDaIn, 0) AS SoTrangDaIn, SoTrangLucThayMuc
              FROM MayIn WITH (UPDLOCK, HOLDLOCK) WHERE MaMayIn = @maMayIn", new { maMayIn }, tx);
        if (may == null || !DuocLamViecVoiChiNhanh(may.MaChiNhanh))
            return NotFound(new { message = "Không tìm thấy máy in trong phạm vi của bạn." });
        if (may.MaVatTuMuc == null)
            return BadRequest(new { message = "Máy chưa khai loại hộp mực. Quản lý vào cấu hình hộp mực trước." });

        // Trừ 1 hộp trong kho chi nhánh. Điều kiện SoLuong >= 1 nằm trong câu UPDATE: hết hộp thì không trừ được.
        int soDong = await conn.ExecuteAsync(
            "UPDATE TonKho SET SoLuong = SoLuong - 1 WHERE MaChiNhanh = @MaChiNhanh AND MaVatTu = @MaVatTu AND SoLuong >= 1",
            new { may.MaChiNhanh, MaVatTu = may.MaVatTuMuc }, tx);
        if (soDong != 1)
            return Conflict(new { message = "Kho chi nhánh không còn hộp mực loại này. Lập phiếu nhập trước, hoặc kiểm kê nếu thực tế vẫn còn hộp." });

        int soTrangHopCu = Math.Max(0, may.SoTrangDaIn - may.SoTrangLucThayMuc);
        bool nghiVan = QuyTacHopMuc.NghiVan(soTrangHopCu, may.NangSuatHopMuc);
        await conn.ExecuteAsync(
            @"INSERT INTO LichSuThayMuc (MaMayIn, MaVatTu, SoTrangHopCu, NangSuatHopMuc, NghiVan, MaNguoiThay)
              VALUES (@MaMayIn, @MaVatTu, @SoTrangHopCu, @NangSuat, @NghiVan, @MaNguoiThay);
              UPDATE MayIn SET SoTrangLucThayMuc = @SoTrangDaIn WHERE MaMayIn = @MaMayIn;",
            new { may.MaMayIn, MaVatTu = may.MaVatTuMuc, SoTrangHopCu = soTrangHopCu, NangSuat = may.NangSuatHopMuc,
                  NghiVan = nghiVan, MaNguoiThay = maNguoiThay, may.SoTrangDaIn }, tx);
        if (nghiVan)
            await conn.ExecuteAsync(@"INSERT INTO ThongBao (MaNguoiDung, TieuDe, NoiDung)
                SELECT MaNguoiDung, N'Hộp mực hết sớm bất thường', @NoiDung FROM NguoiDung
                WHERE TrangThai = N'HOAT_DONG' AND (VaiTro = 'SUPER_ADMIN' OR (VaiTro = 'BRANCH_ADMIN' AND MaChiNhanh = @MaChiNhanh))",
                new { NoiDung = $"Máy #{may.MaMayIn} thay mực khi hộp cũ mới in {soTrangHopCu}/{may.NangSuatHopMuc} trang.", may.MaChiNhanh }, tx);
        await tx.CommitAsync();
        return Ok(new KetQuaDto { Message = nghiVan
            ? $"Đã thay mực và trừ 1 hộp trong kho. Lưu ý: hộp cũ mới in {soTrangHopCu}/{may.NangSuatHopMuc} trang, đã báo quản lý kiểm tra."
            : $"Đã thay mực và trừ 1 hộp trong kho. Hộp cũ in được {soTrangHopCu} trang." });
    }
}
