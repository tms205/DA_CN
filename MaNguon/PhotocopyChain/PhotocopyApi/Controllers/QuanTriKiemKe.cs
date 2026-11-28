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
// KIỂM KÊ KHO (28/11, CSDL 034)
//   GET  api/quan-tri/kiem-ke              — danh sách phiếu kiểm kê trong phạm vi chi nhánh
//   POST api/quan-tri/kiem-ke              — quản lý lập phiếu: nhập số đếm thực tế của 1 vật tư
//   POST api/quan-tri/kiem-ke/{id}/duyet   — CEO duyệt / từ chối
//
// Khác NHẬN HÀNG: nhận hàng chỉ đếm số hàng vừa giao tới; kiểm kê đếm lại TOÀN BỘ số đang có trên kệ
// để phát hiện hao hụt ngoài in ấn (kẹt giấy, ẩm hỏng, lấy dùng riêng).
// Lúc lập: ghi lại tồn theo sổ ngay lúc đó và chênh lệch. Lúc duyệt: CỘNG chênh lệch vào tồn hiện tại.
// Không lệch thì phiếu chỉ là biên bản, duyệt luôn. CEO tự lập thì duyệt luôn (giống phiếu nhập).
// =====================================================================================
public partial class QuanTriController
{
    private const string SqlDanhSachKiemKe = @"
        SELECT pk.MaPhieuKiemKe, pk.MaChiNhanh, cn.TenChiNhanh, pk.MaVatTu, vt.TenVatTu, vt.DonViTinh, vt.DonGia,
               pk.TonTheoSo, pk.SoLuongThucDem, pk.ChenhLech, pk.LyDo,
               lap.HoTen AS TenNguoiLap, duyet.HoTen AS TenNguoiDuyet, pk.TrangThai, pk.NgayLap, pk.NgayDuyet
        FROM PhieuKiemKe pk
        JOIN ChiNhanh cn ON cn.MaChiNhanh = pk.MaChiNhanh
        JOIN VatTu vt ON vt.MaVatTu = pk.MaVatTu
        LEFT JOIN NguoiDung lap ON lap.MaNguoiDung = pk.MaNguoiLap
        LEFT JOIN NguoiDung duyet ON duyet.MaNguoiDung = pk.MaNguoiDuyet";

    [HttpGet("kiem-ke")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> DanhSachKiemKe()
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync<PhieuKiemKeDto>(SqlDanhSachKiemKe +
            " WHERE @MaChiNhanh IS NULL OR pk.MaChiNhanh = @MaChiNhanh ORDER BY pk.NgayLap DESC, pk.MaPhieuKiemKe DESC",
            new { MaChiNhanh = LayChiNhanhTuToken() });
        return Ok(ds);
    }

    [HttpPost("kiem-ke")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> TaoPhieuKiemKe(TaoPhieuKiemKeRequest req)
    {
        int maNguoiLap = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int? maChiNhanh = LayChiNhanhTuToken();
        if (maChiNhanh == null)
            return BadRequest(new { message = "Vui lòng chọn một chi nhánh trước khi kiểm kê." });
        bool laCeo = User.IsInRole(VaiTro.SuperAdmin);
        string lyDo = string.IsNullOrWhiteSpace(req.LyDo) ? null : req.LyDo.Trim();

        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Khóa dòng tồn kho: trong lúc ghi phiếu không cho đơn khác trừ kho chen vào giữa.
        int? tonTheoSo = await conn.ExecuteScalarAsync<int?>(
            "SELECT SoLuong FROM TonKho WITH (UPDLOCK, HOLDLOCK) WHERE MaChiNhanh = @MaChiNhanh AND MaVatTu = @MaVatTu",
            new { MaChiNhanh = maChiNhanh, req.MaVatTu }, tx);
        if (tonTheoSo == null) return NotFound(new { message = "Vật tư này chưa có trong kho của chi nhánh." });

        int chenhLech = QuyTacKiemKe.ChenhLech(tonTheoSo.Value, req.SoLuongThucDem);
        string loi = QuyTacKiemKe.LoiLyDo(chenhLech, lyDo);
        if (loi != null) return BadRequest(new { message = loi });

        bool choVaoKhoNgay = chenhLech == 0 || laCeo;   // không lệch, hoặc CEO tự lập → duyệt luôn
        try
        {
            int maPhieu = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO PhieuKiemKe (MaChiNhanh, MaVatTu, TonTheoSo, SoLuongThucDem, ChenhLech, LyDo,
                                           MaNguoiLap, MaNguoiDuyet, TrangThai, NgayDuyet)
                  VALUES (@MaChiNhanh, @MaVatTu, @TonTheoSo, @SoDem, @ChenhLech, @LyDo, @MaNguoiLap,
                          CASE WHEN @DuyetLuon = 1 THEN @MaNguoiLap END,
                          CASE WHEN @DuyetLuon = 1 THEN 'DA_DUYET' ELSE 'CHO_DUYET' END,
                          CASE WHEN @DuyetLuon = 1 THEN GETDATE() END);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { MaChiNhanh = maChiNhanh, req.MaVatTu, TonTheoSo = tonTheoSo, SoDem = req.SoLuongThucDem,
                      ChenhLech = chenhLech, LyDo = lyDo, MaNguoiLap = maNguoiLap, DuyetLuon = choVaoKhoNgay }, tx);

            if (choVaoKhoNgay && chenhLech != 0)
                await conn.ExecuteAsync(
                    "UPDATE TonKho SET SoLuong = SoLuong + @ChenhLech WHERE MaChiNhanh = @MaChiNhanh AND MaVatTu = @MaVatTu",
                    new { ChenhLech = chenhLech, MaChiNhanh = maChiNhanh, req.MaVatTu }, tx);
            else if (!choVaoKhoNgay)
                await conn.ExecuteAsync(@"INSERT INTO ThongBao (MaNguoiDung, TieuDe, NoiDung)
                    SELECT MaNguoiDung, N'Phiếu kiểm kê chờ duyệt', @NoiDung FROM NguoiDung
                    WHERE VaiTro = 'SUPER_ADMIN' AND TrangThai = N'HOAT_DONG'",
                    new { NoiDung = $"Phiếu KK{maPhieu:0000} lệch {chenhLech:+#;-#;0} so với tồn trên máy. Lý do: {lyDo}" }, tx);
            await tx.CommitAsync();
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new { message = "Vật tư này đang có một phiếu kiểm kê chờ CEO duyệt. Chờ duyệt xong rồi kiểm kê lại." });
        }

