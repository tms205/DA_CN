using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using PhotocopyApi.GiaoHang;
using PhotocopyApi.Hubs;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/giao-hang")]
[Authorize]
public class GiaoHangController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly GeoapifyService _banDo;
    private readonly IHubContext<CapNhatHub> _hub;
    private readonly ILogger<GiaoHangController> _log;

    public GiaoHangController(IConfiguration config, IMemoryCache cache, IHubContext<CapNhatHub> hub,
        ILogger<GiaoHangController> log, GeoapifyService banDo = null)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
        _banDo = banDo ?? new GeoapifyService(config, cache);
        _hub = hub;
        _log = log;
    }

    private int MaNguoiDung => int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
    private int? ChiNhanh => int.TryParse(User.FindFirst(TenClaim.MaChiNhanh)?.Value, out int ma) ? ma : null;
    private bool KhongCoPhamVi => !User.IsInRole(VaiTro.SuperAdmin) && ChiNhanh == null;

    [HttpPost("dia-chi")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> TimDiaChi(DiaChiGiaoRequest req)
    {
        try
        {
            var ds = await _banDo.TimDiaChi(req.DiaChi, req.ViDo, req.KinhDo, HttpContext.RequestAborted);
            foreach (var diem in ds) diem.MaViTriGiao = _banDo.GhiNhanViTri(MaNguoiDung, diem);
            return Ok(ds);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("shipper")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> Shipper()
    {
        if (KhongCoPhamVi) return Forbid();
        using var conn = new SqlConnection(_chuoiKetNoi);
        return Ok(await conn.QueryAsync<ShipperDto>(
            @"SELECT MaNguoiDung AS MaShipper, MaChiNhanh, HoTen, SoDienThoai FROM NguoiDung
              WHERE VaiTro='SHIPPER' AND TrangThai=N'HOAT_DONG'
                AND (@MaChiNhanh IS NULL OR MaChiNhanh=@MaChiNhanh) ORDER BY HoTen",
            new { MaChiNhanh = ChiNhanh }));
    }

    [HttpGet]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF,SHIPPER")]
    public async Task<IActionResult> DanhSach([FromQuery] string nhom = "CHO_GIAO")
    {
        if (KhongCoPhamVi) return Forbid();
        if (nhom != "CHO_GIAO" && nhom != "DANG_GIAO" && nhom != "LICH_SU")
            return BadRequest(new { message = "Bộ lọc giao hàng không hợp lệ." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? shipper = User.IsInRole(VaiTro.Shipper) ? MaNguoiDung : null;
        // Lịch sử lấy từng lượt, không mất lượt thất bại khi đơn được người khác nhận giao lại.
        string joinGiao = nhom == "LICH_SU"
            ? "JOIN GiaoHang gh ON gh.MaDonHang=dh.MaDonHang AND gh.TrangThai<>'DANG_GIAO'"
            : "OUTER APPLY (SELECT TOP 1 * FROM GiaoHang g WHERE g.MaDonHang=dh.MaDonHang ORDER BY g.MaGiaoHang DESC) gh";
        string loc = nhom == "CHO_GIAO"
            ? "AND dh.TrangThai IN ('DA_IN','GIAO_THAT_BAI')"
            : nhom == "DANG_GIAO" ? "AND dh.TrangThai='DANG_GIAO' AND (@MaShipper IS NULL OR gh.MaShipper=@MaShipper)"
            : "AND (@MaShipper IS NULL OR gh.MaShipper=@MaShipper)";
        var ds = await conn.QueryAsync<DonGiaoDto>(
            @"SELECT TOP 100 dh.MaDonHang, 'DH'+RIGHT('00000'+CAST(dh.MaDonHang AS VARCHAR(10)),5) AS MaDonHangHienThi,
                     dh.MaChiNhanh, cn.TenChiNhanh, cn.DiaChi AS DiaChiChiNhanh, kh.HoTen AS TenKhachHang,
                     dh.SdtNhan, dh.DiaChiGiao, dh.PhiShip, dh.TongTien, dh.TrangThai AS TrangThaiDonHang,
                     tt.TrangThai AS TrangThaiThanhToan, gh.MaGiaoHang, gh.MaShipper, sp.HoTen AS TenShipper,
                     gh.TrangThai AS TrangThaiGiao, gh.LanGiao, gh.LyDo, dh.NgayDat, gh.NgayNhan, gh.NgayGiao,
                     (SELECT COUNT(*) FROM GiaoHang dem WHERE dem.MaDonHang=dh.MaDonHang) AS SoLanGiao
              FROM DonHang dh JOIN ChiNhanh cn ON cn.MaChiNhanh=dh.MaChiNhanh
              JOIN NguoiDung kh ON kh.MaNguoiDung=dh.MaKhachHang
              JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang " + joinGiao + @"
              LEFT JOIN NguoiDung sp ON sp.MaNguoiDung=gh.MaShipper
              WHERE dh.HinhThucNhan='GIAO_HANG' AND tt.TrangThai='DA_THANH_TOAN'
                AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh) " + loc +
              (nhom == "LICH_SU" ? " ORDER BY gh.MaGiaoHang DESC" : " ORDER BY dh.InGap DESC, dh.NgayHenTra, dh.MaDonHang"),
            new { MaChiNhanh = ChiNhanh, MaShipper = shipper });
        return Ok(ds);
    }

    [HttpPost("{maDonHang:int:min(1)}/nhan")]
    [Authorize(Roles = "SHIPPER")]
    public Task<IActionResult> NhanDon(int maDonHang) => NhanVaPhanCong(maDonHang, MaNguoiDung);

    [HttpPost("{maDonHang:int:min(1)}/phan-cong")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public Task<IActionResult> PhanCong(int maDonHang, PhanCongGiaoRequest req) => NhanVaPhanCong(maDonHang, req.MaShipper);

    // Khóa đơn trước khi đọc lượt giao: hai người bấm đồng thời chỉ một người nhận được.
    private async Task<IActionResult> NhanVaPhanCong(int maDonHang, int maShipper)
    {
        if (KhongCoPhamVi) return Forbid();
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tran = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
        var don = await conn.QueryFirstOrDefaultAsync<DonDeGiao>(
            @"SELECT MaDonHang, MaKhachHang, MaChiNhanh, TrangThai, HinhThucNhan FROM DonHang WITH (UPDLOCK,HOLDLOCK)
              WHERE MaDonHang=@MaDonHang AND (@MaChiNhanh IS NULL OR MaChiNhanh=@MaChiNhanh)",
            new { MaDonHang = maDonHang, MaChiNhanh = ChiNhanh }, tran);
        if (don == null || don.HinhThucNhan != "GIAO_HANG") return NotFound(new { message = "Không tìm thấy đơn giao trong phạm vi của bạn." });
        bool shipperHopLe = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM NguoiDung WHERE MaNguoiDung=@MaShipper AND MaChiNhanh=@MaChiNhanh
              AND VaiTro='SHIPPER' AND TrangThai=N'HOAT_DONG'", new { MaShipper = maShipper, don.MaChiNhanh }, tran) == 1;
        if (!shipperHopLe) return BadRequest(new { message = "Shipper phải đang hoạt động và thuộc chi nhánh giữ đơn." });
        var dangGiao = await conn.QueryFirstOrDefaultAsync<LuotGiao>(
            "SELECT TOP 1 MaGiaoHang, MaShipper, TrangThai FROM GiaoHang WHERE MaDonHang=@maDonHang AND TrangThai='DANG_GIAO' ORDER BY MaGiaoHang DESC", new { maDonHang }, tran);
        if (dangGiao != null)
        {
            if (dangGiao.MaShipper == maShipper && don.TrangThai == "DANG_GIAO")
                return Ok(new { message = "Đơn đã được giao cho shipper này.", maGiaoHang = dangGiao.MaGiaoHang });
            return Conflict(new { message = "Đơn đã có shipper nhận. Vui lòng tải lại danh sách." });
        }
        string thanhToan = await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM ThanhToan WHERE MaDonHang=@maDonHang", new { maDonHang }, tran);
        int soLan = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GiaoHang WHERE MaDonHang=@maDonHang", new { maDonHang }, tran);
        string loi = QuyTacGiaoHang.LoiNhanDon(don.TrangThai, thanhToan, soLan);
        if (loi != null) return Conflict(new { message = loi });
        int maGiao = await conn.ExecuteScalarAsync<int>(
            @"SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
              INSERT INTO GiaoHang (MaDonHang,MaShipper,LanGiao,TrangThai,TienThu)
              VALUES (@MaDonHang,@MaShipper,@LanGiao,'DANG_GIAO',0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { MaDonHang = maDonHang, MaShipper = maShipper, LanGiao = soLan + 1 }, tran);
        await conn.ExecuteAsync("UPDATE DonHang SET TrangThai='DANG_GIAO' WHERE MaDonHang=@maDonHang", new { maDonHang }, tran);
        await ThongBao(conn, tran, don.MaKhachHang, maDonHang, "Đơn hàng đang được giao", "Shipper nội bộ đã nhận tài liệu. Bạn đã thanh toán, không cần trả thêm tiền.");
        if (maShipper != MaNguoiDung) await ThongBao(conn, tran, maShipper, maDonHang, "Bạn có đơn giao mới", $"Cửa hàng đã giao đơn DH{maDonHang:00000} cho bạn. Không thu COD.");
        await tran.CommitAsync();
        await GuiTin(don, "DANG_GIAO", "Shipper đã nhận đơn.", maShipper);
        return Ok(new { maGiaoHang = maGiao, message = "Đã nhận đơn giao. Khách đã thanh toán, không thu tiền khi giao." });
    }

    [HttpPost("luot/{maGiaoHang:int:min(1)}/ket-qua")]
    [Authorize(Roles = "SHIPPER")]
    public async Task<IActionResult> BaoKetQua(int maGiaoHang, KetQuaGiaoRequest req)
    {
        if (KhongCoPhamVi) return Forbid();
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        // Đọc mã trước, rồi khóa theo cùng thứ tự Đơn → Lượt với thao tác nhận để tránh deadlock.
        int? maDon = await conn.ExecuteScalarAsync<int?>("SELECT MaDonHang FROM GiaoHang WHERE MaGiaoHang=@maGiaoHang AND MaShipper=@MaNguoiDung", new { maGiaoHang, MaNguoiDung });
        if (maDon == null) return NotFound(new { message = "Không tìm thấy lượt giao của bạn." });
        await using var tran = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
        var don = await conn.QueryFirstOrDefaultAsync<DonDeGiao>(
            "SELECT MaDonHang,MaKhachHang,MaChiNhanh,TrangThai,HinhThucNhan FROM DonHang WITH (UPDLOCK,HOLDLOCK) WHERE MaDonHang=@maDon AND MaChiNhanh=@MaChiNhanh",
            new { maDon, MaChiNhanh = ChiNhanh }, tran);
        if (don == null) return NotFound(new { message = "Đơn không thuộc chi nhánh của bạn." });
        bool taiKhoanConHoatDong = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM NguoiDung WHERE MaNguoiDung=@MaNguoiDung AND MaChiNhanh=@MaChiNhanh
              AND VaiTro='SHIPPER' AND TrangThai=N'HOAT_DONG'", new { MaNguoiDung, MaChiNhanh = ChiNhanh }, tran) == 1;
        if (!taiKhoanConHoatDong) return Forbid();
        var luot = await conn.QueryFirstAsync<LuotGiao>("SELECT MaGiaoHang,MaShipper,TrangThai FROM GiaoHang WITH (UPDLOCK,HOLDLOCK) WHERE MaGiaoHang=@maGiaoHang", new { maGiaoHang }, tran);
        string trangThaiGiao = req.ThanhCong ? "THANH_CONG" : "THAT_BAI";
        if (luot.TrangThai == trangThaiGiao) return Ok(new { message = "Kết quả giao này đã được ghi nhận." });
        if (luot.MaShipper != MaNguoiDung || luot.TrangThai != "DANG_GIAO" || don.TrangThai != "DANG_GIAO")
            return Conflict(new { message = "Lượt giao không còn đang thực hiện. Vui lòng tải lại." });
        string thanhToan = await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM ThanhToan WHERE MaDonHang=@maDon", new { maDon }, tran);
        if (thanhToan != "DA_THANH_TOAN") return Conflict(new { message = "Thanh toán của đơn đã thay đổi. Hãy liên hệ cửa hàng, không thu tiền khách." });
        string trangThaiDon = req.ThanhCong ? "HOAN_THANH" : "GIAO_THAT_BAI";
        string lyDo = req.ThanhCong ? null : req.LyDo.Trim();
        await conn.ExecuteAsync(
            @"SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
              UPDATE GiaoHang SET TrangThai=@TrangThaiGiao,LyDo=@LyDo,TienThu=0,NgayGiao=GETDATE() WHERE MaGiaoHang=@MaGiaoHang;
              UPDATE DonHang SET TrangThai=@TrangThaiDon,
                NgayHoanThanh=CASE WHEN @ThanhCong=1 THEN GETDATE() ELSE NgayHoanThanh END WHERE MaDonHang=@MaDonHang;",
            new { TrangThaiGiao = trangThaiGiao, LyDo = lyDo, MaGiaoHang = maGiaoHang, TrangThaiDon = trangThaiDon, req.ThanhCong, MaDonHang = maDon }, tran);
        await ThongBao(conn, tran, don.MaKhachHang, maDon.Value,
            req.ThanhCong ? "Giao hàng thành công" : "Giao hàng chưa thành công",
            req.ThanhCong ? "Tài liệu đã giao đến bạn. Cảm ơn bạn đã sử dụng PhotocopyChain." : lyDo);
        if (!req.ThanhCong)
            await conn.ExecuteAsync(@"INSERT INTO ThongBao (MaNguoiDung,MaDonHang,TieuDe,NoiDung)
                SELECT MaNguoiDung,@MaDon,N'Đơn giao thất bại',@LyDo FROM NguoiDung
                WHERE TrangThai=N'HOAT_DONG' AND (VaiTro='SUPER_ADMIN' OR (VaiTro='BRANCH_ADMIN' AND MaChiNhanh=@MaChiNhanh))",
                new { MaDon = maDon, LyDo = $"DH{maDon:00000}: {lyDo}", don.MaChiNhanh }, tran);
        await tran.CommitAsync();
        await GuiTin(don, trangThaiDon, req.ThanhCong ? "Đã giao thành công." : "Giao thất bại: " + lyDo, MaNguoiDung);
        return Ok(new { message = req.ThanhCong ? "Đã giao thành công, đơn hoàn tất. Không thu thêm tiền." : "Đã lưu lý do giao thất bại và thông báo cửa hàng." });
    }

    private static Task ThongBao(SqlConnection conn, SqlTransaction tran, int maNguoi, int maDon, string tieuDe, string noiDung) =>
        conn.ExecuteAsync("INSERT INTO ThongBao (MaNguoiDung,MaDonHang,TieuDe,NoiDung) VALUES (@maNguoi,@maDon,@tieuDe,@noiDung)", new { maNguoi, maDon, tieuDe, noiDung }, tran);

    private async Task GuiTin(DonDeGiao don, string trangThai, string noiDung, int maShipper)
    {
        try
        {
            var tin = new TinCapNhat { MaDonHang = don.MaDonHang, MaChiNhanh = don.MaChiNhanh,
                MaDonHangHienThi = $"DH{don.MaDonHang:00000}", TrangThai = trangThai, NoiDung = noiDung };
            await CapNhatHub.GuiChoChiNhanh(_hub, don.MaChiNhanh, HubEvents.OrderStatusChanged, tin);
            await CapNhatHub.GuiChoNguoiDung(_hub, don.MaKhachHang, HubEvents.OrderStatusChanged, tin);
            await CapNhatHub.GuiChoNguoiDung(_hub, maShipper, HubEvents.OrderStatusChanged, tin);
        }
        catch { _log.LogWarning("Đơn {MaDonHang} đã lưu kết quả giao nhưng chưa gửi được cập nhật realtime.", don.MaDonHang); }
    }

    private sealed class DonDeGiao
    {
        public int MaDonHang { get; set; }
        public int MaKhachHang { get; set; }
        public int MaChiNhanh { get; set; }
        public string TrangThai { get; set; }
        public string HinhThucNhan { get; set; }
    }
    private sealed class LuotGiao
    {
        public int MaGiaoHang { get; set; }
        public int MaShipper { get; set; }
        public string TrangThai { get; set; }
    }
}
