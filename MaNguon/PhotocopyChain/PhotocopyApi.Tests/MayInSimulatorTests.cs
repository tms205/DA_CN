using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhotocopyPrinterSimulator;
using PhotocopyShared.Dtos.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public class MayInSimulatorTests
{
    [Fact]
    public async Task TatBatGiuaLucIn_PhucHoiCungLenhVaKhongTaiInLai()
    {
        string thuMuc = Path.Combine(Path.GetTempPath(), "photocopy-simulator-" + Guid.NewGuid());
        using var handler = new ApiChoSimulator();
        var config = Options.Create(new CauHinhSimulator
        {
            ThuMucTrangThai = thuMuc, SoGiayKiemTra = 1,
            MayIn = new List<CauHinhTocDoMayIn>
            {
                new() { MaMayIn = 1, TenMay = "Máy thử", TocDoA4 = 15, SoGiayRaTrangDau = 0 }
            }
        });
        using (var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost/") })
        using (var simulator = new MayInSimulator(http, config, NullLogger<MayInSimulator>.Instance))
        {
            await simulator.StartAsync(CancellationToken.None);
            using var han = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string tep = Path.Combine(thuMuc, "may-1.json");
            while (!File.Exists(tep) || !JsonSerializer.Deserialize<TienTrinhInDaLuu>(File.ReadAllText(tep),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)).DaBatDau)
                await Task.Delay(20, han.Token);
            await simulator.StopAsync(CancellationToken.None);
        }

        using (var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost/") })
        using (var simulator = new MayInSimulator(http, config, NullLogger<MayInSimulator>.Instance))
        {
            await simulator.StartAsync(CancellationToken.None);
            await handler.DaHoanTat.Task.WaitAsync(TimeSpan.FromSeconds(8));
            // Chờ simulator nhận ACK và xóa checkpoint trước khi dừng.
            using var han = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (File.Exists(Path.Combine(thuMuc, "may-1.json"))) await Task.Delay(20, han.Token);
            await simulator.StopAsync(CancellationToken.None);
        }
        Assert.Equal(1, handler.SoLanNhanLenh);
        Assert.Equal(1, handler.SoLanTaiFile);
        Assert.Equal(1, handler.SoLanHoanTat);
        Assert.Single(handler.ConnectorIds);
    }

    [Fact]
    public async Task ChuaNhanDuocDon_KhongGuiBaoLoiDon0()
    {
        using var handler = new GhiNhanRequest();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var simulator = new MayInSimulator(http, Options.Create(new CauHinhSimulator()),
            NullLogger<MayInSimulator>.Instance);
        var ham = typeof(MayInSimulator).GetMethod("GuiLoiAnToan", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)ham.Invoke(simulator, new object[] { 1, 0, "API đang tắt", CancellationToken.None, "" });
        Assert.Empty(handler.DuongDan);
    }

    private sealed class GhiNhanRequest : HttpMessageHandler
    {
        public List<string> DuongDan { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken dung)
        {
            DuongDan.Add(req.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    private sealed class ApiChoSimulator : HttpMessageHandler
    {
        private const string MaLenh = "lenh-test-khong-phai-secret";
        public int SoLanNhanLenh { get; private set; }
        public int SoLanTaiFile { get; private set; }
        public int SoLanHoanTat { get; private set; }
        public HashSet<string> ConnectorIds { get; } = new();
        public TaskCompletionSource<bool> DaHoanTat { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken dung)
        {
            string id = req.Headers.GetValues("X-Printer-Connector-Id").Single();
            Assert.True(Guid.TryParse(id, out _));
            ConnectorIds.Add(id);
            string duongDan = req.RequestUri.AbsolutePath;
            if (duongDan.EndsWith("lenh-tiep-theo"))
            {
                if (SoLanNhanLenh > 0) return new HttpResponseMessage(HttpStatusCode.NoContent);
                SoLanNhanLenh++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new LenhInAoDto
                    {
                        MaDonHang = 6, MaMayIn = 1, MaLenh = MaLenh, TenMay = "Máy thử", MaDonHangHienThi = "DH00006",
                        DsDongIn = new List<DongLenhInDto>
                        {
                            new() { MaFile = 13, MaChiTiet = 6, TenFile = "test.pdf", SoTrang = 1, SoBan = 1,
                                KhoGiay = "A4", MatIn = "1_MAT", MauSac = "DEN_TRANG", DuongDanTaiFile = "api/may-in-ao/don/6/file/13" }
                        }
                    })
                };
            }
            if (duongDan.Contains("/file/"))
            {
                Assert.Equal(MaLenh, req.Headers.GetValues("X-Printer-Job-Token").Single());
                SoLanTaiFile++;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) };
            }
            if (duongDan.EndsWith("hoan-tat"))
            {
                var baoCao = await req.Content.ReadFromJsonAsync<BaoCaoMayInRequest>(dung);
                Assert.Equal(MaLenh, baoCao.MaLenh);
                Assert.Equal(1, baoCao.MaMayIn);
                SoLanHoanTat++;
                DaHoanTat.TrySetResult(true);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
