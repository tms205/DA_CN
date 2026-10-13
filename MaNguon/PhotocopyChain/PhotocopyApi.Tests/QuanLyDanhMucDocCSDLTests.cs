using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuanLyDanhMucDocCSDLTests
{
    [KiemThuBaoCaoDoc]
    public async Task DanhMucMoi_DocDuLieuThat_KhongTrungDichVuVaDungPhamVi()
    {
        string file = Environment.GetEnvironmentVariable("PHOTOCOPY_REPORT_ENV_FILE");
        var doc = typeof(QuanTriController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var config = (Dictionary<string, string>)doc.GetMethod("Doc").Invoke(null, new object[] { file });
        var cauHinh = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> {
            ["ConnectionStrings:PhotocopyChain"] = config["ConnectionStrings:PhotocopyChain"]
        }).Build();
        using var conn = new SqlConnection(cauHinh.GetConnectionString("PhotocopyChain"));
        int count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DichVu");
        foreach (int? branch in new int?[] { null, 1, 2 })
        {
            var claims = new List<Claim> { new(TenClaim.VaiTro, VaiTro.SuperAdmin) };
            if (branch != null) claims.Add(new(TenClaim.MaChiNhanh, branch.ToString()));
            var api = new QuanTriController(cauHinh) { ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, TenClaim.VaiTro))
                }
            } };
            var ds = Assert.IsAssignableFrom<IEnumerable<DichVuQuanTriDto>>(Assert.IsType<OkObjectResult>(await api.DanhMucDichVu()).Value).ToList();
            Assert.Equal(count, ds.Count);
            Assert.Equal(count, ds.Select(x => x.MaDichVu).Distinct().Count());
            var vt = Assert.IsAssignableFrom<IEnumerable<VatTuDanhMucDto>>(Assert.IsType<OkObjectResult>(await api.DanhMucVatTu()).Value).ToList();
            Assert.Equal(await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VatTu"), vt.Count);
            var cn = Assert.IsAssignableFrom<IEnumerable<ChiNhanhQuanTriDto>>(Assert.IsType<OkObjectResult>(await api.DanhSachChiNhanh()).Value).ToList();
            var may = Assert.IsAssignableFrom<IEnumerable<MayInQuanTriDto>>(Assert.IsType<OkObjectResult>(await api.DanhSachMayIn()).Value).ToList();
            if (branch != null) { Assert.All(cn, x => Assert.Equal(branch, x.MaChiNhanh)); Assert.All(may, x => Assert.Equal(branch, x.MaChiNhanh)); }
            var kh = Assert.IsAssignableFrom<IEnumerable<KhachHangQuanTriDto>>(Assert.IsType<OkObjectResult>(await api.DanhSachKhachHang()).Value).ToList();
            Assert.Equal(kh.Count, kh.Select(x => x.MaNguoiDung).Distinct().Count());
        }
    }
}
