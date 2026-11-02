using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.CaLamViec;
using Xunit;

namespace PhotocopyApi.Tests;

// Chỉ chạy opt-in trên tempdb. Bảng ## có GUID riêng để hai connection thử đồng thời;
// không gọi procedure thật, không đọc/ghi bảng nghiệp vụ, không tạo object thường trực.
public class CodVaLichCaSqlTests
{
    private static readonly DateTime Now = new(2026,10,29,10,0,0);

    [SqlAudit]
    public async Task HaiConnectionBanGiaoCungDon_ChiMotLanThuTien()
    {
        await using var fixture = await Fixture.Create();
        var p = new { MaDonHang=1, MaNhanVien=1, ThoiDiemKiemThu=Now };
        string sql = fixture.Proc("sp_GiaoTaiQuay").Replace("IF @MaChiNhanhDon IS NULL", "WAITFOR DELAY '00:00:00.150'; IF @MaChiNhanhDon IS NULL");
        async Task<bool> Run()
        {
            await using var conn = new SqlConnection(fixture.ConnectionString);
            try { await conn.ExecuteAsync(sql, p); return true; }
            catch(SqlException ex) when(ex.Number==50026) { return false; }
        }
        var result = await Task.WhenAll(Run(), Run());
        Assert.Single(result,x=>x);
        Assert.Equal(100m, await fixture.Db.ExecuteScalarAsync<decimal>($"SELECT DoanhThuTrongCa FROM {fixture.T("CaLamViec")} WHERE MaCa=1"));
        Assert.Equal("HOAN_THANH", await fixture.Db.ExecuteScalarAsync<string>($"SELECT TrangThai FROM {fixture.T("DonHang")} WHERE MaDonHang=1"));
        Assert.Equal(1, await fixture.Db.ExecuteScalarAsync<int>($"SELECT MaCa FROM {fixture.T("ThanhToan")} WHERE MaDonHang=1"));
    }

    [SqlAudit]
    public async Task CodDaThanhToanKhongCongLai_BanGiaoLapBiTuChoi()
    {
        await using var f = await Fixture.Create();
        await f.Db.ExecuteAsync($"UPDATE {f.T("ThanhToan")} SET TrangThai='DA_THANH_TOAN',MaCa=1; UPDATE {f.T("CaLamViec")} SET DoanhThuTrongCa=100;");
        var p = new { MaDonHang=1,MaNhanVien=1,ThoiDiemKiemThu=Now };
        await f.Db.ExecuteAsync(f.Proc("sp_GiaoTaiQuay"),p);
        await Assert.ThrowsAsync<SqlException>(()=>f.Db.ExecuteAsync(f.Proc("sp_GiaoTaiQuay"),p));
        Assert.Equal(100m,await f.Db.ExecuteScalarAsync<decimal>($"SELECT DoanhThuTrongCa FROM {f.T("CaLamViec")} WHERE MaCa=1"));
    }

    [SqlAudit]
    public async Task DungMocHetCa_KhongThuCod_JobKhongTuNhapTien()
    {
        await using var f = await Fixture.Create();
        var boundary = Now.Date.AddHours(12).AddMinutes(30);
        await Assert.ThrowsAsync<SqlException>(()=>f.Db.ExecuteAsync(f.Proc("sp_GiaoTaiQuay"),new {MaDonHang=1,MaNhanVien=1,ThoiDiemKiemThu=boundary}));
        await f.Db.ExecuteAsync(f.Rewrite(LichCaService.SqlChot),new {BayGio=boundary,MaNhanVien=(int?)1});
        await f.Db.ExecuteAsync(f.Rewrite(LichCaService.SqlChot),new {BayGio=boundary.AddHours(2),MaNhanVien=(int?)1});
        var ca=await f.Db.QuerySingleAsync($"SELECT * FROM {f.T("CaLamViec")} WHERE MaCa=1");
        Assert.Equal("CHO_TONG_KET",(string)ca.TrangThai);
        Assert.Equal(boundary,(DateTime)ca.GioDongCa);
        Assert.Null((object)ca.TongDoanhThuCuoiCa);
        Assert.Null((object)ca.MaNguoiXacNhan);
        Assert.Equal(0m,(decimal)ca.DoanhThuTrongCa);
    }

