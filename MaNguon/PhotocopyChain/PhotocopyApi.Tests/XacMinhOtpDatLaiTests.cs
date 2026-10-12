using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PhotocopyApi.Auth;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public class XacMinhOtpDatLaiTests
{
    [Theory]
    [InlineData("123456", 3)]
    [InlineData("000000", 2)]
    public async Task FormWeb_ChiSangMatKhauSauKhiApiXacMinhDung(string otp, int buoc)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new OtpDatLaiStore(cache, TimeProvider.System);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        var controller = new AuthController(new ConfigurationBuilder().Build(), cache, null,
            NullLogger<AuthController>.Instance, store);
        using var http = new HttpClient(new ApiXacMinh(controller)) { BaseAddress = new Uri("http://localhost/") };
        using var trang = new PhotocopyAdmin.Pages.QuenMatKhau();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var loai = trang.GetType();
        loai.GetProperty("Http", flags).SetValue(trang, http);
        var email = (PhotocopyShared.Dtos.Auth.QuenMatKhauRequest)loai.GetField("_email", flags).GetValue(trang);
        email.Email = "test@example.com";
        await (Task)loai.GetMethod("GuiOtpAsync", flags).Invoke(trang, null);
        Assert.Equal(2, loai.GetField("_buoc", flags).GetValue(trang));
        var req = (PhotocopyShared.Dtos.Auth.XacMinhOtpDatLaiRequest)loai.GetField("_otp", flags).GetValue(trang);
        req.MaOtp = otp;
        await (Task)loai.GetMethod("XacMinhAsync", flags).Invoke(trang, null);
        Assert.Equal(buoc, loai.GetField("_buoc", flags).GetValue(trang));
        var form = (PhotocopyAdmin.DatLaiMatKhauForm)loai.GetField("_matKhau", flags).GetValue(trang);
        Assert.Equal("", form.MatKhauMoi);
        Assert.Equal("", form.MatKhauXacNhan);
        Assert.True(store.DungMa("test@example.com", "123456"));
    }

    // Chỉ thay việc gửi email ở biên HTTP; xác minh dùng controller và OTP store thật.
    // Không truy cập database, không gửi Gmail, không thay mật khẩu tài khoản.
    private sealed class ApiXacMinh : HttpMessageHandler
    {
        private readonly AuthController _controller;

        public ApiXacMinh(AuthController controller) => _controller = controller;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri.AbsolutePath == "/api/auth/forgot-password")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PhotocopyShared.Dtos.Auth.OtpDaGuiDto
                {
                    Message = "Thông báo kiểm thử.", HetHanSauGiay = 600, GuiLaiSauGiay = 60
                }) };

            Assert.Equal("/api/auth/forgot-password/verify-otp", request.RequestUri.AbsolutePath);
            var req = await request.Content.ReadFromJsonAsync<PhotocopyShared.Dtos.Auth.XacMinhOtpDatLaiRequest>(cancellationToken: cancellationToken);
            var result = Assert.IsAssignableFrom<ObjectResult>(_controller.XacMinhOtpDatLai(req));
            return new HttpResponseMessage((HttpStatusCode)result.StatusCode.Value) { Content = JsonContent.Create(result.Value) };
        }
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("000000", false)]
    public void EndpointXacMinh_ChiChapNhanMaDung_VaKhongDoiMatKhau(string otp, bool hopLe)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new OtpDatLaiStore(cache, TimeProvider.System);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        var controller = new AuthController(new ConfigurationBuilder().Build(), cache, null,
            NullLogger<AuthController>.Instance, store);
        var ham = typeof(AuthController).GetMethod("XacMinhOtpDatLai");
        Assert.NotNull(ham);
        Assert.Equal("khoi-phuc-mat-khau", ham.GetCustomAttribute<EnableRateLimitingAttribute>().PolicyName);
        var req = Activator.CreateInstance(ham.GetParameters()[0].ParameterType);
        req.GetType().GetProperty("Email").SetValue(req, " TEST@example.com ");
        req.GetType().GetProperty("MaOtp").SetValue(req, otp);
        var result = ham.Invoke(controller, new[] { req });
        if (hopLe) Assert.IsType<OkObjectResult>(result);
        else Assert.IsType<BadRequestObjectResult>(result);
        Assert.True(store.DungMa("test@example.com", "123456"));
    }

    private static bool KiemTra(OtpDatLaiStore store, string email, string otp)
    {
        var ham = typeof(OtpDatLaiStore).GetMethod("KiemTraMa");
        Assert.NotNull(ham);
        return (bool)ham.Invoke(store, new object[] { email, otp });
    }

    [Fact]
    public void XacMinhDung_KhongTieuThuMa_LuuChiDungMotLan()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new OtpDatLaiStore(cache, TimeProvider.System);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        Assert.True(KiemTra(store, " TEST@example.com ", "123456"));
        Assert.True(KiemTra(store, "test@example.com", "123456"));
        Assert.True(store.DungMa("test@example.com", "123456"));
        Assert.False(KiemTra(store, "test@example.com", "123456"));
        Assert.False(store.DungMa("test@example.com", "123456"));
    }

    [Fact]
    public void XacMinhVaLuu_DungChungGioiHanNamLanSai()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new OtpDatLaiStore(cache, TimeProvider.System);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        for (int i = 0; i < 4; i++) Assert.False(KiemTra(store, "test@example.com", "000000"));
        Assert.False(store.DungMa("test@example.com", "000000"));
        Assert.False(KiemTra(store, "test@example.com", "123456"));
        Assert.False(store.DungMa("test@example.com", "123456"));
        Assert.InRange(store.ThuBatDauGui("test@example.com"), 899, 900);
    }

    [Fact]
    public void XacMinh_KhongGiaHanOtp()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new DongHo();
        var store = new OtpDatLaiStore(cache, clock);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        clock.Tang(TimeSpan.FromMinutes(9));
        Assert.True(KiemTra(store, "test@example.com", "123456"));
        clock.Tang(TimeSpan.FromMinutes(1));
        Assert.False(KiemTra(store, "test@example.com", "123456"));
        Assert.False(store.DungMa("test@example.com", "123456"));
    }

    [Fact]
    public void GuiLai_HuyMaDaXacMinh_TruocKhiLuu()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new DongHo();
        var store = new OtpDatLaiStore(cache, clock);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        Assert.True(KiemTra(store, "test@example.com", "123456"));
        clock.Tang(TimeSpan.FromSeconds(60));
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "654321");
        Assert.False(store.DungMa("test@example.com", "123456"));
        Assert.True(KiemTra(store, "test@example.com", "654321"));
        Assert.False(KiemTra(store, "khac@example.com", "654321"));
    }

    [Theory]
    [InlineData("Abcdef12", "Abcdef12", true)]
    [InlineData("Abcdef12", "Abcdef13", false)]
    [InlineData("Abcdef12", "", false)]
    [InlineData("abcdef12", "abcdef12", false)]
    public void MatKhauMoi_PhaiDungQuyTacVaTrungXacNhan(string matKhau, string xacNhan, bool hopLe)
    {
        var loai = typeof(PhotocopyAdmin.DoiSoatThanhToanWeb).Assembly.GetType("PhotocopyAdmin.DatLaiMatKhauForm");
        Assert.NotNull(loai);
        var form = Activator.CreateInstance(loai);
        loai.GetProperty("MatKhauMoi").SetValue(form, matKhau);
        loai.GetProperty("MatKhauXacNhan").SetValue(form, xacNhan);
        Assert.Equal(hopLe, Validator.TryValidateObject(form, new ValidationContext(form), new List<ValidationResult>(), true));
    }

    private sealed class DongHo : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Tang(TimeSpan duration) => _now += duration;
    }
}
