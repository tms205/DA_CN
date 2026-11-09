using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyApi.DichVu;
using PhotocopyApi.Hubs;
using Xunit;
namespace PhotocopyApi.Tests;
public class AnToanGiaoDichTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void TongTienDuoiMotNghin_BiTuChoi(int tongTien)
    {
        var loi = Assert.Throws<ValidationException>(() => TaoDonHangService.KiemTraTongTienToiThieu(tongTien));
        Assert.Contains("1.000đ", loi.Message);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    public void TongTienTuMotNghin_DatNguong(int tongTien)
    {
        TaoDonHangService.KiemTraTongTienToiThieu(tongTien);
    }

    [Fact]
    public void FingerprintBindsActorChannelScopeAndContent()
    {
        string first=TaoDonHangService.DauYeuCau(1,"APP",2,new {file=3,note="a"});
        Assert.Equal(first,TaoDonHangService.DauYeuCau(1,"APP",2,new {file=3,note="a"}));
        Assert.NotEqual(first,TaoDonHangService.DauYeuCau(2,"APP",2,new {file=3,note="a"}));
        Assert.NotEqual(first,TaoDonHangService.DauYeuCau(1,"QUAY",2,new {file=3,note="a"}));
        Assert.NotEqual(first,TaoDonHangService.DauYeuCau(1,"APP",3,new {file=3,note="a"}));
        Assert.NotEqual(first,TaoDonHangService.DauYeuCau(1,"APP",2,new {file=3,note="b"}));
    }
    private sealed class BrokenHub:IHubContext<CapNhatHub> {
        public IHubClients Clients=>throw new IOException("realtime disconnected");
        public IGroupManager Groups=>throw new IOException();
    }
    [Fact]
    public async Task PostCommitRealtimeOrSnapshotFailureDoesNotFailSavedOperation()
    {
        using var cache=new MemoryCache(new MemoryCacheOptions());
        var controller=new DonHangController(new ConfigurationBuilder().Build(),cache,new BrokenHub()){
            ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext()}};
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        await (Task)typeof(DonHangController).GetMethod("BaoTaoDonAnToan",flags).Invoke(controller,new object[]{1,new TinCapNhat {MaDonHang=1}});
        using var invalid=new SqlConnection();
        await (Task)typeof(DonHangController).GetMethod("BaoDonDoiTrangThai",flags).Invoke(controller,new object[]{invalid,1,"saved"});
    }
    [Fact]
    public async Task ActiveUploadLeaseCannotBeCleanedEvenWhenOld()
    {
        string root=Path.Combine(Path.GetTempPath(),"upload-lease-"+Guid.NewGuid().ToString("N"));
        var paths=DuongDanTaiLen.Tao(root,DateTime.Now,"pdf");
        using(var lease=new LuotTaiFile(paths.ThuMuc)) {
            File.WriteAllText(paths.Pdf,"test");
            File.SetLastWriteTimeUtc(Path.Combine(paths.ThuMuc,LuotTaiFile.TenMarker),DateTime.UtcNow.AddHours(-2));
            await LuotTaiFile.DonBoDo(root,"",default);
            Assert.True(File.Exists(paths.Pdf));
            Assert.Throws<IOException>(()=>new FileStream(Path.Combine(paths.ThuMuc,LuotTaiFile.TenMarker),FileMode.Open,FileAccess.ReadWrite,FileShare.None));
            lease.HoanTat();
        }
        Assert.False(File.Exists(Path.Combine(paths.ThuMuc,LuotTaiFile.TenMarker)));
        Directory.Delete(root,true);
    }
}
public class YeuCauTaoDonSqlTests
{
    private sealed class SqlTemp:FactAttribute {public SqlTemp(){if(Environment.GetEnvironmentVariable("PHOTOCOPY_2MIN_SQL_TEST")!="1")Skip="Opt-in: chỉ bảng tạm và application lock.";}}
    private const string Connection=@"Server=.\MSSQLSERVER01;Database=PhotocopyChain;Integrated Security=True;TrustServerCertificate=True";
    [SqlTemp]
    public async Task MigrationRerunnable_UniqueKey_PairConstraint_AndReplayQuery()
    {
        using var db=new SqlConnection(Connection);await db.OpenAsync();
        await db.ExecuteAsync("CREATE TABLE #DonHang(MaDonHang INT PRIMARY KEY,TongTien DECIMAL(12,0),GhiChu NVARCHAR(255));CREATE TABLE #ThanhToan(MaDonHang INT,PhuongThuc VARCHAR(10));INSERT #DonHang VALUES(1,1234,NULL),(2,4567,NULL);");
        string root=Environment.GetEnvironmentVariable("PHOTOCOPY_SOURCE_ROOT");
        string sql=File.ReadAllText(Path.Combine(root,"Database","023-AnToanTaoDon.sql"))
            .Replace("'dbo.DonHang'","'tempdb..#DonHang'").Replace("dbo.DonHang","#DonHang")
            .Replace("sys.check_constraints","tempdb.sys.check_constraints").Replace("sys.indexes","tempdb.sys.indexes");
        await db.ExecuteAsync(sql);await db.ExecuteAsync(sql);
        Guid key=Guid.NewGuid();string hash=new('A',64);
        await db.ExecuteAsync(TaoDonHangService.SqlLuu.Replace("dbo.DonHang","#DonHang"),new {Id=1,Note="note",Key=key,Hash=hash});
        var old=await db.QuerySingleAsync(TaoDonHangService.SqlTim.Replace("dbo.DonHang","#DonHang").Replace("dbo.ThanhToan","#ThanhToan").Replace(" JOIN "," LEFT JOIN "),new {Key=key});
        Assert.Equal(1,(int)old.MaDonHang);Assert.Equal(hash,(string)old.DauYeuCauTao);
        Assert.Equal(2601,(await Assert.ThrowsAsync<SqlException>(()=>db.ExecuteAsync("UPDATE #DonHang SET MaYeuCauTao=@Key,DauYeuCauTao=@Hash WHERE MaDonHang=2",new {Key=key,Hash=hash}))).Number);
        Assert.Equal(547,(await Assert.ThrowsAsync<SqlException>(()=>db.ExecuteAsync("UPDATE #DonHang SET DauYeuCauTao=NULL WHERE MaDonHang=1"))).Number);
        Assert.Equal(1234m,await db.ExecuteScalarAsync<decimal>("SELECT TongTien FROM #DonHang WHERE MaDonHang=1"));
    }
    [SqlTemp]
    public async Task RequestAndFileMutexSurviveRollbackAndDoNotDeadlockWithSortedAcquisition()
    {
        using var a=new SqlConnection(Connection);using var b=new SqlConnection(Connection);await a.OpenAsync();await b.OpenAsync();
        using var txA=a.BeginTransaction();using var txB=b.BeginTransaction();
        string resource="PhotocopyChain:Test:"+Guid.NewGuid().ToString("N");
        await KhoaGiaoDichSql.Lay(a,txA,resource);
        var pending=KhoaGiaoDichSql.Lay(b,txB,resource);await Task.Delay(100);Assert.False(pending.IsCompleted);
        await txA.RollbackAsync();await pending.WaitAsync(TimeSpan.FromSeconds(3));await txB.CommitAsync();
        using var again=a.BeginTransaction();await KhoaGiaoDichSql.Lay(a,again,resource);await again.CommitAsync();
    }
    [SqlTemp]
    public async Task FileDeletionRetainsPathForRetryAndRejectsActiveReference()
    {
        using var db=new SqlConnection(Connection);await db.OpenAsync();
        await db.ExecuteAsync(@"CREATE TABLE #FileIn(MaFile INT,MaKhachHang INT,DuongDan NVARCHAR(300),TrangThai VARCHAR(20),NgayTaiLen DATETIME);
            CREATE TABLE #ChiTietDonHang(MaFile INT,MaDonHang INT);CREATE TABLE #DonHang(MaDonHang INT,TrangThai VARCHAR(20),GhiChu NVARCHAR(255),NgayDat DATETIME);
            INSERT #FileIn VALUES(1,5,N'C:\test.pdf','SAN_SANG',DATEADD(HOUR,-2,GETDATE()));
            INSERT #ChiTietDonHang VALUES(1,2);INSERT #DonHang VALUES(2,'DANG_IN',NULL,GETDATE());");
        string Rewrite(string sql)=>sql.Replace("ChiTietDonHang","#ChiTietDonHang").Replace("DonHang d","#DonHang d").Replace("FileIn","#FileIn");
        using var tx=db.BeginTransaction();var active=await db.QuerySingleAsync(Rewrite(FileInLifecycleService.SqlDoc),new{Id=1},tx);
        Assert.True((bool)active.CoDonHoatDong);await db.ExecuteAsync("UPDATE #DonHang SET TrangThai='HOAN_THANH'",transaction:tx);
        var ended=await db.QuerySingleAsync(Rewrite(FileInLifecycleService.SqlDoc),new{Id=1},tx);Assert.False((bool)ended.CoDonHoatDong);
        await db.ExecuteAsync(Rewrite(FileInLifecycleService.SqlDanhDau),new{Id=1,Path=@"C:\test.pdf"},tx);await tx.CommitAsync();
        Assert.Equal(@"C:\test.pdf",await db.ExecuteScalarAsync<string>("SELECT DuongDan FROM #FileIn"));
        await db.ExecuteAsync(Rewrite(FileInLifecycleService.SqlBoDuongDan),new{Id=1,Path=@"C:\wrong.pdf"});
        Assert.NotNull(await db.ExecuteScalarAsync<string>("SELECT DuongDan FROM #FileIn"));
        await db.ExecuteAsync(Rewrite(FileInLifecycleService.SqlBoDuongDan),new{Id=1,Path=@"C:\test.pdf"});
        Assert.Null(await db.ExecuteScalarAsync<string>("SELECT DuongDan FROM #FileIn"));
    }
}
