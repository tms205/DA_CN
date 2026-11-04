using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotocopyApi.Ocr;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/ocr")]
[Authorize(Roles = "CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
public class OcrController : ControllerBase
{
    private readonly OcrQueue _queue;
    private readonly ILogger<OcrController> _logger;
    public OcrController(IWebHostEnvironment environment, ILogger<OcrController> logger, IOcrProcessor processor = null, OcrQueue queue = null)
    {
        _queue = queue;
        _logger = logger;
    }
    private string ValidateImage(IFormFile file, string model, string crop) =>
        file == null || !OcrRules.Valid(model, file.FileName, file.Length)
            ? "Chọn model 2B hoặc 4B và ảnh JPG/PNG tối đa 10 MB."
            : crop != null && !OcrCrop.Valid(crop) ? "Vùng cắt không hợp lệ. Hãy chọn lại vùng chữ bên trong ảnh." : null;

    // Giữ tương thích bản app cũ; web/app mới dùng tác vụ ngắn và thăm dò trạng thái.
    [HttpPost("nhan-dang")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> NhanDang(IFormFile file, [FromForm] string model, CancellationToken cancellation, [FromForm] string vungCat = null)
    {
        Response.Headers.CacheControl = "no-store";
        var error = ValidateImage(file, model, vungCat);
        if (error != null) return BadRequest(new { message = error });
        if (!int.TryParse(User.FindFirst(PhotocopyShared.Constants.TenClaim.MaNguoiDung)?.Value, out int owner) || owner <= 0) return Unauthorized();
        if (_queue == null) return StatusCode(503, new { message = "Hàng chờ OCR chưa sẵn sàng." });
        Guid? id = null;
        try
        {
            var ticket = await _queue.Add(owner, file, model, vungCat, cancellation);
            id = ticket.Id;
            var result = await _queue.Wait(owner, ticket.Id, cancellation);
            if (result.State != "HOAN_TAT") return StatusCode(503, new { message = result.Message ?? "Yêu cầu OCR đã dừng. Vui lòng thử lại." });
            return Ok(new { text = result.Text, model, truncated = result.Truncated, requiresReview = true });
        }
        catch (OcrQueueFullException ex) { return StatusCode(429, new { message = ex.Message }); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return new EmptyResult(); }
        catch (Exception ex) { _logger.LogError(ex, "OCR một ảnh thất bại"); return StatusCode(503, new { message = "Không xử lý được OCR. Vui lòng thử lại." }); }
        finally { if (id.HasValue && cancellation.IsCancellationRequested) _queue.Cancel(owner, id.Value); }
    }

    [HttpPost("tac-vu")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> TaoTacVu(IFormFile file, [FromForm] string model, CancellationToken cancellation, [FromForm] string vungCat = null)
    {
        Response.Headers.CacheControl = "no-store";
        if (!int.TryParse(User.FindFirst(PhotocopyShared.Constants.TenClaim.MaNguoiDung)?.Value, out int owner) || owner <= 0) return Unauthorized();
        var error = ValidateImage(file, model, vungCat);
        if (error != null) return BadRequest(new { message = error });
        try { return Accepted(await _queue.Add(owner, file, model, vungCat, cancellation)); }
        catch (OcrQueueFullException ex) { return StatusCode(429, new { message = ex.Message }); }
    }
    [HttpGet("tac-vu/{id:guid}")]
    public IActionResult TacVu(Guid id)
    {
        Response.Headers.CacheControl = "no-store";
        if (!int.TryParse(User.FindFirst(PhotocopyShared.Constants.TenClaim.MaNguoiDung)?.Value, out int owner)) return Unauthorized();
        var result = _queue.Get(owner, id);
        return result == null ? NotFound(new { message = "Tác vụ không còn tồn tại hoặc không thuộc tài khoản của bạn. Hãy thử lại ảnh này." }) : Ok(result);
    }
    [HttpDelete("tac-vu/{id:guid}")]
    public IActionResult HuyTacVu(Guid id)
    {
        if (!int.TryParse(User.FindFirst(PhotocopyShared.Constants.TenClaim.MaNguoiDung)?.Value, out int owner)) return Unauthorized();
        return _queue.Cancel(owner, id) ? Ok(new { message = "Đã dừng yêu cầu OCR." }) : NotFound();
    }
    [HttpPost("xuat-word")]
    [RequestSizeLimit(1024 * 1024)]
    public IActionResult XuatWord([FromBody] XuatWordRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        try { return File(OcrWord.CreatePages(request?.Pages ?? new[] { request?.Text }), OcrWord.Mime, "Van-ban-nhan-dang.docx"); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

}

public sealed class XuatWordRequest
{
    [System.ComponentModel.DataAnnotations.StringLength(100000, ErrorMessage = "Văn bản tối đa 100.000 ký tự.")]
    public string Text { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(10, ErrorMessage = "Mỗi lượt xuất tối đa 10 trang kết quả.")]
    public string[] Pages { get; set; }
}
