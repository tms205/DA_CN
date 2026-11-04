using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

// Chỉ chạy khi được cấp connection tempdb; toàn bộ bảng ## có tên GUID riêng.
public class PhanBienHoiDongSqlTests
{
    private static string Root => Environment.GetEnvironmentVariable("PHOTOCOPY_REVIEW_ROOT");

    [ReviewSql]
    public async Task InLaiXong_KhongTuBanGiaoDonGocDaTraTien()
    {
        await using var f = await Fixture.Create();
        foreach (string payment in new[] { "DA_THANH_TOAN", "CHUA_THANH_TOAN" })
        {
            await f.Db.ExecuteAsync($"UPDATE {f.T("ThanhToan")} SET TrangThai=@payment; UPDATE {f.T("DonHang")} SET TrangThai='DA_IN' WHERE MaDonHang=2", new { payment });
            await f.Db.ExecuteAsync(f.Rewrite(MayInAoController.HoanTatInLaiSql), new { MaDonHang = 2, MaDonHangGoc = 1 });
            Assert.Equal("HOAN_THANH", await f.State(2));
            Assert.Equal("DA_IN", await f.State(1));
            Assert.Null(await f.Db.ExecuteScalarAsync<DateTime?>($"SELECT NgayHoanThanh FROM {f.T("DonHang")} WHERE MaDonHang=1"));
            string note = await f.Db.ExecuteScalarAsync<string>($"SELECT TOP 1 NoiDung FROM {f.T("ThongBao")} ORDER BY NoiDung");
            Assert.Contains("bàn giao", note);
            Assert.Contains(payment == "CHUA_THANH_TOAN" ? "chờ thu tiền" : "không thu lại", note);
            await f.Db.ExecuteAsync($"DELETE FROM {f.T("ThongBao")}");
        }
    }

