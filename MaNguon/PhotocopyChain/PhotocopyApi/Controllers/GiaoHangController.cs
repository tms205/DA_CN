using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using PhotocopyApi.GiaoHang;
using PhotocopyApi.Hubs;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/giao-hang")]
[Authorize]
public partial class GiaoHangController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly GeoapifyService _banDo;
    private readonly IHubContext<CapNhatHub> _hub;
    private readonly ILogger<GiaoHangController> _log;
    private readonly BangChungGiaoHangStore _bangChung;

    public GiaoHangController(IConfiguration config, IMemoryCache cache, IHubContext<CapNhatHub> hub,
        ILogger<GiaoHangController> log, GeoapifyService banDo = null, BangChungGiaoHangStore bangChung = null)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
        _banDo = banDo ?? new GeoapifyService(config, cache);
        _hub = hub;
        _log = log;
        _bangChung = bangChung ?? new BangChungGiaoHangStore(config);
    }

    private int MaNguoiDung => int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
    private int? ChiNhanh => int.TryParse(User.FindFirst(TenClaim.MaChiNhanh)?.Value, out int ma) ? ma : null;
    private bool KhongCoPhamVi => !User.IsInRole(VaiTro.SuperAdmin) && ChiNhanh == null;
    // Phân nhóm theo trạng thái hiện tại và lượt giao mới nhất, không đếm lại các lượt cũ.
    // Lượt shipper đã báo thất bại nhưng cửa hàng chưa xác nhận (CHO_XAC_NHAN) vẫn nằm ở Đang giao.
    private const string NhomDonGiao = @"CASE
        WHEN dh.TrangThai='DA_IN' AND (gh.MaGiaoHang IS NULL OR gh.TrangThai<>'DANG_GIAO') THEN 'CHO_GIAO'
        WHEN dh.TrangThai='DANG_GIAO' AND gh.TrangThai IN ('DANG_GIAO','CHO_XAC_NHAN') THEN 'DANG_GIAO'
        WHEN dh.TrangThai='GIAO_THAT_BAI' AND gh.TrangThai='THAT_BAI' THEN 'THAT_BAI'
        WHEN dh.TrangThai IN ('DA_GIAO','HOAN_THANH') AND gh.TrangThai='THANH_CONG' THEN 'THANH_CONG' END";

    [HttpGet("phan-bo")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> PhanBo()
    {
        if (KhongCoPhamVi) return Forbid();
        using var conn = new SqlConnection(_chuoiKetNoi);
        return Ok(await conn.QueryAsync<PhanBoGiaoHangDto>(@"SELECT Nhom,COUNT(*) AS SoDon FROM (
            SELECT " + NhomDonGiao + @" AS Nhom
            FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
            OUTER APPLY (SELECT TOP 1 MaGiaoHang,TrangThai FROM GiaoHang g WHERE g.MaDonHang=dh.MaDonHang ORDER BY g.MaGiaoHang DESC) gh
            WHERE dh.HinhThucNhan='GIAO_HANG' AND tt.TrangThai='DA_THANH_TOAN'
              AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh)
            ) x WHERE Nhom IS NOT NULL GROUP BY Nhom", new { MaChiNhanh = ChiNhanh }));
    }

    [HttpPost("dia-chi")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> TimDiaChi(DiaChiGiaoRequest req)
    {
        try
        {
            ChiNhanh ganChiNhanh = null;
            if (req.MaChiNhanh != null && req.ViDo == null)
            {
                using var conn = new SqlConnection(_chuoiKetNoi);
                ganChiNhanh = await conn.QueryFirstOrDefaultAsync<ChiNhanh>(
                    "SELECT * FROM ChiNhanh WHERE MaChiNhanh=@MaChiNhanh AND TrangThai=N'HOAT_DONG'", new { req.MaChiNhanh });
            }
            var ds = await _banDo.TimDiaChi(req.DiaChi, req.ViDo, req.KinhDo, HttpContext.RequestAborted, ganChiNhanh);
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
        if (nhom is not ("CHO_GIAO" or "DANG_GIAO" or "THAT_BAI" or "THANH_CONG" or "LICH_SU"))
            return BadRequest(new { message = "Bộ lọc giao hàng không hợp lệ." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? shipper = User.IsInRole(VaiTro.Shipper) ? MaNguoiDung : null;
        // Lịch sử lấy từng lượt, không mất lượt thất bại khi đơn được người khác nhận giao lại.
        string joinGiao = nhom == "LICH_SU"
            ? "JOIN GiaoHang gh ON gh.MaDonHang=dh.MaDonHang AND gh.TrangThai IN ('THANH_CONG','THAT_BAI')"
            : "OUTER APPLY (SELECT TOP 1 * FROM GiaoHang g WHERE g.MaDonHang=dh.MaDonHang ORDER BY g.MaGiaoHang DESC) gh";
        string loc = nhom == "LICH_SU" ? "AND (@MaShipper IS NULL OR gh.MaShipper=@MaShipper)"
            : "AND (" + NhomDonGiao + ")=@Nhom AND (@MaShipper IS NULL OR gh.MaShipper=@MaShipper OR @Nhom='CHO_GIAO')";
        var ds = await conn.QueryAsync<DonGiaoDto>(
            @"SELECT TOP 100 dh.MaDonHang, 'DH'+RIGHT('00000'+CAST(dh.MaDonHang AS VARCHAR(10)),5) AS MaDonHangHienThi,
                     dh.MaChiNhanh, cn.TenChiNhanh, cn.DiaChi AS DiaChiChiNhanh, kh.HoTen AS TenKhachHang,
                     dh.SdtNhan, dh.DiaChiGiao, dh.PhiShip, dh.TongTien, dh.TrangThai AS TrangThaiDonHang,
                     tt.TrangThai AS TrangThaiThanhToan, gh.MaGiaoHang, gh.MaShipper, sp.HoTen AS TenShipper,
                     gh.TrangThai AS TrangThaiGiao, gh.LanGiao, gh.LyDo, gh.LyDoTuChoi, dh.NgayDat, gh.NgayNhan, gh.NgayGiao,
                     CASE WHEN @XemDoiChieu=1 THEN JSON_VALUE(gh.DoiChieuKhachHangJson,'$.TrangThai') ELSE NULL END AS TrangThaiDoiChieu,
                     CASE WHEN @XemDoiChieu=1 THEN (SELECT COUNT(*) FROM GiaoHang ph
                         WHERE ph.MaDonHang=dh.MaDonHang AND ph.TrangThai='THAT_BAI'
                           AND JSON_VALUE(ph.DoiChieuKhachHangJson,'$.TrangThai')='CHO_DOI_CHIEU') ELSE 0 END AS SoPhanHoiChoDoiChieu,
                     (SELECT COUNT(*) FROM GiaoHang dem WHERE dem.MaDonHang=dh.MaDonHang) AS SoLanGiao
              FROM DonHang dh JOIN ChiNhanh cn ON cn.MaChiNhanh=dh.MaChiNhanh
              JOIN NguoiDung kh ON kh.MaNguoiDung=dh.MaKhachHang
              JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang " + joinGiao + @"
              LEFT JOIN NguoiDung sp ON sp.MaNguoiDung=gh.MaShipper
              WHERE dh.HinhThucNhan='GIAO_HANG' AND tt.TrangThai='DA_THANH_TOAN'
                AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh) " + loc +
              (nhom is "LICH_SU" or "THAT_BAI" or "THANH_CONG" ? " ORDER BY gh.MaGiaoHang DESC" : " ORDER BY dh.InGap DESC, dh.NgayHenTra, dh.MaDonHang"),
            new { Nhom=nhom, MaChiNhanh = ChiNhanh, MaShipper = shipper, XemDoiChieu=User.IsInRole(VaiTro.SuperAdmin)||User.IsInRole(VaiTro.BranchAdmin) });
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
            "SELECT TOP 1 MaGiaoHang, MaShipper, TrangThai FROM GiaoHang WHERE MaDonHang=@maDonHang AND TrangThai IN ('DANG_GIAO','CHO_XAC_NHAN') ORDER BY MaGiaoHang DESC", new { maDonHang }, tran);
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
    public Task<IActionResult> BaoKetQua(int maGiaoHang, KetQuaGiaoRequest req)
    {
        if (!req.ThanhCong)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Giao thất bại cần GPS và ảnh nhật ký cuộc gọi. Hãy cập nhật app và dùng Gửi báo cáo giao thất bại." }));
        return GhiKetQua(maGiaoHang, req);
    }

    private async Task<IActionResult> GhiKetQua(int maGiaoHang, KetQuaGiaoRequest req, HoSoBangChung hoSo = null)
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
        // Báo thất bại chưa kết thúc lượt: lượt chờ cửa hàng xem GPS + ảnh rồi xác nhận hoặc từ chối (sơ đồ 4.10).
        string trangThaiGiao = req.ThanhCong ? "THANH_CONG" : "CHO_XAC_NHAN";
        if (luot.TrangThai == trangThaiGiao)
            return Ok(new { message = req.ThanhCong ? "Kết quả giao này đã được ghi nhận." : "Báo cáo giao thất bại đã được gửi, đang chờ cửa hàng xác nhận." });
        if (luot.MaShipper != MaNguoiDung || luot.TrangThai != "DANG_GIAO" || don.TrangThai != "DANG_GIAO")
            return Conflict(new { message = "Lượt giao không còn đang thực hiện. Vui lòng tải lại." });
        string thanhToan = await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM ThanhToan WHERE MaDonHang=@maDon", new { maDon }, tran);
        if (thanhToan != "DA_THANH_TOAN") return Conflict(new { message = "Thanh toán của đơn đã thay đổi. Hãy liên hệ cửa hàng, không thu tiền khách." });
        // Đơn vẫn Đang giao cho tới khi cửa hàng xác nhận thất bại.
        string trangThaiDon = req.ThanhCong ? "HOAN_THANH" : "DANG_GIAO";
        string lyDo = req.ThanhCong ? null : req.LyDo.Trim();
        await conn.ExecuteAsync(
            @"SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
              UPDATE GiaoHang SET TrangThai=@TrangThaiGiao,LyDo=@LyDo,LyDoTuChoi=NULL,TienThu=0,NgayGiao=GETDATE() WHERE MaGiaoHang=@MaGiaoHang;
              UPDATE DonHang SET TrangThai=@TrangThaiDon,
                NgayHoanThanh=CASE WHEN @ThanhCong=1 THEN GETDATE() ELSE NgayHoanThanh END WHERE MaDonHang=@MaDonHang;",
            new { TrangThaiGiao = trangThaiGiao, LyDo = lyDo, MaGiaoHang = maGiaoHang, TrangThaiDon = trangThaiDon, req.ThanhCong, MaDonHang = maDon }, tran);
        if (hoSo != null)
            await conn.ExecuteAsync(@"UPDATE GiaoHang SET ViDoBaoGiao=@ViDo, KinhDoBaoGiao=@KinhDo,
                DoChinhXacGps=@DoChinhXac, ThoiDiemGps=@ThoiDiemGps, ThoiDiemBaoCao=SYSDATETIMEOFFSET(),
                DiaChiBaoGiao=@DiaChi, AnhBangChungJson=@AnhJson WHERE MaGiaoHang=@MaGiaoHang",
                new { hoSo.ViTri.ViDo, hoSo.ViTri.KinhDo, hoSo.ViTri.DoChinhXac, hoSo.ViTri.ThoiDiemGps,
                    hoSo.DiaChi, AnhJson = System.Text.Json.JsonSerializer.Serialize(hoSo.Anh), MaGiaoHang = maGiaoHang }, tran);
        if (req.ThanhCong)
            await ThongBao(conn, tran, don.MaKhachHang, maDon.Value, "Giao hàng thành công",
                "Tài liệu đã giao đến bạn. Cảm ơn bạn đã sử dụng PhotocopyChain.");
        else
            // Khách chỉ được báo khi cửa hàng đã xác nhận; lúc này báo nhân sự chi nhánh và CEO vào xác nhận.
            await conn.ExecuteAsync(@"INSERT INTO ThongBao (MaNguoiDung,MaDonHang,TieuDe,NoiDung)
                SELECT MaNguoiDung,@MaDon,N'Cần xác nhận giao thất bại',@LyDo FROM NguoiDung
                WHERE TrangThai=N'HOAT_DONG' AND (VaiTro='SUPER_ADMIN' OR (VaiTro IN ('BRANCH_ADMIN','STAFF') AND MaChiNhanh=@MaChiNhanh))",
                new { MaDon = maDon, LyDo = $"DH{maDon:00000}: {lyDo}", don.MaChiNhanh }, tran);
        if (hoSo != null) hoSo.BatDauCommit = true;
        await tran.CommitAsync();
        if (hoSo != null) hoSo.DaLuu = true;
        await GuiTin(don, trangThaiDon, req.ThanhCong ? "Đã giao thành công." : "Shipper báo giao thất bại, chờ cửa hàng xác nhận: " + lyDo,
            MaNguoiDung, guiKhach: req.ThanhCong);
        return Ok(new { message = req.ThanhCong ? "Đã giao thành công, đơn hoàn tất. Không thu thêm tiền." : "Đã gửi báo cáo giao thất bại. Chờ cửa hàng xác nhận vị trí và bằng chứng." });
    }

    private static Task ThongBao(SqlConnection conn, SqlTransaction tran, int maNguoi, int maDon, string tieuDe, string noiDung) =>
        conn.ExecuteAsync("INSERT INTO ThongBao (MaNguoiDung,MaDonHang,TieuDe,NoiDung) VALUES (@maNguoi,@maDon,@tieuDe,@noiDung)", new { maNguoi, maDon, tieuDe, noiDung }, tran);

    private async Task GuiTin(DonDeGiao don, string trangThai, string noiDung, int maShipper, bool guiKhach = true)
    {
        try
        {
            var tin = new TinCapNhat { MaDonHang = don.MaDonHang, MaChiNhanh = don.MaChiNhanh,
                MaDonHangHienThi = $"DH{don.MaDonHang:00000}", TrangThai = trangThai, NoiDung = noiDung };
            await CapNhatHub.GuiChoChiNhanh(_hub, don.MaChiNhanh, HubEvents.OrderStatusChanged, tin);
            if (guiKhach) await CapNhatHub.GuiChoNguoiDung(_hub, don.MaKhachHang, HubEvents.OrderStatusChanged, tin);
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