        string thongBao = chenhLech == 0
            ? "Số đếm khớp với tồn trên máy. Đã lưu biên bản kiểm kê, tồn kho không đổi."
            : laCeo ? $"Đã kiểm kê và điều chỉnh tồn kho {chenhLech:+#;-#;0}."
                    : $"Đã gửi phiếu kiểm kê lệch {chenhLech:+#;-#;0}, chờ CEO duyệt rồi mới điều chỉnh tồn.";
        return Ok(new KetQuaDto { Message = thongBao });
    }

    [HttpPost("kiem-ke/{maPhieuKiemKe}/duyet")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> DuyetPhieuKiemKe([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maPhieuKiemKe, DuyetPhieuKiemKeRequest req)
    {
        int maNguoiDuyet = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var phieu = await conn.QueryFirstOrDefaultAsync<PhieuKiemKeDto>(
            @"SELECT MaPhieuKiemKe, MaChiNhanh, MaVatTu, ChenhLech, TrangThai
              FROM PhieuKiemKe WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuKiemKe = @Ma", new { Ma = maPhieuKiemKe }, tx);
        if (phieu == null) return NotFound(new { message = "Không tìm thấy phiếu kiểm kê." });
        if (!DuocLamViecVoiChiNhanh(phieu.MaChiNhanh))
            return StatusCode(403, new { message = "Phiếu kiểm kê không thuộc chi nhánh đang chọn." });
        if (phieu.TrangThai != "CHO_DUYET") return Conflict(new { message = "Phiếu không còn chờ duyệt. Hãy tải lại." });

        if (req.DongY)
        {
            // Cộng chênh lệch vào tồn HIỆN TẠI. Điều kiện >= 0 nằm ngay trong câu UPDATE để không bao giờ làm tồn âm.
            int soDong = await conn.ExecuteAsync(
                @"UPDATE TonKho SET SoLuong = SoLuong + @ChenhLech
                  WHERE MaChiNhanh = @MaChiNhanh AND MaVatTu = @MaVatTu AND SoLuong + @ChenhLech >= 0",
                new { phieu.ChenhLech, phieu.MaChiNhanh, phieu.MaVatTu }, tx);
            if (soDong != 1)
                return Conflict(new { message = "Tồn hiện tại đã nhỏ hơn phần hao hụt cần trừ (có thể đã in thêm sau lúc đếm). Hãy từ chối phiếu này và kiểm kê lại." });
        }
        await conn.ExecuteAsync(
            @"UPDATE PhieuKiemKe SET TrangThai = @TrangThai, MaNguoiDuyet = @MaNguoiDuyet, NgayDuyet = GETDATE()
              WHERE MaPhieuKiemKe = @Ma AND TrangThai = 'CHO_DUYET'",
            new { TrangThai = req.DongY ? "DA_DUYET" : "TU_CHOI", MaNguoiDuyet = maNguoiDuyet, Ma = maPhieuKiemKe }, tx);
        await tx.CommitAsync();
        return Ok(new KetQuaDto { Message = req.DongY
            ? $"Đã duyệt và điều chỉnh tồn kho {phieu.ChenhLech:+#;-#;0}."
            : "Đã từ chối phiếu kiểm kê. Tồn kho không đổi." });
    }
}
