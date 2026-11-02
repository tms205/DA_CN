using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin.Components;
using PhotocopyShared.Dtos.GiaoHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class LichSuGiaoDonWebTests
{
    [Theory]
    [InlineData("THAT_BAI", false)]
    [InlineData("THANH_CONG", false)]
    [InlineData("DANG_GIAO", false)]
    [InlineData("THANH_CONG", true)]
    public async Task BaLuot_HienDu_ChoDoiChieuCuVanDuocUuTien(string trangThaiCuoi, bool daXuLy)
    {
        var fixture = new Fixture(trangThaiCuoi, daXuLy);
        string html = WebUtility.HtmlDecode(await Render(fixture));
        Assert.Contains("3 lượt giao", html);
        Assert.Contains("Lượt 1", html); Assert.Contains("Lượt 2", html); Assert.Contains("Lượt 3", html);
        Assert.Contains(daXuLy ? "0 phản hồi chờ đối chiếu" : "1 phản hồi chờ đối chiếu", html);
        Assert.Contains("Khách chưa gửi phản hồi", html);
        Assert.Contains(daXuLy ? "Đã đối chiếu" : "Ý kiến của khách hàng", html);
        Assert.Contains("/api/giao-hang/don/79/lich-su", fixture.DuongDan);
        // Ưu tiên phản hồi chưa xử lý của lượt 1 kể cả khi lượt cuối đã thành công/đang giao.
        if (daXuLy) Assert.Single(fixture.DuongDan); // Lượt cuối thành công: không gọi API hồ sơ thất bại.
        else Assert.Contains("/api/giao-hang/luot/15/doi-chieu", fixture.DuongDan);
    }

    [Fact]
    public async Task KhongTaiDuoc_HienLoiThuLai_KhongTaoKetQuaGia()
    {
        string html = WebUtility.HtmlDecode(await Render(new Fixture("THAT_BAI", false, HttpStatusCode.Forbidden)));
        Assert.Contains("không có quyền", html); Assert.Contains("Thử lại", html);
        Assert.DoesNotContain("0 phản hồi", html);
    }

    private static async Task<string> Render(Fixture fixture)
    {
        var services = new ServiceCollection(); services.AddLogging();
        using var http = new HttpClient(fixture) { BaseAddress = new Uri("http://fixture.invalid/") };
        services.AddSingleton(http);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<LichSuGiaoDon>(
            ParameterView.FromDictionary(new Dictionary<string, object> { ["MaDonHang"] = 79 }))).ToHtmlString());
    }

    private sealed class Fixture(string cuoi, bool daXuLy, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> DuongDan { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri.AbsolutePath; DuongDan.Add(path);
            object data;
            if (path.EndsWith("/lich-su")) data = new[]
            {
                new LuotLichSuGiaoDto { MaDonHang=79,MaGiaoHang=19,LanGiao=3,TrangThai=cuoi },
                new LuotLichSuGiaoDto { MaDonHang=79,MaGiaoHang=15,LanGiao=1,TrangThai="THAT_BAI",TrangThaiDoiChieu=daXuLy?"DA_XU_LY":"CHO_DOI_CHIEU" },
                new LuotLichSuGiaoDto { MaDonHang=79,MaGiaoHang=16,LanGiao=2,TrangThai="THAT_BAI" }
            };
            else if (path.EndsWith("/bang-chung")) data = new BangChungGiaoDto { MaDonHang=79,MaGiaoHang=15,LanGiao=1 };
            else data = new LuotDoiChieuGiaoDto { MaDonHang=79,MaGiaoHang=15,LanGiao=1,PhanHoi=new() { NoiDung="Phản hồi lượt đầu",TrangThai="CHO_DOI_CHIEU" } };
            return Task.FromResult(new HttpResponseMessage(status) { Content=JsonContent.Create(data) });
        }
    }
}
