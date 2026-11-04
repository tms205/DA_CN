using System.Security.Claims;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyApi.Ocr;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;
public class OcrQueueTests
{
    private sealed class Processor : IOcrProcessor
    {
        public readonly Channel<string> Started = Channel.CreateUnbounded<string>();
        public readonly SemaphoreSlim Proceed = new(0);
        public int Active, MaxActive;
        public async Task<OcrResult> Run(string image, string model, string crop, CancellationToken token)
        {
            int active = Interlocked.Increment(ref Active); MaxActive = Math.Max(MaxActive, active);
            Started.Writer.TryWrite(model);
            try { await Proceed.WaitAsync(token); if (model == "fail") throw new InvalidOperationException("Ảnh không hợp lệ."); return new("Kết quả " + model, false); }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
    private static FormFile Image() => new(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "file", "test.png");
    private static OcrQueue Queue(Processor processor) => new(processor, NullLogger<OcrQueue>.Instance);
    private static async Task<OcrTicket> Finished(OcrQueue queue, int owner, Guid id)
    {
        for (int i = 0; i < 200; i++) { var ticket = queue.Get(owner, id); if (ticket.State is "HOAN_TAT" or "THAT_BAI" or "DA_HUY") return ticket; await Task.Delay(10); }
        throw new TimeoutException("Worker không hoàn tất.");
    }
    [Fact]
    public async Task LegacyAndNewEndpointShareQuotaAndLegacyCancellationReleasesWorker()
    {
        var processor=new Processor();using var queue=Queue(processor);await queue.StartAsync(default);
        var context=new DefaultHttpContext { User=new ClaimsPrincipal(new ClaimsIdentity(new[] {new Claim(TenClaim.MaNguoiDung,"1")},"test")) };
        var controller=new OcrController(null,NullLogger<OcrController>.Instance,processor,queue) { ControllerContext=new(){HttpContext=context} };
        using var cancel=new CancellationTokenSource();
        try {
            var legacy=controller.NhanDang(Image(),"2b",cancel.Token);
            Assert.Equal("2b",await processor.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
            var ticket=Assert.IsType<AcceptedResult>(await controller.TaoTacVu(Image(),"4b",default));
            var rejected=Assert.IsType<ObjectResult>(await controller.TaoTacVu(Image(),"2b",default));Assert.Equal(429,rejected.StatusCode);
            var rejectedOld=Assert.IsType<ObjectResult>(await controller.NhanDang(Image(),"2b",default));Assert.Equal(429,rejectedOld.StatusCode);
            cancel.Cancel();Assert.IsType<EmptyResult>(await legacy.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal("4b",await processor.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
            processor.Proceed.Release();var next=(OcrTicket)ticket.Value;Assert.Equal("HOAN_TAT",(await Finished(queue,1,next.Id)).State);
            Assert.Equal(1,processor.MaxActive);
        } finally { await queue.StopAsync(default); }
    }
    [Fact]
    public async Task WebAndPhoneQueueInOrderAndNeverRunGpuTogether()
    {
        var processor = new Processor(); using var queue = Queue(processor);
        await queue.StartAsync(default);
        try {
            var first = await queue.Add(1, Image(), "2b", null, default);
            Assert.Equal("2b", await processor.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
            var second = await queue.Add(2, Image(), "4b", null, default);
            Assert.Equal("CHO_XU_LY", queue.Get(2, second.Id).State);
            Assert.Equal(2, queue.Get(2, second.Id).Position);
            Assert.Null(queue.Get(1, second.Id)); Assert.False(queue.Cancel(1, second.Id));
            processor.Proceed.Release();
            Assert.Equal("4b", await processor.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
            processor.Proceed.Release();
            Assert.Equal("Kết quả 2b", (await Finished(queue, 1, first.Id)).Text);
            Assert.Equal("Kết quả 4b", (await Finished(queue, 2, second.Id)).Text);
            Assert.Equal(1, processor.MaxActive);
        } finally { await queue.StopAsync(default); }
    }
    [Fact]
    public async Task CancelQueuedImageDoesNotRunAndLaterImagesStillRun()
    {
        var processor = new Processor(); using var queue = Queue(processor);
        var canceled = await queue.Add(1, Image(), "skip", null, default);
        queue.Cancel(1, canceled.Id);
        var next = await queue.Add(2, Image(), "2b", null, default);
        await queue.StartAsync(default);
        try {
            Assert.Equal("2b", await processor.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
            processor.Proceed.Release();
            Assert.Equal("HOAN_TAT", (await Finished(queue, 2, next.Id)).State);
            Assert.Equal("DA_HUY", queue.Get(1, canceled.Id).State);
        } finally { await queue.StopAsync(default); }
    }
    [Fact]
    public async Task FailureAndRunningCancellationDoNotBlockOtherOwners()
    {
        var processor = new Processor(); using var queue = Queue(processor);
        var failed = await queue.Add(1, Image(), "fail", null, default);
        var cancel = await queue.Add(2, Image(), "4b", null, default);
        var last = await queue.Add(3, Image(), "2b", null, default);
        await queue.StartAsync(default);
        try {
            await processor.Started.Reader.ReadAsync(); processor.Proceed.Release();
            Assert.Equal("4b", await processor.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
            queue.Cancel(2, cancel.Id);
            Assert.Equal("2b", await processor.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
            processor.Proceed.Release();
            Assert.Equal("HOAN_TAT", (await Finished(queue, 3, last.Id)).State);
            Assert.Equal("THAT_BAI", queue.Get(1, failed.Id).State);
            Assert.Equal("DA_HUY", queue.Get(2, cancel.Id).State);
            Assert.Equal(1, processor.MaxActive);
        } finally { await queue.StopAsync(default); }
    }
    [Fact]
    public async Task AdmissionIsBoundedEvenWhenCanceledJobsHaveNotBeenCleaned()
    {
        using var queue = Queue(new Processor());
        var first = await queue.Add(1, Image(), "2b", null, default);
        await queue.Add(1, Image(), "2b", null, default);
        await Assert.ThrowsAsync<OcrQueueFullException>(() => queue.Add(1, Image(), "2b", null, default));
        queue.Cancel(1, first.Id);
        await Assert.ThrowsAsync<OcrQueueFullException>(() => queue.Add(1, Image(), "2b", null, default));
        for (int i = 2; i <= 11; i++) await queue.Add(i, Image(), "2b", null, default);
        await Assert.ThrowsAsync<OcrQueueFullException>(() => queue.Add(20, Image(), "2b", null, default));
        await queue.StartAsync(default); await queue.StopAsync(default);
    }
    [Fact]
    public async Task ApiStatusDoesNotExposeOtherOwnersAndValidatesBeforeQueueing()
    {
        using var queue = Queue(new Processor());
        var ticket = await queue.Add(1, Image(), "2b", null, default);
        var controller = new OcrController(null, NullLogger<OcrController>.Instance, new Processor(), queue) {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(TenClaim.MaNguoiDung, "2") }, "test")) } }
        };
        Assert.IsType<NotFoundObjectResult>(controller.TacVu(ticket.Id));
        Assert.IsType<NotFoundResult>(controller.HuyTacVu(ticket.Id));
        Assert.IsType<BadRequestObjectResult>(await controller.TaoTacVu(Image(), "unsupported", default));
        controller.HttpContext.User = new ClaimsPrincipal();
        Assert.IsType<UnauthorizedResult>(controller.TacVu(ticket.Id));
        await queue.StartAsync(default); await queue.StopAsync(default);
    }
}
