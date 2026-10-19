using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public class ThemVatTuSqlTests
{
    [Fact]
    public void CoCauLenhTaoVatTu_DungChungVoiKiemThuSql()
    {
        Assert.False(string.IsNullOrWhiteSpace(SqlTaoVatTu()));
    }

    // Chỉ tạo bảng # riêng, không tạo/sửa/xóa vật tư hoặc tồn kho nghiệp vụ thật.
    [KiemThuTachGiay]
    public async Task ThemVatTu_KhoMoiBang0_ChongTrung_KhongSuaTonVaGiaCu()
    {
        var doc = typeof(QuanTriController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var config = (Dictionary<string, string>)doc.GetMethod("Doc").Invoke(null,
            new object[] { Environment.GetEnvironmentVariable("PHOTOCOPY_REPORT_ENV_FILE") });
        using var conn = new SqlConnection(config["ConnectionStrings:PhotocopyChain"]);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            CREATE TABLE #VatTu(MaVatTu INT IDENTITY PRIMARY KEY,TenVatTu NVARCHAR(100) COLLATE DATABASE_DEFAULT,DonViTinh NVARCHAR(20),DonGia DECIMAL(12,0));
            CREATE TABLE #ChiNhanh(MaChiNhanh INT PRIMARY KEY);
            CREATE TABLE #TonKho(MaChiNhanh INT,MaVatTu INT,SoLuong INT,MucToiThieu INT,PRIMARY KEY(MaChiNhanh,MaVatTu));
            INSERT #ChiNhanh VALUES (1),(2);
            INSERT #VatTu VALUES (N'Giấy cũ',N'Tờ',100);
            INSERT #TonKho VALUES (1,1,700,50),(2,1,250,20);");
        string sql = SqlTaoVatTu().Replace("dbo.VatTu", "#VatTu").Replace("dbo.TonKho", "#TonKho").Replace("dbo.ChiNhanh", "#ChiNhanh");
        var req = new { TenVatTu = "Giấy A4 100gsm", DonViTinh = "Tờ", DonGia = 140 };
        await using var tx = await conn.BeginTransactionAsync();
        int ma = await conn.ExecuteScalarAsync<int>(sql, req, tx);
        Assert.True(ma > 1);
        var ton = (await conn.QueryAsync<int>("SELECT SoLuong FROM #TonKho WHERE MaVatTu=@ma", new { ma }, tx)).ToList();
        Assert.Equal(new[] { 0, 0 }, ton);
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(sql, req, tx));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(sql, new { req.TenVatTu, DonViTinh = "Ram", DonGia = 70000 }, tx));
        Assert.Equal(2, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #VatTu", transaction: tx));
        Assert.Equal(950, await conn.ExecuteScalarAsync<int>("SELECT SUM(SoLuong) FROM #TonKho WHERE MaVatTu=1", transaction: tx));
        Assert.Equal(100m, await conn.ExecuteScalarAsync<decimal>("SELECT DonGia FROM #VatTu WHERE MaVatTu=1", transaction: tx));
        Assert.Equal(140m, await conn.ExecuteScalarAsync<decimal>("SELECT DonGia FROM #VatTu WHERE MaVatTu=@ma", new { ma }, tx));
        await tx.RollbackAsync();
    }

    private static string SqlTaoVatTu()
    {
        var field = typeof(QuanTriController).GetField("SqlTaoVatTu", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return (string)field.GetRawConstantValue();
    }
}
