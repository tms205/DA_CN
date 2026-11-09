using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class DoiSoatThanhToanWebTests
{
    private static object Goi(string ham, params object[] args)
    {
        var lop = typeof(PhamViChiNhanh).Assembly.GetType("PhotocopyAdmin.DoiSoatThanhToanWeb");
        Assert.NotNull(lop);
        try { return lop.GetMethod(ham).Invoke(null, args); }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    [Theory]
    [InlineData(null, "10", 10)]
    [InlineData(7, null, 7)]
    [InlineData(null, "-1", null)]
    [InlineData(null, "2147483648", null)]
    [InlineData(null, "khong-phai-ma-don", null)]
    public void QuayLaiWeb_NhanOrderCodeVaLinkCu(int? maDon, string orderCode, int? mongDoi)
    {
        Assert.Equal(mongDoi, (int?)Goi("LayMaDonQuayLai", maDon, orderCode));
    }

    [Theory]
    [InlineData("CHUA_THANH_TOAN")]
    [InlineData("DA_THANH_TOAN")]
    public async Task TrangThai_ChiLayTuApiCoXacThuc_KhongTuDanhDauDaTra(string trangThai)
    {
        using var handler = new ApiThanhToanGia(HttpStatusCode.OK, trangThai);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://test.local/") };
        var result = await (Task<ThanhToanDto>)Goi("KiemTraAsync", http, 10);
        Assert.Equal(trangThai, result.TrangThai);
        Assert.Equal("/api/thanh-toan/10", handler.DuongDan);
    }

    [Theory]
    [InlineData("CHUA_THANH_TOAN", true)]
    [InlineData("DA_THANH_TOAN", false)]
    public async Task HangDoi_ChiMoPopupKhiPayOSChuaXacNhan(string trangThai, bool chuaDuTien)
    {
        const BindingFlags rieng = BindingFlags.Instance | BindingFlags.NonPublic;
        using var http = new HttpClient(new ApiThanhToanGia(HttpStatusCode.OK, trangThai))
            { BaseAddress = new Uri("http://test.local/") };
        var trang = new HangDoiDon();
        var lop = typeof(HangDoiDon);
        lop.GetProperty("Http", rieng).SetValue(trang, http);
        var kiemTra = lop.GetMethod("KiemTraThanhToanAsync", rieng);
        var popup = lop.GetField("_canhBaoThanhToan", rieng);
        var thanhCong = lop.GetField("_tinThanhToan", rieng);

        for (int i = 0; i < 2; i++)
        {
            await (Task)kiemTra.Invoke(trang, new object[] { 10 });
            Assert.Equal(chuaDuTien ? "payOS chưa xác nhận đủ tiền. Vui lòng kiểm tra lại sau." : null,
                popup.GetValue(trang));
            Assert.Equal(chuaDuTien ? null : "Đã xác minh thanh toán với payOS.", thanhCong.GetValue(trang));
            // Đóng popup rồi bấm kiểm tra lại vẫn nhận thông báo mới.
            popup.SetValue(trang, null);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task NgoaiQuyenHoacKhongCoDon_KhongBaoDaThanhToan(HttpStatusCode status)
    {
        using var handler = new ApiThanhToanGia(status, "DA_THANH_TOAN");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://test.local/") };
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await (Task<ThanhToanDto>)Goi("KiemTraAsync", http, 10));
    }

    private sealed class ApiThanhToanGia(HttpStatusCode status, string trangThai) : HttpMessageHandler
    {
        public string DuongDan { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            DuongDan = req.RequestUri.AbsolutePath;
            string json = DuongDan == "/api/don-hang/hang-doi"
                ? "[]"
                : "{\"maDonHang\":10,\"trangThai\":\"" + trangThai + "\"}";
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json,
                    System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
