using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public class AuthOtpTests
{
    [Fact]
    public async Task ChuaCauHinhGmail_KhongDuocBaoDaGuiEmailKeCaDevelopment()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new AuthController(new ConfigurationBuilder().Build(), cache,
            new MoiTruongDev(), NullLogger<AuthController>.Instance);
        var ham = typeof(AuthController).GetMethod("GuiEmailOtp", BindingFlags.Instance | BindingFlags.NonPublic);
        bool daGui = await (Task<bool>)ham.Invoke(controller, new object[] {
            "test@example.com", "Kiểm thử", "123456", "đặt lại mật khẩu", 10 });
        Assert.False(daGui);
    }

    [Fact]
    public void TienDauCaTuDong_MacDinh200000()
    {
        var tien = typeof(PhotocopyApi.CaLamViec.QuyTacCaDangNhap).GetField("TienDauCaMacDinh");
        Assert.NotNull(tien);
        Assert.Equal(200000m, tien.GetValue(null));
    }

    [Fact]
    public async Task GmailDocDungTenKhoa_CoCauHinhThiKiemTraHanGuiLai()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Email:Host"] = "smtp.gmail.com",
            ["Email:TenDangNhap"] = "test@example.com",
            ["Email:MatKhau"] = "fake-test-password"
        }).Build();
        var store = new PhotocopyApi.Auth.OtpDatLaiStore(cache, TimeProvider.System);
        store.ThuBatDauGui("test@example.com");
        var controller = new AuthController(config, cache, new MoiTruongDev(),
            NullLogger<AuthController>.Instance, store);
        var result = await controller.QuenMatKhau(new PhotocopyShared.Dtos.Auth.QuenMatKhauRequest { Email = "test@example.com" });
        Assert.Equal(429, Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(result).StatusCode);
    }

    private sealed class MoiTruongDev : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "PhotocopyApi";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
