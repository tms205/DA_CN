using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.Auth;
using PhotocopyShared.Dtos.DanhMuc;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.NhanVien;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class CanCotThaoTacWebTests
{
    [Fact]
    public async Task DichVu_NutVaTieuDeThaoTacCungCanTrai()
    {
        string html = await Render<PhotocopyAdmin.Pages.DichVu>();
        Assert.Contains("class=\"bang bang-thao-tac-trai\"", html);
        Assert.Contains("class=\"cot-hanh-dong\">Thao tác</th>", html);
        Assert.Contains("Sửa dịch vụ</button>", html);
    }

    [Fact]
    public async Task NhanVien_CoTieuDeThaoTac_ChiGiuKhoaKhongCapLaiMatKhau()
    {
        string html = await Render<NhanVien>();
        Assert.Contains("class=\"cot-hanh-dong\">Thao tác</th>", html);
        Assert.Contains(">Khóa</button>", html);
        Assert.DoesNotContain("Cấp lại mật khẩu", html);
        Assert.DoesNotContain("Ai cấp lại mật khẩu", html);
        Assert.DoesNotContain("Mật khẩu tạm", html);
    }

    [Fact]
    public async Task DanhGia_CotTrangThaiThaoTacDungCanGiua()
    {
        string html = await Render<DanhGia>();
        Assert.Contains("class=\"bang bang-thao-tac-giua\"", html);
        Assert.Contains("class=\"cot-hanh-dong\">Trạng thái / Thao tác</th>", html);
        Assert.Contains("Ẩn đánh giá</button>", html);
    }

    [Fact]
    public async Task MauDon_NutLamMoiCoChuThichChiTaiLaiDanhSach()
    {
        string html = await Render<MauDon>();
        Assert.Contains("Tải lại danh sách mẫu từ máy chủ, không xóa hay tạo lại thư viện.", html);
    }

    private static async Task<string> Render<T>() where T : IComponent
    {
        using var http = new HttpClient(new ApiDanhSach()) { BaseAddress = new Uri("http://test.local/") };
        var js = new JsRong();
        var phamVi = new PhamViChiNhanh(js, http);
        var phien = new PhienDangNhap(js, http, phamVi);
        await phien.DangNhap(new DangNhapResponse { AccessToken = "fixture-only", HetHanLuc = DateTime.UtcNow.AddHours(1),
            NguoiDung = new NguoiDungDto { MaNguoiDung = 1, TenVaiTro = "SUPER_ADMIN", HoTen = "CEO thử" } }, false);
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(http); services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton(phamVi); services.AddSingleton(phien);
        await using var sp = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(sp, sp.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode((await renderer.RenderComponentAsync<T>()).ToHtmlString()));
    }

    private sealed class ApiDanhSach : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            object data = req.RequestUri!.AbsolutePath switch {
                "/api/nhan-vien" => new[] { new NhanVienDto { MaNguoiDung = 2, HoTen = "Nhân viên thử", Email = "staff@gmail.com", TenVaiTro = "STAFF", TrangThai = "HOAT_DONG", DuocCapLai = true } },
                "/api/danh-muc/chi-nhanh" => Array.Empty<ChiNhanhDto>(),
                "/api/quan-tri/danh-muc-dich-vu" => new[] { new DichVuQuanTriDto { MaDichVu = 1, TenDichVu = "In thử", LoaiDichVu = "IN_AN", TrangThai = "HIEN" } },
                "/api/quan-tri/danh-muc-vat-tu" => Array.Empty<VatTuDanhMucDto>(),
                "/api/don-hang/danh-gia" => new[] { new DanhGiaDto { MaDanhGia = 1, TenKhachHang = "Khách thử", SoSao = 5, TrangThai = "HIEN" } },
                "/api/quan-tri/mau-don" => Array.Empty<PhotocopyShared.Dtos.DatIn.MauDonDto>(),
                _ => throw new InvalidOperationException("API ngoài dự kiến: " + req.RequestUri.AbsolutePath)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data) });
        }
    }

    private sealed class JsRong : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string name, object[] args) => ValueTask.FromResult(default(T));
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }
}
