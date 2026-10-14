using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyApi.GiaoHang;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.GiaoHang;
using Xunit;
using Xunit.Abstractions;

namespace PhotocopyApi.Tests;

// Opt-in, chỉ SELECT. Không gọi nhan/phan-cong/ket-qua, không bật các test ghi database khác.
public class GiaoHangDocCSDLTests
{
    private readonly ITestOutputHelper _output;
    public GiaoHangDocCSDLTests(ITestOutputHelper output) => _output = output;
    private static IConfiguration CauHinh()
    {
        string file = Environment.GetEnvironmentVariable("PHOTOCOPY_DELIVERY_ENV_FILE");
        var doc = typeof(GiaoHangController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var config = (Dictionary<string, string>)doc.GetMethod("Doc").Invoke(null, new object[] { file });
        return new ConfigurationBuilder().AddInMemoryCollection(config).Build();
    }

    [KiemThuGiaoHangDoc]
    public async Task DanhSachThat_KhopPhamViChiNhanhVaCacCotCSDL()
    {
        var config = CauHinh();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        var branches = (await conn.QueryAsync<int>("SELECT MaChiNhanh FROM ChiNhanh")).ToList();
        Assert.NotEmpty(branches);
        foreach (int? branch in branches.Select(x => (int?)x).Prepend(null))
        {
            var claims = new List<Claim> { new(TenClaim.MaNguoiDung, "1"), new(TenClaim.VaiTro, VaiTro.SuperAdmin) };
            if (branch != null) claims.Add(new(TenClaim.MaChiNhanh, branch.ToString()));
            var api = new GiaoHangController(config, cache, null, NullLogger<GiaoHangController>.Instance) {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture", ClaimTypes.Name, TenClaim.VaiTro))
                } }
            };
            foreach (string group in new[] { "CHO_GIAO", "DANG_GIAO", "LICH_SU" })
            {
                var ds = Assert.IsAssignableFrom<IEnumerable<DonGiaoDto>>(Assert.IsType<OkObjectResult>(await api.DanhSach(group)).Value).ToList();
                Assert.InRange(ds.Count, 0, 100);
                Assert.All(ds, x => {
                    if (branch != null) Assert.Equal(branch, x.MaChiNhanh);
                    Assert.Equal("DA_THANH_TOAN", x.TrangThaiThanhToan);
                    if (group == "CHO_GIAO") Assert.Contains(x.TrangThaiDonHang, new[] { "DA_IN", "GIAO_THAT_BAI" });
                });
            }
            var shippers = Assert.IsAssignableFrom<IEnumerable<ShipperDto>>(Assert.IsType<OkObjectResult>(await api.Shipper()).Value);
            if (branch != null) Assert.All(shippers, x => Assert.Equal(branch, x.MaChiNhanh));
        }
    }

    [KiemThuGiaoHangDoc]
    public async Task GeoapifyThat_TimDiaChiTuyenXeMayVaBaoGiaKhongGhiDon()
    {
        var config = CauHinh();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var geo = new GeoapifyService(config, cache);
        Assert.True(geo.DaCauHinh, "Cần điền key Geoapify vào .env để kiểm tra bản đồ thật.");
        using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        var cn = await conn.QueryFirstAsync<ChiNhanh>("SELECT TOP 1 * FROM ChiNhanh WHERE ViDo IS NOT NULL AND KinhDo IS NOT NULL AND BanKinhGiao>0 ORDER BY MaChiNhanh");
        var ds = await geo.TimDiaChi("Trường Đại học Công Thương 140 Lê Trọng Tấn TP Hồ Chí Minh", null, null);
        Assert.NotEmpty(ds);
        var diem = ds[0];
        string ma = geo.GhiNhanViTri(1, diem);
        geo.KiemTraViTri(ma, 1, diem.DiaChi, diem.ViDo, diem.KinhDo);
        var reverse = await geo.TimDiaChi(null, cn.ViDo, cn.KinhDo);
        Assert.NotEmpty(reverse);
        var result = await geo.TinhPhi(cn, diem.DiaChi, diem.ViDo, diem.KinhDo);
        Assert.InRange(result.KhoangCachKm, 0, cn.BanKinhGiao);
        Assert.True(result.PhiShip >= 15000);
        _output.WriteLine($"Geoapify thật: {result.KhoangCachKm} km; phí {result.PhiShip}đ; thời gian tuyến {result.ThoiGianDuKienGiay}s. Chỉ 3 request, không tạo/đổi đơn.");
    }
}

public sealed class KiemThuGiaoHangDocAttribute : FactAttribute
{
    public KiemThuGiaoHangDocAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHOTOCOPY_DELIVERY_ENV_FILE")))
            Skip = "Cần PHOTOCOPY_DELIVERY_ENV_FILE; chỉ đọc CSDL và 3 request bản đồ, không ghi dữ liệu.";
    }
}
