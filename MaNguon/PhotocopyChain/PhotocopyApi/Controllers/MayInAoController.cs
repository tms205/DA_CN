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
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/may-in-ao")]
public sealed partial class MayInAoController : ControllerBase
{
    // Cùng thứ tự với tạo in lại: khóa đơn gốc trước, rồi mới khóa đơn con.
    public const string KhoaDonGocTruocHoanTatSql = @"
        DECLARE @MaGoc INT;
        SELECT @MaGoc=MaDonHangGoc FROM DonHang
        WHERE MaDonHang=@MaDonHang AND LoaiPhatSinh='IN_LAI_LOI_QUAY';
        IF @MaGoc IS NOT NULL
            SELECT MaDonHang FROM DonHang WITH (UPDLOCK,HOLDLOCK) WHERE MaDonHang=@MaGoc;";

    // In lại hoàn tất chỉ kết thúc việc in nội bộ; đơn gốc phải bàn giao riêng qua sp_GiaoTaiQuay.
    public const string HoanTatInLaiSql = @"
        UPDATE DonHang SET TrangThai='HOAN_THANH',NgayHoanThanh=GETDATE()
        WHERE MaDonHang=@MaDonHang AND MaDonHangGoc=@MaDonHangGoc AND LoaiPhatSinh='IN_LAI_LOI_QUAY';
        DELETE FROM ThongBao WHERE MaDonHang=@MaDonHang;
        INSERT INTO ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
        SELECT dh.MaKhachHang,dh.MaDonHang,N'Bản in lại đã sẵn sàng',
            CASE WHEN tt.TrangThai='CHUA_THANH_TOAN'
                THEN N'Bản in lại miễn phí đã xong. Đơn gốc vẫn chờ thu tiền và bàn giao.'
                ELSE N'Bản in lại miễn phí đã xong. Đơn gốc vẫn chờ bàn giao, không thu lại khoản đã thanh toán.' END
        FROM DonHang dh LEFT JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
        WHERE dh.MaDonHang=@MaDonHangGoc;";
    public const string DieuKienNhanLenhSql = @"((tt.PhuongThuc='VIETQR' AND tt.TrangThai='DA_THANH_TOAN')
        OR (tt.PhuongThuc='COD' AND dh.HinhThucNhan='TAI_QUAY'
            AND tt.TrangThai IN ('CHUA_THANH_TOAN','DA_THANH_TOAN')
            AND dh.MaDonHang IN (SELECT CONVERT(int,[value]) FROM OPENJSON(@DonCodDaGui))))";
    private readonly string _chuoiKetNoi;
    private readonly string _khoaConnector;
    private readonly TrangThaiMayInAoStore _trangThai;
    private readonly QuyenNhanLenhInStore _quyenNhanLenh;
    private readonly PhatHienMayInStore _phatHien;
    private readonly YeuCauInCodStore _yeuCauCod;
    private readonly ChuyenMayInStore _chuyenMay;
    private readonly IHubContext<CapNhatHub> _hub;
    private readonly ILogger<MayInAoController> _logger;

    public MayInAoController(
        IConfiguration config,
        TrangThaiMayInAoStore trangThai,
        IHubContext<CapNhatHub> hub,
        ILogger<MayInAoController> logger,
        QuyenNhanLenhInStore quyenNhanLenh,
        PhatHienMayInStore phatHien = null, YeuCauInCodStore yeuCauCod = null, ChuyenMayInStore chuyenMay = null)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain")!;
        _khoaConnector = config["PrinterConnector:ApiKey"];
        _trangThai = trangThai;
        _hub = hub;
        _logger = logger;
        _quyenNhanLenh = quyenNhanLenh;
        _phatHien = phatHien;
        _yeuCauCod = yeuCauCod ?? new YeuCauInCodStore();
        _chuyenMay = chuyenMay;
    }

    [HttpPost("{maMayIn:int}/heartbeat")]
    [AllowAnonymous]
    public async Task<IActionResult> Heartbeat([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maMayIn, BaoCaoMayInRequest req)
    {
        IActionResult loiKhoa = KiemTraKhoa();
        if (loiKhoa != null) return loiKhoa;

        if (req.MaDonHang is int donCu && _chuyenMay?.ChanLenhCu(donCu, maMayIn, req.MaLenh) == true)
            return Conflict(new { message = "Lệnh này đã được yêu cầu dừng để chuyển máy." });

        var lienKet = _phatHien?.LayLienKet(maMayIn);
        if (lienKet != null && lienKet.ConnectorId != ConnectorId())
            return Conflict(new { message = "Máy đã liên kết với connector khác." });
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
        _trangThai.CapNhat(maMayIn, trangThai, lenhDangIn?.MaDonHang, req.Loi, req);
        return Ok(new KetQuaDto { Message = "Đã nhận heartbeat." });
    }

    [HttpGet("{maMayIn:int}/dieu-khien")]
    [AllowAnonymous]
    public IActionResult LayDieuKhien([Range(1, int.MaxValue)] int maMayIn)
    {
        var loi = KiemTraKhoa();
        if (loi != null) return loi;
        var lenh = _quyenNhanLenh.LayLenhDangIn(maMayIn);
        if (lenh != null && lenh.ConnectorId != ConnectorId()) return Conflict();
        var lienKet = _phatHien?.LayLienKet(maMayIn);
        if (lienKet != null && lienKet.ConnectorId != ConnectorId()) return Conflict();
        var dieuKhien = _trangThai.LayDieuKhien(maMayIn);
        dieuKhien.LienKet = lienKet;
        dieuKhien.MaLenhDung = _chuyenMay?.LayLenhDung(maMayIn, ConnectorId())?.MaLenhCu;
        return Ok(dieuKhien);
    }

    [HttpPost("{maMayIn:int}/dieu-khien")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> DieuKhien([Range(1, int.MaxValue)] int maMayIn, DieuKhienMayInRequest req)
    {
        int? chiNhanh = int.TryParse(User.FindFirst(TenClaim.MaChiNhanh)?.Value, out int ma) ? ma : null;
        if (!User.IsInRole(VaiTro.SuperAdmin) && chiNhanh == null) return StatusCode(403);
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? chiNhanhMay = await conn.ExecuteScalarAsync<int?>("SELECT MaChiNhanh FROM MayIn WHERE MaMayIn=@maMayIn", new { maMayIn });
        if (chiNhanhMay == null) return NotFound(new { message = "Không tìm thấy máy in." });
        if (chiNhanh != null && chiNhanh != chiNhanhMay) return StatusCode(403);
        var tt = _trangThai.LayTatCa().FirstOrDefault(x => x.MaMayIn == maMayIn);
        if (tt == null || tt.TrangThai == "MAT_KET_NOI") return Conflict(new { message = "Connector chưa kết nối. Không gửi thao tác khi chưa biết trạng thái máy." });
        var lenh = _quyenNhanLenh.LayLenhDangIn(maMayIn);
        if (req.ThaoTac == "XAC_NHAN_BAN_GIAY" && (lenh == null || lenh.MaDonHang != req.MaDonHang))
            return Conflict(new { message = "Lệnh đang giữ đã thay đổi. Tải lại trạng thái và kiểm tra đúng đơn." });
        string loi = _trangThai.DieuKhien(maMayIn, req, lenh?.MaLenh);
        if (loi != null) return BadRequest(new { message = loi });
        _logger.LogInformation("Nhân sự {NguoiDung} gửi thao tác {ThaoTac} cho máy {MaMayIn}, đơn {MaDonHang}.",
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, req.ThaoTac, maMayIn, req.MaDonHang);
        return Ok(new KetQuaDto { Message = "Đã gửi thao tác; chờ connector phản hồi." });
    }

    [HttpGet("{maMayIn:int}/lenh-tiep-theo")]
    [AllowAnonymous]
    public async Task<IActionResult> LenhTiepTheo([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maMayIn)
    {
        IActionResult loiKhoa = KiemTraKhoa();
        if (loiKhoa != null) return loiKhoa;

        if (_phatHien != null && !_phatHien.DuocNhanLenh(maMayIn, ConnectorId(),
            Request.Headers["X-Printer-Binding-Version"].ToString()))
            return Conflict(new { message = "Liên kết máy đã đổi. Tải lại cấu hình trước khi nhận lệnh." });

        var lenhDangIn = _quyenNhanLenh.LayLenhDangIn(maMayIn);
        if (lenhDangIn != null && lenhDangIn.ConnectorId != ConnectorId())
            return Conflict(new { message = "Máy đang có lệnh thuộc connector khác." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        LenhInAoDto lenh = await conn.QueryFirstOrDefaultAsync<LenhInAoDto>(
            @"SELECT TOP 1 dh.MaDonHang,
                     'DH' + RIGHT('00000' + CAST(dh.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     dh.MaMayIn, m.TenMay
              FROM DonHang dh WITH (UPDLOCK, HOLDLOCK)
              JOIN MayIn m ON m.MaMayIn = dh.MaMayIn
              JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
              WHERE dh.MaMayIn = @MaMayIn AND dh.TrangThai = 'DANG_IN'
                AND " + DieuKienNhanLenhSql + @"
                AND dh.MaDonHang NOT IN (SELECT CONVERT(int,[value]) FROM OPENJSON(@DonDung))
                AND (@MaDonDangIn IS NULL OR dh.MaDonHang = @MaDonDangIn)
                AND (m.TrangThai = 'HOAT_DONG' OR @MaDonDangIn IS NOT NULL)
              ORDER BY dh.InGap DESC, dh.NgayDat, dh.MaDonHang",
            new { MaMayIn = maMayIn, MaDonDangIn = lenhDangIn?.MaDonHang,
                DonCodDaGui = System.Text.Json.JsonSerializer.Serialize(_yeuCauCod.DonChoMay(maMayIn).Concat(_chuyenMay?.DonChoMay(maMayIn) ?? Array.Empty<int>()).Distinct()),
                DonDung = System.Text.Json.JsonSerializer.Serialize(_chuyenMay?.DonBiChanChoMay(maMayIn) ?? Array.Empty<int>()) }, tx);

        if (lenh == null)
        {
            if (lenhDangIn != null)
                return Conflict(new { message = "Lệnh đang giữ không còn ở trạng thái đang in; cần kiểm tra thủ công." });
            _trangThai.CapNhat(maMayIn, "SAN_SANG");
            return NoContent();
        }

        if (_chuyenMay?.ChanNhanLenh(lenh.MaDonHang, maMayIn) == true) return NoContent();

        lenh.DsDongIn = (await conn.QueryAsync<DongLenhInDto>(
            @"SELECT ct.MaChiTiet, ct.MaFile, f.TenFile,
                     dv.KhoGiay, dv.MauSac, dv.MatIn, ct.HuongGiay, ct.SoTrang, ct.SoBan, ct.TuTrang, ct.DenTrang
              FROM ChiTietDonHang ct
              JOIN FileIn f ON f.MaFile = ct.MaFile
              JOIN DichVu dv ON dv.MaDichVu = ct.MaDichVu
              WHERE ct.MaDonHang = @MaDonHang
              ORDER BY ct.MaChiTiet",
            new { lenh.MaDonHang }, tx)).ToList();
        var phien = _chuyenMay?.LayHienTai(lenh.MaDonHang);
        if (phien?.TrangThai == "CHO_AP_DUNG" && phien.MaMayMoi == maMayIn)
            lenh.DsDongIn = phien.PhanConLai;

        QuyenNhanLenhIn quyen;
        lock (_phatHien?.Khoa ?? _quyenNhanLenh)
        {
            if (_phatHien != null && !_phatHien.DuocNhanLenh(maMayIn, ConnectorId(),
                Request.Headers["X-Printer-Binding-Version"].ToString()))
                return Conflict(new { message = "Liên kết máy đã đổi. Connector cần tải lại cấu hình trước khi nhận lệnh." });
            string loiThietBi = _phatHien?.KiemTraQuyCach(maMayIn, lenh.DsDongIn);
            if (loiThietBi != null) return Conflict(new { message = loiThietBi });
            quyen = _quyenNhanLenh.NhanLenh(maMayIn, lenh.MaDonHang, ConnectorId(), phien?.MaYeuCau);
        }
        if (quyen == null) return Conflict(new { message = "Lệnh đã được connector khác nhận." });
        lenh.MaLenh = quyen.MaLenh;
        await tx.CommitAsync();

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
        if (_chuyenMay?.ChanLenhCu(maDonHang, maMayIn.Value, Request.Headers["X-Printer-Job-Token"].ToString()) == true)
            return Conflict(new { message = "Lệnh cũ đang dừng để chuyển máy; không tải thêm file." });
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
        await conn.ExecuteAsync(KhoaDonGocTruocHoanTatSql, new { MaDonHang = maDonHang }, giaoDich);
        DonDangIn don = await conn.QueryFirstOrDefaultAsync<DonDangIn>(
            @"SELECT MaDonHang, MaChiNhanh, MaKhachHang, MaMayIn, TrangThai, LoaiPhatSinh, MaDonHangGoc
              FROM DonHang WITH (UPDLOCK, HOLDLOCK) WHERE MaDonHang = @MaDonHang",
            new { MaDonHang = maDonHang }, giaoDich);

        if (don == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (_chuyenMay?.ChanLenhCu(maDonHang, req.MaMayIn, req.MaLenh) == true)
            return Conflict(new { message = "Lệnh cũ đã được yêu cầu dừng, không được báo hoàn tất." });
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
        var phienHoanTat = _chuyenMay?.LayHienTai(maDonHang);
        if (phienHoanTat?.TrangThai == "CHO_AP_DUNG" && phienHoanTat.MaMayMoi == req.MaMayIn)
            soTrang = checked(phienHoanTat.PhanConLai.Sum(x => checked(x.SoTrang * x.SoBan)));

        DonDangIn donGoc = null;
        try
        {
            var files = (await conn.QueryAsync<FileCanGiu>(
                @"SELECT DISTINCT f.MaFile, f.DuongDan FROM ChiTietDonHang ct JOIN FileIn f ON f.MaFile=ct.MaFile
                  WHERE ct.MaDonHang=@MaDonHang AND f.DuongDan IS NOT NULL", new { MaDonHang = maDonHang }, giaoDich)).ToList();
            await conn.ExecuteAsync(
                "sp_CapNhatTrangThaiIn",
                new { MaDonHang = maDonHang, TrangThaiMoi = "DA_IN" },
                transaction: giaoDich,
                commandType: CommandType.StoredProcedure);
            await conn.ExecuteAsync(
                "UPDATE MayIn SET SoTrangDaIn = SoTrangDaIn + @SoTrang WHERE MaMayIn=@MaMayIn",
                new { SoTrang = soTrang, req.MaMayIn }, giaoDich);
            // Tương thích thủ tục cũ: giữ PDF cho tới khi đơn thực sự hoàn tất/bị hủy.
            foreach (var file in files)
                await conn.ExecuteAsync("UPDATE FileIn SET TrangThai='SAN_SANG', DuongDan=@DuongDan WHERE MaFile=@MaFile", file, giaoDich);
            if (don.LoaiPhatSinh == "IN_LAI_LOI_QUAY" && don.MaDonHangGoc != null)
            {
                // Giữ nghiệp vụ in lại miễn phí khi chuyển việc hoàn tất sang connector.
                await conn.ExecuteAsync(HoanTatInLaiSql,
                    new { MaDonHang = maDonHang, don.MaDonHangGoc }, giaoDich);
                don.TrangThai = "HOAN_THANH";
                donGoc = await conn.QuerySingleOrDefaultAsync<DonDangIn>(
                    "SELECT MaDonHang,MaChiNhanh,MaKhachHang,TrangThai FROM DonHang WHERE MaDonHang=@ma", new { ma = don.MaDonHangGoc }, giaoDich);
            }
            else don.TrangThai = "DA_IN";
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
        if (donGoc != null) await GuiCapNhat(donGoc, "Bản in lại đã sẵn sàng; đơn gốc vẫn chờ bàn giao");
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
        if (_chuyenMay?.ChanLenhCu(maDonHang, req.MaMayIn, req.MaLenh) == true)
        {
            // Vẫn cần nhận lỗi dừng của đúng chủ lệnh, nếu không web sẽ tưởng connector mất kết nối.
            // Không cho lệnh đã dừng/chuyển ghi đè trạng thái của lần in mới.
            var phien = _chuyenMay.LayHienTai(maDonHang);
            if (phien?.TrangThai != "DANG_DUNG" || phien.MaMayCu != req.MaMayIn
                || phien.MaLenhCu != req.MaLenh || phien.ConnectorCu != ConnectorId())
                return Conflict(new { message = "Không cập nhật từ lệnh cũ đã dừng/chuyển máy." });
        }

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

    [HttpGet("danh-sach")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> DanhSachMay()
    {
        int? chiNhanh = int.TryParse(User.FindFirst(TenClaim.MaChiNhanh)?.Value, out int ma) ? ma : null;
        if (!User.IsInRole(VaiTro.SuperAdmin) && chiNhanh == null) return StatusCode(403);
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = (await conn.QueryAsync<MayInQuanTriDto>(
            @"SELECT m.*, cn.TenChiNhanh FROM MayIn m JOIN ChiNhanh cn ON cn.MaChiNhanh=m.MaChiNhanh
              WHERE @chiNhanh IS NULL OR m.MaChiNhanh=@chiNhanh ORDER BY m.MaChiNhanh,m.MaMayIn", new { chiNhanh })).ToList();
        foreach (var may in ds) may.KetNoi = _phatHien?.LayKetNoi(may.MaMayIn);
        return Ok(ds);
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
            TrangThai = don.TrangThai,
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

    private sealed class FileCanGiu
    {
        public int MaFile { get; set; }
        public string DuongDan { get; set; }
    }

    private sealed class DonDangIn
    {
        public int? MaDonHangGoc { get; set; }
        public string LoaiPhatSinh { get; set; }
        public int MaDonHang { get; set; }
        public int MaChiNhanh { get; set; }
        public int MaKhachHang { get; set; }
        public int? MaMayIn { get; set; }
        public string TrangThai { get; set; } = "";
    }
}
