using System.Diagnostics;
using System.Text.Json;

namespace PhotocopyApi.Ocr;

public record OcrResult(string Text, bool Truncated);
public interface IOcrProcessor
{
    Task<OcrResult> Run(string image, string model, string crop, CancellationToken cancellation);
}

// Khóa dùng chung cả API một ảnh cũ và worker: không nạp hai model cùng lúc.
public sealed class OcrPython(IWebHostEnvironment environment, ILogger<OcrPython> logger) : IOcrProcessor
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public async Task<OcrResult> Run(string image, string model, string crop, CancellationToken cancellation)
    {
        await Gate.WaitAsync(cancellation);
        Process process = null;
        string result = Path.Combine(Path.GetDirectoryName(image)!, "result.json");
        try
        {
            string root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "photocopy_ocr"));
            string python = Path.Combine(root, ".venv", "Scripts", "python.exe");
            string script = Path.Combine(root, "ocr_api.py");
            if (!File.Exists(python) || !File.Exists(script))
                throw new InvalidOperationException("Chưa tìm thấy môi trường OCR trên máy chủ.");
            var start = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root };
            foreach (var argument in new[] { script, image, model, result }) start.ArgumentList.Add(argument);
            if (crop != null) start.ArgumentList.Add(crop);
            process = Process.Start(start) ?? throw new InvalidOperationException("Không khởi động được OCR.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromMinutes(14));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            { throw new InvalidOperationException("OCR xử lý quá lâu. Hãy cắt vùng chữ nhỏ hơn hoặc thử model 2B."); }
            if (process.ExitCode != 0 || !File.Exists(result))
                throw new InvalidOperationException("Không chạy được OCR. Kiểm tra runtime và model trên máy chủ.");
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(result, cancellation));
            var output = json.RootElement;
            if (!output.GetProperty("ok").GetBoolean())
                throw new InvalidOperationException(output.GetProperty("message").GetString());
            string text = output.GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(text) || text.Length > 100000)
                throw new InvalidOperationException("Không nhận được văn bản hợp lệ. Hãy kiểm tra vùng chữ và thử lại.");
            return new(text, output.GetProperty("truncated").GetBoolean());
        }
        finally
        {
            try
            {
                if (process != null)
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                    process.Dispose();
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Không dừng được tiến trình OCR của yêu cầu"); }
            finally { Gate.Release(); }
        }
    }
}
