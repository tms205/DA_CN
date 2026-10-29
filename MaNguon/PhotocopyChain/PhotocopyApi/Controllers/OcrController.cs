using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotocopyApi.Ocr;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/ocr")]
[Authorize(Roles = "CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
public class OcrController : ControllerBase
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OcrController> _logger;
    public OcrController(IWebHostEnvironment environment, ILogger<OcrController> logger)
        => (_environment, _logger) = (environment, logger);

    [HttpPost("nhan-dang")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> NhanDang(IFormFile file, [FromForm] string model, CancellationToken cancellation, [FromForm] string vungCat = null)
    {
        Response.Headers.CacheControl = "no-store";
        if (file == null || !OcrRules.Valid(model, file.FileName, file.Length))
            return BadRequest(new { message = "Chọn model 2B hoặc 4B và ảnh JPG/PNG tối đa 10 MB." });
        if (vungCat != null && !OcrCrop.Valid(vungCat))
            return BadRequest(new { message = "Vùng cắt không hợp lệ. Hãy chọn lại vùng chữ bên trong ảnh." });
        if (!await Gate.WaitAsync(0, cancellation))
            return StatusCode(429, new { message = "OCR đang xử lý ảnh khác. Vui lòng thử lại sau." });
        string directory = Path.Combine(Path.GetTempPath(), "PhotocopyOcr", Guid.NewGuid().ToString("N"));
        Process process = null;
        try
        {
            var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", "photocopy_ocr"));
            var python = Path.Combine(root, ".venv", "Scripts", "python.exe");
            var script = Path.Combine(root, "ocr_api.py");
            if (!System.IO.File.Exists(python) || !System.IO.File.Exists(script))
                return StatusCode(503, new { message = "Chưa tìm thấy môi trường OCR trên máy chủ. Kiểm tra thư mục photocopy_ocr." });
            Directory.CreateDirectory(directory);
            var image = Path.Combine(directory, "input" + Path.GetExtension(file.FileName).ToLowerInvariant());
            await using (var output = new FileStream(image, FileMode.CreateNew))
                await file.CopyToAsync(output, cancellation);
            var result = Path.Combine(directory, "result.json");
            var start = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root };
            start.ArgumentList.Add(script); start.ArgumentList.Add(image); start.ArgumentList.Add(model); start.ArgumentList.Add(result);
            if (vungCat != null) start.ArgumentList.Add(vungCat);
            process = Process.Start(start) ?? throw new InvalidOperationException("Không khởi động được OCR.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromMinutes(14));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                if (cancellation.IsCancellationRequested) throw;
                return StatusCode(504, new { message = "OCR xử lý quá lâu. Hãy thử ảnh nhỏ hơn hoặc model 2B." });
            }
            if (!System.IO.File.Exists(result))
                return StatusCode(503, new { message = "Không khởi động được OCR. Kiểm tra model, runtime và GPU trên máy chủ." });
            using var json = JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(result, cancellation));
            var rootResult = json.RootElement;
            if (!rootResult.GetProperty("ok").GetBoolean())
                return BadRequest(new { message = rootResult.GetProperty("message").GetString() });
            return Ok(new { text = rootResult.GetProperty("text").GetString(), model,
                truncated = rootResult.GetProperty("truncated").GetBoolean(), requiresReview = true });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return new EmptyResult(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể xử lý yêu cầu OCR");
            return StatusCode(503, new { message = "OCR chưa sẵn sàng. Kiểm tra cấu hình trên máy chủ và thử lại." });
        }
        finally
        {
            if (process != null) { if (!process.HasExited) process.Kill(entireProcessTree: true); process.Dispose(); }
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _logger.LogWarning("Không dọn được thư mục OCR tạm"); }
            Gate.Release();
        }
    }
    [HttpPost("xuat-word")]
    [RequestSizeLimit(1024 * 1024)]
    public IActionResult XuatWord([FromBody] XuatWordRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        try { return File(OcrWord.Create(request?.Text), OcrWord.Mime, "Van-ban-nhan-dang.docx"); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

}

public sealed class XuatWordRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập văn bản cần xuất.")]
    [System.ComponentModel.DataAnnotations.StringLength(100000, ErrorMessage = "Văn bản tối đa 100.000 ký tự.")]
    public string Text { get; set; }
}
