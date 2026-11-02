using System.Threading.Channels;
using PhotocopyApi.BaoTri;

namespace PhotocopyApi.Ocr;

public record OcrTicket(Guid Id, string State, int Position, string Text = null, bool Truncated = false, string Message = null);
public sealed class OcrQueueFullException(string message) : Exception(message);

// Hàng chờ tạm cho một máy chủ. Không chứa lịch sử hay bản người dùng đã chỉnh sửa.
public sealed class OcrQueue(IOcrProcessor processor, ILogger<OcrQueue> logger, BaoTriStore maintenance = null) : BackgroundService
{
    private sealed class Job
    {
        public Guid Id = Guid.NewGuid();
        public int Owner;
        public string Directory, Image, Model, Crop, State = "CHO_XU_LY", Text, Message;
        public bool Truncated, Cleaned;
        public DateTimeOffset Created = DateTimeOffset.UtcNow, Ended;
        public CancellationTokenSource Stop = new();
    }
    private readonly object _lock = new();
    private readonly Dictionary<Guid, Job> _jobs = new();
    private readonly Channel<Job> _pending = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = true });
    private static bool Active(Job job) => job.State is "CHO_XU_LY" or "DANG_XU_LY";

    public async Task<OcrTicket> Add(int owner, IFormFile file, string model, string crop, CancellationToken cancellation)
    {
        var job = new Job { Owner = owner, Model = model, Crop = crop };
        lock (_lock)
        {
            Prune();
            if (_jobs.Values.Count(j => !j.Cleaned) >= 12 || _jobs.Values.Count(j => j.Owner == owner && !j.Cleaned) >= 2)
                throw new OcrQueueFullException("Hàng chờ OCR đã đủ lượt. Vui lòng đợi một tác vụ hoàn tất rồi gửi tiếp.");
            job.Directory = Path.Combine(Path.GetTempPath(), "PhotocopyOcrQueue", job.Id.ToString("N"));
            job.Image = Path.Combine(job.Directory, "input" + Path.GetExtension(file.FileName).ToLowerInvariant());
            _jobs.Add(job.Id, job);
        }
        try
        {
            Directory.CreateDirectory(job.Directory);
            await using (var output = new FileStream(job.Image, FileMode.CreateNew)) await file.CopyToAsync(output, cancellation);
            cancellation.ThrowIfCancellationRequested();
            // Đặt mốc xếp hàng sau khi upload hoàn tất; thứ tự không phụ thuộc tốc độ upload.
            lock (_lock) { job.Created = DateTimeOffset.UtcNow; _pending.Writer.TryWrite(job); return Snapshot(job); }
        }
        catch { lock (_lock) _jobs.Remove(job.Id); Clean(job); job.Stop.Dispose(); throw; }
    }

    public OcrTicket Get(int owner, Guid id)
    {
        lock (_lock) { Prune(); return _jobs.TryGetValue(id, out var job) && job.Owner == owner ? Snapshot(job) : null; }
    }
    public bool Cancel(int owner, Guid id)
    {
        lock (_lock)
        {
            if (!_jobs.TryGetValue(id, out var job) || job.Owner != owner) return false;
            if (Active(job)) { job.State = "DA_HUY"; job.Ended = DateTimeOffset.UtcNow; job.Stop.Cancel(); }
            return true;
        }
    }
    private OcrTicket Snapshot(Job job) => new(job.Id, job.State,
        job.State == "CHO_XU_LY" ? _jobs.Values.Count(j => Active(j) && (j.State == "DANG_XU_LY" || j.Created < job.Created)) + 1 : 0,
        job.Text, job.Truncated, job.Message);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _pending.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    lock (_lock)
                    {
                        if (!Active(job)) continue;
                        if (BaoTriStore.KhoaTacVuNen(maintenance?.Read(), "OCR"))
                            throw new InvalidOperationException("OCR đang bảo trì. Vui lòng gửi lại sau khi hệ thống mở.");
                        job.State = "DANG_XU_LY";
                    }
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(job.Stop.Token, stoppingToken);
                    var result = await processor.Run(job.Image, job.Model, job.Crop, linked.Token);
                    lock (_lock)
                    {
                        if (job.State != "DA_HUY") { job.Text = result.Text; job.Truncated = result.Truncated; job.State = "HOAN_TAT"; }
                    }
                }
                catch (OperationCanceledException) { lock (_lock) job.State = "DA_HUY"; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Tác vụ OCR {Id} thất bại", job.Id);
                    lock (_lock) if (job.State != "DA_HUY")
                    { job.State = "THAT_BAI"; job.Message = ex is InvalidOperationException ? ex.Message : "OCR gặp lỗi. Vui lòng thử lại ảnh này."; }
                }
                finally { lock (_lock) job.Ended = DateTimeOffset.UtcNow; Clean(job); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            lock (_lock) foreach (var job in _jobs.Values) { if (Active(job)) job.State = "DA_HUY"; Clean(job); }
        }
    }
    private void Prune()
    {
        foreach (var job in _jobs.Values.Where(j => j.Cleaned && !Active(j) && j.Ended < DateTimeOffset.UtcNow.AddMinutes(-15)).ToArray())
        { _jobs.Remove(job.Id); job.Stop.Dispose(); }
        // Giới hạn cả kết quả tạm trong bộ nhớ, không chỉ số tác vụ đang chờ.
        foreach (var job in _jobs.Values.Where(j => j.Cleaned && !Active(j)).OrderBy(j => j.Ended).Take(Math.Max(0, _jobs.Count - 120)).ToArray())
        { _jobs.Remove(job.Id); job.Stop.Dispose(); }
    }
    private void Clean(Job job)
    {
        try { if (Directory.Exists(job.Directory)) Directory.Delete(job.Directory, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Không dọn được ảnh OCR tạm {Id}", job.Id); }
        finally { lock (_lock) job.Cleaned = true; }
    }
}
