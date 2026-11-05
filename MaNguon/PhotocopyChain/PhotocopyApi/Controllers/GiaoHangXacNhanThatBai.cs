using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.Controllers;

// Sơ đồ 4.10: shipper báo giao thất bại (GPS + ảnh) thì lượt giao chờ cửa hàng xem bằng chứng.
// Đúng nơi giao → xác nhận thất bại; sai vị trí → từ chối, shipper tiếp tục lượt giao.
public partial class GiaoHangController
{
    // POST api/giao-hang/luot/5/xac-nhan-that-bai
    [HttpPost("luot/{maGiaoHang:int:min(1)}/xac-nhan-that-bai")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> XacNhanThatBai(int maGiaoHang)
    {
        if (KhongCoPhamVi) return Forbid();
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        int? maDon = await conn.ExecuteScalarAsync<int?>("SELECT MaDonHang FROM GiaoHang WHERE MaGiaoHang=@maGiaoHang", new { maGiaoHang });
        if (maDon == null) return NotFound(new { message = "Không tìm thấy lượt giao trong phạm vi của bạn." });

        // Khóa Đơn → Lượt cùng thứ tự với nhận đơn và báo kết quả để tránh deadlock.
        await using var tran = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
        var don = await conn.QueryFirstOrDefaultAsync<DonDeGiao>(
            @"SELECT MaDonHang,MaKhachHang,MaChiNhanh,TrangThai,HinhThucNhan FROM DonHang WITH (UPDLOCK,HOLDLOCK)
              WHERE MaDonHang=@maDon AND (@MaChiNhanh IS NULL OR MaChiNhanh=@MaChiNhanh)",
            new { maDon, MaChiNhanh = ChiNhanh }, tran);
        if (don == null || don.HinhThucNhan != "GIAO_HANG")
            return NotFound(new { message = "Không tìm thấy lượt giao trong phạm vi của bạn." });
        var luot = await conn.QueryFirstAsync<LuotChoXacNhan>(
            "SELECT MaGiaoHang,MaShipper,TrangThai,LyDo FROM GiaoHang WITH (UPDLOCK,HOLDLOCK) WHERE MaGiaoHang=@maGiaoHang",
            new { maGiaoHang }, tran);
        if (luot.TrangThai != "CHO_XAC_NHAN" || don.TrangThai != "DANG_GIAO")
            return Conflict(new { message = "Lượt giao không còn chờ xác nhận. Có thể người khác đã xử lý, vui lòng tải lại." });

        await conn.ExecuteAsync(
            @"SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
              UPDATE GiaoHang SET TrangThai='THAT_BAI',MaNguoiXuLyThatBai=@MaNguoiDung,NgayXuLyThatBai=GETDATE()
              WHERE MaGiaoHang=@maGiaoHang;
              UPDATE DonHang SET TrangThai='GIAO_THAT_BAI' WHERE MaDonHang=@maDon;",
            new { MaNguoiDung, maGiaoHang, maDon }, tran);
        await ThongBao(conn, tran, don.MaKhachHang, maDon.Value, "Giao hàng chưa thành công", luot.LyDo);
        await ThongBao(conn, tran, luot.MaShipper, maDon.Value, "Cửa hàng đã xác nhận giao thất bại",
            $"DH{maDon:00000}: cửa hàng đã kiểm tra vị trí và bằng chứng của bạn.");
        await conn.ExecuteAsync(@"INSERT INTO ThongBao (MaNguoiDung,MaDonHang,TieuDe,NoiDung)
            SELECT MaNguoiDung,@MaDon,N'Đơn giao thất bại',@LyDo FROM NguoiDung
            WHERE TrangThai=N'HOAT_DONG' AND (VaiTro='SUPER_ADMIN' OR (VaiTro='BRANCH_ADMIN' AND MaChiNhanh=@MaChiNhanh))",
            new { MaDon = maDon, LyDo = $"DH{maDon:00000}: {luot.LyDo}", don.MaChiNhanh }, tran);
        await tran.CommitAsync();

        await GuiTin(don, "GIAO_THAT_BAI", "Cửa hàng xác nhận giao thất bại: " + luot.LyDo, luot.MaShipper);
        return Ok(new { message = "Đã xác nhận giao thất bại. Đơn chuyển sang nhóm Giao thất bại để giao lại nếu còn lượt." });
    }

    // POST api/giao-hang/luot/5/tu-choi-that-bai  { "lyDo": "..." }
    [HttpPost("luot/{maGiaoHang:int:min(1)}/tu-choi-that-bai")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> TuChoiThatBai(int maGiaoHang, TuChoiThatBaiRequest req)
    {
        if (KhongCoPhamVi) return Forbid();
        string lyDoTuChoi = req.LyDo.Trim();
        if (lyDoTuChoi.Length < 5) return BadRequest(new { message = "Lý do từ chối cần từ 5 đến 255 ký tự." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        int? maDon = await conn.ExecuteScalarAsync<int?>("SELECT MaDonHang FROM GiaoHang WHERE MaGiaoHang=@maGiaoHang", new { maGiaoHang });
        if (maDon == null) return NotFound(new { message = "Không tìm thấy lượt giao trong phạm vi của bạn." });

        await using var tran = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
        var don = await conn.QueryFirstOrDefaultAsync<DonDeGiao>(
            @"SELECT MaDonHang,MaKhachHang,MaChiNhanh,TrangThai,HinhThucNhan FROM DonHang WITH (UPDLOCK,HOLDLOCK)
              WHERE MaDonHang=@maDon AND (@MaChiNhanh IS NULL OR MaChiNhanh=@MaChiNhanh)",
            new { maDon, MaChiNhanh = ChiNhanh }, tran);
        if (don == null || don.HinhThucNhan != "GIAO_HANG")
            return NotFound(new { message = "Không tìm thấy lượt giao trong phạm vi của bạn." });
        var luot = await conn.QueryFirstAsync<LuotChoXacNhan>(
            "SELECT MaGiaoHang,MaShipper,TrangThai,LyDo FROM GiaoHang WITH (UPDLOCK,HOLDLOCK) WHERE MaGiaoHang=@maGiaoHang",
            new { maGiaoHang }, tran);
        if (luot.TrangThai != "CHO_XAC_NHAN" || don.TrangThai != "DANG_GIAO")
            return Conflict(new { message = "Lượt giao không còn chờ xác nhận. Có thể người khác đã xử lý, vui lòng tải lại." });

        // Lượt quay về đang giao, không tính thêm lượt; bằng chứng cũ được thay khi shipper báo lại.
        await conn.ExecuteAsync(
            @"SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
              UPDATE GiaoHang SET TrangThai='DANG_GIAO',LyDo=NULL,NgayGiao=NULL,LyDoTuChoi=@LyDoTuChoi,
                  MaNguoiXuLyThatBai=@MaNguoiDung,NgayXuLyThatBai=GETDATE()
              WHERE MaGiaoHang=@maGiaoHang;",
            new { LyDoTuChoi = lyDoTuChoi, MaNguoiDung, maGiaoHang }, tran);
        await ThongBao(conn, tran, luot.MaShipper, maDon.Value, "Báo giao thất bại bị từ chối",
            $"DH{maDon:00000}: {lyDoTuChoi}. Hãy tiếp tục giao đơn này.");
        await tran.CommitAsync();

        await GuiTin(don, "DANG_GIAO", "Cửa hàng từ chối báo giao thất bại: " + lyDoTuChoi, luot.MaShipper, guiKhach: false);
        return Ok(new { message = "Đã từ chối báo giao thất bại. Shipper được báo để tiếp tục giao." });
    }

    private sealed class LuotChoXacNhan
    {
        public int MaGiaoHang { get; set; }
        public int MaShipper { get; set; }
        public string TrangThai { get; set; }
        public string LyDo { get; set; }
    }
}
