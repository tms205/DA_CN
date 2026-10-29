using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.Auth;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class HuyDonWebTests
{
    public static IEnumerable<object[]> CacTrangThai()
    {
        foreach (string role in new[] { "SUPER_ADMIN", "BRANCH_ADMIN", "STAFF" })
        foreach (string state in new[] { "CHO_XAC_NHAN", "DA_XAC_NHAN", "DANG_IN", "DA_IN", "DANG_GIAO", "GIAO_THAT_BAI", "HOAN_THANH", "DA_HUY" })
            yield return new object[] { role, state };
    }

    [Theory]
    [MemberData(nameof(CacTrangThai))]
    public async Task NutHuyChiChoQuanLyVaCeoTruocIn(string role, string state)
    {
        string html = await Render(role, state, "DA_THANH_TOAN");
        bool allowed = role != "STAFF" && state is "CHO_XAC_NHAN" or "DA_XAC_NHAN";
        Assert.Equal(allowed, html.Contains(">Hủy đơn hàng</button>"));
    }

    [Fact]
    public async Task DaHoanTien_KhongHienChuaThanhToanHayNutKiemTraThanhToan()
    {
        string html = await Render("SUPER_ADMIN", "DA_HUY", "DA_HOAN_TIEN");
        Assert.Contains("Đã hoàn tiền", html);
        Assert.DoesNotContain("Chưa thanh toán", html);
        Assert.DoesNotContain("Kiểm tra thanh toán", html);
    }

    private static async Task<string> Render(string role, string state, string payment)
    {
        using var http = new HttpClient(new ApiKhongGhi()) { BaseAddress = new Uri("http://fixture.local/") };
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(http);
        services.AddSingleton<IJSRuntime>(new JsGia());
        services.AddSingleton(new PhamViChiNhanh(null, http));
        services.AddSingleton(new PhienDangNhap(null, http, new PhamViChiNhanh(null, http)));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => {
            var view = await renderer.RenderComponentAsync<TrangThu>(ParameterView.FromDictionary(new Dictionary<string, object> {
                [nameof(TrangThu.Role)] = role, [nameof(TrangThu.State)] = state, [nameof(TrangThu.Payment)] = payment
            }));
            return WebUtility.HtmlDecode(view.ToHtmlString());
        });
    }

    public sealed class TrangThu : HangDoiDon
    {
        [Parameter] public string Role { get; set; }
        [Parameter] public string State { get; set; }
        [Parameter] public string Payment { get; set; }
        protected override Task OnInitializedAsync()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var phien = (PhienDangNhap)typeof(HangDoiDon).GetProperty("Phien", flags)!.GetValue(this)!;
            typeof(PhienDangNhap).GetField("_phien", flags)!.SetValue(phien, new DangNhapResponse {
                NguoiDung = new() { MaNguoiDung = 1, TenVaiTro = Role, MaChiNhanh = 1 }
            });
            typeof(HangDoiDon).GetField("_ds", flags)!.SetValue(this, new List<HangDoiDonDto>());
            typeof(HangDoiDon).GetField("_donDangChon", flags)!.SetValue(this, new DonHangChiTietDto {
                MaDonHang = 1, MaDonHangHienThi = "DH00001", TenKhachHang = "Khách thử",
                MaChiNhanh = 1, HinhThucNhan = "TAI_QUAY", TrangThaiDonHang = State,
                PhuongThucThanhToan = "VIETQR", TrangThaiThanhToan = Payment
            });
            return Task.CompletedTask;
        }
        protected override Task OnParametersSetAsync() => Task.CompletedTask;
    }

    private sealed class ApiKhongGhi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Kiểm thử giao diện không được gọi API thật.");
    }

    private sealed class JsGia : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string id, object[] args) => ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }
}
