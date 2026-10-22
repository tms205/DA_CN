using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.MayInAo;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/may-in-ao/phat-hien")]
public sealed class PhatHienMayInController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly PhatHienMayInStore _store;
    private readonly QuyenNhanLenhInStore _lenh;
    private readonly ILogger<PhatHienMayInController> _logger;
    public PhatHienMayInController(IConfiguration config, PhatHienMayInStore store,
        QuyenNhanLenhInStore lenh, ILogger<PhatHienMayInController> logger)
    { _config = config; _store = store; _lenh = lenh; _logger = logger; }

    [HttpPost, AllowAnonymous]
    public IActionResult BaoCao(BaoCaoPhatHienRequest req)
    {
        string key = _config["PrinterConnector:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return StatusCode(503);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes(Request.Headers["X-Printer-Api-Key"].ToString()))) return Unauthorized();
        if (!Guid.TryParseExact(Request.Headers["X-Printer-Connector-Id"], "N", out var id))
            return BadRequest(new { message = "Mã connector không hợp lệ." });
        if (req.MaMayCauHinh == null || req.KetNoiCauHinh == null || req.KetNoiCauHinh.Any(x => x == null)
            || req.HangDoi == null || req.HangDoi.Any(x => x == null)
            || req.MaMayCauHinh.Any(x => x <= 0)
            || req.HangDoi.GroupBy(x => x.Ten, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            return BadRequest(new { message = "Danh sách thiết bị không hợp lệ hoặc trùng queue." });
        _store.CapNhat(id.ToString("N"), req);
        return Ok();
    }

    [HttpGet, Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public IActionResult DanhSach()
    {
        int? branch = int.TryParse(User.FindFirst(TenClaim.MaChiNhanh)?.Value, out int b) ? b : null;
        if (!User.IsInRole(VaiTro.SuperAdmin) && branch == null) return StatusCode(403);
        return Ok(User.IsInRole(VaiTro.SuperAdmin)
            ? _store.LayTatCa(null).Where(x => branch == null || x.MaChiNhanh == null || x.MaChiNhanh == branch).ToList()
            : _store.LayTatCa(branch));
    }

    // Chỉ CEO kích hoạt đường in vật lý; nhân viên không tự đổi đích nhận tài liệu.
    [HttpPost("{maMayIn:int}/lien-ket"), Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> LienKet(int maMayIn, LienKetMayInRequest req)
    {
        using var db = new SqlConnection(_config.GetConnectionString("PhotocopyChain"));
        var may = await db.QuerySingleOrDefaultAsync<PhotocopyApi.Models.MayIn>(
            "SELECT MaChiNhanh, InMau, In2Mat FROM MayIn WHERE MaMayIn=@maMayIn", new { maMayIn });
        if (may == null) return NotFound();
        int branch = may.MaChiNhanh;
        int? selected = int.TryParse(User.FindFirst(TenClaim.MaChiNhanh)?.Value, out int b) ? b : null;
        if (selected != null && selected != branch) return StatusCode(403);
        var queue = _store.LayTatCa(null).FirstOrDefault(x => x.ConnectorId == req.ConnectorId)?.HangDoi
            .FirstOrDefault(x => x.Ten == req.TenHangDoi?.Trim());
        if (req.CheDo == "THAT" && queue != null && ((may.InMau && queue.InMau == false) || (may.In2Mat && queue.InHaiMat == false)))
            return Conflict(new { message = "Năng lực driver không khớp danh mục máy (màu/hai mặt). Kiểm tra driver và cập nhật danh mục trước." });
        string loi = _store.LienKet(maMayIn, branch, req, () => _lenh.LayLenhDangIn(maMayIn) != null);
        if (loi != null) return Conflict(new { message = loi });
        _logger.LogInformation("Nhân sự {Nguoi} liên kết máy {May} với connector {Connector}, chế độ {CheDo}.",
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, maMayIn, req.ConnectorId, req.CheDo);
        return Ok(new { message = "Đã lưu liên kết. Connector áp dụng khi máy rảnh." });
    }
}
