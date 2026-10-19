using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotocopyApi.DichVu;
using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/quan-tri/mau-don")]
[Authorize(Roles = "SUPER_ADMIN")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MauDonController : ControllerBase
{
    private readonly ThuVienMauDon _thuVien;
    private readonly ILogger<MauDonController> _logger;
    public MauDonController(ThuVienMauDon thuVien, ILogger<MauDonController> logger)
    { _thuVien = thuVien; _logger = logger; }

    [HttpGet]
    public IActionResult DanhSach() => XuLy(() => Ok(_thuVien.DanhSach(true)));

    [HttpPost]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<IActionResult> Them([FromForm] string tenMau, [FromForm] string nhom, [FromForm] string moTa, IFormFile file)
    {
        byte[] bytes;
        try { bytes = await DocFile(file); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        return XuLy(() => Ok(_thuVien.Them(tenMau, nhom, moTa, bytes)));
    }

    [HttpPut("{ma}")]
    public IActionResult Sua(string ma, LuuMauDonRequest req) => XuLy(() => Ok(_thuVien.Sua(ma, req)));

    [HttpPut("{ma}/file")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<IActionResult> ThayFile(string ma, [FromForm] long phienBan, IFormFile file)
    {
        byte[] bytes;
        try { bytes = await DocFile(file); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        return XuLy(() => Ok(_thuVien.ThayFile(ma, phienBan, bytes)));
    }

    [HttpPut("{ma}/trang-thai")]
    public IActionResult TrangThai(string ma, HienMauDonRequest req) => XuLy(() => Ok(_thuVien.Hien(ma, req)));

    [HttpDelete("{ma}")]
    public IActionResult Go(string ma, [FromQuery] long phienBan) => XuLy(() => { _thuVien.Go(ma, phienBan); return NoContent(); });

    [HttpGet("{ma}/trang/{trang:int}")]
    public IActionResult XemTrang(string ma, int trang, [FromQuery] long? phienBan) =>
        XuLy(() => File(_thuVien.DocAnh(ma, trang, true, phienBan), "image/png"));

    private static async Task<byte[]> DocFile(IFormFile file)
    {
        if (file == null || file.Length < 8 || file.Length > XemTruocMauDon.SoByteToiDa
            || !string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Chọn file PDF, tối đa 20 MB; Word cần xuất PDF trước.");
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory);
        return memory.ToArray();
    }

    private IActionResult XuLy(Func<IActionResult> viec)
    {
        try { return viec(); }
        catch (MauDonDaDoiException ex) { return Conflict(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Không đọc/ghi được thư viện mẫu đơn.");
            return StatusCode(503, new { message = "Thư viện mẫu đơn chưa sẵn sàng. Kiểm tra quyền thư mục và bản sao lưu catalog.json; không xóa thư viện để tạo lại." });
        }
    }
}
