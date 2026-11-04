using System.Reflection;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Controllers;
using Xunit;
namespace PhotocopyApi.Tests;
public class LuuBienLaiHoanTienSqlTests
{
    private sealed class SqlTempAttribute:FactAttribute {public SqlTempAttribute(){if(Environment.GetEnvironmentVariable("PHOTOCOPY_2MIN_SQL_TEST")!="1")Skip="Opt-in; only session temporary table.";}}
    [SqlTemp]
    public async Task AddingReceiptPreservesCancellationAuditAndNeverTouchesPayments()
    {
        await using var db=new SqlConnection(@"Server=.\MSSQLSERVER01;Database=PhotocopyChain;Integrated Security=True;TrustServerCertificate=True");await db.OpenAsync();
        await db.ExecuteAsync("CREATE TABLE #DonHang(MaDonHang INT PRIMARY KEY,BangChungHuyJson NVARCHAR(MAX) NULL CHECK(BangChungHuyJson IS NULL OR ISJSON(BangChungHuyJson)=1));");
        await db.ExecuteAsync("INSERT #DonHang VALUES(91,@json)",new {json="{\"Loai\":\"HUY_TRUOC_TIEP_NHAN\",\"LyDo\":\"Lý do hủy cũ\",\"AuditKhac\":123,\"Anh\":[]}"});
        var sql=(string)typeof(DonHangController).GetField("SqlLuuBienLaiHoanTien",BindingFlags.Static|BindingFlags.NonPublic)!.GetRawConstantValue()!;
        Assert.DoesNotContain("ThanhToan",sql);Assert.DoesNotContain("sp_HoanTien",sql);
        await db.ExecuteAsync(sql.Replace("UPDATE DonHang","UPDATE #DonHang"),new {id=91,json=JsonSerializer.Serialize(new {LyDo="Cửa hàng đã chuyển hoàn",MaChuyenKhoan="TEST-ONLY",Anh=new {MaAnh=new string('a',32)}})});
        using var doc=JsonDocument.Parse(await db.ExecuteScalarAsync<string>("SELECT BangChungHuyJson FROM #DonHang WHERE MaDonHang=91"));
        Assert.Equal("HUY_TRUOC_TIEP_NHAN",doc.RootElement.GetProperty("Loai").GetString());
        Assert.Equal(123,doc.RootElement.GetProperty("AuditKhac").GetInt32());
        Assert.Equal("Lý do hủy cũ",doc.RootElement.GetProperty("LyDo").GetString());
        Assert.Equal("Cửa hàng đã chuyển hoàn",doc.RootElement.GetProperty("BienLaiHoanTien").GetProperty("LyDo").GetString());
    }
}
