using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DanhMuc;
using Xunit;

namespace PhotocopyApi.Tests;
public class DanhGiaChiNhanhTests
{
    [Fact]
    public async Task ManagerWithoutBranchCannotReadHideOrRestoreAnyReview()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var api = new DonHangController(new ConfigurationBuilder().Build(), cache, null) {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(TenClaim.VaiTro,VaiTro.BranchAdmin) }, "test",ClaimTypes.Name,TenClaim.VaiTro)) } }
        };
        Assert.IsType<ForbidResult>(await api.DanhSachDanhGia());
        Assert.IsType<ForbidResult>(await api.AnDanhGia(1));
        Assert.IsType<ForbidResult>(await api.HienDanhGia(1));
    }
    [Theory]
    [InlineData(nameof(DonHangController.AnDanhGia))]
    [InlineData(nameof(DonHangController.HienDanhGia))]
    public void ModerationRequiresCeoOrBranchManager(string method)
    {
        var roles = typeof(DonHangController).GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute),false).Cast<AuthorizeAttribute>().Single().Roles!.Split(',');
        Assert.Equal(new[]{VaiTro.SuperAdmin,VaiTro.BranchAdmin}, roles);
    }
    [Theory]
    [InlineData(0,1)] [InlineData(1,0)] [InlineData(1,10001)]
    public async Task InvalidPublicQueryDoesNotTouchDatabase(int branch,int page)
    {
        var api=new DanhMucController(new ConfigurationBuilder().Build()) {ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        Assert.IsType<BadRequestObjectResult>(await api.DanhGiaChiNhanh(branch,page));
    }
    [Fact]
    public void PublicReviewContractExcludesOrderAndContactDetails()
    {
        var properties=typeof(DanhGiaCongKhaiDto).GetProperties().Select(p=>p.Name).ToArray();
        Assert.Equal(new[]{"TenKhachHang","SoSao","NhanXet","NgayDanhGia"},properties);
    }
    [KiemThuGiaoHangDoc]
    public async Task PublicListAndAverageMatchVisibleReviewsInRealDatabase_ReadOnly()
    {
        var reader=typeof(DanhMucController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv")!;
        var env=(Dictionary<string,string>)reader.GetMethod("Doc")!.Invoke(null,new object[]{Environment.GetEnvironmentVariable("PHOTOCOPY_DELIVERY_ENV_FILE")!})!;
        var config=new ConfigurationBuilder().AddInMemoryCollection(env).Build();
        using var conn=new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        var branches=(await conn.QueryAsync<int>("SELECT MaChiNhanh FROM ChiNhanh WHERE TrangThai=N'HOAT_DONG'")).ToList();
        Assert.NotEmpty(branches);
        foreach(var branch in branches) {
            var api=new DanhMucController(config){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
            var result=Assert.IsType<DanhGiaChiNhanhDto>(Assert.IsType<OkObjectResult>(await api.DanhGiaChiNhanh(branch)).Value);
            var expected=(await conn.QueryAsync<int>("SELECT dg.SoSao FROM DanhGia dg JOIN DonHang dh ON dh.MaDonHang=dg.MaDonHang WHERE dh.MaChiNhanh=@branch AND dg.TrangThai=N'HIEN'",new{branch})).ToList();
            Assert.Equal(expected.Count,result.SoLuotDanhGia);
            Assert.Equal(expected.Count==0?0:expected.Average(),result.DiemTrungBinh,8);
            Assert.Equal(Math.Min(20,expected.Count),result.DanhGia.Count);
            Assert.Equal(expected.Count>20,result.CoTrangSau);
            Assert.All(result.DanhGia,r=>Assert.True(r.TenKhachHang.EndsWith("***")||r.TenKhachHang=="Khách hàng"));
        }
    }
}
