using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.DuLieu;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/quan-tri/bao-cao")]
[Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
public class BaoCaoTongQuanController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    public BaoCaoTongQuanController(IConfiguration config) => _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");

    [HttpGet("phan-tich")]
    public async Task<IActionResult> PhanTich([FromQuery] DateTime tuNgay, [FromQuery] DateTime denNgay)
    {
        if (!LayPhamVi(out int? maChiNhanh)) return StatusCode(403);
        DateTime tu = tuNgay.Date, den = denNgay.Date;
        if (tu.Year < 1753 || den >= DateTime.MaxValue.Date || den < tu || (den-tu).Days >= 366)
            return BadRequest(new { message = "Chọn từ 1 đến 366 ngày trong khoảng SQL Server hỗ trợ." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        using var ds = await conn.QueryMultipleAsync(BaoCaoTongQuanSql.PhanTich,
            new { TuNgay = tu, NgaySau = den.AddDays(1), MaChiNhanh = maChiNhanh });
        var kq = await ds.ReadSingleAsync<TongQuanPhanTichDto>();
        kq.DichVu = (await ds.ReadAsync<CoCauDoanhThuDto>()).ToList();
        kq.ThanhToan = (await ds.ReadAsync<CoCauDoanhThuDto>()).ToList();
        kq.GioCaoDiem = (await ds.ReadAsync<GioCaoDiemDto>()).ToList();
        kq.HieuQua = await ds.ReadSingleAsync<HieuQuaDonDto>();
        kq.DanhGia = await ds.ReadSingleAsync<DanhGiaTongQuanDto>();
        return Ok(kq);
    }

    [HttpGet("hien-tai")]
    public async Task<IActionResult> HienTai()
    {
        if (!LayPhamVi(out int? maChiNhanh)) return StatusCode(403);
        DateTime docLuc = DateTime.Now, tu = docLuc.Date.AddDays(-29);
        using var conn = new SqlConnection(_chuoiKetNoi);
        using var ds = await conn.QueryMultipleAsync(BaoCaoTongQuanSql.HienTai,
            new { TuNgay = tu, DocLuc = docLuc, MaChiNhanh = maChiNhanh });
        var kq = await ds.ReadSingleAsync<TongQuanHienTaiDto>();
        var ca = await ds.ReadSingleAsync<TongQuanHienTaiDto>();
        kq.SoCaKiemDem = ca.SoCaKiemDem;
        kq.SoCaLech = ca.SoCaLech;
        kq.TienLech = ca.TienLech;
        kq.SoPhieuChoDuyet = await ds.ReadSingleAsync<int>();
        int hoanTien = await ds.ReadSingleAsync<int>();
        // Cả quản lý và CEO thấy số yêu cầu trong phạm vi; chỉ CEO có quyền quyết định.
        kq.SoHoanTienChoDuyet = hoanTien;
        kq.DocLuc = docLuc;
        kq.TuNgayKiemDem = tu;
        return Ok(kq);
    }

    [HttpGet("yeu-cau-hoan-tien")]
    public async Task<IActionResult> YeuCauHoanTien()
    {
        if (!LayPhamVi(out int? maChiNhanh)) return StatusCode(403);
        Response.Headers.CacheControl = "no-store";
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync<CanhBaoHoanTienDto>(@"SELECT dh.MaDonHang,kh.HoTen AS TenKhachHang,cn.TenChiNhanh,tt.SoTien,
            CASE WHEN dh.TrangThai='DA_HUY' THEN 'HUY_DON' ELSE 'SAN_PHAM' END AS Loai,
            CASE WHEN dh.TrangThai='DA_HUY' THEN 'CHAP_THUAN' ELSE JSON_VALUE(dh.YeuCauHoanTienJson,'$.TrangThai') END AS TrangThai,
            COALESCE(TRY_CONVERT(DATETIMEOFFSET,JSON_VALUE(dh.YeuCauHoanTienJson,'$.GuiLuc')),
                TRY_CONVERT(DATETIMEOFFSET,JSON_VALUE(dh.BangChungHuyJson,'$.GuiLuc')),
                TODATETIMEOFFSET(dh.NgayDat,'+07:00')) AS GuiLuc
            FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
            JOIN NguoiDung kh ON kh.MaNguoiDung=dh.MaKhachHang JOIN ChiNhanh cn ON cn.MaChiNhanh=dh.MaChiNhanh
            WHERE tt.TrangThai='DA_THANH_TOAN' AND ((dh.TrangThai='DA_HUY' AND dh.MaMayIn IS NULL)
                OR JSON_VALUE(dh.YeuCauHoanTienJson,'$.TrangThai') IN ('CHO_DUYET','CHAP_THUAN'))
              AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh)
            ORDER BY GuiLuc DESC,dh.MaDonHang DESC",new{MaChiNhanh=maChiNhanh});
        return Ok(ds);
    }

    private bool LayPhamVi(out int? maChiNhanh)
    {
        maChiNhanh = null;
        var claim = User.FindFirst(TenClaim.MaChiNhanh);
        if (claim != null)
        {
            if (!int.TryParse(claim.Value, out int ma) || ma <= 0) return false;
            maChiNhanh = ma;
            return User.IsInRole(VaiTro.SuperAdmin) || User.IsInRole(VaiTro.BranchAdmin);
        }
        return User.IsInRole(VaiTro.SuperAdmin);
    }
}
