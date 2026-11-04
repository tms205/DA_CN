using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.CaLamViec;
using Xunit;

namespace PhotocopyApi.Tests;

// SQL thủ tục chạy trên # bảng tạm trong phiên, không gọi thủ tục/bảng nghiệp vụ thật.
public class CaThongDemSqlTests
{
    private sealed class SqlCaDemAttribute:FactAttribute {
        public SqlCaDemAttribute() { if(Environment.GetEnvironmentVariable("PHOTOCOPY_2MIN_SQL_TEST")!="1")Skip="Opt-in: # tables only."; }
    }
    private static async Task<SqlConnection> Db()
    {
        var db=new SqlConnection(@"Server=.\MSSQLSERVER01;Database=PhotocopyChain;Integrated Security=True;TrustServerCertificate=True");
        await db.OpenAsync();
        await db.ExecuteAsync(@"CREATE TABLE #NguoiDung(MaNguoiDung INT,MaChiNhanh INT,VaiTro VARCHAR(20),TrangThai VARCHAR(20));
            CREATE TABLE #ChiNhanh(MaChiNhanh INT,TrangThai VARCHAR(20));
            CREATE TABLE #CaLamViec(MaCa INT IDENTITY PRIMARY KEY,MaNhanVien INT,MaChiNhanh INT,TrangThai VARCHAR(20) DEFAULT 'DANG_MO',GioMoCa DATETIME,GioDongCa DATETIME NULL,TienDauCa DECIMAL(12,0),DoanhThuTrongCa DECIMAL(12,0) DEFAULT 0,TongDoanhThuCuoiCa DECIMAL(12,0) NULL,TienQuanLyDemCuoiCa DECIMAL(12,0) NULL,TienChi DECIMAL(12,0) DEFAULT 0,TienBu DECIMAL(12,0) DEFAULT 0,MaNguoiXacNhan INT NULL,GhiChu NVARCHAR(255));
            CREATE TABLE #DonHang(MaDonHang INT,MaChiNhanh INT,TrangThai VARCHAR(20),HinhThucNhan VARCHAR(20),NgayHoanThanh DATETIME);
            CREATE TABLE #ThanhToan(MaDonHang INT,PhuongThuc VARCHAR(10),TrangThai VARCHAR(20),SoTien DECIMAL(12,0),MaCa INT,NgayThanhToan DATETIME);
            CREATE TABLE #ThongBao(MaNguoiDung INT,MaDonHang INT,TieuDe NVARCHAR(255),NoiDung NVARCHAR(2000));
            INSERT #NguoiDung VALUES(1,1,'STAFF','HOAT_DONG');INSERT #ChiNhanh VALUES(1,'HOAT_DONG');
            INSERT #DonHang VALUES(11,1,'DA_IN','TAI_QUAY',NULL);INSERT #ThanhToan VALUES(11,'COD','CHUA_THANH_TOAN',100,NULL,NULL);");
        return db;
    }
    private static string Rewrite(string sql)
    {
        foreach(var n in new[]{"NguoiDung","ChiNhanh","CaLamViec","DonHang","ThanhToan","ThongBao"})
            sql=Regex.Replace(sql,@"(?<![@\w])(?:dbo\.)?"+n+@"\b","#"+n);
        return sql.Replace("GETDATE()","@FakeNow");
    }
    private static string Body(string name)
    {
        var dir=new DirectoryInfo(Environment.GetEnvironmentVariable("PHOTOCOPY_SOURCE_ROOT")??Directory.GetCurrentDirectory());
        while(dir!=null&&!Directory.Exists(Path.Combine(dir.FullName,"Database")))dir=dir.Parent;
        var text=File.ReadAllText(Path.Combine(dir.FullName,"Database","021-HuyTruocTiepNhan_CaThongDem.sql"));
        var m=Regex.Match(text,@"CREATE OR ALTER PROCEDURE (?:dbo\.)?"+name+@"\b.*?\nAS\s*\n(?<body>BEGIN\b.*?\nEND;)\s*\nGO",RegexOptions.Singleline);
        Assert.True(m.Success,name);return Rewrite(m.Groups["body"].Value);
    }
    private static async Task<int> Open(SqlConnection db,DateTime now)
    {
        var p=new DynamicParameters(new{MaNhanVien=1,TienDauCa=200000,MaChiNhanhThayThe=(int?)null,FakeNow=now});
        p.Add("MaCa",dbType:DbType.Int32,direction:ParameterDirection.InputOutput,value:0);
        await db.ExecuteAsync(Body("sp_MoCa"),p);return p.Get<int>("MaCa");
    }
    [SqlCaDem]
    public async Task MigrationSettingsAllowStoredProcedureInsertIntoFilteredIndex()
    {
        await using var db = await Db();
        await db.ExecuteAsync("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; CREATE UNIQUE INDEX UX_TestCa ON #CaLamViec(MaNhanVien) WHERE TrangThai='DANG_MO';");
        // Reproduce the INSERT and persisted module options; the tests below exercise the full shift logic.
        var definition = "CREATE PROCEDURE #TestMoCa @MaNhanVien INT, @TienDauCa DECIMAL(12,0), @MaCa INT OUTPUT, @FakeNow DATETIME AS BEGIN SET NOCOUNT ON; INSERT #CaLamViec(MaChiNhanh,MaNhanVien,TienDauCa,GioMoCa) VALUES(1,@MaNhanVien,@TienDauCa,@FakeNow); SET @MaCa=SCOPE_IDENTITY(); END;";
        async Task ExecuteOpen()
        {
            var p = new DynamicParameters(new { MaNhanVien=1, TienDauCa=200000, FakeNow=new DateTime(2026,11,3,22,0,0) });
            p.Add("MaCa", dbType:DbType.Int32, direction:ParameterDirection.Output);
            await db.ExecuteAsync("#TestMoCa", p, commandType:CommandType.StoredProcedure);
            Assert.True(p.Get<int>("MaCa") > 0);
        }
        // Caller settings do not repair the settings captured when a module was created.
        await db.ExecuteAsync("SET QUOTED_IDENTIFIER OFF;");
        await db.ExecuteAsync(definition);
        await db.ExecuteAsync("SET QUOTED_IDENTIFIER ON;");
        var error = await Assert.ThrowsAsync<SqlException>(ExecuteOpen);
        Assert.Equal(1934, error.Number);
        await db.ExecuteAsync("DROP PROCEDURE #TestMoCa; SET QUOTED_IDENTIFIER OFF;");

        var dir = new DirectoryInfo(Environment.GetEnvironmentVariable("PHOTOCOPY_SOURCE_ROOT") ?? Directory.GetCurrentDirectory());
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName,"Database"))) dir=dir.Parent;
        var migration = File.ReadAllText(Path.Combine(dir!.FullName,"Database","021-HuyTruocTiepNhan_CaThongDem.sql"));
        var settings = Regex.Match(migration, @"GO\s*(?<settings>.*?)BEGIN TRAN;", RegexOptions.Singleline);
        Assert.True(settings.Success);
        await db.ExecuteAsync(settings.Groups["settings"].Value);
        await db.ExecuteAsync(definition);
        await db.ExecuteAsync("SET QUOTED_IDENTIFIER ON;");
        await ExecuteOpen();
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #CaLamViec"));
    }
    [SqlCaDem]
    public async Task NightShiftSurvivesMidnightAndMorningLogin_ClosesAt1230()
    {
        await using var db=await Db();var start=new DateTime(2026,11,3,22,0,0);
        int ca=await Open(db,start);
        Assert.Equal(ca,await Open(db,start.Date.AddDays(1).AddHours(1)));
        Assert.Equal(ca,await Open(db,start.Date.AddDays(1).AddHours(8)));
        Assert.Equal(1,await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #CaLamViec"));
        var end=start.Date.AddDays(1).AddHours(12).AddMinutes(30);
        await db.ExecuteAsync(Rewrite(LichCaService.SqlChot),new{BayGio=end.AddSeconds(-1),MaNhanVien=(int?)1});
        Assert.Equal("DANG_MO",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #CaLamViec"));
        await db.ExecuteAsync(Rewrite(LichCaService.SqlChot),new{BayGio=end,MaNhanVien=(int?)1});
        Assert.Equal("CHO_TONG_KET",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #CaLamViec"));
        Assert.Equal(end,await db.ExecuteScalarAsync<DateTime>("SELECT GioDongCa FROM #CaLamViec"));
        Assert.Null(await db.ExecuteScalarAsync<decimal?>("SELECT TongDoanhThuCuoiCa FROM #CaLamViec"));
    }
    [SqlCaDem]
    public async Task NightCodCanCollectOnce_ExpiredShiftCannotCollect()
    {
        await using(var db=await Db()) {
            var now=new DateTime(2026,11,3,23,0,0);int ca=await Open(db,now);
            await db.ExecuteAsync(Body("sp_GiaoTaiQuay"),new{MaDonHang=11,MaNhanVien=1,FakeNow=now.AddMinutes(5)});
            Assert.Equal(100m,await db.ExecuteScalarAsync<decimal>("SELECT DoanhThuTrongCa FROM #CaLamViec"));
            Assert.Equal(ca,await db.ExecuteScalarAsync<int>("SELECT MaCa FROM #ThanhToan"));
        }
        await using(var db=await Db()) {
            await Open(db,new DateTime(2026,11,4,1,0,0));
            var ex=await Assert.ThrowsAsync<SqlException>(()=>db.ExecuteAsync(Body("sp_GiaoTaiQuay"),new{MaDonHang=11,MaNhanVien=1,FakeNow=new DateTime(2026,11,4,12,30,0)}));
            Assert.Equal(50028,ex.Number);
            Assert.Equal("CHUA_THANH_TOAN",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #ThanhToan"));
        }
    }
}
