using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.GiaoHang;
using Xunit;
using Xunit.Abstractions;

namespace PhotocopyApi.Tests;

// Chỉ bật sau khi được phép tạo/dọn dữ liệu test. Không gọi payOS, máy in hay SignalR thật.
public class GiaoHangGhiCSDLTests
{
    private readonly ITestOutputHelper _output;
    public GiaoHangGhiCSDLTests(ITestOutputHelper output) => _output = output;

    [KiemThuGiaoHangGhi]
    public async Task DonTest_NhanDongThoi_KetQuaLap_ThanhCong_BaLanThatBai_VaDungPhamVi()
    {
        var doc = typeof(GiaoHangController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var values = (Dictionary<string, string>)doc.GetMethod("Doc").Invoke(null,
            new object[] { Environment.GetEnvironmentVariable("PHOTOCOPY_DELIVERY_ENV_FILE") });
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        await conn.OpenAsync();
        var ships = (await conn.QueryAsync<NguoiGiao>(
            "SELECT MaNguoiDung,MaChiNhanh FROM NguoiDung WHERE VaiTro='SHIPPER' AND TrangThai=N'HOAT_DONG' AND MaChiNhanh IS NOT NULL ORDER BY MaChiNhanh,MaNguoiDung")).ToList();
        Assert.NotEmpty(ships);
        var cungChiNhanh = ships.GroupBy(x => x.MaChiNhanh).OrderByDescending(x => x.Count()).First().Take(2).ToList();
        var ship1 = cungChiNhanh[0];
        int branch = ship1.MaChiNhanh;
        int customer = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaNguoiDung FROM NguoiDung WHERE VaiTro='CUSTOMER' AND TrangThai=N'HOAT_DONG' ORDER BY MaNguoiDung");
        int ceo = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaNguoiDung FROM NguoiDung WHERE VaiTro='SUPER_ADMIN' AND TrangThai=N'HOAT_DONG' ORDER BY MaNguoiDung");
        int otherBranch = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaChiNhanh FROM ChiNhanh WHERE MaChiNhanh<>@branch ORDER BY MaChiNhanh", new { branch });
        // Chặn trước khi ghi nếu database có trigger mới với tác dụng phụ chưa được xét.
        var triggers = await conn.QueryAsync<string>(@"SELECT tr.name FROM sys.triggers tr
            WHERE tr.parent_id IN (OBJECT_ID('DonHang'),OBJECT_ID('ThanhToan'),OBJECT_ID('GiaoHang')) AND tr.is_disabled=0");
        Assert.All(triggers, name => Assert.Contains(name, new[] { "trg_DonHang_HoanThanh", "trg_DonHang_Huy" }));
        string marker = "KIEM_THU_GIAO_" + Guid.NewGuid().ToString("N");
        var ids = new List<int>();
        GiaoHangController Api(string role, int user, int? cn) {
            var claims = new List<Claim> { new(TenClaim.MaNguoiDung, user.ToString()), new(TenClaim.VaiTro, role) };
            if (cn != null) claims.Add(new(TenClaim.MaChiNhanh, cn.ToString()));
            return new GiaoHangController(config, cache, null, NullLogger<GiaoHangController>.Instance) {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture", ClaimTypes.Name, TenClaim.VaiTro))
                } }
            };
        }
        async Task<int> Tao(bool paid = true, string state = "DA_IN") {
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            int id = await conn.ExecuteScalarAsync<int>(@"SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
                INSERT DonHang(MaKhachHang,MaChiNhanh,HinhThucNhan,DiaChiGiao,SdtNhan,TienHang,PhiShip,TongTien,TrangThai,GhiChu)
                VALUES(@customer,@branch,'GIAO_HANG',N'ĐỊA CHỈ TEST KHÔNG GIAO THẬT','0900000000',20000,19000,39000,@state,@marker);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", new { customer, branch, state, marker }, tx);
            ids.Add(id);
            await conn.ExecuteAsync(@"INSERT ThanhToan(MaDonHang,PhuongThuc,SoTien,TrangThai,MaGiaoDich)
                VALUES(@id,'VIETQR',39000,@status,@marker)", new { id, marker, status = paid ? "DA_THANH_TOAN" : "CHUA_THANH_TOAN" }, tx);
            await tx.CommitAsync();
            return id;
        }
        async Task<int> Luot(int id) => await conn.ExecuteScalarAsync<int>(
            "SELECT TOP 1 MaGiaoHang FROM GiaoHang WHERE MaDonHang=@id ORDER BY MaGiaoHang DESC", new { id });
        async Task<int> Dem(string table, int id) {
            Assert.Contains(table, new[] { "GiaoHang", "ThongBao" });
            return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM " + table + " WHERE MaDonHang=@id", new { id });
        }
        async Task<string> TrangThai(int id) => await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM DonHang WHERE MaDonHang=@id", new { id });
        try
        {
            int unpaid = await Tao(false);
            Assert.IsType<ConflictObjectResult>(await Api(VaiTro.Shipper, ship1.MaNguoiDung, branch).NhanDon(unpaid));
            Assert.Equal(0, await Dem("GiaoHang", unpaid));
            int unfinished = await Tao(true, "DANG_IN");
            Assert.IsType<ConflictObjectResult>(await Api(VaiTro.Shipper, ship1.MaNguoiDung, branch).NhanDon(unfinished));
            Assert.Equal(0, await Dem("GiaoHang", unfinished));
            int ready = await Tao();
            Assert.IsType<NotFoundObjectResult>(await Api(VaiTro.BranchAdmin, ceo, otherBranch).PhanCong(ready, new PhanCongGiaoRequest { MaShipper=ship1.MaNguoiDung }));
            var ngoaiCn = ships.FirstOrDefault(x => x.MaChiNhanh != branch);
            if (ngoaiCn != null)
                Assert.IsType<BadRequestObjectResult>(await Api(VaiTro.SuperAdmin, ceo, null).PhanCong(ready, new PhanCongGiaoRequest { MaShipper=ngoaiCn.MaNguoiDung }));
            Task<IActionResult> first = Api(VaiTro.Shipper, ship1.MaNguoiDung, branch).NhanDon(ready);
            Task<IActionResult> second = cungChiNhanh.Count == 2
                ? Api(VaiTro.Shipper, cungChiNhanh[1].MaNguoiDung, branch).NhanDon(ready)
                : Api(VaiTro.SuperAdmin, ceo, null).PhanCong(ready, new PhanCongGiaoRequest { MaShipper=ship1.MaNguoiDung });
            var results = await Task.WhenAll(first, second);
            Assert.Equal(cungChiNhanh.Count == 2 ? 1 : 2, results.Count(x => x is OkObjectResult));
            if (cungChiNhanh.Count == 2) Assert.Single(results.OfType<ConflictObjectResult>());
            Assert.Equal(1, await Dem("GiaoHang", ready));
            Assert.Equal("DANG_GIAO", await TrangThai(ready));
            int assigned = await conn.ExecuteScalarAsync<int>("SELECT MaShipper FROM GiaoHang WHERE MaDonHang=@ready", new { ready });
            int attempt = await Luot(ready);
            if (ngoaiCn != null) Assert.IsType<NotFoundObjectResult>(await Api(VaiTro.Shipper, ngoaiCn.MaNguoiDung, ngoaiCn.MaChiNhanh).BaoKetQua(attempt, new KetQuaGiaoRequest { ThanhCong=true }));
            var owner = Api(VaiTro.Shipper, assigned, branch);
            Assert.IsType<OkObjectResult>(await owner.BaoKetQua(attempt, new KetQuaGiaoRequest { ThanhCong=true }));
            Assert.Equal("HOAN_THANH", await TrangThai(ready));
            int notices = await Dem("ThongBao", ready);
            Assert.IsType<OkObjectResult>(await owner.BaoKetQua(attempt, new KetQuaGiaoRequest { ThanhCong=true }));
            Assert.Equal(notices, await Dem("ThongBao", ready));
            Assert.NotNull(await conn.ExecuteScalarAsync<DateTime?>("SELECT NgayHoanThanh FROM DonHang WHERE MaDonHang=@ready", new { ready }));
            int failed = await Tao();
            var shipApi = Api(VaiTro.Shipper, ship1.MaNguoiDung, branch);
            for (int n=1; n<=3; n++) {
                Assert.IsType<OkObjectResult>(await shipApi.NhanDon(failed));
                int a = await Luot(failed);
                Assert.IsType<OkObjectResult>(await shipApi.BaoKetQua(a, new KetQuaGiaoRequest { ThanhCong=false, LyDo="TEST: khách không nghe máy" }));
                Assert.Equal("GIAO_THAT_BAI", await TrangThai(failed));
                int before = await Dem("ThongBao", failed);
                Assert.IsType<OkObjectResult>(await shipApi.BaoKetQua(a, new KetQuaGiaoRequest { ThanhCong=false, LyDo="TEST: khách không nghe máy" }));
                Assert.Equal(before, await Dem("ThongBao", failed));
                Assert.Equal(n, await Dem("GiaoHang", failed));
            }
            Assert.IsType<ConflictObjectResult>(await shipApi.NhanDon(failed));
            Assert.Equal(3, await Dem("GiaoHang", failed));
            Assert.Equal(0m, await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(TienThu),0) FROM GiaoHang WHERE MaDonHang IN @ids", new { ids }));
            Assert.Equal(39000m, await conn.ExecuteScalarAsync<decimal>("SELECT SoTien FROM ThanhToan WHERE MaDonHang=@failed", new { failed }));
            _output.WriteLine("PASS: 4 đơn test; chưa trả tiền/chưa in/bên ngoài phạm vi bị chặn; nhận đồng thời chỉ 1 lượt; thành công/lặp; 3 thất bại; không thu COD.");
        }
        finally
        {
            if (ids.Count > 0)
            {
                await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
                int foreign = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM DonHang WHERE MaDonHang IN @ids AND (GhiChu IS NULL OR GhiChu<>@marker)", new { ids, marker }, tx);
                Assert.Equal(0, foreign); // Không dọn nếu ID đã không còn đúng dấu sở hữu.
                await conn.ExecuteAsync(@"SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
                    DELETE tb FROM ThongBao tb JOIN DonHang dh ON dh.MaDonHang=tb.MaDonHang WHERE dh.MaDonHang IN @ids AND dh.GhiChu=@marker;
                    DELETE gh FROM GiaoHang gh JOIN DonHang dh ON dh.MaDonHang=gh.MaDonHang WHERE dh.MaDonHang IN @ids AND dh.GhiChu=@marker;
                    DELETE tt FROM ThanhToan tt JOIN DonHang dh ON dh.MaDonHang=tt.MaDonHang WHERE dh.MaDonHang IN @ids AND dh.GhiChu=@marker AND tt.MaGiaoDich=@marker;
                    DELETE FROM DonHang WHERE MaDonHang IN @ids AND GhiChu=@marker;", new { ids, marker }, tx);
                await tx.CommitAsync();
                Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DonHang WHERE GhiChu=@marker", new { marker }));
                _output.WriteLine("Đã dọn đúng các ID test đã ghi nhận và xác nhận không còn đơn test. Không reset identity.");
            }
        }
    }
    private sealed class NguoiGiao {
        public int MaNguoiDung { get; set; }
        public int MaChiNhanh { get; set; }
    }
}

public sealed class KiemThuGiaoHangGhiAttribute : FactAttribute
{
    public KiemThuGiaoHangGhiAttribute()
    {
        if (Environment.GetEnvironmentVariable("PHOTOCOPY_DELIVERY_WRITE_TESTS") != "YES" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHOTOCOPY_DELIVERY_ENV_FILE")))
            Skip = "Cần cho phép tạo/dọn dữ liệu test, PHOTOCOPY_DELIVERY_WRITE_TESTS=YES và file cấu hình riêng.";
    }
}
