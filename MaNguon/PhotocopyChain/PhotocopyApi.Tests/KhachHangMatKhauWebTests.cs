using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
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

public class KhachHangMatKhauWebTests
{
    [Fact]
    public async Task DanhSachKhachHang_CoNutSua_NhungChuaMoForm()
    {
        var html = await Render<KhachHang>();
        Assert.Matches(">Sửa</button>", html);
        Assert.DoesNotContain("id=\"kh-form-sua\"", html);
    }

    [Fact]
    public async Task SuaKhachHang_PopupDienSan_GiuEmailChiDoc()
    {
        var html = await Render<MoSuaKhach>();
        Assert.Contains("Sửa khách hàng", html);
        Assert.Contains("id=\"kh-form-sua\"", html);
        Assert.Contains("Khách thử", html);
        Assert.Contains("value=\"0901234567\"", html);
        Assert.Matches("<input[^>]*id=\"kh-email\"[^>]*readonly", html);
        Assert.Contains("form=\"kh-form-sua\"", html);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("mk-cu", true)]
    [InlineData("mk-moi", true)]
    [InlineData("mk-lai", true)]
    [InlineData("mk-cu", false)]
    [InlineData("mk-moi", false)]
    [InlineData("mk-lai", false)]
    public async Task MatKhau_ConMatDocLap_VaQuyTacNamSauNhapLai(string hien, bool moLai)
    {
        string html = await Render<MoDoiMatKhau>(new() { ["Hien"] = hien, ["MoLai"] = moLai });
        foreach (string id in new[] { "mk-cu", "mk-moi", "mk-lai" })
        {
            string input = Regex.Match(html, "<input[^>]*id=\"" + id + "\"[^>]*>").Value;
            Assert.Contains("type=\"" + (id == hien && !moLai ? "text" : "password") + "\"", input);
            Assert.Contains("aria-controls=\"" + id + "\"", html);
        }
        Assert.Equal(3, Regex.Matches(html, "class=\"o-nut-mat\"").Count);
        Assert.True(html.IndexOf("id=\"mk-lai\"", StringComparison.Ordinal) < html.IndexOf("class=\"ds-quy-tac\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LuuKhachHang_GuiDungBanGhi_KhongGuiEmailQuyenHayMatKhau()
    {
        var api = new ApiHoSo();
        string html = await Render<LuuKhach>(api: api);
        Assert.Single(api.Writes);
        Assert.Equal("/api/quan-tri/khach-hang/5", api.Writes[0].Path);
        using var json = JsonDocument.Parse(api.Writes[0].Body);
        Assert.Equal("Tên cập nhật", json.RootElement.GetProperty("hoTen").GetString());
        Assert.Equal("0901234567", json.RootElement.GetProperty("soDienThoai").GetString());
        Assert.False(json.RootElement.TryGetProperty("email", out _));
        Assert.False(json.RootElement.TryGetProperty("vaiTro", out _));
        Assert.False(json.RootElement.TryGetProperty("matKhau", out _));
        Assert.DoesNotContain("id=\"kh-form-sua\"", html);
    }

    public class MoSuaKhach : KhachHang
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            var mo = typeof(KhachHang).GetMethod("MoSua", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mo);
            mo.Invoke(this, new object[] { ApiHoSo.Khach() });
        }
    }

    public sealed class LuuKhach : MoSuaKhach
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            var form = (CapNhatHoSoRequest)typeof(KhachHang).GetField("_formSua", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this);
            form.HoTen = "Tên cập nhật";
            await (Task)typeof(KhachHang).GetMethod("LuuKhachHangAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(this, null);
        }
    }

    public sealed class MoDoiMatKhau : ThongTinCaNhan
    {
        [Parameter] public string Hien { get; set; }
        [Parameter] public bool MoLai { get; set; }
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            void Goi(string method)
            {
                var action = typeof(ThongTinCaNhan).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(action);
                action.Invoke(this, null);
            }
            Goi("MoMatKhau");
            if (!string.IsNullOrEmpty(Hien)) Goi(Hien == "mk-cu" ? "DoiHienMatKhauCu" : Hien == "mk-lai" ? "DoiHienMatKhauLai" : "DoiHienMatKhau");
            if (MoLai) { Goi("DongMatKhau"); Goi("MoMatKhau"); }
        }
    }

    private static async Task<string> Render<T>(Dictionary<string, object> parameters = null, ApiHoSo api = null) where T : IComponent
    {
        using var http = new HttpClient(api ?? new ApiHoSo()) { BaseAddress = new Uri("http://test.local/") };
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(http);
        services.AddSingleton<IJSRuntime, JsKhongChay>();
        services.AddSingleton(new PhienDangNhap(null, http, new PhamViChiNhanh(null, http)));
        await using var sp = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(sp, sp.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode((await renderer.RenderComponentAsync<T>(
            ParameterView.FromDictionary(parameters ?? new()))).ToHtmlString()));
    }

    private sealed class ApiHoSo : HttpMessageHandler
    {
        public List<(string Path, string Body)> Writes { get; } = new();
        public static KhachHangQuanTriDto Khach() => new() { MaNguoiDung = 5, HoTen = "Khách thử", Email = "khach@gmail.com", SoDienThoai = "0901234567", DiaChi = "123 Đường thử", TrangThai = "HOAT_DONG" };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            object data;
            if (request.Method == HttpMethod.Put)
            {
                Assert.Equal("/api/quan-tri/khach-hang/5", request.RequestUri!.AbsolutePath);
                Writes.Add((request.RequestUri.AbsolutePath, await request.Content!.ReadAsStringAsync(token)));
                data = new { message = "Đã cập nhật khách hàng." };
            }
            else
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                data = request.RequestUri!.AbsolutePath switch {
                    "/api/quan-tri/khach-hang" => new[] { Khach() },
                    "/api/auth/me" => new NguoiDungDto { MaNguoiDung = 1, HoTen = "CEO thử", TenVaiTro = "SUPER_ADMIN", Email = "ceo@gmail.com" },
                    _ => throw new InvalidOperationException("API ngoài dự kiến: " + request.RequestUri.AbsolutePath)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data) };
        }
    }

    private sealed class JsKhongChay : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string name, object[] args) => ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }
}
