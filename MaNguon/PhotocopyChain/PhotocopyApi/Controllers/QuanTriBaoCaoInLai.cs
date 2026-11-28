using System.Globalization;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

// =====================================================================================
// BÁO CÁO IN LẠI DO LỖI QUẦY THEO NHÂN VIÊN
//   GET api/quan-tri/bao-cao-in-lai?thang=2026-11
//
// Đơn in lại (LoaiPhatSinh = 'IN_LAI_LOI_QUAY') là đơn 0đ, cửa hàng chịu giấy mực.
// Mỗi lần in lại đã lưu: ai tạo (MaNguoiTaoPhatSinh), lý do (LyDoPhatSinh), đơn gốc (MaDonHangGoc).
// Báo cáo này chỉ ĐỌC các cột đó rồi gom theo nhân viên để quản lý thấy ai in lại nhiều bất thường.
//
// Phạm vi: quản lý chi nhánh chỉ thấy chi nhánh mình (claim trong token);
//          CEO chưa chọn chi nhánh thì thấy toàn chuỗi, chọn rồi thì thấy chi nhánh đó.
// Lần in lại đã hủy (DA_HUY) không tính, giống quy tắc tối đa 3 lần in lại.
// =====================================================================================
public partial class QuanTriController
{
    [HttpGet("bao-cao-in-lai")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> BaoCaoInLai([FromQuery] string thang = "")
    {
        // Tháng dạng yyyy-MM; bỏ trống thì lấy tháng hiện tại.
        DateTime dauThang;
        if (string.IsNullOrWhiteSpace(thang))
            dauThang = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        else if (!DateTime.TryParseExact(thang.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out dauThang)
                 || dauThang.Year < 2000 || dauThang.Year > 2100)
            return BadRequest(new { message = "Tháng không hợp lệ (định dạng yyyy-MM)." });

        var thamSo = new
        {
            TuNgay = dauThang,
            NgaySau = dauThang.AddMonths(1),
            ChiNhanh = LayChiNhanhTuToken()
        };

        // Số trang của một đơn = (đến trang - từ trang + 1) × số bản; dòng ảnh thẻ không có từ/đến trang thì lấy SoTrang.
        const string soTrangCuaDon = @"(SELECT ISNULL(SUM(ISNULL(ct.DenTrang - ct.TuTrang + 1, ct.SoTrang) * ISNULL(ct.SoBan, 1)), 0)
                                         FROM ChiTietDonHang ct WHERE ct.MaDonHang = dh.MaDonHang)";

        const string dieuKienInLai = @"dh.LoaiPhatSinh = 'IN_LAI_LOI_QUAY' AND dh.TrangThai <> 'DA_HUY'
              AND dh.MaNguoiTaoPhatSinh IS NOT NULL
              AND dh.NgayDat >= @TuNgay AND dh.NgayDat < @NgaySau
              AND (@ChiNhanh IS NULL OR dh.MaChiNhanh = @ChiNhanh)";

        string sql = @"
            -- 1) Tổng hợp theo nhân viên + chi nhánh nơi làm đơn
            WITH inLai AS (
                SELECT dh.MaDonHang, dh.MaDonHangGoc, dh.MaNguoiTaoPhatSinh AS MaNV, dh.MaChiNhanh,
                       " + soTrangCuaDon + @" AS SoTrang
                FROM DonHang dh
                WHERE " + dieuKienInLai + @"
            ),
            donThuong AS (
                -- Đơn bình thường người đó tiếp nhận trong tháng, để tính tỷ lệ in lại trên số đơn đã làm
                SELECT dh.MaNhanVien AS MaNV, dh.MaChiNhanh, COUNT(*) AS SoDon
                FROM DonHang dh
                WHERE ISNULL(dh.LoaiPhatSinh, '') <> 'IN_LAI_LOI_QUAY' AND dh.MaNhanVien IS NOT NULL
                  AND dh.TrangThai <> 'DA_HUY'
                  AND dh.NgayDat >= @TuNgay AND dh.NgayDat < @NgaySau
                  AND (@ChiNhanh IS NULL OR dh.MaChiNhanh = @ChiNhanh)
                GROUP BY dh.MaNhanVien, dh.MaChiNhanh
            ),
            tongInLai AS (
                SELECT MaNV, MaChiNhanh, COUNT(*) AS SoLan, COUNT(DISTINCT MaDonHangGoc) AS SoDonGoc, SUM(SoTrang) AS SoTrang
                FROM inLai GROUP BY MaNV, MaChiNhanh
            )
            SELECT nd.MaNguoiDung AS MaNhanVien, nd.HoTen, cn.TenChiNhanh,
                   ISNULL(dt.SoDon, 0) AS SoDonDaXuLy,
                   ISNULL(t.SoLan, 0) AS SoLanInLai,
                   ISNULL(t.SoDonGoc, 0) AS SoDonGocBiInLai,
                   ISNULL(t.SoTrang, 0) AS SoTrangInLai
            FROM (SELECT MaNV, MaChiNhanh FROM tongInLai UNION SELECT MaNV, MaChiNhanh FROM donThuong) ds
            JOIN NguoiDung nd ON nd.MaNguoiDung = ds.MaNV
            JOIN ChiNhanh cn ON cn.MaChiNhanh = ds.MaChiNhanh
            LEFT JOIN tongInLai t ON t.MaNV = ds.MaNV AND t.MaChiNhanh = ds.MaChiNhanh
            LEFT JOIN donThuong dt ON dt.MaNV = ds.MaNV AND dt.MaChiNhanh = ds.MaChiNhanh
            WHERE nd.VaiTro <> 'CUSTOMER'
            ORDER BY ISNULL(t.SoLan, 0) DESC, nd.HoTen;

            -- 2) Từng lần in lại, mới nhất trước, để xem lý do
            SELECT dh.MaDonHang, dh.MaDonHangGoc, dh.MaNguoiTaoPhatSinh AS MaNhanVien,
                   nv.HoTen AS TenNhanVien, ISNULL(kh.HoTen, N'') AS TenKhachHang, dh.NgayDat,
                   " + soTrangCuaDon + @" AS SoTrang,
                   ISNULL(dh.LyDoPhatSinh, N'') AS LyDo, dh.TrangThai
            FROM DonHang dh
            JOIN NguoiDung nv ON nv.MaNguoiDung = dh.MaNguoiTaoPhatSinh
            LEFT JOIN NguoiDung kh ON kh.MaNguoiDung = dh.MaKhachHang
            WHERE " + dieuKienInLai + @"
            ORDER BY dh.NgayDat DESC, dh.MaDonHang DESC;";

        using var conn = new SqlConnection(_chuoiKetNoi);
        using var ketQua = await conn.QueryMultipleAsync(sql, thamSo);
        var baoCao = new BaoCaoInLaiDto
        {
            Thang = dauThang.ToString("yyyy-MM"),
            TheoNhanVien = (await ketQua.ReadAsync<InLaiTheoNhanVienDto>()).ToList(),
            ChiTiet = (await ketQua.ReadAsync<LanInLaiDto>()).ToList()
        };
        // Ảnh bản in lỗi lưu ngoài SQL (thư mục ReprintEvidence) → đếm số ảnh của từng lần in lại.
        foreach (LanInLaiDto lan in baoCao.ChiTiet)
            lan.SoAnh = _bangChungInLai.DocTheoDonInLai(lan.MaDonHang)?.Anh.Count ?? 0;
        return Ok(baoCao);
    }
}
