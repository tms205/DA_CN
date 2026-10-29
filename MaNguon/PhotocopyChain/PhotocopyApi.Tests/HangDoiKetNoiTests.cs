using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public class HangDoiKetNoiTests
{
    [Fact]
    public async Task Poll_TimeoutChiTiet_KhongLamHongVongCapNhatVaRoiTrang()
    {
        using var api = new ApiCham(false);
        await KiemTra(api, async () => await api.DaPhucHoi.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(2, api.SoLanDocDon);
    }

    [Fact]
    public async Task RoiTrang_HuyNgayRequestTrangThaiMayInDangCho()
    {
        using var api = new ApiCham(true);
        await KiemTra(api, async () =>
        {
            await api.DaGoiMay.Task.WaitAsync(TimeSpan.FromSeconds(5));
            TrangThu.Dung();
            await api.DaHuyMay.Task.WaitAsync(TimeSpan.FromSeconds(1));
        });
    }

    private static async Task KiemTra(ApiCham api, Func<Task> kiemTra)
    {
        using var http = new HttpClient(api) { BaseAddress = new Uri("http://fixture.local/") };
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(http);
        services.AddSingleton(new PhamViChiNhanh(null, http));
        services.AddSingleton(new PhienDangNhap(null, http, new PhamViChiNhanh(null, http)));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<TrangThu>());
        try { await kiemTra(); }
        finally
        {
            TrangThu.Dung();
            api.DonDep.Cancel();
            // Dọn cả phiên bản đang lỗi để test không để lại timer/request nền.
            try { await TrangThu.Poll.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            typeof(HangDoiDon).GetField("_pollTienDo", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(TrangThu.Trang, null);
        }
    }

    // Chạy đúng vòng polling của trang thật trong dispatcher, không nối SignalR hay DB.
    public sealed class TrangThu : HangDoiDon
    {
        public static TrangThu Trang { get; private set; }
        public static Task Poll { get; private set; }
        private const BindingFlags Rieng = BindingFlags.Instance | BindingFlags.NonPublic;
        protected override Task OnInitializedAsync()
        {
            Trang = this;
            typeof(HangDoiDon).GetField("_donDangChon", Rieng)!.SetValue(this, new DonHangChiTietDto
            {
                MaDonHang = 55, TrangThaiDonHang = "DANG_IN", PhuongThucThanhToan = "COD"
            });
            Poll = (Task)typeof(HangDoiDon).GetMethod("PollTienDoAsync", Rieng)!.Invoke(this, null);
            return Task.CompletedTask;
        }
        protected override Task OnParametersSetAsync() => Task.CompletedTask;
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "Trang thử kết nối");
        public static void Dung() => ((CancellationTokenSource)typeof(HangDoiDon).GetField("_dungTienDo", Rieng)!.GetValue(Trang)).Cancel();
    }

    private sealed class ApiCham(bool choMay) : HttpMessageHandler
    {
        public CancellationTokenSource DonDep { get; } = new();
        public TaskCompletionSource DaGoiMay { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DaHuyMay { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DaPhucHoi { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int SoLanDocDon { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Get, request.Method); // Không có thao tác in/ghi dữ liệu.
            if (request.RequestUri!.AbsolutePath == "/api/may-in-ao/trang-thai")
            {
                if (choMay)
                {
                    using var dung = CancellationTokenSource.CreateLinkedTokenSource(token, DonDep.Token);
                    DaGoiMay.TrySetResult();
                    try { await Task.Delay(Timeout.Infinite, dung.Token); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        DaHuyMay.TrySetResult();
                        throw;
                    }
                }
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<TrangThaiKetNoiMayInDto>()) };
            }
            Assert.Equal("/api/don-hang/55", request.RequestUri.AbsolutePath);
            if (++SoLanDocDon == 1)
                throw new TaskCanceledException("API hết thời gian chờ", new TimeoutException());
            DaPhucHoi.TrySetResult();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new DonHangChiTietDto
            {
                MaDonHang = 55, TrangThaiDonHang = "DANG_IN", PhuongThucThanhToan = "COD"
            }) };
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) DonDep.Dispose();
            base.Dispose(disposing);
        }
    }
}
