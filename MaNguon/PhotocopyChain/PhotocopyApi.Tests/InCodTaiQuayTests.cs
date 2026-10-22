using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.MayInAo;
using PhotocopyApi.MayInAo;
using Microsoft.Data.SqlClient;
using Dapper;
using Xunit;

namespace PhotocopyApi.Tests;

public sealed class InCodTaiQuayTests
{
    [Fact]
    public async Task KhongChoWebBoQuaConnectorDeDanhDauDaIn()
    {
        var controller = new DonHangController(new ConfigurationBuilder().Build(), new MemoryCache(new MemoryCacheOptions()), null);
        var result = await controller.CapNhatTrangThaiIn(1, new CapNhatTrangThaiInRequest { TrangThaiMoi = "DA_IN" });
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task BatDauIn_PhaiChonMayTruoc_KhongChamDatabaseKhiThieuMay()
    {
        var controller = new DonHangController(new ConfigurationBuilder().Build(), new MemoryCache(new MemoryCacheOptions()), null);
        Assert.IsType<BadRequestObjectResult>(await controller.CapNhatTrangThaiIn(1, new() { TrangThaiMoi = "DANG_IN" }));
    }

    [Theory]
    [InlineData("COD", "CHUA_THANH_TOAN", "TAI_QUAY", true)]
    [InlineData("COD", "DA_THANH_TOAN", "TAI_QUAY", true)]
    [InlineData("COD", "DA_HOAN_TIEN", "TAI_QUAY", false)]
    [InlineData("COD", "CHUA_THANH_TOAN", "GIAO_HANG", false)]
    [InlineData("VIETQR", "CHUA_THANH_TOAN", "TAI_QUAY", false)]
    [InlineData("VIETQR", "DA_THANH_TOAN", "TAI_QUAY", true)]
    [InlineData("VIETQR", "DA_THANH_TOAN", "GIAO_HANG", true)]
    [InlineData("KHAC", "DA_THANH_TOAN", "TAI_QUAY", false)]
    public void DieuKien_CodQuayKhongCanTraTruoc_QrPhaiTraTruoc(string pt, string tt, string nhan, bool expected) =>
        Assert.Equal(expected, QuyTacMayInAo.DuocNhanLenh(pt, tt, nhan));

    [Fact]
    public void YeuCauCod_Persist_Idempotent_KhongTuNhanDonCuHoacDoiMay()
    {
        string dir = Path.Combine(Path.GetTempPath(), "photocopy-cod-" + Guid.NewGuid());
        var s = new YeuCauInCodStore(dir);
        Assert.Empty(s.DonChoMay(1));
        Assert.True(s.DangKy(5, 1)); Assert.True(s.DangKy(5, 1)); Assert.False(s.DangKy(5, 2));
        Assert.Single(s.DonChoMay(1));
        var restarted = new YeuCauInCodStore(dir);
        Assert.True(restarted.DaDangKy(5, 1)); Assert.False(restarted.DaDangKy(6, 1));
    }

    // Chỉ SELECT biểu thức nghiệp vụ với giá trị fixture, không dùng/sửa bất kỳ bảng hoặc đơn thật nào.
    [SqlDocFact]
    public async Task SqlNhanLenh_KhopThanhToanVaBatBuocYeuCauCodMoi()
    {
        var type = typeof(MayInAoController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var config = (Dictionary<string, string>)type.GetMethod("Doc").Invoke(null,
            new object[] { Environment.GetEnvironmentVariable("PHOTOCOPY_PRINT_READ_ENV_FILE") });
        using var db = new SqlConnection(config["ConnectionStrings:PhotocopyChain"]);
        foreach (var x in new[] {
            ("COD", "CHUA_THANH_TOAN", "TAI_QUAY", "[5]", true),
            ("COD", "CHUA_THANH_TOAN", "TAI_QUAY", "[]", false),
            ("COD", "CHUA_THANH_TOAN", "TAI_QUAY", "[6]", false),
            ("COD", "DA_HOAN_TIEN", "TAI_QUAY", "[5]", false),
            ("COD", "DA_THANH_TOAN", "TAI_QUAY", "[5]", true),
            ("COD", "CHUA_THANH_TOAN", "GIAO_HANG", "[5]", false),
            ("VIETQR", "CHUA_THANH_TOAN", "TAI_QUAY", "[]", false),
            ("VIETQR", "DA_THANH_TOAN", "GIAO_HANG", "[]", true)
        })
        {
            bool actual = await db.ExecuteScalarAsync<bool>("SELECT CAST(CASE WHEN " + MayInAoController.DieuKienNhanLenhSql
                + " THEN 1 ELSE 0 END AS bit) FROM (SELECT @pt AS PhuongThuc,@tt AS TrangThai) tt CROSS JOIN (SELECT @nhan AS HinhThucNhan,5 AS MaDonHang) dh",
                new { pt = x.Item1, tt = x.Item2, nhan = x.Item3, DonCodDaGui = x.Item4 });
            Assert.Equal(x.Item5, actual);
        }
    }
    private sealed class SqlDocFactAttribute : FactAttribute
    {
        public SqlDocFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PHOTOCOPY_PRINT_READ_ENV_FILE"))) Skip = "Cần file .env riêng để SELECT biểu thức SQL, không ghi dữ liệu."; }
    }
}
