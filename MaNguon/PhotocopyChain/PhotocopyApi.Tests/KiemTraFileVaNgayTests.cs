using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;

public class KiemTraFileVaNgayTests
{
    [Fact]
    public async Task TenFileQuaDai_BaoLoiRoRangTruocKhiLuu()
    {
        var controller = new FileInController(new ConfigurationBuilder().Build(), NullLogger<FileInController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new(TenClaim.VaiTro, "CUSTOMER"), new(TenClaim.MaNguoiDung, "1")], "test")) } };
        using var stream = new MemoryStream([1]);
        var file = new FormFile(stream, 0, 1, "file", new string('A', 252) + ".pdf");
        var result = Assert.IsType<BadRequestObjectResult>(await controller.TaiLen(file, null));
        var json = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        Assert.Contains("Tên file", json.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2026, 9999)]
    public async Task NgayBaoCao_KhongVuotMienSqlHoacTranNgayKetThuc(int namTu, int namDen)
    {
        var controller = new QuanTriController(new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        Assert.IsType<BadRequestObjectResult>(await controller.BaoCaoDoanhThu(
            new DateTime(namTu, 1, 1), new DateTime(namDen, 12, 31)));
    }
}
