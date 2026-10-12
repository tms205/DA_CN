using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuanTriControllerTests
{
    [Fact]
    public async Task CEOChonChiNhanh1_KhongDuocSuaChiNhanh2()
    {
        var controller = new QuanTriController(new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(TenClaim.VaiTro, "SUPER_ADMIN"),
                    new Claim(TenClaim.MaChiNhanh, "1")
                }, "test"))
            }
        };

        var ketQua = await controller.CapNhatChiNhanh(2,
            new ChiNhanhQuanTriDto { TenChiNhanh = "Chi nhánh 2", DiaChi = "Địa chỉ" });

        Assert.Equal(403, Assert.IsType<ObjectResult>(ketQua).StatusCode);
    }
}
