using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public sealed class ChuyenMayWebTests
{
    [Fact]
    public async Task PopupDaDung_PhaiDoiChieuGiay_KhongTuChonInLaiToanBo()
    {
        var type=typeof(HangDoiDon).Assembly.GetType("PhotocopyAdmin.Components.ChuyenMayIn");
        Assert.NotNull(type);
        using var http=new HttpClient(new ApiGia()) {BaseAddress=new Uri("http://fixture.local/")};
        var services=new ServiceCollection().AddLogging();
        services.AddSingleton(http); services.AddSingleton<IJSRuntime>(new JsGia());
        await using var provider=services.BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        string html=await renderer.Dispatcher.InvokeAsync(async()=> {
            var view=await renderer.RenderComponentAsync(type,ParameterView.FromDictionary(new Dictionary<string,object>{["Mo"]=true,["MaDonHang"]=57}));
            return System.Net.WebUtility.HtmlDecode(view.ToHtmlString());
        });
        Assert.Contains("máy cũ",html,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DH00057",html);
        Assert.DoesNotContain("DH@",html);
        Assert.Contains("bản giấy",html,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chưa xác định",html);
        Assert.Contains("Chuyển phần còn lại",html);
        Assert.Contains("disabled",html);
        Assert.DoesNotContain("token-private",html);
    }
    [Fact]
    public async Task PopupChoDung_HienLoiQueueTuConnector_KhongGoiLaDaDung()
    {
        var type=typeof(HangDoiDon).Assembly.GetType("PhotocopyAdmin.Components.ChuyenMayIn");
        using var http=new HttpClient(new ApiGia(true)){BaseAddress=new Uri("http://fixture.local/")};
        var services=new ServiceCollection().AddLogging();
        services.AddSingleton(http); services.AddSingleton<IJSRuntime>(new JsGia());
        await using var provider=services.BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        string html=await renderer.Dispatcher.InvokeAsync(async()=>{
            var view=await renderer.RenderComponentAsync(type,ParameterView.FromDictionary(new Dictionary<string,object>{["Mo"]=true,["MaDonHang"]=57}));
            return System.Net.WebUtility.HtmlDecode(view.ToHtmlString());
        });
        Assert.Contains("Windows không truy cập được queue HP.",html);
        Assert.Contains("Đang chờ connector dừng lệnh cũ",html);
        Assert.DoesNotContain("Connector đã dừng lệnh cũ",html);
    }

    private sealed class JsGia:IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string id,object[] args)=>ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string id,CancellationToken c,object[] args)=>ValueTask.FromResult(default(T));
    }
    private sealed class ApiGia(bool choDung=false):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken t)
        {
            Assert.Equal(HttpMethod.Get,req.Method);
            object result=req.RequestUri.AbsolutePath.EndsWith("/chuyen-may") ? new TrangThaiChuyenMayDto {
                MaDonHang=57,MaMayHienTai=1,TrangThaiDon="DANG_IN",PhanDangIn=new(){new(){MaChiTiet=1,MaFile=2,TenFile="fixture.pdf",SoTrang=5,SoBan=1}},
                LichSu=new(){new(){MaYeuCau=new string('a',32),MaMayCu=1,TrangThai=choDung?"DANG_DUNG":"DA_DUNG",LyDo="Hết giấy"}}
            } : req.RequestUri.AbsolutePath.EndsWith("/trang-thai") && choDung
                ? new List<TrangThaiKetNoiMayInDto>{new(){MaMayIn=1,TrangThai="LOI",MaDonHangDangIn=57,Loi="Windows không truy cập được queue HP."}}
                : Array.Empty<object>();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(result,result.GetType())});
        }
    }
}
