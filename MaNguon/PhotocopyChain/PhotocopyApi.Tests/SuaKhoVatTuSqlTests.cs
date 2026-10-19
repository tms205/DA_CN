using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public class SuaKhoVatTuSqlTests
{
    [Fact]
    public void CoSqlSuaThongTinVaMucCanhBao()
    {
        Assert.NotEmpty(Sql("SqlCapNhatVatTu"));
        Assert.NotEmpty(Sql("SqlCapNhatCanhBao"));
    }

    [KiemThuTachGiay]
    public async Task SuaVatTu_ChongTrungVaKhoaDonViDaSuDung_KhongSuaTon()
    {
        // Mỗi trường hợp có bảng # mới riêng; không ghi/xóa vật tư thật.
        foreach (string truongHop in new[] { "chua-dung", "ton-kho", "phieu-nhap", "dich-vu", "trung-ten", "khong-co" })
        {
            using var conn = KetNoi();
            await conn.OpenAsync();
            await TaoBangTam(conn);
            if (truongHop == "ton-kho") await conn.ExecuteAsync("UPDATE #TonKho SET SoLuong=20 WHERE MaChiNhanh=2 AND MaVatTu=1");
            if (truongHop == "phieu-nhap") await conn.ExecuteAsync("INSERT #PhieuNhap VALUES(1)");
            if (truongHop == "dich-vu") await conn.ExecuteAsync("INSERT #DichVu VALUES(1)");
            await using var tx = await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            int ketQua = await conn.ExecuteScalarAsync<int>(Sql("SqlCapNhatVatTu"), new {
                MaVatTu = truongHop == "khong-co" ? 999 : 1,
                TenVatTu = truongHop == "trung-ten" ? "Giấy trùng" : "Giấy đã sửa",
                DonViTinh = "Hộp", DonGia = 150
            }, tx);
            Assert.Equal(truongHop switch { "chua-dung" => 200, "trung-ten" => 409, "khong-co" => 404, _ => 400 }, ketQua);
            var vt = await conn.QuerySingleAsync("SELECT * FROM #VatTu WHERE MaVatTu=1", transaction: tx);
            Assert.Equal(truongHop == "chua-dung" ? "Giấy đã sửa" : "Giấy thử", (string)vt.TenVatTu);
            Assert.Equal(truongHop == "chua-dung" ? 150m : 100m, (decimal)vt.DonGia);
            Assert.Equal(truongHop == "ton-kho" ? 20 : 0,
                await conn.ExecuteScalarAsync<int>("SELECT SUM(SoLuong) FROM #TonKho", transaction: tx));
            Assert.Equal(11, await conn.ExecuteScalarAsync<int>("SELECT SUM(MucToiThieu) FROM #TonKho", transaction: tx));
            // Vật tư có lịch sử vẫn được sửa tên/giá khi giữ nguyên đơn vị.
            if (truongHop is "ton-kho" or "phieu-nhap" or "dich-vu")
                Assert.Equal(200, await conn.ExecuteScalarAsync<int>(Sql("SqlCapNhatVatTu"),
                    new { MaVatTu = 1, TenVatTu = "Giấy đổi tên", DonViTinh = "Tờ", DonGia = 180 }, tx));
            await tx.RollbackAsync();
        }
    }

    [KiemThuTachGiay]
    public async Task MucCanhBao_ChiSuaDungCapChiNhanhVatTu_KhongSuaSoLuong()
    {
        using var conn = KetNoi();
        await conn.OpenAsync();
        await TaoBangTam(conn);
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>(Sql("SqlCapNhatCanhBao"), new { MaChiNhanh = 1, MaVatTu = 1, MucToiThieu = 50 }));
        Assert.Equal(50, await conn.ExecuteScalarAsync<int>("SELECT MucToiThieu FROM #TonKho WHERE MaChiNhanh=1 AND MaVatTu=1"));
        Assert.Equal(6, await conn.ExecuteScalarAsync<int>("SELECT MucToiThieu FROM #TonKho WHERE MaChiNhanh=2 AND MaVatTu=1"));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT SUM(SoLuong) FROM #TonKho"));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(Sql("SqlCapNhatCanhBao"), new { MaChiNhanh = 99, MaVatTu = 1, MucToiThieu = 50 }));
    }

    private static string Sql(string ten)
    {
        var field = typeof(QuanTriController).GetField(ten, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        string sql = (string)field.GetRawConstantValue();
        foreach (string bang in new[] { "VatTu", "TonKho", "PhieuNhap", "DichVu" })
            sql = sql.Replace("dbo." + bang, "#" + bang);
        return sql;
    }

    private static SqlConnection KetNoi()
    {
        var doc = typeof(QuanTriController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var config = (Dictionary<string, string>)doc.GetMethod("Doc").Invoke(null,
            new object[] { Environment.GetEnvironmentVariable("PHOTOCOPY_REPORT_ENV_FILE") });
        return new SqlConnection(config["ConnectionStrings:PhotocopyChain"]);
    }

    private static Task TaoBangTam(SqlConnection conn) => conn.ExecuteAsync(@"
        CREATE TABLE #VatTu(MaVatTu INT PRIMARY KEY,TenVatTu NVARCHAR(100) COLLATE DATABASE_DEFAULT,DonViTinh NVARCHAR(20) COLLATE DATABASE_DEFAULT,DonGia DECIMAL(12,0));
        CREATE TABLE #TonKho(MaChiNhanh INT,MaVatTu INT,SoLuong INT,MucToiThieu INT,PRIMARY KEY(MaChiNhanh,MaVatTu));
        CREATE TABLE #PhieuNhap(MaVatTu INT);
        CREATE TABLE #DichVu(MaVatTu INT);
        INSERT #VatTu VALUES(1,N'Giấy thử',N'Tờ',100),(2,N'Giấy trùng',N'Tờ',200);
        INSERT #TonKho VALUES(1,1,0,5),(2,1,0,6);");
}