    [ReviewSql]
    public async Task HaiYeuCauInLaiCungLuc_ChiTaoMotDonCon()
    {
        await using var f = await Fixture.Create();
        await f.Db.ExecuteAsync($"DELETE FROM {f.T("DonHang")} WHERE MaDonHang=2");
        string guard = DonHangController.KiemTraDonGocPhatSinhSql;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        async Task<bool> Run(int id)
        {
            await using var db = new SqlConnection(f.ConnectionString);
            await db.OpenAsync();
            if (Interlocked.Increment(ref reads) == 2) gate.SetResult();
            await gate.Task;
            await using var tx = (SqlTransaction)await db.BeginTransactionAsync();
            try
            {
                await db.ExecuteAsync(f.Rewrite(guard), new { MaDonHangGoc = 1, LoaiPhatSinh = "IN_LAI_LOI_QUAY" }, tx);
                await db.ExecuteAsync($"INSERT INTO {f.T("DonHang")}(MaDonHang,MaDonHangGoc,LoaiPhatSinh,TrangThai,HinhThucNhan) VALUES(@id,1,'IN_LAI_LOI_QUAY','DA_XAC_NHAN','TAI_QUAY')", new { id }, tx);
                await tx.CommitAsync();
                return true;
            }
            catch (SqlException e) when (e.Number == 50116)
            {
                await tx.RollbackAsync();
                return false;
            }
        }
        var results = await Task.WhenAll(Run(2), Run(3));
        Assert.Single(results, x => x);
        Assert.Equal(1, await f.Db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {f.T("DonHang")} WHERE MaDonHangGoc=1"));
    }

    [ReviewSql]
    public async Task GiayHaiMat_LeTrang_LamTronTungBanTruocKhiNhan()
    {
        await using var f = await Fixture.Create();
        foreach (string filename in new[] { "04_StoredProcedure.sql", "019-SuaTieuHaoGiayHaiMat.sql" })
        {
            string source = File.ReadAllText(Path.Combine(Root, "Database", filename));
            string sql = Regex.Match(source, @"CREATE (?:OR ALTER )?PROCEDURE (?:dbo\.)?sp_CapNhatTrangThaiIn.*?\nAS\s*\n(?<body>BEGIN\b.*?\nEND;)\s*\nGO", RegexOptions.Singleline).Groups["body"].Value;
            Assert.NotEmpty(sql);
            foreach (var x in new[] { (5,3,"2_MAT",9), (6,3,"2_MAT",9), (1,2,"2_MAT",2), (5,3,"1_MAT",15) })
            {
                await f.Db.ExecuteAsync($@"DELETE FROM {f.T("DichVu")}; DELETE FROM {f.T("ChiTietDonHang")}; DELETE FROM {f.T("TonKho")};
                    UPDATE {f.T("DonHang")} SET TrangThai='DA_XAC_NHAN' WHERE MaDonHang=1;
                    INSERT INTO {f.T("DichVu")} VALUES(1,1,@mat);
                    INSERT INTO {f.T("ChiTietDonHang")} VALUES(1,1,1,@trang,@ban);
                    INSERT INTO {f.T("TonKho")} VALUES(1,1,100);", new { trang=x.Item1, ban=x.Item2, mat=x.Item3 });
                await f.Db.ExecuteAsync(f.Rewrite(sql), new { MaDonHang = 1, TrangThaiMoi = "DANG_IN" });
                Assert.Equal(100-x.Item4, await f.Db.ExecuteScalarAsync<int>($"SELECT SoLuong FROM {f.T("TonKho")}"));
            }
        }
    }

    [ReviewSql]
    public async Task CallbackInLai_KhoaDonGocTruoc_KhongDaoThuTuVoiTaoPhatSinh()
    {
        await using var f = await Fixture.Create();
        await using var tx = (SqlTransaction)await f.Db.BeginTransactionAsync();
        string firstLock = MayInAoController.KhoaDonGocTruocHoanTatSql;
        await f.Db.ExecuteAsync(f.Rewrite(firstLock), new { MaDonHang=2 },tx);
        await using var other = new SqlConnection(f.ConnectionString);
        await other.OpenAsync();
        var ex = await Assert.ThrowsAsync<SqlException>(()=>other.ExecuteAsync($"SELECT MaDonHang FROM {f.T("DonHang")} WITH(UPDLOCK,NOWAIT) WHERE MaDonHang=1"));
        Assert.Equal(1222,ex.Number);
        await tx.RollbackAsync();
    }

    [ReviewSql]
    public async Task DonConChuaXong_ChanTaoTrungVaBanGiao_DonConDaXongThiChoPhep()
    {
        await using var f = await Fixture.Create();
        foreach (string state in new[] { "DA_XAC_NHAN", "DANG_IN", "DA_IN", "HOAN_THANH", "DA_HUY" })
        {
            await f.Db.ExecuteAsync($"UPDATE {f.T("DonHang")} SET TrangThai=@state WHERE MaDonHang=2", new { state });
            await using var tx = (SqlTransaction)await f.Db.BeginTransactionAsync();
            Task Check() => f.Db.ExecuteAsync(f.Rewrite(DonHangController.KiemTraDonGocPhatSinhSql),
                new { MaDonHangGoc=1, LoaiPhatSinh="IN_LAI_LOI_QUAY" }, tx);
            if (state is "HOAN_THANH" or "DA_HUY") await Check();
            else Assert.Equal(50116, (await Assert.ThrowsAsync<SqlException>(Check)).Number);
            await tx.RollbackAsync();
        }
    }

    [ReviewSql]
    public async Task BanGiaoSauInLai_TransactionNgoaiTuongThichVoiThuTuc_VaHuyKhiLoi()
    {
        await using var f = await Fixture.Create();
        string source = File.ReadAllText(Path.Combine(Root, "Database", "015-KhacPhucCOD_VaLichCa.sql"));
        string sql = Regex.Match(source, @"CREATE OR ALTER PROCEDURE dbo\.sp_GiaoTaiQuay\b.*?\nAS\s*\n(?<body>BEGIN\b.*?\nEND;)\s*\nGO", RegexOptions.Singleline).Groups["body"].Value;
        Assert.NotEmpty(sql);
        await f.Db.ExecuteAsync($"UPDATE {f.T("DonHang")} SET TrangThai='HOAN_THANH' WHERE MaDonHang=2");
        foreach (bool authorized in new[] { false, true })
        {
            await f.Db.ExecuteAsync($"UPDATE {f.T("NguoiDung")} SET VaiTro=@role", new { role=authorized ? "STAFF" : "CUSTOMER" });
            await using (var tx = (SqlTransaction)await f.Db.BeginTransactionAsync())
            {
                await f.Db.ExecuteAsync(f.Rewrite(DonHangController.KiemTraDonGocPhatSinhSql),
                    new { MaDonHangGoc=1, LoaiPhatSinh="IN_LAI_LOI_QUAY" }, tx);
                try
                {
                    await f.Db.ExecuteAsync(f.Rewrite(sql), new { MaDonHang=1, MaNhanVien=1 }, tx);
                    Assert.True(authorized);
                    await tx.CommitAsync();
                }
                catch (SqlException ex) when (!authorized && ex.Number == 50021)
                {
                    // Procedure đã rollback toàn bộ transaction; dispose không được che lỗi gốc.
                }
            }
            Assert.Equal(authorized ? "HOAN_THANH" : "DA_IN", await f.State(1));
            Assert.Equal(0, await f.Db.ExecuteScalarAsync<int>("SELECT @@TRANCOUNT"));
        }
    }

    [ReviewSql]
    public async Task DonGocDaBanGiao_KhongTaoPhatSinh_NhungDonBoSungKhongBiChanBoiInLai()
    {
        await using var f = await Fixture.Create();
        await using var tx = (SqlTransaction)await f.Db.BeginTransactionAsync();
        await f.Db.ExecuteAsync(f.Rewrite(DonHangController.KiemTraDonGocPhatSinhSql),
            new { MaDonHangGoc=1, LoaiPhatSinh="BO_SUNG" },tx);
        await f.Db.ExecuteAsync($"UPDATE {f.T("DonHang")} SET TrangThai='HOAN_THANH' WHERE MaDonHang=1",transaction:tx);
        var ex = await Assert.ThrowsAsync<SqlException>(() => f.Db.ExecuteAsync(f.Rewrite(DonHangController.KiemTraDonGocPhatSinhSql),
            new { MaDonHangGoc=1, LoaiPhatSinh="IN_LAI_LOI_QUAY" },tx));
        Assert.Equal(50115,ex.Number);
        await tx.RollbackAsync();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly string[] Tables = ["DonHang", "ThanhToan", "ThongBao", "ChiTietDonHang", "DichVu", "ChiTietGiaCong", "TonKho", "NguoiDung", "CaLamViec"];
        private readonly string suffix = Guid.NewGuid().ToString("N");
        public SqlConnection Db { get; private set; }
        public string ConnectionString => Db.ConnectionString;
        public string T(string table) => "##Review_" + table + "_" + suffix;
        public string Rewrite(string sql)
        {
            foreach (string t in Tables) sql = Regex.Replace(sql, @"(?<![@\w])(?:dbo\.)?" + t + @"\b", T(t));
            return sql;
        }
        public Task<string> State(int id) => Db.ExecuteScalarAsync<string>($"SELECT TrangThai FROM {T("DonHang")} WHERE MaDonHang=@id", new { id });
        public static async Task<Fixture> Create()
        {
            var cs = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PHOTOCOPY_AUDIT_TEST_SQL"));
            Assert.Equal("tempdb", cs.InitialCatalog, StringComparer.OrdinalIgnoreCase);
            var f = new Fixture { Db = new SqlConnection(cs.ConnectionString) };
            await f.Db.OpenAsync();
            await f.Db.ExecuteAsync($@"
                CREATE TABLE {f.T("DonHang")}(MaDonHang INT PRIMARY KEY,MaDonHangGoc INT NULL,LoaiPhatSinh VARCHAR(30),TrangThai VARCHAR(30),HinhThucNhan VARCHAR(20),NgayHoanThanh DATETIME NULL,MaKhachHang INT,MaChiNhanh INT);
                CREATE TABLE {f.T("ThanhToan")}(MaDonHang INT PRIMARY KEY,TrangThai VARCHAR(30),PhuongThuc VARCHAR(10),SoTien DECIMAL(12,0),MaCa INT NULL,NgayThanhToan DATETIME NULL);
                CREATE TABLE {f.T("ThongBao")}(MaNguoiDung INT,MaDonHang INT,TieuDe NVARCHAR(200),NoiDung NVARCHAR(500));
                CREATE TABLE {f.T("DichVu")}(MaDichVu INT PRIMARY KEY,MaVatTu INT,MatIn VARCHAR(10));
                CREATE TABLE {f.T("ChiTietDonHang")}(MaChiTiet INT PRIMARY KEY,MaDonHang INT,MaDichVu INT,SoTrang INT,SoBan INT);
                CREATE TABLE {f.T("ChiTietGiaCong")}(MaChiTiet INT,MaDichVu INT);
                CREATE TABLE {f.T("TonKho")}(MaChiNhanh INT,MaVatTu INT,SoLuong INT);
                CREATE TABLE {f.T("NguoiDung")}(MaNguoiDung INT PRIMARY KEY,MaChiNhanh INT,VaiTro VARCHAR(20),TrangThai VARCHAR(20));
                CREATE TABLE {f.T("CaLamViec")}(MaCa INT PRIMARY KEY,MaNhanVien INT,MaChiNhanh INT,TrangThai VARCHAR(20),GioMoCa DATETIME,DoanhThuTrongCa DECIMAL(12,0));
                INSERT INTO {f.T("DonHang")} VALUES(1,NULL,NULL,'DA_IN','TAI_QUAY',NULL,1,1),(2,1,'IN_LAI_LOI_QUAY','DA_IN','TAI_QUAY',NULL,1,1);
                INSERT INTO {f.T("ThanhToan")}(MaDonHang,TrangThai,PhuongThuc,SoTien) VALUES(1,'DA_THANH_TOAN','VIETQR',100);
                INSERT INTO {f.T("NguoiDung")} VALUES(1,1,'STAFF','HOAT_DONG');");
            return f;
        }
        public async ValueTask DisposeAsync()
        {
            foreach (string table in Tables) await Db.ExecuteAsync($"DROP TABLE IF EXISTS {T(table)}");
            await Db.DisposeAsync();
        }
    }
    private sealed class ReviewSqlAttribute : FactAttribute
    {
        public ReviewSqlAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHOTOCOPY_AUDIT_TEST_SQL")) || string.IsNullOrWhiteSpace(Root))
                Skip = "Opt-in: chỉ chạy trên tempdb với PHOTOCOPY_AUDIT_TEST_SQL và PHOTOCOPY_REVIEW_ROOT.";
        }
    }
}
