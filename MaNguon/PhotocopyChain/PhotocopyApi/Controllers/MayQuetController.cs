using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotocopyApi.MayQuet;
using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/may-quet")]
[Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
public class MayQuetController : ControllerBase
{
    private readonly IMayQuet _may;
    public MayQuetController(IMayQuet may) => _may = may;

    [HttpPost("quet")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Quet(IFormFile file, [FromForm] QuyCachQuet quyCach, CancellationToken dung)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            // Chỉ nguồn ảo cần ảnh nạp vào; adapter connector thật lấy ảnh từ thiết bị.
            var anh = file == null ? null : await DocAnh(file, dung);
            return Ok(await _may.QuetAsync(new LenhQuet(anh, quyCach), dung));
        }
        catch (ArgumentException) { return BadRequest(new { message = "Không quét được. Cần ảnh JPG/PNG tối đa 10 MB, 20 megapixel; vùng cắt nằm trong ảnh và góc xoay là 0/90/180/270 độ." }); }
    }

    [HttpPost("ghep")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<IActionResult> Ghep(IFormFile matTruoc, IFormFile matSau, CancellationToken dung)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var truoc = await DocAnh(matTruoc, dung);
            var sau = await DocAnh(matSau, dung);
            dung.ThrowIfCancellationRequested();
            return Ok(SaoChupPdf.Tao(truoc, sau));
        }
        catch (ArgumentException) { return BadRequest(new { message = "Cần đủ hai mặt JPG/PNG hợp lệ, mỗi mặt tối đa 10 MB và 20 megapixel." }); }
    }

    [HttpGet("anh-minh-hoa/{mat}")]
    public IActionResult AnhMinhHoa(string mat)
    {
        Response.Headers.CacheControl = "no-store";
        if (mat is not ("TRUOC" or "SAU")) return BadRequest(new { message = "Chỉ có mặt TRUOC hoặc SAU." });
        return File(SaoChupPdf.AnhMinhHoa(mat == "SAU"), "image/png");
    }

    private static async Task<byte[]> DocAnh(IFormFile file, CancellationToken dung)
    {
        if (file == null || file.Length < 1 || file.Length > AnhQuetXuLy.DungLuongToiDa ||
            file.FileName.Length > 255 || Path.GetExtension(file.FileName).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png"))
            throw new ArgumentException("Ảnh nguồn không hợp lệ.");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, dung);
        if (stream.Length > AnhQuetXuLy.DungLuongToiDa) throw new ArgumentException("Ảnh quá lớn.");
        return stream.ToArray();
    }
}
