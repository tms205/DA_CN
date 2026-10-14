using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhotocopyPrinterSimulator;
using PhotocopyShared.Dtos.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public class MayInThatTests
{
    [Fact]
    public async Task GuiPdfCham_VanNhanDuocLenhTamDungVaGuiHeartbeat()
    {
        using var handler = new ApiInThat();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var windows = new WindowsGuiCham();
        using var worker = new MayInSimulator(http, Options.Create(new CauHinhSimulator {
            ThuMucTrangThai = Path.Combine(Path.GetTempPath(), "photocopy-slow-test-" + Guid.NewGuid()),
            MayIn = new() { new() { MaMayIn = 1, CheDo = "THAT", TenHangDoiWindows = "USB fixture" } }
        }), NullLogger<MayInSimulator>.Instance, windows);
        await worker.StartAsync(default);
        await windows.BatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.TamDung = true;
        try { await windows.DaPause.Task.WaitAsync(TimeSpan.FromSeconds(4)); }
        finally { await worker.StopAsync(default); }
        Assert.Equal(0, handler.SoLanHoanTat);
    }

    private sealed class WindowsGuiCham : MayInWindows
    {
        private string _tenTep;
        public TaskCompletionSource<bool> BatDau = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> DaPause = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void KiemTraCauHinh(string engine, string printer) { }
        public override async Task GuiPdf(string engine, string printer, string file, DongLenhInDto dong, CancellationToken token)
        {
            _tenTep = Path.GetFileName(file);
            BatDau.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
        }
        public override List<JobWindows> DocJob(string printer) => _tenTep == null ? new() : new() { new() { Id = 1, TenTaiLieu = _tenTep, Status = 16 } };
        public override void TamDungJob(string printer, JobWindows job, bool pause) { if (pause) DaPause.TrySetResult(true); }
    }
    [Fact]
    public async Task GuiThanhCongQueueRong_ChuaHoanTat_RestartKhongGuiLai_ChiXacNhanDungLenh()
    {
        string dir = Path.Combine(Path.GetTempPath(), "photocopy-real-test-" + Guid.NewGuid());
        var config = Options.Create(new CauHinhSimulator { ThuMucTrangThai = dir, MayIn = new() {
            new() { MaMayIn = 1, CheDo = "THAT", TenHangDoiWindows = "USB fixture", TocDoA4 = 30 }
        }});
        using var handler = new ApiInThat();
        var windows = new WindowsKhongInGiay();
        using (var http = new HttpClient(handler, false) { BaseAddress = new Uri("http://localhost/") })
        using (var worker = new MayInSimulator(http, config, NullLogger<MayInSimulator>.Instance, windows))
        {
            await worker.StartAsync(default);
            await handler.ChoXacNhan.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, handler.SoLanHoanTat);
            Assert.Equal(1, windows.SoLanGui);
            await worker.StopAsync(default);
        }
        handler.MaXacNhan = "sai-lenh";
        handler.ChoXacNhan = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var http = new HttpClient(handler, false) { BaseAddress = new Uri("http://localhost/") })
        using (var worker = new MayInSimulator(http, config, NullLogger<MayInSimulator>.Instance, windows))
        {
            await worker.StartAsync(default);
            await handler.ChoXacNhan.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, handler.SoLanHoanTat);
            Assert.Equal(1, windows.SoLanGui);
            handler.MaXacNhan = "lenh-test";
            await handler.HoanTat.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var han = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (File.Exists(Path.Combine(dir, "may-1.json"))) await Task.Delay(20, han.Token);
            await worker.StopAsync(default);
        }
        Assert.Equal(1, handler.SoLanTai);
        Assert.Equal(1, handler.SoLanNhan);
        Assert.Equal(1, handler.SoLanHoanTat);
    }

    [Fact]
    public void JobKhongDungTenFile_KhongDuocPauseJobNguoiKhac()
    {
        Assert.False(MayInWindows.ThuocTep(new() { TenTaiLieu = "tai-lieu-ngoai.pdf" }, "C:/temp/DH00006-x.pdf"));
        Assert.True(MayInWindows.ThuocTep(new() { TenTaiLieu = "DH00006-x.pdf" }, "C:/temp/DH00006-x.pdf"));
    }

    // Chỉ thay biên thiết bị ngoài tiến trình; checkpoint/HTTP/worker chạy thật trong test.
    private sealed class WindowsKhongInGiay : MayInWindows
    {
        public int SoLanGui;
        public override void KiemTraCauHinh(string engine, string printer) { }
        public override Task GuiPdf(string engine, string printer, string file, DongLenhInDto dong, CancellationToken token)
        {
            Assert.True(File.Exists(file));
            Assert.StartsWith("%PDF-", File.ReadAllText(file));
            SoLanGui++;
            return Task.CompletedTask;
        }
        public override List<JobWindows> DocJob(string printer) => new();
    }

    private sealed class ApiInThat : HttpMessageHandler
    {
        public int SoLanTai, SoLanNhan, SoLanHoanTat;
        public volatile string MaXacNhan;
        public volatile bool TamDung;
        public TaskCompletionSource<bool> ChoXacNhan = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> HoanTat = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            string url = req.RequestUri.AbsolutePath;
            if (url.EndsWith("dieu-khien")) return Tra(new DieuKhienMayInDto { MaLenhXacNhan = MaXacNhan, TamDung = TamDung });
            if (url.EndsWith("lenh-tiep-theo"))
            {
                if (SoLanNhan++ > 0) return new(HttpStatusCode.NoContent);
                return Tra(new LenhInAoDto { MaLenh = "lenh-test", MaDonHang = 6, MaMayIn = 1, DsDongIn = new() {
                    new() { MaFile = 1, SoTrang = 2, SoBan = 1, DuongDanTaiFile = "api/may-in-ao/don/6/file/1" }
                }});
            }
            if (url.Contains("/file/")) { SoLanTai++; return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-test-fixture")) }; }
            var body = await req.Content.ReadFromJsonAsync<BaoCaoMayInRequest>(token);
            if (body.TrangThai == "CHO_XAC_NHAN") ChoXacNhan.TrySetResult(true);
            if (url.EndsWith("hoan-tat")) { SoLanHoanTat++; HoanTat.TrySetResult(true); }
            return new(HttpStatusCode.OK);
        }
        private static HttpResponseMessage Tra<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
