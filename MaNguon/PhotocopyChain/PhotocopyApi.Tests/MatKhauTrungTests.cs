using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Auth;
using PhotocopyApi.Controllers;
using PhotocopyShared.Dtos.Auth;
using Xunit;

namespace PhotocopyApi.Tests;

public class MatKhauTrungTests
{
    private const string ThongBao = "Mật khẩu mới trùng với mật khẩu hiện tại. Vui lòng chọn mật khẩu khác.";

    [Theory]
    [InlineData("Abcdef12", "Abcdef12", true)]
    [InlineData("Abcdef12", "Abcdef13", false)]
    [InlineData("Abcdef12", "abcdef12", false)]
    [InlineData("Abcdef12", " Abcdef12", false)]
    [InlineData(" Abcdef12", " Abcdef12", true)]
    public void MatKhauMoi_SoVoiHashHienTai_KhongTrimHoacSoHaiHash(string cu, string moi, bool trung)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = TaoController(cache);
        var ham = typeof(AuthController).GetMethod("KiemTraMatKhauMoi", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(ham);

        // Hai lần băm cùng mật khẩu có salt khác nhau, nhưng đều phải phát hiện trùng.
        for (int i = 0; i < 2; i++)
        {
            string hash = BCrypt.Net.BCrypt.HashPassword(cu, 4);
            string loi = (string)ham.Invoke(controller, new object[] { moi, hash });
            Assert.Equal(trung ? ThongBao : null, loi);
        }
    }

    [Fact]
    public async Task DatLai_ChuaKiemTraDuocTaiKhoan_KhongTieuThuOtp()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new OtpDatLaiStore(cache, TimeProvider.System);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        var controller = TaoController(cache, store);

        // Không cấu hình DB: lỗi truy cập xảy ra trước khi có thể đổi mật khẩu.
        // Tuyệt đối không đọc cấu hình thật, gửi email hoặc cập nhật tài khoản thật.
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.DatLaiMatKhau(new DatLaiMatKhauRequest
        {
            Email = "test@example.com", MaOtp = "123456", MatKhauMoi = "Abcdef13"
        }));
        Assert.True(store.KiemTraMa("test@example.com", "123456"));
    }

    private static AuthController TaoController(MemoryCache cache, OtpDatLaiStore store = null)
    {
        return new AuthController(new ConfigurationBuilder().Build(), cache, null,
            NullLogger<AuthController>.Instance, store);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, 3)]
    [InlineData(HttpStatusCode.BadRequest, 2)]
    public async Task FormQuenMatKhau_TrungThiGiuBuocNhapMatKhau_SaiOtpThiQuayLai(HttpStatusCode ma, int buoc)
    {
        using var http = new HttpClient(new LoiDatLai(ma)) { BaseAddress = new Uri("http://localhost/") };
        using var trang = new PhotocopyAdmin.Pages.QuenMatKhau();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var loai = trang.GetType();
        loai.GetProperty("Http", flags).SetValue(trang, http);
        loai.GetField("_buoc", flags).SetValue(trang, 3);
        var otp = (XacMinhOtpDatLaiRequest)loai.GetField("_otp", flags).GetValue(trang);
        otp.Email = "test@example.com";
        otp.MaOtp = "123456";
        var form = (PhotocopyAdmin.DatLaiMatKhauForm)loai.GetField("_matKhau", flags).GetValue(trang);
        form.MatKhauMoi = form.MatKhauXacNhan = "Abcdef12";

        await (Task)loai.GetMethod("DatLaiAsync", flags).Invoke(trang, null);

        Assert.Equal(buoc, loai.GetField("_buoc", flags).GetValue(trang));
        Assert.False((bool)loai.GetField("_xong", flags).GetValue(trang));
        Assert.Equal(ma == HttpStatusCode.Conflict ? ThongBao : "Mã OTP không đúng.",
            loai.GetField("_loi", flags).GetValue(trang));
        if (ma == HttpStatusCode.Conflict) Assert.Equal("123456", otp.MaOtp);
    }

    // Giả lập lỗi tại biên HTTP để kiểm tra form thật, không gửi OTP hoặc đổi mật khẩu thật.
    private sealed class LoiDatLai(HttpStatusCode ma) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken dung)
        {
            Assert.Equal("/api/auth/reset-password", req.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(ma)
            {
                Content = JsonContent.Create(new { message = ma == HttpStatusCode.Conflict ? ThongBao : "Mã OTP không đúng." })
            });
        }
    }
}