    [SqlAudit]
    public async Task TongKetCanLyDoKhiLech_VaDoiSoatGiuGhiChuNhanVien()
    {
        await using var f = await Fixture.Create();
        await f.Db.ExecuteAsync($"UPDATE {f.T("CaLamViec")} SET TrangThai='CHO_TONG_KET',GioDongCa='20261029 12:30';");
        string sql=f.Proc("sp_DongCa");
        await Assert.ThrowsAsync<SqlException>(()=>f.Db.ExecuteAsync(sql,new{MaCa=1,TongDoanhThuCuoiCa=199900m,GhiChu=(string)null,ThoiDiemKiemThu=Now.AddHours(3)}));
        await f.Db.ExecuteAsync(sql,new{MaCa=1,TongDoanhThuCuoiCa=199900m,GhiChu="Nhân viên: thiếu 100đ.",ThoiDiemKiemThu=Now.AddHours(3)});
        Assert.Equal("CHO_XAC_NHAN",await f.Db.ExecuteScalarAsync<string>($"SELECT TrangThai FROM {f.T("CaLamViec")} WHERE MaCa=1"));
        string approve=f.Proc("sp_XacNhanCa").Replace("dbo.fn_LaQuanLy(@MaQuanLy,@ChiNhanh)","(CASE WHEN @MaQuanLy=99 THEN 1 ELSE 0 END)");
        await f.Db.ExecuteAsync(approve,new{MaCa=1,MaQuanLy=99,TienQuanLyDemCuoiCa=199900m,TienChi=0m,TienBu=100m,GhiChu="Đã nhận bù 100đ.",ThoiDiemKiemThu=Now.AddHours(3)});
        string note=await f.Db.ExecuteScalarAsync<string>($"SELECT GhiChu FROM {f.T("CaLamViec")} WHERE MaCa=1");
        Assert.Contains("Nhân viên: thiếu 100đ.",note);
        Assert.Contains("\r\nQuản lý: Đã nhận bù 100đ.",note);
    }

