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
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuanTriKhoPopupTests
{
    [Fact]
    public async Task ChiNhanhMayIn_KhongConOChiSoHayFormSuaInline()
    {
        string html = await Render<QuanTri>();
        Assert.Contains("Thêm chi nhánh", html);
        Assert.Contains("Sửa chỉ số", html);
        Assert.DoesNotContain("<form", html);
    }

    [Fact]
    public async Task KhoVatTu_ChuaBamThem_KhongHienFormNhap()
    {
        string html = await Render<KhoVatTu>();
        Assert.Contains("Lập phiếu nhập", html);
        Assert.Contains("Thêm vật tư mới", html);
        Assert.Contains("Thao tác", html);
        Assert.Contains("Sửa thông tin", html);
        Assert.Contains("Sửa mức cảnh báo", html);
        Assert.DoesNotContain("<form", html);
        Assert.DoesNotContain("Tên đơn vị cung ứng", html);
    }

    [Fact]
    public async Task QuanLyChiNhanh_ChiSuaMucCanhBao_KhongSuaDanhMucChung()
    {
        string html = await Render<KhoVatTu>("BRANCH_ADMIN");
        Assert.Contains("Sửa mức cảnh báo", html);
        Assert.DoesNotContain("Sửa thông tin", html);
        Assert.DoesNotContain("Thêm vật tư mới", html);
    }

    [Fact]
    public async Task SuaVatTu_PopupDienThongTin_KhoaDonViDaSuDung()
    {
        string html = await Render<MoSuaVatTu>();
        Assert.Contains("Sửa thông tin vật tư", html);
        Assert.Contains("value=\"Giấy thử\"", html);
        Assert.Contains("toàn chuỗi", html);
        Assert.Contains("đơn vị", html);
        Assert.Matches("id=\"vt-don-vi\"[^>]*(disabled|readonly)", html);
    }

    [Fact]
    public async Task SuaMucCanhBao_PopupDungChiNhanh_KhongCoOSuaSoLuongTon()
    {
        string html = await Render<MoCanhBao>();
        Assert.Contains("id=\"kho-form-canh-bao\"", html);
        Assert.Contains("form=\"kho-form-canh-bao\"", html);
        Assert.Contains("Chi nhánh thử", html);
        Assert.Contains("Giấy thử", html);
        Assert.Contains("value=\"5\"", html);
        Assert.DoesNotContain("@bind", html);
        Assert.DoesNotContain("Số lượng tồn mới", html);
    }

    private sealed class MoSuaVatTu : KhoVatTu
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            MoThaoTacKho(this, "MoSuaVatTu");
        }
    }

    private sealed class MoCanhBao : KhoVatTu
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            MoThaoTacKho(this, "MoSuaCanhBao");
        }
    }

    private static void MoThaoTacKho(KhoVatTu trang, string tenHam)
    {
        const BindingFlags rieng = BindingFlags.NonPublic | BindingFlags.Instance;
        var ham = typeof(KhoVatTu).GetMethod(tenHam, rieng);
        Assert.NotNull(ham);
        var ds = (List<TonKhoDto>)typeof(KhoVatTu).GetField("_tonKho", rieng).GetValue(trang);
        ham.Invoke(trang, new object[] { ds[0] });
    }

    [Fact]
    public async Task ThemVatTu_HienPopupRieng_KhongTaoTonHoacGiaBanTuDong()
    {
        string html = await Render<MoVatTu>();
        Assert.Contains("<dialog", html);
        Assert.Contains("id=\"kho-form-vat-tu\"", html);
        Assert.Contains("form=\"kho-form-vat-tu\"", html);
        Assert.Contains("Tên vật tư", html);
        Assert.Contains("Đơn vị tính", html);
        Assert.Contains("Giá nhập tham khảo", html);
        Assert.Contains("tồn 0", html);
        Assert.DoesNotContain("id=\"kho-form-phieu\"", html);
    }

    private sealed class MoVatTu : KhoVatTu
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            var mo = typeof(KhoVatTu).GetMethod("MoThemVatTu", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mo);
            mo.Invoke(this, null);
        }
    }

    [Fact]
    public async Task ThemChiNhanh_HienFormTrongDialogCoNutLuuGanVoiForm()
    {
        string html = await Render<MoChiNhanh>();
        Assert.Contains("<dialog", html);
        Assert.Contains("id=\"qt-form-chi-nhanh\"", html);
        Assert.Contains("form=\"qt-form-chi-nhanh\"", html);
    }

    [Fact]
    public async Task ThemPhieuNhap_HienFormTrongDialogVaKhongNhapKhoTruocKhiNhan()
    {
        string html = await Render<MoPhieuNhap>();
        Assert.Contains("<dialog", html);
        Assert.Contains("id=\"kho-form-phieu\"", html);
        Assert.Contains("form=\"kho-form-phieu\"", html);
        Assert.Contains("nhận hàng", html);
        Assert.Contains("Số lượng nhập (Tờ)", html);
        Assert.Contains("Đơn giá nhập (₫/Tờ)", html);
    }

    private sealed class MoChiNhanh : QuanTri
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            typeof(QuanTri).GetMethod("ThemChiNhanh", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(this, null);
        }
    }

    private sealed class MoPhieuNhap : KhoVatTu
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            var mo = typeof(KhoVatTu).GetMethod("MoTaoPhieu", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mo);
            mo.Invoke(this, null);
        }
    }

    private static async Task<string> Render<T>(string vaiTro = "SUPER_ADMIN") where T : IComponent
    {
        using var http = new HttpClient(new ApiDanhMuc()) { BaseAddress = new Uri("http://test.local/") };
        var js = new JsRong();
        var phamVi = new PhamViChiNhanh(js, http);
        await phamVi.ChonAsync(1);
        var phien = new PhienDangNhap(js, http, phamVi);
        await phien.DangNhap(new DangNhapResponse { AccessToken = "kiem-thu-ui", HetHanLuc = DateTime.UtcNow.AddHours(1),
            NguoiDung = new NguoiDungDto { MaNguoiDung = 1, HoTen = "Người kiểm thử", TenVaiTro = vaiTro, MaChiNhanh = vaiTro == "SUPER_ADMIN" ? null : 1 } }, false);
        await phamVi.ChonAsync(1);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(http);
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton(phamVi);
        services.AddSingleton(phien);
        services.AddSingleton<NavigationManager>(new DieuHuong());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode((await renderer.RenderComponentAsync<T>()).ToHtmlString()));
    }

    private sealed class ApiDanhMuc : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method); // Mở và xem form không được ghi dữ liệu.
            object data = request.RequestUri!.AbsolutePath switch {
                "/api/quan-tri/chi-nhanh" => new[] { new ChiNhanhQuanTriDto { MaChiNhanh = 1, TenChiNhanh = "Chi nhánh thử", TrangThai = "HOAT_DONG" } },
                "/api/quan-tri/may-in" => new[] { new MayInQuanTriDto { MaMayIn = 1, TenMay = "Máy thử", TrangThai = "HOAT_DONG", SoTrangDaIn = 100 } },
                "/api/quan-tri/kho" => new[] { new TonKhoDto { MaVatTu = 1, TenVatTu = "Giấy thử", DonViTinh = "Tờ", MaChiNhanh = 1, TenChiNhanh = "Chi nhánh thử", SoLuong = 20, MucToiThieu = 5, DonGia = 120 } },
                "/api/quan-tri/phieu-nhap" => Array.Empty<PhieuNhapDto>(),
                _ => throw new InvalidOperationException("API không dự kiến: " + request.RequestUri.AbsolutePath)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data) });
        }
    }

    private sealed class JsRong : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args) => ValueTask.FromResult(default(TValue));
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object[] args) => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class DieuHuong : NavigationManager
    {
        public DieuHuong() => Initialize("http://test.local/", "http://test.local/quan-tri");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("Mở form không được điều hướng.");
    }
}
