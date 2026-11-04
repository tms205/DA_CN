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
    public async Task AnhHaiBen_DocDungHashVaKhongLayCheo_SauTachThuMuc()
    {
        var config=CauHinh();using var cache=new MemoryCache(new MemoryCacheOptions());
        using var conn=new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        var rows=await conn.QueryAsync(@"SELECT gh.MaGiaoHang,gh.MaShipper,dh.MaKhachHang,dh.MaChiNhanh,
            gh.AnhBangChungJson,gh.DoiChieuKhachHangJson FROM GiaoHang gh JOIN DonHang dh ON dh.MaDonHang=gh.MaDonHang
            WHERE gh.TrangThai='THAT_BAI' AND (gh.AnhBangChungJson IS NOT NULL OR gh.DoiChieuKhachHangJson IS NOT NULL)");
        GiaoHangController Api(string role,int actor,int branch)=>new(config,cache,null,NullLogger<GiaoHangController>.Instance) {
            ControllerContext=new() {HttpContext=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity(new[]{
                new Claim(TenClaim.MaNguoiDung,actor.ToString()),new Claim(TenClaim.VaiTro,role),
                new Claim(TenClaim.MaChiNhanh,branch.ToString())},"fixture",ClaimTypes.Name,TenClaim.VaiTro))}}
        };
        async Task Check(IActionResult result,AnhBangChungDto expected) {
            var image=Assert.IsType<FileStreamResult>(result);
            await using var stream=image.FileStream;
            Assert.Equal(expected.Sha256,Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream)));
            Assert.Equal(expected.LoaiNoiDung,image.ContentType);
        }
        int count=0;
        foreach(var row in rows) {
            int attempt=row.MaGiaoHang,branch=row.MaChiNhanh;
            var ship=Api(VaiTro.Shipper,(int)row.MaShipper,branch);
            var customer=Api(VaiTro.Customer,(int)row.MaKhachHang,branch);
            var shipImages=System.Text.Json.JsonSerializer.Deserialize<List<AnhBangChungDto>>((string)row.AnhBangChungJson??"[]");
            var feedback=System.Text.Json.JsonSerializer.Deserialize<PhanHoiGiaoHangDto>((string)row.DoiChieuKhachHangJson??"{}");
            foreach(var image in shipImages) {
                await Check(await ship.XemAnhBangChung(attempt,image.MaAnh),image);count++;
                Assert.IsType<NotFoundResult>(await customer.XemAnhDoiChieu(attempt,image.MaAnh));
            }
            foreach(var image in feedback.Anh) {
                await Check(await customer.XemAnhDoiChieu(attempt,image.MaAnh),image);count++;
                Assert.IsType<NotFoundResult>(await ship.XemAnhBangChung(attempt,image.MaAnh));
            }
        }
        Assert.True(count>0,"Cần ảnh đã lưu để kiểm tra đọc thực tế.");
        _output.WriteLine($"Đã kiểm tra {count} ảnh: đúng hash, đúng nguồn và không lấy chéo; không ghi database.");
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
            foreach (string group in new[] { "CHO_GIAO", "DANG_GIAO", "THAT_BAI", "THANH_CONG", "LICH_SU" })
            {
                var ds = Assert.IsAssignableFrom<IEnumerable<DonGiaoDto>>(Assert.IsType<OkObjectResult>(await api.DanhSach(group)).Value).ToList();
                Assert.InRange(ds.Count, 0, 100);
                Assert.All(ds, x => {
                    if (branch != null) Assert.Equal(branch, x.MaChiNhanh);
                    Assert.Equal("DA_THANH_TOAN", x.TrangThaiThanhToan);
                    if (group == "CHO_GIAO") Assert.Equal("DA_IN", x.TrangThaiDonHang);
                    if (group == "THAT_BAI") { Assert.Equal("GIAO_THAT_BAI",x.TrangThaiDonHang);Assert.Equal("THAT_BAI",x.TrangThaiGiao); }
                    if (group == "THANH_CONG") { Assert.Contains(x.TrangThaiDonHang,new[]{"DA_GIAO","HOAN_THANH"});Assert.Equal("THANH_CONG",x.TrangThaiGiao); }
                });
            }
            var counts=Assert.IsAssignableFrom<IEnumerable<PhanBoGiaoHangDto>>(Assert.IsType<OkObjectResult>(await api.PhanBo()).Value).ToDictionary(x=>x.Nhom,x=>x.SoDon);
            var states=await conn.QueryAsync(@"SELECT dh.TrangThai AS TrangThaiDon,gh.TrangThai AS TrangThaiGiao
                FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
                OUTER APPLY(SELECT TOP 1 TrangThai FROM GiaoHang WHERE MaDonHang=dh.MaDonHang ORDER BY MaGiaoHang DESC) gh
                WHERE dh.HinhThucNhan='GIAO_HANG' AND tt.TrangThai='DA_THANH_TOAN'
                AND (@branch IS NULL OR dh.MaChiNhanh=@branch)",new{branch});
            var expected=new Dictionary<string,int>();
            foreach(var row in states)
            {
                string group=(string)row.TrangThaiDon switch {
                    "DA_IN" when (string)row.TrangThaiGiao!="DANG_GIAO"=>"CHO_GIAO",
                    "DANG_GIAO" when (string)row.TrangThaiGiao=="DANG_GIAO"=>"DANG_GIAO",
                    "GIAO_THAT_BAI" when (string)row.TrangThaiGiao=="THAT_BAI"=>"THAT_BAI",
                    "DA_GIAO" or "HOAN_THANH" when (string)row.TrangThaiGiao=="THANH_CONG"=>"THANH_CONG",_=>null};
                if(group!=null) expected[group]=expected.GetValueOrDefault(group)+1;
            }
            Assert.Equal(expected.OrderBy(x=>x.Key),counts.OrderBy(x=>x.Key));
            var shippers = Assert.IsAssignableFrom<IEnumerable<ShipperDto>>(Assert.IsType<OkObjectResult>(await api.Shipper()).Value);
            if (branch != null) Assert.All(shippers, x => Assert.Equal(branch, x.MaChiNhanh));
        }
    }

    [KiemThuGiaoHangDoc]
    public async Task BangChungLuotThat_DocDuocSauMigration_VaChanNgoaiPhamVi()
    {
        var config = CauHinh();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        var rows = (await conn.QueryAsync(@"SELECT TOP 3 gh.MaGiaoHang,gh.MaDonHang,gh.MaShipper,dh.MaChiNhanh
            FROM GiaoHang gh JOIN DonHang dh ON dh.MaDonHang=gh.MaDonHang ORDER BY gh.MaGiaoHang DESC")).ToList();
        Assert.NotEmpty(rows);
        GiaoHangController Controller(string role,int id,int? branch)
        {
            var claims = new List<Claim> { new(TenClaim.MaNguoiDung,id.ToString()),new(TenClaim.VaiTro,role) };
            if(branch!=null) claims.Add(new(TenClaim.MaChiNhanh,branch.ToString()));
            return new GiaoHangController(config,cache,null,NullLogger<GiaoHangController>.Instance) {
                ControllerContext=new ControllerContext { HttpContext=new DefaultHttpContext {
                    User=new ClaimsPrincipal(new ClaimsIdentity(claims,"fixture",ClaimTypes.Name,TenClaim.VaiTro)) }} };
        }
        foreach(var row in rows)
        {
            int attempt=row.MaGiaoHang,branch=row.MaChiNhanh,shipper=row.MaShipper,order=row.MaDonHang;
            var result=Assert.IsType<OkObjectResult>(await Controller(VaiTro.Shipper,shipper,branch).XemBangChung(attempt));
            var data=Assert.IsType<BangChungGiaoDto>(result.Value);
            Assert.Equal(attempt,data.MaGiaoHang);Assert.Equal(order,data.MaDonHang);
            Assert.IsType<NotFoundObjectResult>(await Controller(VaiTro.Shipper,int.MaxValue,branch).XemBangChung(attempt));
            Assert.IsType<NotFoundObjectResult>(await Controller(VaiTro.BranchAdmin,1,int.MaxValue).XemBangChung(attempt));
        }
        _output.WriteLine("PASS: đọc 3 lượt thật, kể cả trường mới NULL; shipper khác/chi nhánh khác bị chặn. Không ghi báo cáo hoặc đổi trạng thái.");
    }

    [KiemThuGiaoHangDoc]
    public async Task LichSuTheoDon_DuMoiLuot_DemPhanHoiCu_ChanKhacChiNhanh()
    {
        var config = CauHinh();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        GiaoHangController Api(int? branch) => new(config, cache, null, NullLogger<GiaoHangController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                    new Claim(TenClaim.MaNguoiDung,"1"), new Claim(TenClaim.VaiTro,branch==null?VaiTro.SuperAdmin:VaiTro.BranchAdmin)
                }.Concat(branch==null?Array.Empty<Claim>():new[] { new Claim(TenClaim.MaChiNhanh,branch.ToString()) }),"fixture",ClaimTypes.Name,TenClaim.VaiTro))
            } }
        };
        var orders = (await conn.QueryAsync<int>(@"SELECT DISTINCT g.MaDonHang FROM GiaoHang g
            JOIN DonHang d ON d.MaDonHang=g.MaDonHang WHERE d.HinhThucNhan='GIAO_HANG'")).ToList();
        Assert.NotEmpty(orders);
        foreach (int order in orders)
        {
            var list = Assert.IsAssignableFrom<IEnumerable<LuotLichSuGiaoDto>>(
                Assert.IsType<OkObjectResult>(await Api(null).LichSuGiaoCuaDon(order)).Value).ToList();
            var actual = (await conn.QueryAsync<int>("SELECT MaGiaoHang FROM GiaoHang WHERE MaDonHang=@order ORDER BY LanGiao,MaGiaoHang",new { order })).ToList();
            Assert.Equal(actual,list.Select(x=>x.MaGiaoHang));
            Assert.IsType<NotFoundObjectResult>(await Api(int.MaxValue).LichSuGiaoCuaDon(order));
        }
        foreach (string group in new[] { "CHO_GIAO","DANG_GIAO","LICH_SU" })
        {
            var list = Assert.IsAssignableFrom<IEnumerable<DonGiaoDto>>(Assert.IsType<OkObjectResult>(await Api(null).DanhSach(group)).Value);
            foreach (var row in list)
            {
                int count = await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM GiaoHang WHERE MaDonHang=@id
                    AND TrangThai='THAT_BAI' AND JSON_VALUE(DoiChieuKhachHangJson,'$.TrangThai')='CHO_DOI_CHIEU'", new { id=row.MaDonHang });
                Assert.Equal(count,row.SoPhanHoiChoDoiChieu);
            }
        }
        _output.WriteLine("PASS: đủ mọi lượt giao; số phản hồi chờ bao gồm lượt cũ; chi nhánh khác bị chặn. Chỉ SELECT.");
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
