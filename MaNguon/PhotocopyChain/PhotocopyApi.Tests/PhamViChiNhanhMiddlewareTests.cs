using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PhotocopyApi;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;

public class PhamViChiNhanhMiddlewareTests
{
    [Fact]
    public async Task CeoChonChiNhanh_ThemPhamViQuanLyNhungVanGiuQuyenCeo()
    {
        var context = TaoContext(VaiTro.SuperAdmin, null);
        context.Request.Headers[TenHeader.MaChiNhanh] = "7";
        string vaiTroHieuLuc = null;
        bool vanLaCeo = false;
        string maChiNhanh = null;
        var middleware = new PhamViChiNhanhMiddleware(_ =>
        {
            vaiTroHieuLuc = context.User.FindFirst(TenClaim.VaiTro)?.Value;
            vanLaCeo = context.User.IsInRole(VaiTro.SuperAdmin);
            maChiNhanh = context.User.FindFirst(TenClaim.MaChiNhanh)?.Value;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(VaiTro.BranchAdmin, vaiTroHieuLuc);
        Assert.True(vanLaCeo);
        Assert.Equal("7", maChiNhanh);
    }

    [Fact]
    public async Task QuanLyThuongGuiHeader_KhongDoiDuocChiNhanh()
    {
        var context = TaoContext(VaiTro.BranchAdmin, 2);
        context.Request.Headers[TenHeader.MaChiNhanh] = "7";
        string maChiNhanh = null;
        var middleware = new PhamViChiNhanhMiddleware(_ =>
        {
            maChiNhanh = context.User.FindFirst(TenClaim.MaChiNhanh)?.Value;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("2", maChiNhanh);
    }

    [Fact]
    public async Task CeoGuiMaChiNhanhKhongHopLe_TraVeBadRequest()
    {
        var context = TaoContext(VaiTro.SuperAdmin, null);
        context.Request.Headers[TenHeader.MaChiNhanh] = "khong-hop-le";
        context.Response.Body = new MemoryStream();
        bool daChayXuLyTiep = false;
        var middleware = new PhamViChiNhanhMiddleware(_ =>
        {
            daChayXuLyTiep = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(daChayXuLyTiep);
    }

    private static DefaultHttpContext TaoContext(string vaiTro, int? maChiNhanh)
    {
        var claims = new List<Claim>
        {
            new(TenClaim.MaNguoiDung, "1"),
            new(TenClaim.VaiTro, vaiTro)
        };
        if (maChiNhanh != null)
            claims.Add(new Claim(TenClaim.MaChiNhanh, maChiNhanh.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "test", TenClaim.HoTen, TenClaim.VaiTro);
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}
