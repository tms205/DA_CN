using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using PhotocopyAdmin;
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
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{\"maDonHang\":10,\"trangThai\":\"" + trangThai + "\"}",
                    System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
