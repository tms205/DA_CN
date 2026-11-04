using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace PhotocopyApi.Tests;

// Chạy thân thủ tục trên bảng # của riêng kết nối; không sửa ca hoặc schema nghiệp vụ.
public class CeoDongCaSqlTests
{
    private sealed class SqlTemp : FactAttribute
    {
        public SqlTemp() { if (Environment.GetEnvironmentVariable("PHOTOCOPY_2MIN_SQL_TEST") != "1") Skip="Opt-in: bảng tạm đóng ca."; }
    }
    private sealed class SqlTheory : TheoryAttribute
    {
        public SqlTheory() { if (Environment.GetEnvironmentVariable("PHOTOCOPY_2MIN_SQL_TEST") != "1") Skip="Opt-in: bảng tạm đóng ca."; }
    }
    private static readonly DateTime Now = new(2026,11,4,11,0,0);
    private static async Task<SqlConnection> Db(string role="SUPER_ADMIN")
    {
        var db=new SqlConnection(@"Server=.\MSSQLSERVER01;Database=PhotocopyChain;Integrated Security=True;TrustServerCertificate=True");
        await db.OpenAsync();
        await db.ExecuteAsync(@"SET QUOTED_IDENTIFIER ON;SET ANSI_NULLS ON;SET ANSI_WARNINGS ON;SET ANSI_PADDING ON;SET ARITHABORT ON;SET CONCAT_NULL_YIELDS_NULL ON;SET NUMERIC_ROUNDABORT OFF;
            CREATE TABLE #NguoiDung(MaNguoiDung INT PRIMARY KEY,VaiTro VARCHAR(20),TrangThai VARCHAR(20));
            CREATE TABLE #CaLamViec(MaCa INT PRIMARY KEY,MaNhanVien INT,TrangThai VARCHAR(20),GioMoCa DATETIME,GioDongCa DATETIME NULL,
                TienDauCa DECIMAL(12,0),DoanhThuTrongCa DECIMAL(12,0),TongDoanhThuCuoiCa DECIMAL(12,0) NULL,
                TienQuanLyDemCuoiCa DECIMAL(12,0) NULL,TienChi DECIMAL(12,0),TienBu DECIMAL(12,0),MaNguoiXacNhan INT NULL,GhiChu NVARCHAR(255));
            CREATE UNIQUE INDEX UX_TempOpen ON #CaLamViec(MaNhanVien) WHERE TrangThai='DANG_MO';
            INSERT #CaLamViec VALUES(1,1,'DANG_MO','20261104 08:00',NULL,200000,100000,NULL,NULL,0,0,NULL,NULL);");
        await db.ExecuteAsync("INSERT #NguoiDung VALUES(1,@Role,'HOAT_DONG')",new {Role=role});
        return db;
    }
    private static string Body()
    {
        var root=new DirectoryInfo(Environment.GetEnvironmentVariable("PHOTOCOPY_SOURCE_ROOT")??Directory.GetCurrentDirectory());
        while(root!=null&&!Directory.Exists(Path.Combine(root.FullName,"Database")))root=root.Parent;
        string sql=File.ReadAllText(Path.Combine(root.FullName,"Database","024-CeoTuChotCa.sql"));
        string body=Regex.Match(sql,@"\nAS\s*\n(?<body>BEGIN.*?\nEND;)\s*\nGO",RegexOptions.Singleline).Groups["body"].Value;
        Assert.NotEmpty(body);
        foreach(string table in new[]{"CaLamViec","NguoiDung"})
            body=Regex.Replace(body,@"(?<![@\w])dbo\."+table+@"\b","#"+table);
        return body.Replace("GETDATE()","@FakeNow");
    }
    private static Task<bool> Close(SqlConnection db,decimal? counted,string note=null,DateTime? now=null) =>
        db.ExecuteScalarAsync<bool>(Body(),new {MaCa=1,TongDoanhThuCuoiCa=counted,GhiChu=note,FakeNow=now??Now});

