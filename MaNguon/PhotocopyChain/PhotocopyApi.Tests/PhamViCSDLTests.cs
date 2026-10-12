using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using PhotocopyApi.MayInAo;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

// Chỉ thử các request phải bị từ chối, không tạo/sửa đơn hay giả giao dịch thanh toán.
public class PhamViCSDLTests
{
    private static IConfiguration CauHinh() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string> { ["ConnectionStrings:PhotocopyChain"] = Environment.GetEnvironmentVariable("PHOTOCOPY_TEST_DB") }).Build();

    private static void CEOChonChiNhanh(ControllerBase controller, int maChiNhanh)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(TenClaim.MaNguoiDung, "1"),
                    new Claim(TenClaim.VaiTro, VaiTro.BranchAdmin),
                    new Claim(TenClaim.VaiTro, VaiTro.SuperAdmin),
                    new Claim(TenClaim.MaChiNhanh, maChiNhanh.ToString())
                }, "test"))
            }
        };
    }

    [KiemThuCSDL]
    public async Task Don6DaTraTienNhungMatFile_KhongBatDauIn()
    {
        var config = CauHinh();
        using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        var truoc = (await conn.QueryAsync<int>("SELECT SoLuong FROM TonKho ORDER BY MaChiNhanh,MaVatTu")).ToArray();
        var service = new TuDongInService(config, null, NullLogger<TuDongInService>.Instance, new TrangThaiMayInAoStore());
        var ketQua = await service.XepInSauThanhToan(6);
        Assert.False(ketQua.DaBatDauIn);
        Assert.Equal("DA_XAC_NHAN", await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM DonHang WHERE MaDonHang=6"));
        Assert.Equal(truoc, (await conn.QueryAsync<int>("SELECT SoLuong FROM TonKho ORDER BY MaChiNhanh,MaVatTu")).ToArray());
    }

    [KiemThuCSDL]
    public async Task DonMau_ChiChonMayMauDangKetNoiCungChiNhanh()
    {
        using var conn = new SqlConnection(CauHinh().GetConnectionString("PhotocopyChain"));
        await conn.OpenAsync();
        await using var giaoDich = (SqlTransaction)await conn.BeginTransactionAsync();
        var ham = typeof(TuDongInService).GetMethod("ChonMayIn", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(ham);
        var coMayMau = await (Task<int?>)ham.Invoke(null, new object[] { conn, giaoDich, 6, null, new[] { 1, 2, 3, 4 } });
        Assert.Equal(2, coMayMau);
        var mayMauOffline = await (Task<int?>)ham.Invoke(null, new object[] { conn, giaoDich, 6, null, new[] { 1, 3, 4 } });
        Assert.Null(mayMauOffline);
    }

    [KiemThuCSDL]
    public async Task CEOChonChiNhanh1_KhongSuaMayChiNhanh2()
    {
        var config = CauHinh();
        using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        int maMay = await conn.QueryFirstAsync<int>("SELECT MaMayIn FROM MayIn WHERE MaChiNhanh=2");
        var controller = new QuanTriController(config);
        CEOChonChiNhanh(controller, 1);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.CapNhatMayIn(maMay,
            new CapNhatMayInRequest { TrangThai = "HOAT_DONG" })).StatusCode);
    }

    [KiemThuCSDL]
    public async Task CEOChonChiNhanh2_KhongPhucHoiFileDon6()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new DonHangController(CauHinh(), cache, null);
        CEOChonChiNhanh(controller, 2);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.PhucHoiFileIn(6, 6,
            new PhucHoiFileInRequest { MaFileIn = 13 }, null)).StatusCode);
    }

    [KiemThuCSDL]
    public async Task CEOChonChiNhanh2_KhongHoanTienDon6()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new DonHangController(CauHinh(), cache, null);
        CEOChonChiNhanh(controller, 2);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.HoanTien(6)).StatusCode);
    }

    [KiemThuCSDL]
    public async Task CEOChonChiNhanh2_KhongKhoaKhachChiCoDonChiNhanh1()
    {
        var controller = new QuanTriController(CauHinh());
        CEOChonChiNhanh(controller, 2);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.CapNhatKhachHang(12,
            new CapNhatTrangThaiRequest { TrangThai = "HOAT_DONG" })).StatusCode);
    }
}

public sealed class KiemThuCSDLAttribute : FactAttribute
{
    public KiemThuCSDLAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHOTOCOPY_TEST_DB")))
            Skip = "Cần PHOTOCOPY_TEST_DB và bộ dữ liệu mẫu chi nhánh 1/2, đơn 6 để chạy kiểm thử CSDL.";
    }
}
