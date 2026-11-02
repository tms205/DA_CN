using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin;
using PhotocopyAdmin.Components;
using PhotocopyApi.BaoTri;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class BaoTriTrangThaiGiaoDienTests
{
    [Fact]
    public void PhamViDungNenTang_ChanChonTinhNangKhongCoTrenApp_LocCauHinhCu()
    {
        Assert.Contains("QUAN_LY",DanhMucBaoTri.Web.Keys); Assert.Contains("BAO_CAO",DanhMucBaoTri.Web.Keys);
        Assert.DoesNotContain("QUAN_LY",DanhMucBaoTri.App.Keys); Assert.DoesNotContain("BAO_CAO",DanhMucBaoTri.App.Keys);
        var legacy = new TrangThaiBaoTri(true,"Cũ",KhoaWeb:false,KhoaApp:false,KhoaTinhNangApp:new[]{"QUAN_LY","BAO_CAO","OCR"});
        Assert.Equal(new[]{"OCR"},BaoTriStore.ChucNang(legacy,"app"));
        string dir=Path.Combine(Path.GetTempPath(),"BaoTriPlatform-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new BaoTriStore(Path.Combine(dir,"state.json"));
            foreach (string key in new[]{"QUAN_LY","BAO_CAO"})
                Assert.Throws<ArgumentException>(()=>store.Set(true,"Nâng cấp",new(false,false,Array.Empty<string>(),new[]{key},1)));
            Assert.False(store.Read().DangBaoTri);
        }
        finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }

    [Fact]
    public async Task TongQuanBaoTri_HienLoiNhan_KhongBaoMatKetNoiVaKhongGiaDoanhThuKhong()
    {
        string html=await Render<PhotocopyAdmin.Pages.TongQuan>(new Fixture(_=>BaoTri()));
        Assert.Contains("Tạm dừng để bảo trì",html); Assert.Contains("Nâng cấp dữ liệu kế toán",html);
        Assert.Contains("18:00",html); Assert.DoesNotContain("Không tải được tổng quan",html);
        Assert.DoesNotContain("Doanh thu kỳ đã chọn",html); Assert.DoesNotContain("class=\"o-loi\"",html);
    }

    [Fact]
    public async Task VanHanh_ChiNguonBaoTriBiTamDung_CacNguonKhacVanXemDuoc()
    {
        string html=await Render<TongQuanVanHanh>(new Fixture(path=>path.EndsWith("/hien-tai")?BaoTri():Ok(Array.Empty<object>())));
        Assert.Contains("Chỉ số ca / phê duyệt tạm dừng",html); Assert.Contains("Nâng cấp dữ liệu kế toán",html);
        Assert.DoesNotContain("Không tải được chỉ số",html); Assert.DoesNotContain("Ca đang mở",html);
        Assert.Contains("Không có vật tư dưới mức tối thiểu",html); Assert.Contains("Không có đơn cần ưu tiên",html);
    }

    [Theory]
    [InlineData(401)] [InlineData(500)] [InlineData(503)]
    public async Task LoiThat_KhongBiGiaLamBaoTri(int status)
    {
        using var http=new HttpClient(new Fixture(_=>new HttpResponseMessage((HttpStatusCode)status){Content=JsonContent.Create(new{message="Lỗi API thật"})})) {BaseAddress=new Uri("http://fixture.invalid/")};
        await Assert.ThrowsAsync<HttpRequestException>(()=>http.DocJsonAsync<object>("api/test"));
    }

    [Fact]
    public async Task BadgePheDuyet_BaoTriKhongHienSoKhong_HetBaoTriThiCapNhatLai()
    {
        bool maintenance=true;
        using var http=new HttpClient(new Fixture(_=>maintenance?BaoTri():Ok(new TongQuanHienTaiDto{SoPhieuChoDuyet=2,SoHoanTienChoDuyet=1}))) {BaseAddress=new Uri("http://fixture.invalid/")};
        var badge=new ThongBaoPheDuyet(http);
        await badge.TaiAsync(); Assert.True(badge.DangBaoTri); Assert.Null(badge.SoCho);
        maintenance=false; await badge.TaiAsync(); Assert.False(badge.DangBaoTri); Assert.Equal(3,badge.SoCho);
    }

    [Fact]
    public async Task CauHinhWebApp_HienDungDanhMuc()
    {
        string html=await Render<PhotocopyAdmin.Pages.BaoTri>(new Fixture(_=>Ok(new{
            trangThai=new {dangBaoTri=false,khoaWeb=false,khoaApp=false,khoaTinhNangWeb=Array.Empty<string>(),khoaTinhNangApp=new[]{"QUAN_LY","BAO_CAO"}},gioHeThong=DateTimeOffset.UtcNow})));
        Assert.Contains("App điện thoại</legend>",html);
        string app=html.Split("App điện thoại</legend>")[1].Split("</fieldset>")[0];
        Assert.DoesNotContain("Quản lý danh mục",app); Assert.DoesNotContain("báo cáo",app);
        Assert.Contains("Giao hàng / đối chiếu",app);
        Assert.Contains("Quản lý danh mục / nhân sự",html); Assert.Contains("Dashboard / báo cáo",html);
    }

    [Theory]
    [InlineData("giao-hang","GIAO_HANG")]
    [InlineData("bao-cao?tuNgay=2026-11-02","BAO_CAO")]
    [InlineData("bao-tri","")]
    [InlineData("tong-quan","")] // Dashboard vẫn cần hiển thị các nguồn ngoài phạm vi bảo trì.
    public void PhanLoaiTrangDeChanDungTinhNang(string route,string expected)=>Assert.Equal(expected,DocDuLieuApi.ChucNangTrang(route));

    private static HttpResponseMessage Ok(object data)=>new(HttpStatusCode.OK){Content=JsonContent.Create(data)};
    private static HttpResponseMessage BaoTri()=>new(HttpStatusCode.ServiceUnavailable){Content=JsonContent.Create(new{
        dangBaoTri=false,loiNhan="Nâng cấp dữ liệu kế toán\nDự kiến mở lại lúc 18:00",message="Chức năng đang bảo trì.\nNâng cấp dữ liệu kế toán\nDự kiến mở lại lúc 18:00"})};
    private static async Task<string> Render<T>(HttpMessageHandler fixture) where T:IComponent
    {
        var services=new ServiceCollection();services.AddLogging();
        using var http=new HttpClient(fixture){BaseAddress=new Uri("http://fixture.invalid/")};services.AddSingleton(http);
        await using var provider=services.BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        return WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async()=>
            (await renderer.RenderComponentAsync<T>()).ToHtmlString()));
    }
    private sealed class Fixture(Func<string,HttpResponseMessage> response):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(response(request.RequestUri.AbsolutePath));
    }
}
