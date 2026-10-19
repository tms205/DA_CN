using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using PhotocopyAdmin.Layout;
using TrangCa = PhotocopyAdmin.Pages.CaLamViec;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.Auth;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.DanhMuc;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class CaTuDongWebTests
{
    [Fact]
    public async Task CeoChonChiNhanh_KhongTuMoCa_KhiHienTrang()
    {
        await using var thu = await PhienThu.Tao("SUPER_ADMIN", 2);
        string html = await thu.Render();
        Assert.Empty(thu.DaMo);
        Assert.Contains("noi-dung-nghiep-vu", html);
        Assert.DoesNotContain("Không thể tự mở ca", html);
    }

    [Theory]
    [InlineData("SUPER_ADMIN", null)]
    [InlineData("BRANCH_ADMIN", 1)]
    [InlineData("STAFF", 1)]
    public async Task KhongTuMoBangLayout_KhiToanChuoiHoacVaiTroDaMoTaiApiLogin(string vaiTro, int? chiNhanh)
    {
        await using var thu = await PhienThu.Tao(vaiTro, chiNhanh);
        Assert.Contains("noi-dung-nghiep-vu", await thu.Render());
        Assert.Empty(thu.DaMo);
    }

    [Fact]
    public async Task CeoF5_DangNhapLaiVaDoiChiNhanh_KhongTuMoCa()
    {
        await using var thu = await PhienThu.Tao("SUPER_ADMIN", 1);
        await thu.Render();
        await thu.Render(); // component mới, cùng sessionStorage như F5.
        Assert.Empty(thu.DaMo);
        await thu.DangNhap();
        await thu.Render();
        Assert.Empty(thu.DaMo);
        await thu.PhamVi.ChonAsync(null);
        await thu.Render();
        Assert.Empty(thu.DaMo);
        await thu.PhamVi.ChonAsync(1);
        await thu.Render();
        Assert.Empty(thu.DaMo);
        await thu.PhamVi.ChonAsync(2);
        await thu.Render();
        Assert.Empty(thu.DaMo);
    }

    [Fact]
    public async Task CeoCaCuVanGiuNguyen_KhiChiXemChiNhanhKhac()
    {
        await using var thu = await PhienThu.Tao("SUPER_ADMIN", 2);
        thu.CaDangMo = new() { MaCa = 50, MaNhanVien = 10, MaChiNhanh = 1, TienDauCa = 350000, TrangThai = "DANG_MO" };
        string html = await thu.Render();
        Assert.Contains("noi-dung-nghiep-vu", html);
        Assert.Empty(thu.DaMo);
        Assert.Equal(1, thu.CaDangMo.MaChiNhanh);
        Assert.Equal(350000m, thu.CaDangMo.TienDauCa);
        Assert.DoesNotContain("Không thể tự mở ca", html);
    }

    [Fact]
    public async Task CeoTrangCa_ChuaMo_ThiHuongDanBamMoThuCong()
    {
        await using var thu = await PhienThu.Tao("SUPER_ADMIN", 2);
        string html = await thu.RenderCa(false);
        Assert.Contains("Mở ca làm việc", html);
        Assert.Contains("thủ công", html);
        Assert.DoesNotContain("CEO tự mở", html);
        Assert.Empty(thu.DaMo);
    }

    [Fact]
    public async Task CeoBamMoCa_ThiGui200000TheoChiNhanhDangChon()
    {
        await using var thu = await PhienThu.Tao("SUPER_ADMIN", 2);
        await thu.RenderCa(true);
        Assert.Equal((2, 200000m), Assert.Single(thu.DaMo));
    }

    private sealed class BamMoCa : TrangCa
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            const BindingFlags rieng = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(TrangCa).GetMethod("MoHopCa", rieng).Invoke(this, null);
            await (Task)typeof(TrangCa).GetMethod("MoCaAsync", rieng).Invoke(this, null);
        }
    }

    private sealed class PhienThu : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly HttpClient _http;
        private readonly PhienDangNhap _phien;
        private readonly string _vaiTro;
        public PhamViChiNhanh PhamVi { get; }
        public List<(int ChiNhanh, decimal Tien)> DaMo { get; } = new();
        public CaLamViecDto CaDangMo { get; set; }

        private PhienThu(string vaiTro, int? chiNhanh)
        {
            _vaiTro = vaiTro;
            var js = new BoNho();
            if (chiNhanh != null) js.DuLieu["localStorage:photocopychain.pham-vi-chi-nhanh"] = chiNhanh.ToString();
            _http = new HttpClient(new ApiCa(this)) { BaseAddress = new Uri("http://test.local/") };
            PhamVi = new PhamViChiNhanh(js, _http);
            _phien = new PhienDangNhap(js, _http, PhamVi);
            var services = new ServiceCollection();
            services.AddLogging(); services.AddSingleton<IJSRuntime>(js);
            services.AddSingleton(_http); services.AddSingleton(PhamVi); services.AddSingleton(_phien);
            services.AddSingleton<ThongBaoPheDuyet>();
            services.AddSingleton<NavigationManager>(new DieuHuong());
            _services = services.BuildServiceProvider();
        }
        public static async Task<PhienThu> Tao(string vaiTro, int? chiNhanh)
        {
            var thu = new PhienThu(vaiTro, chiNhanh);
            await thu.DangNhap();
            return thu;
        }
        public Task DangNhap() => _phien.DangNhap(new DangNhapResponse {
            AccessToken = "phien-kiem-thu", HetHanLuc = DateTime.UtcNow.AddHours(1),
            NguoiDung = new NguoiDungDto { MaNguoiDung = 10, HoTen = "Người thử", TenVaiTro = _vaiTro, MaChiNhanh = _vaiTro == "SUPER_ADMIN" ? null : 1 }
        }, false);
        public async Task<string> Render()
        {
            await using var renderer = new HtmlRenderer(_services, _services.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async () => {
                RenderFragment layout = b => {
                    b.OpenComponent<MainLayout>(0);
                    b.AddAttribute(1, "Body", (RenderFragment)(body => body.AddContent(0, "noi-dung-nghiep-vu")));
                    b.CloseComponent();
                };
                var p = ParameterView.FromDictionary(new Dictionary<string, object> {
                    ["Value"] = _phien.GetAuthenticationStateAsync(), ["ChildContent"] = layout
                });
                var html = await renderer.RenderComponentAsync<CascadingValue<Task<AuthenticationState>>>(p);
                return WebUtility.HtmlDecode(html.ToHtmlString());
            });
        }
        public async Task<string> RenderCa(bool bamMo)
        {
            await using var renderer = new HtmlRenderer(_services, _services.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode(
                (await renderer.RenderComponentAsync(bamMo ? typeof(BamMoCa) : typeof(TrangCa), ParameterView.Empty)).ToHtmlString()));
        }
        public async ValueTask DisposeAsync() { _http.Dispose(); await _services.DisposeAsync(); }
    }

    private sealed class ApiCa(PhienThu thu) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Method == HttpMethod.Get)
            {
                if (request.RequestUri.AbsolutePath == "/api/ca/dang-mo")
                    return thu.CaDangMo == null ? new(HttpStatusCode.NoContent) : new(HttpStatusCode.OK) { Content = JsonContent.Create(thu.CaDangMo) };
                object ds = request.RequestUri.AbsolutePath switch {
                    "/api/danh-muc/chi-nhanh" => new[] { new ChiNhanhDto { MaChiNhanh = 1 }, new ChiNhanhDto { MaChiNhanh = 2 } },
                    "/api/quan-tri/bao-cao/hien-tai" => new TongQuanHienTaiDto(),
                    "/api/ca" or "/api/ca/cho-xac-nhan" => Array.Empty<CaLamViecDto>(),
                    _ => throw new InvalidOperationException("GET ngoài dự kiến: " + request.RequestUri.AbsolutePath)
                };
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(ds) };
            }
            Assert.Equal("POST", request.Method.Method);
            Assert.Equal("/api/ca/mo", request.RequestUri.AbsolutePath);
            Assert.Equal("phien-kiem-thu", request.Headers.Authorization?.Parameter);
            int cn = int.Parse(request.Headers.GetValues(TenHeader.MaChiNhanh).Single());
            var data = await request.Content.ReadFromJsonAsync<MoCaRequest>(cancellationToken: token);
            thu.DaMo.Add((cn, data.TienDauCa));
            thu.CaDangMo = new() { MaCa = 50, MaNhanVien = 10, MaChiNhanh = cn, TienDauCa = data.TienDauCa, TrangThai = "DANG_MO" };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(thu.CaDangMo) };
        }
    }
    private sealed class BoNho : IJSRuntime
    {
        public Dictionary<string, string> DuLieu { get; } = new();
        public ValueTask<T> InvokeAsync<T>(string id, object[] args) => InvokeAsync<T>(id, CancellationToken.None, args);
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken token, object[] args)
        {
            string[] phan = id.Split('.');
            string key = phan[0] + ":" + args[0];
            if (phan[1] == "getItem") return ValueTask.FromResult(DuLieu.TryGetValue(key, out string value) ? (T)(object)value : default);
            if (phan[1] == "setItem") DuLieu[key] = args[1]?.ToString();
            if (phan[1] == "removeItem") DuLieu.Remove(key);
            return ValueTask.FromResult(default(T));
        }
    }
    private sealed class DieuHuong : NavigationManager
    {
        public DieuHuong() => Initialize("http://test.local/", "http://test.local/ca-lam-viec");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("Đọc ca không được tự điều hướng.");
    }
}
