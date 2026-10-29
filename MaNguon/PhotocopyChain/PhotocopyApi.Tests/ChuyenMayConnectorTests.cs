using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhotocopyPrinterSimulator;
using PhotocopyShared.Dtos.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public sealed class ChuyenMayConnectorTests
{
    [Fact]
    public async Task CheckpointLoi_GapYeuCauDung_KhongGuiLaiVaAckTruocKhiBoCheckpoint()
    {
        string dir = Path.Combine(Path.GetTempPath(), "photocopy-stop-worker-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "may-1.json"), """
            {"maLenh":"token-stop","maDonHang":57,"cheDo":"THAT","tenHangDoi":"queue-fixture",
             "daBatDau":true,"daGuiDuFile":false,"choXacNhan":true,"tepDaGui":[],
             "lenh":{"maLenh":"token-stop","maDonHang":57,"maMayIn":1,"dsDongIn":[{"maChiTiet":1,"maFile":1,"soTrang":5,"soBan":1}]}}
            """);
        using var api = new ApiDung();
        using var http = new HttpClient(api) { BaseAddress = new Uri("http://fixture.local/") };
        using var worker = new MayInSimulator(http, Options.Create(new CauHinhSimulator {
            ThuMucTrangThai = dir, MayIn = new() { new() { MaMayIn = 1, CheDo = "THAT", TenHangDoiWindows = "queue-fixture" } }
        }), NullLogger<MayInSimulator>.Instance, new WindowsKhongIn());
        await worker.StartAsync(default);
        try
        {
            await Task.WhenAny(api.DaDung.Task, Task.Delay(3500));
            Assert.True(api.DaDung.Task.IsCompleted, "Connector phải ACK dừng, không giữ mãi ở LOI.");
            Assert.Equal(0, api.HoanTat);
            Assert.True(File.Exists(Path.Combine(dir, "may-1.json")), "ACK thất bại phải giữ checkpoint dừng để retry sau restart.");
            using var checkpoint = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, "may-1.json")));
            Assert.True(checkpoint.RootElement.GetProperty("daDungDeChuyen").GetBoolean());
        }
        finally { await worker.StopAsync(default); Directory.Delete(dir, true); }
    }

    [Fact]
    public void WindowsPhaiCoHuyJobRieng_KhongDungPurgeToanQueue()
        => Assert.NotNull(typeof(MayInWindows).GetMethod("HuyJob"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DungQueue_ChiHuyJobCuaDon_PhaiDoiJobBienMatMoiAck(bool jobKhongXoaDuoc)
    {
        string dir = Path.Combine(Path.GetTempPath(), "photocopy-stop-queue-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        string pdf = Path.Combine(dir, "DH00057-fixture.pdf");
        await File.WriteAllTextAsync(pdf, "%PDF-fixture");
        await File.WriteAllTextAsync(Path.Combine(dir, "may-1.json"), JsonSerializer.Serialize(new TienTrinhInDaLuu {
            MaLenh="token-stop", MaDonHang=57, CheDo="THAT", TenHangDoi="queue-fixture", DaBatDau=true,
            TepDaGui=new(){pdf}, Lenh=new(){MaLenh="token-stop",MaDonHang=57,MaMayIn=1}
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var windows = new WindowsCoJob(jobKhongXoaDuoc);
        using var api = new ApiDung { KetQuaAck=HttpStatusCode.OK };
        using var http = new HttpClient(api) { BaseAddress=new Uri("http://fixture.local/") };
        using var worker = TaoWorker(http, dir, windows);
        await worker.StartAsync(default);
        try
        {
            await windows.DaHuy.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (jobKhongXoaDuoc)
            {
                await windows.DaDocLai.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(api.DaDung.Task.IsCompleted);
                Assert.True(File.Exists(Path.Combine(dir,"may-1.json")));
            }
            else
            {
                await api.DaDung.Task.WaitAsync(TimeSpan.FromSeconds(5));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                while (File.Exists(Path.Combine(dir,"may-1.json"))) await Task.Delay(20,timeout.Token);
                Assert.Single(Directory.GetFiles(Path.Combine(dir,"LichSuDung"),"*.json"));
            }
            Assert.Equal(new uint[]{11}, windows.JobDaHuy);
            Assert.True(File.Exists(pdf)); // Giữ chứng cứ, không xóa tài liệu khi chuyển máy.
            Assert.Equal(0,api.HoanTat);
        }
        finally { await worker.StopAsync(default); Directory.Delete(dir,true); }
    }

    [Fact]
    public async Task DangGuiPdf_PhaiDoiBoGuiNgungTruocKhiAck_KhongGuiFileKeTiep()
    {
        string dir=Path.Combine(Path.GetTempPath(),"photocopy-stop-sender-"+Guid.NewGuid());
        var windows=new WindowsGuiDangDung();
        using var api=new ApiDung { GuiYeuCauDung=false, CapLenhMoi=true, KetQuaAck=HttpStatusCode.OK };
        using var http=new HttpClient(api) {BaseAddress=new Uri("http://fixture.local/")};
        using var worker=TaoWorker(http,dir,windows);
        await worker.StartAsync(default);
        try
        {
            await windows.BatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
            api.GuiYeuCauDung=true;
            await windows.NhanDung.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(api.DaDung.Task.IsCompleted);
            windows.ChoDungXong.TrySetResult();
            await api.DaDung.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1,windows.SoLanGui);
            Assert.Equal(0,api.HoanTat);
        }
        finally { windows.ChoDungXong.TrySetResult(); await worker.StopAsync(default); if(Directory.Exists(dir))Directory.Delete(dir,true); }
    }

    private static MayInSimulator TaoWorker(HttpClient http,string dir,MayInWindows windows) => new(http,
        Options.Create(new CauHinhSimulator {ThuMucTrangThai=dir,MayIn=new(){new(){MaMayIn=1,CheDo="THAT",TenHangDoiWindows="queue-fixture"}}}),
        NullLogger<MayInSimulator>.Instance, windows);

    private sealed class WindowsCoJob(bool giuJob) : MayInWindows
    {
        public List<uint> JobDaHuy=new();
        public TaskCompletionSource DaHuy=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DaDocLai=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void KiemTraCauHinh(string a,string b) { }
        public override List<JobWindows> DocJob(string queue)
        {
            Assert.Equal("queue-fixture",queue);
            if(JobDaHuy.Count>0) DaDocLai.TrySetResult();
            var jobs=new List<JobWindows>{new(){Id=22,TenTaiLieu="don-cua-ung-dung-khac.pdf"}};
            if(giuJob || JobDaHuy.Count==0)jobs.Add(new(){Id=11,TenTaiLieu="DH00057-fixture.pdf"});
            return jobs;
        }
        public override void HuyJob(string queue,JobWindows job) {JobDaHuy.Add(job.Id); DaHuy.TrySetResult();}
        public override Task GuiPdf(string a,string b,string c,DongLenhInDto d,CancellationToken t)
            => throw new InvalidOperationException("Không được gửi lại PDF của checkpoint cũ.");
    }

    private sealed class WindowsGuiDangDung : MayInWindows
    {
        public int SoLanGui;
        public TaskCompletionSource BatDau=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource NhanDung=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ChoDungXong=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void KiemTraCauHinh(string a,string b) { }
        public override List<JobWindows> DocJob(string queue)=>new();
        public override async Task GuiPdf(string a,string b,string c,DongLenhInDto d,CancellationToken token)
        {
            Interlocked.Increment(ref SoLanGui); BatDau.TrySetResult();
            try {await Task.Delay(Timeout.Infinite,token);}
            catch(OperationCanceledException) {NhanDung.TrySetResult();await ChoDungXong.Task;throw;}
        }
    }

    private sealed class WindowsKhongIn : MayInWindows
    {
        public override void KiemTraCauHinh(string a, string b) { }
        public override List<JobWindows> DocJob(string p) => new();
        public override Task GuiPdf(string a, string b, string c, DongLenhInDto d, CancellationToken t)
            => throw new InvalidOperationException("Không được gửi lại PDF khi chuyển máy.");
    }
    private sealed class ApiDung : HttpMessageHandler
    {
        public TaskCompletionSource DaDung = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int HoanTat;
        public HttpStatusCode KetQuaAck=HttpStatusCode.ServiceUnavailable;
        public volatile bool GuiYeuCauDung=true;
        public bool CapLenhMoi;
        private int _soLenh;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            string url = req.RequestUri.AbsolutePath;
            if (url.EndsWith("dieu-khien")) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new DieuKhienMayInDto{MaLenhDung=GuiYeuCauDung?"token-stop":null}) };
            if(url.EndsWith("lenh-tiep-theo"))
            {
                if(!CapLenhMoi || Interlocked.Increment(ref _soLenh)>1) return new(HttpStatusCode.NoContent);
                return new(HttpStatusCode.OK){Content=JsonContent.Create(new LenhInAoDto{MaLenh="token-stop",MaDonHang=57,MaMayIn=1,DsDongIn=new(){
                    new(){MaFile=1,SoTrang=5,SoBan=1,DuongDanTaiFile="api/may-in-ao/don/57/file/1"},new(){MaFile=2,SoTrang=2,SoBan=1,DuongDanTaiFile="api/may-in-ao/don/57/file/2"}
                }})};
            }
            if(url.Contains("/file/")) return new(HttpStatusCode.OK){Content=new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-fixture"))};
            if (url.EndsWith("da-dung"))
            {
                var body = await req.Content.ReadFromJsonAsync<BaoCaoMayInRequest>(token);
                Assert.Equal("token-stop", body.MaLenh);
                DaDung.TrySetResult();
                return new(KetQuaAck); // ACK lỗi phải giữ bằng chứng, không gửi lại PDF.
            }
            if (url.EndsWith("hoan-tat")) HoanTat++;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { }) };
        }
    }
}
