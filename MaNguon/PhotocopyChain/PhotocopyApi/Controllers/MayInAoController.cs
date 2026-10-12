using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Hubs;
using PhotocopyApi.MayInAo;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/may-in-ao")]
public sealed class MayInAoController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly string _khoaConnector;
    private readonly TrangThaiMayInAoStore _trangThai;
    private readonly QuyenNhanLenhInStore _quyenNhanLenh;
    private readonly IHubContext<CapNhatHub> _hub;
    private readonly ILogger<MayInAoController> _logger;

    public MayInAoController(
        IConfiguration config,
        TrangThaiMayInAoStore trangThai,
        IHubContext<CapNhatHub> hub,
        ILogger<MayInAoController> logger,
        QuyenNhanLenhInStore quyenNhanLenh)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain")!;
        _khoaConnector = config["PrinterConnector:ApiKey"];
        _trangThai = trangThai;
        _hub = hub;
        _logger = logger;
        _quyenNhanLenh = quyenNhanLenh;
    }

    [HttpPost("{maMayIn:int}/heartbeat")]
    [AllowAnonymous]
    public async Task<IActionResult> Heartbeat([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maMayIn, BaoCaoMayInRequest req)
    {
        IActionResult loiKhoa = KiemTraKhoa();
        if (loiKhoa != null) return loiKhoa;

        using var conn = new SqlConnection(_chuoiKetNoi);
        bool tonTai = await conn.ExecuteScalarAsync<bool>(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM MayIn WHERE MaMayIn=@MaMayIn) THEN 1 ELSE 0 END AS BIT)",
            new { MaMayIn = maMayIn });
        if (!tonTai) return NotFound(new { message = "Không tìm thấy máy in." });

        var lenhDangIn = _quyenNhanLenh.LayLenhDangIn(maMayIn);
        if (lenhDangIn != null && lenhDangIn.ConnectorId != ConnectorId())
            return Conflict(new { message = "Máy đang có lệnh thuộc connector khác." });
        if (req.MaDonHang != null && !_quyenNhanLenh.CoQuyen(
            maMayIn, req.MaDonHang.Value, ConnectorId(), req.MaLenh))
            return StatusCode(403, new { message = "Không có quyền báo trạng thái lệnh này." });

        string trangThai = string.IsNullOrWhiteSpace(req.Loi)
            ? (string.IsNullOrWhiteSpace(req.TrangThai) ? "SAN_SANG" : req.TrangThai.Trim().ToUpperInvariant())
            : "LOI";
        _trangThai.CapNhat(maMayIn, trangThai, lenhDangIn?.MaDonHang, req.Loi);
        return Ok(new KetQuaDto { Message = "Đã nhận heartbeat." });
    }

    [HttpGet("{maMayIn:int}/lenh-tiep-theo")]
    [AllowAnonymous]
    public async Task<IActionResult> LenhTiepTheo([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maMayIn)
    {
        IActionResult loiKhoa = KiemTraKhoa();
        if (loiKhoa != null) return loiKhoa;

        var lenhDangIn = _quyenNhanLenh.LayLenhDangIn(maMayIn);
        if (lenhDangIn != null && lenhDangIn.ConnectorId != ConnectorId())
            return Conflict(new { message = "Máy đang có lệnh thuộc connector khác." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        LenhInAoDto lenh = await conn.QueryFirstOrDefaultAsync<LenhInAoDto>(
            @"SELECT TOP 1 dh.MaDonHang,
                     'DH' + RIGHT('00000' + CAST(dh.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     dh.MaMayIn, m.TenMay
              FROM DonHang dh
              JOIN MayIn m ON m.MaMayIn = dh.MaMayIn
              JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
              WHERE dh.MaMayIn = @MaMayIn AND dh.TrangThai = 'DANG_IN'
                AND tt.PhuongThuc='VIETQR' AND tt.TrangThai='DA_THANH_TOAN'
                AND (@MaDonDangIn IS NULL OR dh.MaDonHang = @MaDonDangIn)
                AND (m.TrangThai = 'HOAT_DONG' OR @MaDonDangIn IS NOT NULL)
              ORDER BY dh.InGap DESC, dh.NgayDat, dh.MaDonHang",
            new { MaMayIn = maMayIn, MaDonDangIn = lenhDangIn?.MaDonHang });

        if (lenh == null)
        {
            if (lenhDangIn != null)
                return Conflict(new { message = "Lệnh đang giữ không còn ở trạng thái đang in; cần kiểm tra thủ công." });
            _trangThai.CapNhat(maMayIn, "SAN_SANG");
            return NoContent();
        }

        var quyen = _quyenNhanLenh.NhanLenh(maMayIn, lenh.MaDonHang, ConnectorId());
        if (quyen == null) return Conflict(new { message = "Lệnh đã được connector khác nhận." });
        lenh.MaLenh = quyen.MaLenh;

        lenh.DsDongIn = (await conn.QueryAsync<DongLenhInDto>(
            @"SELECT ct.MaChiTiet, ct.MaFile, f.TenFile,
                     dv.KhoGiay, dv.MauSac, dv.MatIn, ct.HuongGiay, ct.SoTrang, ct.SoBan, ct.TuTrang, ct.DenTrang
              FROM ChiTietDonHang ct
              JOIN FileIn f ON f.MaFile = ct.MaFile
              JOIN DichVu dv ON dv.MaDichVu = ct.MaDichVu
              WHERE ct.MaDonHang = @MaDonHang
              ORDER BY ct.MaChiTiet",
            new { lenh.MaDonHang })).ToList();

        foreach (DongLenhInDto dong in lenh.DsDongIn)
            dong.DuongDanTaiFile = $"api/may-in-ao/don/{lenh.MaDonHang}/file/{dong.MaFile}";

        _trangThai.CapNhat(maMayIn, "DANG_IN", lenh.MaDonHang);
        return Ok(lenh);
    }

    [HttpGet("don/{maDonHang:int}/file/{maFile:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> TaiFile([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, [Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maFile)
    {
        IActionResult loiKhoa = KiemTraKhoa();
        if (loiKhoa != null) return loiKhoa;

        using var conn = new SqlConnection(_chuoiKetNoi);
        int? maMayIn = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaMayIn FROM DonHang WHERE MaDonHang=@MaDonHang", new { MaDonHang = maDonHang });
        if (maMayIn == null || !_quyenNhanLenh.CoQuyen(maMayIn.Value, maDonHang,
            ConnectorId(), Request.Headers["X-Printer-Job-Token"].ToString()))
            return StatusCode(403, new { message = "Không có quyền tải file của lệnh này." });
        FileChoMayIn file = await conn.QueryFirstOrDefaultAsync<FileChoMayIn>(
            @"SELECT f.TenFile, f.DuongDan, f.TrangThai
              FROM DonHang dh
              JOIN ChiTietDonHang ct ON ct.MaDonHang = dh.MaDonHang
              JOIN FileIn f ON f.MaFile = ct.MaFile
              WHERE dh.MaDonHang = @MaDonHang AND ct.MaFile = @MaFile
                AND dh.TrangThai = 'DANG_IN'",
            new { MaDonHang = maDonHang, MaFile = maFile });

        if (file == null || file.TrangThai != "SAN_SANG" || string.IsNullOrWhiteSpace(file.DuongDan)
            || !System.IO.File.Exists(file.DuongDan))
            return NotFound(new { message = "Không tìm thấy file sẵn sàng để in." });

        string tenTaiVe = Path.GetFileNameWithoutExtension(file.TenFile) + ".pdf";
        return PhysicalFile(file.DuongDan, "application/pdf", tenTaiVe);
    }

    [HttpPost("don/{maDonHang:int}/hoan-tat")]
    [AllowAnonymous]
    public async Task<IActionResult> HoanTat([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, BaoCaoMayInRequest req)
    {
        IActionResult loiKhoa = KiemTraKhoa();
        if (loiKhoa != null) return loiKhoa;
        if (!_quyenNhanLenh.CoQuyen(req.MaMayIn, maDonHang, ConnectorId(), req.MaLenh))
            return StatusCode(403, new { message = "Không có quyền hoàn tất lệnh này." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        DonDangIn don = await conn.QueryFirstOrDefaultAsync<DonDangIn>(
            @"SELECT MaDonHang, MaChiNhanh, MaKhachHang, MaMayIn, TrangThai
              FROM DonHang WITH (UPDLOCK, HOLDLOCK) WHERE MaDonHang = @MaDonHang",
            new { MaDonHang = maDonHang }, giaoDich);

        if (don == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (don.MaMayIn != req.MaMayIn)
            return BadRequest(new { message = "Đơn không được phân công cho máy in này." });
        if (don.TrangThai is "DA_IN" or "DANG_GIAO" or "DA_GIAO" or "HOAN_THANH")
        {
            await giaoDich.CommitAsync();
            _quyenNhanLenh.HoanTat(req.MaMayIn, maDonHang, ConnectorId(), req.MaLenh);
            return Ok(new KetQuaDto { Message = "Đơn đã được ghi nhận in xong trước đó." });
        }
        if (don.TrangThai != "DANG_IN")
            return BadRequest(new { message = "Đơn không ở trạng thái đang in." });

        int soTrang = await conn.ExecuteScalarAsync<int>(
            "SELECT ISNULL(SUM(SoTrang * SoBan), 0) FROM ChiTietDonHang WHERE MaDonHang=@MaDonHang",
            new { MaDonHang = maDonHang }, giaoDich);

        try
        {
            await conn.ExecuteAsync(
                "sp_CapNhatTrangThaiIn",
                new { MaDonHang = maDonHang, TrangThaiMoi = "DA_IN" },
                transaction: giaoDich,
                commandType: CommandType.StoredProcedure);
            await conn.ExecuteAsync(
                "UPDATE MayIn SET SoTrangDaIn = SoTrangDaIn + @SoTrang WHERE MaMayIn=@MaMayIn",
                new { SoTrang = soTrang, req.MaMayIn }, giaoDich);
            await giaoDich.CommitAsync();
        }
        catch (SqlException ex)
        {
            await giaoDich.RollbackAsync();
            _logger.LogWarning(ex, "Máy {MaMayIn} không thể hoàn tất đơn {MaDonHang}.", req.MaMayIn, maDonHang);
            return BadRequest(new { message = ex.Message });
        }

        _quyenNhanLenh.HoanTat(req.MaMayIn, maDonHang, ConnectorId(), req.MaLenh);
        _trangThai.CapNhat(req.MaMayIn, "SAN_SANG");
        await GuiCapNhat(don, "Máy in đã hoàn thành đơn hàng");
        return Ok(new KetQuaDto { Message = "Đã ghi nhận máy in hoàn thành." });
    }

    [HttpPost("don/{maDonHang:int}/loi")]
    [AllowAnonymous]
    public IActionResult BaoLoi([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, BaoCaoMayInRequest req)
    {
        IActionResult loiKhoa = KiemTraKhoa();
        if (loiKhoa != null) return loiKhoa;
        if (maDonHang <= 0 || !_quyenNhanLenh.CoQuyen(req.MaMayIn, maDonHang, ConnectorId(), req.MaLenh))
            return StatusCode(403, new { message = "Không có quyền báo lỗi lệnh này." });

        string loi = string.IsNullOrWhiteSpace(req.Loi) ? "Máy in báo lỗi không xác định." : req.Loi.Trim();
        _trangThai.CapNhat(req.MaMayIn, "LOI", maDonHang, loi);
        _logger.LogWarning("Máy {MaMayIn} báo lỗi đơn {MaDonHang}: {Loi}", req.MaMayIn, maDonHang, loi);
        return Ok(new KetQuaDto { Message = "Đã ghi nhận lỗi máy in; đơn vẫn ở trạng thái đang in." });
    }

    [HttpGet("trang-thai")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> TrangThaiMayIn()
    {
        var claim = User.FindFirst(TenClaim.MaChiNhanh);
        if (claim == null && User.IsInRole(VaiTro.SuperAdmin)) return Ok(_trangThai.LayTatCa());
        if (claim == null) return StatusCode(403);
        using var conn = new SqlConnection(_chuoiKetNoi);
        var maMay = (await conn.QueryAsync<int>("SELECT MaMayIn FROM MayIn WHERE MaChiNhanh=@MaChiNhanh",
            new { MaChiNhanh = int.Parse(claim.Value) })).ToHashSet();
        return Ok(_trangThai.LayTatCa().Where(x => maMay.Contains(x.MaMayIn)));
    }

    private string ConnectorId() => Request.Headers["X-Printer-Connector-Id"].ToString();

    private IActionResult KiemTraKhoa()
    {
        if (string.IsNullOrWhiteSpace(_khoaConnector))
            return StatusCode(503, new { message = "Máy chủ chưa cấu hình khóa Printer Connector." });

        string khoaGuiLen = Request.Headers["X-Printer-Api-Key"].ToString();
        byte[] khoaDung = Encoding.UTF8.GetBytes(_khoaConnector);
        byte[] khoaNhan = Encoding.UTF8.GetBytes(khoaGuiLen);
        bool hopLe = khoaDung.Length == khoaNhan.Length
            && CryptographicOperations.FixedTimeEquals(khoaDung, khoaNhan);
        if (!hopLe) return Unauthorized(new { message = "Printer Connector không hợp lệ." });
        if (!Guid.TryParse(ConnectorId(), out _))
            return BadRequest(new { message = "Thiếu hoặc sai mã định danh Printer Connector." });
        return null;
    }

    private async Task GuiCapNhat(DonDangIn don, string noiDung)
    {
        var tin = new TinCapNhat
        {
            MaDonHang = don.MaDonHang,
            MaDonHangHienThi = "DH" + don.MaDonHang.ToString("D5"),
            MaChiNhanh = don.MaChiNhanh,
            TrangThai = "DA_IN",
            NoiDung = noiDung
        };
        await CapNhatHub.GuiChoChiNhanh(_hub, don.MaChiNhanh, HubEvents.OrderStatusChanged, tin);
        await CapNhatHub.GuiChoNguoiDung(_hub, don.MaKhachHang, HubEvents.OrderStatusChanged, tin);
    }

    private sealed class FileChoMayIn
    {
        public string TenFile { get; set; } = "";
        public string DuongDan { get; set; } = "";
        public string TrangThai { get; set; } = "";
    }

    private sealed class DonDangIn
    {
        public int MaDonHang { get; set; }
        public int MaChiNhanh { get; set; }
        public int MaKhachHang { get; set; }
        public int? MaMayIn { get; set; }
        public string TrangThai { get; set; } = "";
    }
}