    [SqlTheory]
    [InlineData(0)]
    [InlineData(300000)]
    [InlineData(299000)]
    [InlineData(301000)]
    public async Task CeoChotNgay_GiuTienVaLyDo_KhongGiaLapNguoiKiemDem(decimal counted)
    {
        using var db=await Db();
        string note=counted==300000?null:"  Chênh lệch đã được CEO kiểm tra.  ";
        Assert.True(await Close(db,counted,note));
        var ca=await db.QuerySingleAsync("SELECT * FROM #CaLamViec");
        Assert.Equal("DA_XAC_NHAN",(string)ca.TrangThai);
        Assert.Equal(counted,(decimal)ca.TongDoanhThuCuoiCa);
        Assert.Equal(note?.Trim(),(string)ca.GhiChu);
        Assert.Null(ca.MaNguoiXacNhan);Assert.Null(ca.TienQuanLyDemCuoiCa);
        Assert.Equal(0m,(decimal)ca.TienBu);Assert.Equal(0m,(decimal)ca.TienChi);
        Assert.Equal(200000m,(decimal)ca.TienDauCa);Assert.Equal(100000m,(decimal)ca.DoanhThuTrongCa);
        Assert.Equal(Now,(DateTime)ca.GioDongCa);
        Assert.Equal(0,await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #CaLamViec WHERE TrangThai='CHO_XAC_NHAN'"));
    }

    [SqlTheory]
    [InlineData("STAFF")]
    [InlineData("BRANCH_ADMIN")]
    public async Task NhanVienVaQuanLyVanChoDoiSoat(string role)
    {
        using var db=await Db(role);
        Assert.False(await Close(db,299000,"Nhân viên ghi lý do thiếu tiền."));
        Assert.Equal("CHO_XAC_NHAN",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #CaLamViec"));
        Assert.Null(await db.ExecuteScalarAsync<int?>("SELECT MaNguoiXacNhan FROM #CaLamViec"));
    }
    [SqlTheory]
    [InlineData(299000,null)]
    [InlineData(301000,"   ")]
    public async Task CEOThieuHoacThuaBatBuocLyDo_VaLoiKhongDongCa(decimal counted,string note)
    {
        using var db=await Db();
        Assert.Equal(50057,(await Assert.ThrowsAsync<SqlException>(()=>Close(db,counted,note))).Number);
        Assert.Equal("DANG_MO",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #CaLamViec"));
        Assert.Null(await db.ExecuteScalarAsync<decimal?>("SELECT TongDoanhThuCuoiCa FROM #CaLamViec"));
        Assert.Null(await db.ExecuteScalarAsync<DateTime?>("SELECT GioDongCa FROM #CaLamViec"));
    }
    [SqlTheory]
    [InlineData(null)]
    [InlineData(-1)]
    public async Task TuChoiTienThieuHoacAm(int? counted)
    {
        using var db=await Db();
        Assert.Equal(50053,(await Assert.ThrowsAsync<SqlException>(()=>Close(db,counted.HasValue?(decimal?)counted.Value:null))).Number);
    }
    [SqlTemp]
    public async Task HetGioChoTongKet_GiuMocKetCa_VaKhongTheDongLanHai()
    {
        using var db=await Db();var end=Now.Date.AddHours(12).AddMinutes(30);
        await db.ExecuteAsync("UPDATE #CaLamViec SET TrangThai='CHO_TONG_KET',GioDongCa=@End",new {End=end});
        Assert.True(await Close(db,299000,"Thiếu tiền có ghi nhận nguyên nhân.",end.AddHours(1)));
        Assert.Equal(end,await db.ExecuteScalarAsync<DateTime>("SELECT GioDongCa FROM #CaLamViec"));
        Assert.Equal(50053,(await Assert.ThrowsAsync<SqlException>(()=>Close(db,301000,"Không được ghi đè lần đầu.",end.AddHours(2)))).Number);
        Assert.Equal(299000m,await db.ExecuteScalarAsync<decimal>("SELECT TongDoanhThuCuoiCa FROM #CaLamViec"));
    }
    [SqlTemp]
    public async Task CaThongDemVanKetThuc1230NgayHomSau()
    {
        using var db=await Db();await db.ExecuteAsync("UPDATE #CaLamViec SET GioMoCa='20261103 22:00'");
        Assert.True(await Close(db,300000,now:Now.Date.AddHours(13)));
        Assert.Equal(Now.Date.AddHours(12).AddMinutes(30),await db.ExecuteScalarAsync<DateTime>("SELECT GioDongCa FROM #CaLamViec"));
    }
}
