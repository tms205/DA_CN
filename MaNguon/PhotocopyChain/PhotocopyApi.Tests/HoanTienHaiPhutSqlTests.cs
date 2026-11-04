using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace PhotocopyApi.Tests;

// Chỉ chạy body thủ tục trên các bảng # trong phiên kiểm thử; không áp dụng migration vào DB thật.
public class HoanTienHaiPhutSqlTests
{
    private sealed class SqlHaiPhutAttribute : FactAttribute
    {
        public SqlHaiPhutAttribute() { if(Environment.GetEnvironmentVariable("PHOTOCOPY_2MIN_SQL_TEST")!="1") Skip="Opt-in: chỉ tạo bảng # tạm."; }
    }
    private static string Source => ReadMigration("020-HoanTien2Phut_KhieuNaiSanPham.sql");
    private static string ReadMigration(string file)
    {
            var d=new DirectoryInfo(Environment.GetEnvironmentVariable("PHOTOCOPY_SOURCE_ROOT")??Directory.GetCurrentDirectory());
            while(d!=null&&!Directory.Exists(Path.Combine(d.FullName,"Database")))d=d.Parent;
            return File.ReadAllText(Path.Combine(d.FullName,"Database",file));
    }
    private static async Task<SqlConnection> Db(int elapsed, string state="CHO_XAC_NHAN", string channel="APP")
    {
        var db=new SqlConnection(@"Server=.\MSSQLSERVER01;Database=PhotocopyChain;Integrated Security=True;TrustServerCertificate=True");
        await db.OpenAsync();
        await db.ExecuteAsync(@"CREATE TABLE #DonHang(MaDonHang INT,MaChiNhanh INT,MaKhachHang INT,TrangThai VARCHAR(20) COLLATE DATABASE_DEFAULT,
            KenhDatDon VARCHAR(10) COLLATE DATABASE_DEFAULT,MaNhanVien INT NULL,MaMayIn INT NULL,GhiChu NVARCHAR(255),NgayHenTra DATETIME,BangChungHuyJson NVARCHAR(MAX));
            CREATE TABLE #ThanhToan(MaDonHang INT,PhuongThuc VARCHAR(10),TrangThai VARCHAR(20),NgayThanhToan DATETIME);
            CREATE TABLE #NguoiDung(MaNguoiDung INT,MaChiNhanh INT,VaiTro VARCHAR(20),TrangThai NVARCHAR(20));
            CREATE TABLE #CaLamViec(MaNhanVien INT,MaChiNhanh INT,TrangThai VARCHAR(20));
            CREATE TABLE #ThongBao(MaNguoiDung INT,MaDonHang INT,TieuDe NVARCHAR(255),NoiDung NVARCHAR(2000));");
        await db.ExecuteAsync(@"INSERT #DonHang VALUES(999999,1,987654,@state,@channel,NULL,NULL,NULL,DATEADD(MINUTE,1,GETDATE()),NULL);
            INSERT #ThanhToan VALUES(999999,'VIETQR','DA_THANH_TOAN',DATEADD(SECOND,-@elapsed,GETDATE()));
            INSERT #NguoiDung VALUES(987654,1,'CUSTOMER',N'HOAT_DONG'),(987655,1,'STAFF',N'HOAT_DONG');
            INSERT #CaLamViec VALUES(987655,1,'DANG_MO');",new{elapsed,state,channel});
        return db;
    }
    private static string Body(string name)
    {
        var p=Regex.Match(name=="sp_HuyDonHang"?ReadMigration("021-HuyTruocTiepNhan_CaThongDem.sql"):Source,@"^CREATE OR ALTER\s+PROCEDURE (?:dbo\.)?"+name+@"\b.*?(?=^GO\s*$)",RegexOptions.Singleline|RegexOptions.Multiline).Value;
        Assert.NotEmpty(p);
        p=Regex.Replace(p,@"^.*?\bAS\s*\r?\n", "",RegexOptions.Singleline);
        foreach(string n in new[]{"DonHang","ThanhToan","NguoiDung","CaLamViec","ThongBao"}) p=Regex.Replace(p,@"\b(?:dbo\.)?"+n+@"\b","#"+n);
        return p;
    }
    private static object CancelArgs => new {MaDonHang=999999,MaNguoiHuy=987654,LyDo="Hủy kiểm thử",BangChungJson="{\"Loai\":\"THU_CONG\",\"MaDonHang\":999999,\"MaNguoiGui\":987654,\"Anh\":[{\"MaAnh\":\"test\"}]}"};

    [SqlHaiPhut]
    public async Task WaitingOrderCancelsWithoutPhotosAndWritesServerJournal()
    {
        foreach(bool paid in new[]{false,true}) {
            await using var db=await Db(30);
            if(!paid)await db.ExecuteAsync("UPDATE #ThanhToan SET TrangThai='CHUA_THANH_TOAN',NgayThanhToan=NULL");
            await db.ExecuteAsync(Body("sp_HuyDonHang"),new{MaDonHang=999999,MaNguoiHuy=987654,LyDo="Khách đổi nhu cầu",BangChungJson=(string)null});
            Assert.Equal("DA_HUY",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #DonHang"));
            Assert.Equal("HUY_TRUOC_TIEP_NHAN",await db.ExecuteScalarAsync<string>("SELECT JSON_VALUE(BangChungHuyJson,'$.Loai') FROM #DonHang"));
            Assert.Equal(0,await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #DonHang CROSS APPLY OPENJSON(BangChungHuyJson,'$.Anh')"));
            Assert.Equal(paid?"DA_THANH_TOAN":"CHUA_THANH_TOAN",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #ThanhToan"));
        }
    }
    [SqlHaiPhut]
    public async Task RefundRequestWithinWindowSucceedsEvenNearAppointment()
    {
        await using var db=await Db(30);
        await db.ExecuteAsync(Body("sp_HuyDonHang"),CancelArgs);
        Assert.Equal("DA_HUY",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #DonHang"));
        Assert.Equal("DA_THANH_TOAN",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #ThanhToan"));
    }
    [SqlHaiPhut]
    public async Task DeadlineAndReceptionAreMutuallyExclusive()
    {
        foreach(int elapsed in new[]{120,121,600}) {
            await using(var db=await Db(elapsed)) {
                await Assert.ThrowsAsync<SqlException>(()=>db.ExecuteAsync(Body("sp_HuyDonHang"),CancelArgs));
                Assert.Equal("CHO_XAC_NHAN",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #DonHang"));
            }
            await using(var db=await Db(elapsed)) {
                await db.ExecuteAsync(Body("sp_TiepNhanDon"),new{MaDonHang=999999,MaNhanVien=987655});
                Assert.Equal("DA_XAC_NHAN",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #DonHang"));
            }
        }
    }
    [SqlHaiPhut]
    public async Task CannotReceiveBeforePaymentOrDuringWindow()
    {
        foreach(int elapsed in new[]{0,30,118}) {
            await using var db=await Db(elapsed);
            await Assert.ThrowsAsync<SqlException>(()=>db.ExecuteAsync(Body("sp_TiepNhanDon"),new{MaDonHang=999999,MaNhanVien=987655}));
            Assert.Null(await db.ExecuteScalarAsync<int?>("SELECT MaNhanVien FROM #DonHang"));
        }
        await using var unpaid=await Db(600);
        await unpaid.ExecuteAsync("UPDATE #ThanhToan SET TrangThai='CHUA_THANH_TOAN',NgayThanhToan=NULL");
        await Assert.ThrowsAsync<SqlException>(()=>unpaid.ExecuteAsync(Body("sp_TiepNhanDon"),new{MaDonHang=999999,MaNhanVien=987655}));
    }
    [SqlHaiPhut]
    public async Task CounterHasNoTwoMinuteHoldAndCustomerCannotCancelReceivedOrder()
    {
        await using(var db=await Db(1,channel:"TAI_QUAY")) {
            await db.ExecuteAsync(Body("sp_TiepNhanDon"),new{MaDonHang=999999,MaNhanVien=987655});
            Assert.Equal("DA_XAC_NHAN",await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #DonHang"));
        }
        await using(var db=await Db(1,state:"DA_XAC_NHAN")) {
            await Assert.ThrowsAsync<SqlException>(()=>db.ExecuteAsync(Body("sp_HuyDonHang"),CancelArgs));
        }
    }
}
