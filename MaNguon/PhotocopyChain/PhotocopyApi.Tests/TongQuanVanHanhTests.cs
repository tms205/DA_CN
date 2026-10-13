using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.MayInAo;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class TongQuanVanHanhTests
{
    [Fact]
    public async Task VanHanh_ChiDemDonDangXuLy_TachQrVaCod()
    {
        string html = await Render();
        Assert.Contains("Chờ tiếp nhận: 3 đơn", html);
        Assert.Contains("Cần tiếp nhận: 1 đơn", html);
        Assert.Contains("Chờ thanh toán QR: 1 đơn", html);
        Assert.Contains("Chờ hệ thống tiếp nhận: 1 đơn", html);
        Assert.Contains("Đang in: 1 đơn", html);
        Assert.Contains("Chờ bàn giao: 1 đơn", html);
        Assert.Contains("Hỏa tốc đang xử lý: 1 đơn", html);
        Assert.Contains("Quá hạn chưa hoàn tất: 1 đơn", html);
        Assert.DoesNotContain("KHÔNG HIỂN THỊ ĐƠN CŨ", html);
    }

    [Fact]
    public async Task MayIn_KhongCoHeartbeatKhongDuocCoiLaKetNoi()
    {
        string html = await Render();
        Assert.Contains("Máy kết nối: 1 / 3", html);
        Assert.Contains("Sẵn sàng", html);
        Assert.Contains("Chưa có phản hồi", html);
        Assert.Contains("Mất kết nối", html);
        Assert.Contains("Bảo trì", html);
    }

    [Fact]
    public async Task KhoVaPhieu_ChiHienCanhBaoVaPhieuChoDuyet()
    {
        string html = await Render();
        Assert.Contains("Giấy gần hết", html);
        Assert.DoesNotContain("Giấy đủ", html);
        Assert.Contains("Phiếu chờ duyệt: 1", html);
        Assert.Contains("PN0001", html);
        Assert.DoesNotContain("PN0002", html);
    }

    [Theory]
    [InlineData("/api/don-hang/hang-doi", "Không tải được hàng đợi", "Chờ tiếp nhận: 0 đơn")]
    [InlineData("/api/quan-tri/kho", "Không tải được tồn kho", "Không có vật tư dưới mức tối thiểu")]
    [InlineData("/api/quan-tri/phieu-nhap", "Không tải được phiếu nhập", "Phiếu chờ duyệt: 0")]
    [InlineData("/api/quan-tri/may-in", "Không tải được danh sách máy", "Máy kết nối: 0")]
    [InlineData("/api/may-in-ao/trang-thai", "Không tải được kết nối máy", "Chưa có phản hồi")]
    public async Task LoiNguon_KhongGiaSoKhong_VanHienNguonKhac(string endpoint, string loi, string khongHien)
    {
        string html = await Render(endpoint);
        Assert.Contains(loi, html);
        Assert.DoesNotContain(khongHien, html);
        Assert.Contains(endpoint == "/api/quan-tri/kho" ? "PN0001" : "Giấy gần hết", html);
    }

    [Fact]
    public async Task Rong_HienThongBaoRieng_KhongChiaChoKhong()
    {
        string html = await Render(rong: true);
        Assert.Contains("Không có đơn đang xử lý", html);
        Assert.Contains("Chưa có máy in trong phạm vi", html);
        Assert.Contains("Không có vật tư dưới mức tối thiểu", html);
        Assert.Contains("Không có phiếu nhập chờ duyệt", html);
        Assert.DoesNotContain("NaN", html);
        Assert.DoesNotContain("Infinity", html);
    }

    private static async Task<string> Render(string endpointLoi = "", bool rong = false)
    {
        Type lop = typeof(PhamViChiNhanh).Assembly.GetType("PhotocopyAdmin.Components.TongQuanVanHanh");
        Assert.NotNull(lop);
        using var http = new HttpClient(new ApiVanHanh(endpointLoi, rong)) { BaseAddress = new Uri("http://test.local/") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(http);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => {
            var kq = await renderer.RenderComponentAsync(lop, ParameterView.Empty);
            return WebUtility.HtmlDecode(kq.ToHtmlString());
        });
    }

    private sealed class ApiVanHanh(string endpointLoi, bool rong) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            string path = req.RequestUri.AbsolutePath;
            if (path == endpointLoi) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            object ds = path switch {
                "/api/don-hang/hang-doi" => rong ? new List<HangDoiDonDto>() : Don(),
                "/api/quan-tri/kho" => rong ? new List<TonKhoDto>() : new List<TonKhoDto> {
                    new() { MaVatTu = 1, TenVatTu = "Giấy gần hết", SoLuong = 10, MucToiThieu = 20, CanhBao = true },
                    new() { MaVatTu = 2, TenVatTu = "Giấy đủ", SoLuong = 100, MucToiThieu = 20, CanhBao = false }
                },
                "/api/quan-tri/phieu-nhap" => rong ? new List<PhieuNhapDto>() : new List<PhieuNhapDto> {
                    new() { MaPhieuNhap = 1, TenVatTu = "Vật tư cần duyệt", TrangThai = "CHO_DUYET" },
                    new() { MaPhieuNhap = 2, TenVatTu = "Đã duyệt", TrangThai = "DA_DUYET" }
                },
                "/api/quan-tri/may-in" => rong ? new List<MayInQuanTriDto>() : new List<MayInQuanTriDto> {
                    new() { MaMayIn = 1, TenMay = "Máy 1", TrangThai = "HOAT_DONG" },
                    new() { MaMayIn = 2, TenMay = "Máy 2", TrangThai = "HOAT_DONG" },
                    new() { MaMayIn = 3, TenMay = "Máy 3", TrangThai = "BAO_TRI" }
                },
                "/api/may-in-ao/trang-thai" => rong ? new List<TrangThaiKetNoiMayInDto>() : new List<TrangThaiKetNoiMayInDto> {
                    new() { MaMayIn = 1, TrangThai = "SAN_SANG", LanCuoiPhanHoi = DateTime.Now },
                    new() { MaMayIn = 3, TrangThai = "MAT_KET_NOI", LanCuoiPhanHoi = DateTime.Now.AddMinutes(-1) },
                    new() { MaMayIn = 999, TrangThai = "SAN_SANG", LanCuoiPhanHoi = DateTime.Now }
                },
                _ => throw new InvalidOperationException("API ngoài phạm vi kiểm thử: " + path)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(ds) });
        }

        private static List<HangDoiDonDto> Don() => new() {
            new() { MaDonHang = 1, MaDonHangHienThi = "DH00001", TrangThaiDonHang = "CHO_XAC_NHAN", PhuongThucThanhToan = "COD", InHoaToc = true, NgayHenTra = DateTime.Now.AddHours(-1) },
            new() { MaDonHang = 2, MaDonHangHienThi = "DH00002", TrangThaiDonHang = "CHO_XAC_NHAN", PhuongThucThanhToan = "VIETQR", TrangThaiThanhToan = "CHUA_THANH_TOAN" },
            new() { MaDonHang = 3, MaDonHangHienThi = "DH00003", TrangThaiDonHang = "CHO_XAC_NHAN", PhuongThucThanhToan = "VIETQR", TrangThaiThanhToan = "DA_THANH_TOAN" },
            new() { MaDonHang = 4, MaDonHangHienThi = "DH00004", TrangThaiDonHang = "DANG_IN" },
            new() { MaDonHang = 5, MaDonHangHienThi = "DH00005", TrangThaiDonHang = "DA_IN" },
            new() { MaDonHang = 6, TenKhachHang = "KHÔNG HIỂN THỊ ĐƠN CŨ", TrangThaiDonHang = "HOAN_THANH", InHoaToc = true, NgayHenTra = DateTime.Now.AddDays(-1) },
            new() { MaDonHang = 7, TenKhachHang = "KHÔNG HIỂN THỊ ĐƠN CŨ", TrangThaiDonHang = "DA_HUY", InHoaToc = true, NgayHenTra = DateTime.Now.AddDays(-1) }
        };
    }
}