    [SqlAudit]
    public async Task MoCaKhongTrungVaKhongVuotCaChoTongKet()
    {
        await using var f=await Fixture.Create();
        string sql=f.Proc("sp_MoCa");
        DynamicParameters Params(DateTime time)
        {
            var p=new DynamicParameters(new{MaNhanVien=1,TienDauCa=200000m,MaChiNhanhThayThe=(int?)null,ThoiDiemKiemThu=time});
            p.Add("MaCa",dbType:DbType.Int32,direction:ParameterDirection.InputOutput,value:0);
            return p;
        }
        var p=Params(Now);
        await f.Db.ExecuteAsync(sql,p);
        Assert.Equal(1,p.Get<int>("MaCa"));
        Assert.Equal(1,await f.Db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {f.T("CaLamViec")}"));
        await f.Db.ExecuteAsync($"UPDATE {f.T("CaLamViec")} SET TrangThai='CHO_TONG_KET';");
        await Assert.ThrowsAsync<SqlException>(()=>f.Db.ExecuteAsync(sql,Params(Now.AddHours(3))));
        await Assert.ThrowsAsync<SqlException>(()=>f.Db.ExecuteAsync(sql,Params(Now.Date.AddHours(22))));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly string[] Tables={"DonHang","ThanhToan","CaLamViec","NguoiDung","ChiNhanh"};
        public SqlConnection Db {get;private init;}
        public string ConnectionString=>Db.ConnectionString;
        private readonly string _id=Guid.NewGuid().ToString("N");
        public string T(string name)=>"##Audit_"+_id+"_"+name;
        public string Rewrite(string sql)
        {
            foreach(string t in Tables)sql=Regex.Replace(sql,@"(?<![@\w])(?:dbo\.)?"+t+@"\b",T(t));
            return sql.Replace("GETDATE()","@ThoiDiemKiemThu");
        }
        public string Proc(string name)
        {
            string dir=AppContext.BaseDirectory;
            // Test output nằm ngoài repo; nguồn được truyền rõ, không dò qua thư mục người dùng.
            string file=Environment.GetEnvironmentVariable("PHOTOCOPY_AUDIT_MIGRATION");
            string text=File.ReadAllText(file);
            var match=Regex.Match(text,@"CREATE OR ALTER PROCEDURE dbo\."+name+@"\b.*?\nAS\s*\n(?<body>BEGIN\b.*?\nEND;)\s*\nGO",RegexOptions.Singleline);
            Assert.True(match.Success,"Không tìm thấy thủ tục "+name);
            return Rewrite(match.Groups["body"].Value);
        }
        public static async Task<Fixture> Create()
        {
            var cs=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PHOTOCOPY_AUDIT_TEST_SQL"));
            Assert.Equal("tempdb",cs.InitialCatalog,StringComparer.OrdinalIgnoreCase);
            var f=new Fixture{Db=new SqlConnection(cs.ConnectionString)};
            await f.Db.OpenAsync();
            await f.Db.ExecuteAsync($@"
                CREATE TABLE {f.T("NguoiDung")}(MaNguoiDung INT PRIMARY KEY,MaChiNhanh INT,VaiTro VARCHAR(20),TrangThai VARCHAR(20));
                CREATE TABLE {f.T("ChiNhanh")}(MaChiNhanh INT PRIMARY KEY,TrangThai VARCHAR(20));
                CREATE TABLE {f.T("DonHang")}(MaDonHang INT PRIMARY KEY,MaChiNhanh INT,TrangThai VARCHAR(20),HinhThucNhan VARCHAR(20),NgayHoanThanh DATETIME NULL);
                CREATE TABLE {f.T("ThanhToan")}(MaDonHang INT PRIMARY KEY,PhuongThuc VARCHAR(10),TrangThai VARCHAR(20),SoTien DECIMAL(12,0),MaCa INT NULL,NgayThanhToan DATETIME NULL);
                CREATE TABLE {f.T("CaLamViec")}(MaCa INT IDENTITY PRIMARY KEY,MaNhanVien INT,MaChiNhanh INT,TrangThai VARCHAR(20),GioMoCa DATETIME,GioDongCa DATETIME NULL,TienDauCa DECIMAL(12,0),DoanhThuTrongCa DECIMAL(12,0),TongDoanhThuCuoiCa DECIMAL(12,0) NULL,TienQuanLyDemCuoiCa DECIMAL(12,0) NULL,TienChi DECIMAL(12,0),TienBu DECIMAL(12,0),MaNguoiXacNhan INT NULL,GhiChu NVARCHAR(255) NULL);
                INSERT INTO {f.T("NguoiDung")} VALUES(1,1,'STAFF','HOAT_DONG'),(2,2,'STAFF','HOAT_DONG'),(99,1,'BRANCH_ADMIN','HOAT_DONG');
                INSERT INTO {f.T("ChiNhanh")} VALUES(1,'HOAT_DONG');
                INSERT INTO {f.T("DonHang")} VALUES(1,1,'DA_IN','TAI_QUAY',NULL);
                INSERT INTO {f.T("ThanhToan")} VALUES(1,'COD','CHUA_THANH_TOAN',100,NULL,NULL);
                INSERT INTO {f.T("CaLamViec")}(MaNhanVien,MaChiNhanh,TrangThai,GioMoCa,TienDauCa,DoanhThuTrongCa,TienChi,TienBu) VALUES(1,1,'DANG_MO','20261029 09:00',200000,0,0,0);");
            return f;
        }
        public async ValueTask DisposeAsync()
        {
            foreach(string t in Tables)await Db.ExecuteAsync($"DROP TABLE IF EXISTS {T(t)}");
            await Db.DisposeAsync();
        }
    }
    private sealed class SqlAuditAttribute:FactAttribute
    {
        public SqlAuditAttribute()
        {
            if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHOTOCOPY_AUDIT_TEST_SQL")) ||
               string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHOTOCOPY_AUDIT_MIGRATION")))
                Skip="Cần tempdb riêng và đường dẫn migration; chỉ tạo bảng ## tên GUID, không sửa DB nghiệp vụ.";
        }
    }
}
