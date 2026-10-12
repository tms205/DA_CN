using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.Auth;
using PhotocopyShared.Dtos.DatIn;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class LapDonTaiQuayWebTests
{
    private const BindingFlags Rieng = BindingFlags.NonPublic | BindingFlags.Instance;

    private static void Gan(LapDonTaiQuay trang, string ten, object giaTri)
        => trang.GetType().GetField(ten, Rieng).SetValue(trang, giaTri);

    private static T Doc<T>(LapDonTaiQuay trang, string ten)
        => (T)trang.GetType().GetField(ten, Rieng).GetValue(trang);

    private static Task Goi(LapDonTaiQuay trang, string ten)
    {
        var ham = trang.GetType().GetMethod(ten, Rieng);
        Assert.NotNull(ham);
        return (Task)ham.Invoke(trang, null);
    }

    private static LapDonTaiQuay TaoTrang(HttpClient http)
    {
        var trang = new LapDonTaiQuay();
        var phamVi = new PhamViChiNhanh(null, http);
        var phien = new PhienDangNhap(null, http, phamVi);
        trang.GetType().GetProperty("Http", Rieng).SetValue(trang, http);
        trang.GetType().GetProperty("PhamVi", Rieng).SetValue(trang, phamVi);
        trang.GetType().GetProperty("Phien", Rieng).SetValue(trang, phien);
        Gan(trang, "_khach", new KhachTaiQuayDto { MaKhachHang = 1, HoTen = "Khách thử" });
        Gan(trang, "_dsFile", new List<FileInDto> { new() { MaFileIn = 1, SoTrang = 8, TenFileGoc = "test.pdf" } });
        Gan(trang, "_maDichVu", 1);
        return trang;
    }

    [Fact]
    public async Task DoiThuongHoaToc_GoiLaiGiaVaGioTuApi()
    {
        var dsInGap = new List<bool>();
        using var http = new HttpClient(new ApiGia(async req => {
            if (req.RequestUri.AbsolutePath == "/api/auth/me") return Tra(new NguoiDungDto { MaChiNhanh = 1 });
            var gia = await req.Content.ReadFromJsonAsync<BaoGiaRequest>();
            dsInGap.Add(gia.InGap);
            return Tra(new BaoGiaResponse {
                TongTien = gia.InGap ? 17400 : 7400,
                NgayHenTra = new DateTime(2026, 10, 12, gia.InGap ? 14 : 15, 30, 0),
            });
        })) { BaseAddress = new Uri("http://test.local/") };
        var trang = TaoTrang(http);
        await Goi(trang, "BaoGiaAsync");
        Assert.Equal(15, Doc<BaoGiaResponse>(trang, "_baoGia").NgayHenTra.Value.Hour);
        Gan(trang, "_inGap", true);
        await Goi(trang, "DoiKieuInAsync");
        Assert.Equal(14, Doc<BaoGiaResponse>(trang, "_baoGia").NgayHenTra.Value.Hour);
        Assert.Equal(17400, Doc<BaoGiaResponse>(trang, "_baoGia").TongTien);
        Gan(trang, "_inGap", false);
        await Goi(trang, "DoiKieuInAsync");
        Assert.Equal(15, Doc<BaoGiaResponse>(trang, "_baoGia").NgayHenTra.Value.Hour);
        Assert.Equal(new[] { false, true, false }, dsInGap);
    }

    [Fact]
    public async Task BaoGiaThatBai_KhongGiuGiaVaGioCu()
    {
        using var http = new HttpClient(new ApiGia(req => Task.FromResult(
            req.RequestUri.AbsolutePath == "/api/auth/me" ? Tra(new NguoiDungDto { MaChiNhanh = 1 }) :
            Tra(new { message = "Không thể báo giá." }, HttpStatusCode.BadRequest))))
            { BaseAddress = new Uri("http://test.local/") };
        var trang = TaoTrang(http);
        Gan(trang, "_baoGia", new BaoGiaResponse { NgayHenTra = DateTime.Now, TongTien = 7400 });
        await Goi(trang, "BaoGiaAsync");
        Assert.Null(Doc<BaoGiaResponse>(trang, "_baoGia"));
        Assert.Equal("Không thể báo giá.", Doc<string>(trang, "_loi"));
    }

    [Fact]
    public async Task DoiQuyCachTrongLucCho_KhongNhanLaiBaoGiaCu()
    {
        var cho = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var daNhan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new ApiGia(req => {
            if (req.RequestUri.AbsolutePath == "/api/auth/me") return Task.FromResult(Tra(new NguoiDungDto { MaChiNhanh = 1 }));
            daNhan.SetResult();
            return cho.Task;
        })) { BaseAddress = new Uri("http://test.local/") };
        var trang = TaoTrang(http);
        var dangTinh = Goi(trang, "BaoGiaAsync");
        await daNhan.Task;
        trang.GetType().GetMethod("XoaBaoGia", Rieng).Invoke(trang, null);
        cho.SetResult(Tra(new BaoGiaResponse { NgayHenTra = DateTime.Now, TongTien = 7400 }));
        await dangTinh;
        Assert.Null(Doc<BaoGiaResponse>(trang, "_baoGia"));
        Assert.False(Doc<bool>(trang, "_dangGui"));
    }

    [Fact]
    public async Task ThieuTenVaSdt_WebChanTruocKhiGoiApi()
    {
        int soLan = 0;
        using var http = new HttpClient(new ApiGia(req => {
            soLan++;
            return Task.FromResult(Tra(new KhachTaiQuayDto()));
        })) { BaseAddress = new Uri("http://test.local/") };
        var trang = TaoTrang(http);
        await Goi(trang, "ChonKhachAsync");
        Assert.Equal(0, soLan);
        Assert.Equal("Vui lòng nhập họ tên hoặc số điện thoại khách hàng.", Doc<string>(trang, "_loi"));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    public async Task Form_WebKhongBatBuocTungO_VaHienGioNhanCuThe(bool inGap, bool coGio, bool dangGui)
    {
        using var http = new HttpClient(new ApiGia(req => Task.FromResult(Tra(Array.Empty<object>()))))
            { BaseAddress = new Uri("http://test.local/") };
        var trang = TaoTrang(http);
        Gan(trang, "_inGap", inGap);
        Gan(trang, "_dangGui", dangGui);
        Gan(trang, "_baoGia", new BaoGiaResponse { NgayHenTra = coGio ? new DateTime(2026, 10, 12, 15, 30, 0) : null });
        var phamVi = new PhamViChiNhanh(null, http);
        typeof(PhamViChiNhanh).GetField("<MaChiNhanh>k__BackingField", Rieng).SetValue(phamVi, 1);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime>(new JsKhongDung());
        services.AddSingleton(http);
        services.AddSingleton(phamVi);
        services.AddSingleton(new PhienDangNhap(null, http, phamVi));
        services.AddSingleton<IComponentActivator>(new TaoComponent(trang));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => {
            var ketQua = await renderer.RenderComponentAsync<LapDonTaiQuay>();
            return WebUtility.HtmlDecode(ketQua.ToHtmlString());
        });
        if (coGio)
        {
            Assert.Contains("15:30 12/10/2026", html);
            Assert.Contains("Giờ nhận dự kiến", html);
        }
        else
        {
            Assert.Contains("Chưa ước tính được giờ nhận", html);
            Assert.DoesNotContain("15:30", html);
        }
        Assert.Contains(inGap ? "<option value=\"HOA_TOC\" selected>" : "<option value=\"THUONG\" selected>", html);
        Assert.Contains("In thường", html);
        int dauQuyCach = html.IndexOf("class=\"ca-loc-luoi\"", StringComparison.Ordinal);
        int dauThanhToan = html.IndexOf("class=\"ldq-thanh-toan-khuyen-mai\"", StringComparison.Ordinal);
        Assert.True(dauThanhToan > dauQuyCach, "Thanh toán và mã khuyến mãi phải nằm ở hàng riêng sau quy cách in.");
        string quyCach = html[dauQuyCach..dauThanhToan];
        Assert.DoesNotContain("Mã khuyến mãi", quyCach);
        Assert.DoesNotContain("Thanh toán", quyCach);
        int dauGioNhan = html.IndexOf("class=\"o-canh-bao\"", dauThanhToan, StringComparison.Ordinal);
        string thanhToan = html[dauThanhToan..dauGioNhan];
        Assert.Contains("for=\"ldq-thanh-toan\"", thanhToan);
        Assert.Contains("id=\"ldq-thanh-toan\"", thanhToan);
        Assert.Contains("Quét QR payOS tại quầy", thanhToan);
        Assert.Contains("COD · trả khi nhận tài liệu", thanhToan);
        Assert.Contains("for=\"ldq-ma-khuyen-mai\"", thanhToan);
        Assert.Contains("id=\"ldq-ma-khuyen-mai\"", thanhToan);
        var oTen = System.Text.RegularExpressions.Regex.Match(html, "<input[^>]*placeholder=\"Nguyễn Văn An\"[^>]*>").Value;
        var oSdt = System.Text.RegularExpressions.Regex.Match(html, "<input[^>]*placeholder=\"09xxxxxxxx\"[^>]*>").Value;
        Assert.NotEmpty(oTen);
        Assert.NotEmpty(oSdt);
        Assert.DoesNotContain("required", oTen);
        Assert.DoesNotContain("required", oSdt);
        if (dangGui)
        {
            string oFile = System.Text.RegularExpressions.Regex.Match(html, "<input[^>]*type=\"file\"[^>]*>").Value;
            Assert.Contains("disabled", oFile);
        }
    }

    [Theory]
    [InlineData("Nguyễn Văn An", "")]
    [InlineData("", "0912345678")]
    public async Task Web_ChiMotThongTin_VanGuiDuocYeuCauChonKhach(string ten, string sdt)
    {
        KhachTaiQuayRequest reqNhanDuoc = null;
        using var http = new HttpClient(new ApiGia(async req => {
            Assert.Equal("/api/don-hang/khach-tai-quay", req.RequestUri.AbsolutePath);
            reqNhanDuoc = await req.Content.ReadFromJsonAsync<KhachTaiQuayRequest>();
            return Tra(new KhachTaiQuayDto { MaKhachHang = 12, HoTen = ten, SoDienThoai = sdt });
        })) { BaseAddress = new Uri("http://test.local/") };
        var trang = TaoTrang(http);
        Gan(trang, "_khach", null);
        Gan(trang, "_dsFile", new List<FileInDto>());
        Gan(trang, "_hoTen", ten);
        Gan(trang, "_soDienThoai", sdt);
        await Goi(trang, "ChonKhachAsync");
        Assert.NotNull(reqNhanDuoc);
        Assert.Equal(ten, reqNhanDuoc.HoTen);
        Assert.Equal(sdt, reqNhanDuoc.SoDienThoai);
        Assert.Equal(12, Doc<KhachTaiQuayDto>(trang, "_khach").MaKhachHang);
    }

    private static HttpResponseMessage Tra<T>(T body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = JsonContent.Create(body) };

    private sealed class ApiGia(Func<HttpRequestMessage, Task<HttpResponseMessage>> xuLy) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token) => xuLy(req);
    }

    private sealed class TaoComponent(LapDonTaiQuay trang) : IComponentActivator
    {
        public IComponent CreateInstance(Type type) => type == typeof(LapDonTaiQuay)
            ? trang : (IComponent)Activator.CreateInstance(type);
    }

    private sealed class JsKhongDung : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object[] args) => throw new NotSupportedException();
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object[] args) => throw new NotSupportedException();
    }
}
