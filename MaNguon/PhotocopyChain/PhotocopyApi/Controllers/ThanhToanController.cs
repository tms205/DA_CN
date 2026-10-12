using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models.V2.PaymentRequests;
using PhotocopyApi.DuLieu;
using PhotocopyApi.Hubs;
using PhotocopyApi.MayInAo;
using PhotocopyApi.ThanhToan;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.DonHang;
using PayOSWebhook = PayOS.Models.Webhooks.Webhook;

namespace PhotocopyApi.Controllers;

// F-10: thanh toán đơn hàng.
//   - Khách chọn "Chuyển khoản VietQR": API tạo link payOS và App mở trang checkout chính thức.
//   - Ngân hàng nhận được tiền → payOS gọi vào /api/thanh-toan/webhook,
//     API gọi sp_XacNhanChuyenKhoan rồi đẩy tin real-time để màn Thanh toán trên App tự chuyển sang "Đã thanh toán".
//   - Đơn VietQR quá 15 phút chưa chuyển khoản sẽ bị DonQuaHanJob hủy.
// Khách chọn COD thì không có gì để làm ở đây — trả tiền lúc nhận tài liệu.
[ApiController]
[Route("api/thanh-toan")]
public class ThanhToanController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly IConfiguration _config;
    private readonly IHubContext<CapNhatHub> _hub;
    private readonly ILogger<ThanhToanController> _logger;
    private readonly PayOSClient _payOs;
    private readonly TuDongInService _tuDongIn;

    // Đơn chuyển khoản phải trả trong 15 phút (đúng bằng thời gian sp_HuyDonQuaHan dùng)
    private const int SoPhutChoChuyenKhoan = 15;

    public ThanhToanController(
        IConfiguration config,
        IHubContext<CapNhatHub> hub,
        ILogger<ThanhToanController> logger,
        PayOSClient payOs,
        TuDongInService tuDongIn)
    {
        _config = config;
        _hub = hub;
        _logger = logger;
        _payOs = payOs;
        _tuDongIn = tuDongIn;
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    // GET api/thanh-toan/5 — màn Thanh toán trên App gọi liên tục để biết ngân hàng đã báo tiền chưa
    [HttpGet("{maDonHang:int}")]
    [Authorize(Roles = "CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> XemThanhToan(int maDonHang)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        ThanhToanDto tt = await DocThanhToanDuocPhep(conn, maDonHang);

        if (tt == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });

        // Webhook có thể bị lỡ khi Quick Tunnel tắt. Nút "Kiểm tra thanh toán" đối soát lại
        // trực tiếp với payOS; chỉ dữ liệu do API payOS trả về mới được dùng để khôi phục đơn.
        if (tt.PhuongThuc == "VIETQR" && tt.TrangThai != "DA_THANH_TOAN"
            && await ThuDongBoTuPayOS(tt))
        {
            tt = await DocThanhToanDuocPhep(conn, maDonHang);
        }

        // COD trả tại quầy nên không có hạn thanh toán online.
        if (tt.PhuongThuc != "VIETQR")
            tt.HanThanhToan = null;

        tt.ChoPhepGiaLap = _config.GetValue<bool>("ThanhToan:ChoPhepGiaLap");
        return Ok(tt);
    }

    // POST api/thanh-toan/5/payos — tạo mới hoặc lấy lại trang checkout của đúng đơn này.
    [HttpPost("{maDonHang:int}/payos")]
    [Authorize(Roles = "CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> TaoLienKetPayOS(int maDonHang)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        ThanhToanDto tt = await DocThanhToanDuocPhep(conn, maDonHang);

        if (tt == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (tt.PhuongThuc != "VIETQR")
            return BadRequest(new { message = "Đơn này không thanh toán bằng VietQR." });
        if (tt.TrangThai == "DA_THANH_TOAN")
            return BadRequest(new { message = "Đơn hàng đã được thanh toán." });
        if (tt.TrangThaiDonHang == TrangThaiDonHang.DaHuy)
            return BadRequest(new { message = "Đơn đã bị hủy, vui lòng đặt lại." });
        if (tt.HanThanhToan == null || tt.HanThanhToan <= DateTime.Now)
            return BadRequest(new { message = "Đơn đã quá hạn thanh toán, vui lòng đặt lại." });

        string diaChiGoc = QuyTacPayOS.ChuanHoaDiaChiGoc(_config["PayOS:PublicBaseUrl"]);
        if (diaChiGoc == null)
            return StatusCode(503, new { message = "Máy chủ chưa cấu hình địa chỉ HTTPS công khai cho payOS." });

        try
        {
            string kenh = User.IsInRole(VaiTro.Customer) ? "mobile" : "admin";
            string checkoutUrl = await TaoHoacLayCheckoutUrl(tt, diaChiGoc, kenh);
            return Ok(new LienKetPayOSDto
            {
                CheckoutUrl = checkoutUrl,
                HanThanhToan = tt.HanThanhToan.Value
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Không tạo/lấy được link payOS cho đơn {MaDonHang}.", maDonHang);
            return StatusCode(502, new { message = "Chưa kết nối được payOS, vui lòng thử lại." });
        }
    }

    // payOS chuyển trình duyệt về đây sau khi thanh toán hoặc hủy.
    // Query string chỉ dùng để hiển thị; trạng thái trong CSDL chỉ được đổi bởi webhook có chữ ký.
    [HttpGet("payos/ket-qua")]
    [AllowAnonymous]
    public IActionResult KetQuaPayOS(
        [FromQuery] string status,
        [FromQuery] bool cancel = false,
        [FromQuery] long orderCode = 0,
        [FromQuery] string kenh = "admin")
    {
        bool thanhCong = !cancel && string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase);

        // Link mở từ Web quản trị quay lại hàng đợi. Đây chỉ là điều hướng giao diện;
        // việc xác nhận tiền vẫn chỉ do webhook có chữ ký thực hiện.
        if (thanhCong && !string.Equals(kenh, "mobile", StringComparison.OrdinalIgnoreCase))
        {
            string diaChiQuayLai = QuyTacPayOS.TaoDiaChiTrangChuWeb(
                _config["ThanhToan:WebAdminUrl"]);
            if (diaChiQuayLai != null) return Redirect(diaChiQuayLai);
        }

        string tieuDe = thanhCong ? "Thanh toán thành công" : "Chưa hoàn tất thanh toán";
        string noiDung = thanhCong
            ? "Hệ thống đang xác nhận giao dịch. Bạn có thể quay lại ứng dụng PhotocopyChain."
            : "Bạn có thể quay lại ứng dụng để thử thanh toán lại nếu đơn vẫn còn hạn.";

        string html = $"""
            <!doctype html>
            <html lang="vi">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{tieuDe}</title></head>
            <body style="font-family:Arial,sans-serif;background:#f5f7fb;padding:32px;color:#1f2937">
              <main style="max-width:520px;margin:auto;background:white;padding:28px;border-radius:16px;text-align:center">
                <h2>{tieuDe}</h2><p style="line-height:1.6">{noiDung}</p>
              </main>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    // POST api/thanh-toan/webhook — payOS báo tài khoản đã nhận được tiền.
    // Không dùng khóa tự tạo trong header nữa; SDK sẽ kiểm tra chữ ký bằng Checksum Key.
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(PayOSWebhook req)
    {
        PayOS.Models.Webhooks.WebhookData duLieu;

        try
        {
            // Chỉ nhận dữ liệu có chữ ký hợp lệ từ payOS
            duLieu = await _payOs.Webhooks.VerifyAsync(req);
        }
        catch (WebhookException ex)
        {
            _logger.LogWarning(ex, "Webhook payOS có chữ ký không hợp lệ.");
            return BadRequest(new { message = "Webhook payOS không hợp lệ." });
        }

        // payOS có thể gửi dữ liệu mẫu khi đăng ký webhook hoặc sự kiện không thành công.
        if (!req.Success || duLieu.Code != "00")
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });

        if (duLieu.OrderCode <= 0 || duLieu.OrderCode > int.MaxValue)
        {
            _logger.LogWarning("Webhook payOS có mã đơn không hợp lệ: {OrderCode}", duLieu.OrderCode);
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });
        }

        int maDonHang = (int)duLieu.OrderCode;

        // Luôn đối chiếu với CSDL, không tin số tiền hay trạng thái do phía giao diện gửi lên.
        using var conn = new SqlConnection(_chuoiKetNoi);
        DonWebhook don = await conn.QueryFirstOrDefaultAsync<DonWebhook>(
            @"SELECT dh.MaDonHang, dh.TrangThai AS TrangThaiDonHang, dh.GhiChu,
                     tt.PhuongThuc, tt.SoTien, tt.TrangThai AS TrangThaiThanhToan
              FROM DonHang dh
              JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
              WHERE dh.MaDonHang = @MaDonHang",
            new { MaDonHang = maDonHang });

        // Dữ liệu mẫu của payOS hoặc đơn không thuộc hệ thống: nhận nhưng không cập nhật
        if (don == null)
        {
            _logger.LogWarning("Webhook payOS báo mã đơn không tồn tại: {MaDonHang}", maDonHang);
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });
        }

        bool biHuyThuCong = don.TrangThaiDonHang == TrangThaiDonHang.DaHuy
            && don.GhiChu != "Quá 15 phút chưa thanh toán";
        if (don.PhuongThuc != "VIETQR" || biHuyThuCong)
        {
            _logger.LogWarning("Webhook payOS báo cho đơn {MaDonHang} không còn được phép thanh toán.", maDonHang);
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });
        }

        if (!string.Equals(duLieu.Currency, "VND", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(duLieu.Description, QuyTacPayOS.TaoMoTaDon(maDonHang), StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(duLieu.Reference))
        {
            _logger.LogWarning("Webhook payOS của đơn {MaDonHang} có thông tin đối chiếu không hợp lệ.", maDonHang);
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });
        }

        // Chỉ xác nhận khi số tiền ngân hàng nhận đúng bằng số tiền của đơn
        if (!QuyTacPayOS.SoTienKhop(don.SoTien, duLieu.Amount))
        {
            _logger.LogWarning(
                "Đơn {MaDonHang} nhận sai số tiền. Cần {SoTienCanTra}, thực nhận {SoTienNhan}.",
                maDonHang, don.SoTien, duLieu.Amount);

            return Ok(new KetQuaDto { Message = "Đã nhận webhook nhưng số tiền không khớp." });
        }

        _logger.LogInformation(
            "payOS báo đã nhận {SoTien} cho đơn {MaDonHang}, giao dịch {MaGiaoDich}.",
            duLieu.Amount, maDonHang, duLieu.Reference);

        return await XacNhanDaTra(maDonHang, duLieu.Reference);
    }

    // ===================================================================== CÁC HÀM PHỤ

    // Đối soát dự phòng khi webhook bị lỡ do tunnel/API tạm dừng.
    private async Task<bool> ThuDongBoTuPayOS(ThanhToanDto tt)
    {
        try
        {
            PaymentLink link = await _payOs.PaymentRequests.GetAsync(tt.MaDonHang);
            if (link.Status != PaymentLinkStatus.Paid
                || link.OrderCode != tt.MaDonHang
                || !QuyTacPayOS.SoTienKhop(tt.SoTien, link.Amount)
                || !QuyTacPayOS.SoTienKhop(tt.SoTien, link.AmountPaid))
                return false;

            string maGiaoDich = link.Transactions?
                .LastOrDefault(x => !string.IsNullOrWhiteSpace(x.Reference))?.Reference
                ?? "PAYOS-" + link.Id;
            if (maGiaoDich.Length > 50) maGiaoDich = maGiaoDich[..50];

            IActionResult ketQua = await XacNhanDaTra(tt.MaDonHang, maGiaoDich);
            return ketQua is OkObjectResult;
        }
        catch (ApiException ex) when (QuyTacPayOS.ChuaCoLienKet(ex.StatusCode, ex.ErrorCode))
        {
            return false;
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "Không đối soát được đơn {MaDonHang} với payOS.", tt.MaDonHang);
            return false;
        }
    }

    // Khách chỉ xem đơn của mình; nhân viên/quản lý chỉ xem đơn thuộc chi nhánh đang làm việc.
    private async Task<ThanhToanDto> DocThanhToanDuocPhep(SqlConnection conn, int maDonHang)
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;

        if (vaiTro == VaiTro.Customer)
        {
            return await conn.QueryFirstOrDefaultAsync<ThanhToanDto>(
                TruyVanSql.ThanhToan.CuaDonHang,
                new { MaDonHang = maDonHang, MaKhachHang = maNguoiDung, SoPhut = SoPhutChoChuyenKhoan });
        }

        string maChiNhanhClaim = User.FindFirst(TenClaim.MaChiNhanh)?.Value;
        int? maChiNhanh = int.TryParse(maChiNhanhClaim, out int maChiNhanhDaChon)
            ? maChiNhanhDaChon
            : null;
        if (maChiNhanh == null && vaiTro != VaiTro.SuperAdmin) return null;

        return await conn.QueryFirstOrDefaultAsync<ThanhToanDto>(
            @"SELECT dh.MaDonHang,
                     'DH' + RIGHT('00000' + CAST(dh.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     tt.PhuongThuc, tt.TrangThai, tt.SoTien, dh.TrangThai AS TrangThaiDonHang,
                     DATEADD(MINUTE, @SoPhut, dh.NgayDat) AS HanThanhToan
              FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
              WHERE dh.MaDonHang = @MaDonHang
                AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh = @MaChiNhanh)",
            new { MaDonHang = maDonHang, MaChiNhanh = maChiNhanh, SoPhut = SoPhutChoChuyenKhoan });
    }

    private async Task<string> TaoHoacLayCheckoutUrl(ThanhToanDto tt, string diaChiGoc, string kenh)
    {
        try
        {
            PaymentLink linkCu = await _payOs.PaymentRequests.GetAsync(tt.MaDonHang);

            if (linkCu.Amount != decimal.ToInt64(tt.SoTien))
                throw new InvalidOperationException("Số tiền trên link payOS cũ không khớp với đơn hàng.");
            if (linkCu.Status is PaymentLinkStatus.Cancelled or PaymentLinkStatus.Expired or PaymentLinkStatus.Failed)
                throw new InvalidOperationException("Link payOS của đơn đã hết hiệu lực, vui lòng đặt lại đơn.");

            return QuyTacPayOS.TaoCheckoutUrl(linkCu.Id);
        }
        catch (ApiException ex) when (QuyTacPayOS.ChuaCoLienKet(ex.StatusCode, ex.ErrorCode))
        {
            // Chưa có link cho orderCode này thì tạo mới ở bên dưới.
        }

        string diaChiKetQua = string.Equals(kenh, "mobile", StringComparison.OrdinalIgnoreCase)
            ? diaChiGoc + "/api/thanh-toan/payos/ket-qua?kenh=mobile"
            : QuyTacPayOS.TaoDiaChiTrangChuWeb(_config["ThanhToan:WebAdminUrl"]);
        if (diaChiKetQua == null)
            throw new InvalidOperationException("Chưa cấu hình địa chỉ Web Admin hợp lệ.");

        var yeuCau = new CreatePaymentLinkRequest
        {
            OrderCode = tt.MaDonHang,
            Amount = decimal.ToInt64(tt.SoTien),
            Description = QuyTacPayOS.TaoMoTaDon(tt.MaDonHang),
            ReturnUrl = diaChiKetQua,
            CancelUrl = diaChiKetQua,
            ExpiredAt = new DateTimeOffset(tt.HanThanhToan.Value).ToUnixTimeSeconds()
        };

        CreatePaymentLinkResponse linkMoi = await _payOs.PaymentRequests.CreateAsync(yeuCau);
        return linkMoi.CheckoutUrl;
    }

    // Ghi nhận đã nhận tiền: gọi thủ tục rồi báo real-time cho khách và cho quầy
    private async Task<IActionResult> XacNhanDaTra(int maDonHang, string maGiaoDich)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);

        var don = await conn.QueryFirstOrDefaultAsync<DonDeBao>(
            @"SELECT dh.MaDonHang, 'DH' + RIGHT('00000' + CAST(dh.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     dh.MaChiNhanh, dh.MaKhachHang, dh.TrangThai, dh.GhiChu,
                     tt.TrangThai AS TrangThaiThanhToan
              FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
              WHERE dh.MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });

        if (don == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (don.TrangThai == TrangThaiDonHang.DaHuy)
        {
            int daKhoiPhuc = await conn.ExecuteAsync(
                @"UPDATE DonHang
                  SET TrangThai = 'CHO_XAC_NHAN', GhiChu = NULL
                  WHERE MaDonHang = @MaDonHang AND TrangThai = 'DA_HUY'
                    AND GhiChu = N'Quá 15 phút chưa thanh toán'",
                new { MaDonHang = maDonHang });
            if (daKhoiPhuc == 0)
                return BadRequest(new { message = "Đơn đã bị hủy thủ công nên không thể tự khôi phục." });
            don.TrangThai = TrangThaiDonHang.ChoXacNhan;
        }

        if (don.TrangThaiThanhToan != "DA_THANH_TOAN")
        {
            try
            {
                // Thủ tục chỉ cập nhật đúng đơn VietQR đang "CHUA_THANH_TOAN" và tự ghi thông báo cho khách
                await conn.ExecuteAsync("sp_XacNhanChuyenKhoan",
                    new { MaDonHang = maDonHang, MaGiaoDich = maGiaoDich },
                    commandType: CommandType.StoredProcedure);
            }
            catch (SqlException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Webhook có thể được gửi lại. Hàm xếp in kiểm tra trạng thái hiện tại nên gọi lặp vẫn an toàn.
        KetQuaXepIn ketQuaIn = await _tuDongIn.XepInSauThanhToan(maDonHang);

        DonDeBao donSau = await conn.QueryFirstAsync<DonDeBao>(
            @"SELECT dh.MaDonHang, 'DH' + RIGHT('00000' + CAST(dh.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     dh.MaChiNhanh, dh.MaKhachHang, dh.TrangThai, tt.TrangThai AS TrangThaiThanhToan
              FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
              WHERE dh.MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });

        var tin = new TinCapNhat
        {
            MaDonHang = donSau.MaDonHang,
            MaDonHangHienThi = donSau.MaDonHangHienThi,
            MaChiNhanh = donSau.MaChiNhanh,
            TrangThai = donSau.TrangThai,
            NoiDung = "Đơn " + donSau.MaDonHangHienThi + " đã thanh toán"
        };
        await CapNhatHub.GuiChoNguoiDung(_hub, donSau.MaKhachHang, HubEvents.PaymentConfirmed, tin);
        await CapNhatHub.GuiChoChiNhanh(_hub, donSau.MaChiNhanh, HubEvents.PaymentConfirmed, tin);

        string message = ketQuaIn.DaBatDauIn
            ? "Đã ghi nhận thanh toán và bắt đầu in tự động."
            : "Đã ghi nhận thanh toán. " + ketQuaIn.Message;
        return Ok(new KetQuaDto { Message = message });
    }

    // Đơn hàng đọc ra để gửi tin real-time
    public class DonDeBao
    {
        public int MaDonHang { get; set; }
        public string MaDonHangHienThi { get; set; }
        public int MaChiNhanh { get; set; }
        public int MaKhachHang { get; set; }
        public string TrangThai { get; set; }
        public string GhiChu { get; set; }
        public string TrangThaiThanhToan { get; set; }
    }

    private class DonWebhook
    {
        public int MaDonHang { get; set; }
        public string TrangThaiDonHang { get; set; }
        public string GhiChu { get; set; }
        public string PhuongThuc { get; set; }
        public decimal SoTien { get; set; }
        public string TrangThaiThanhToan { get; set; }
    }
}
