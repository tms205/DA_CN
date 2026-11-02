using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

// Chỉ tạo bảng # tạm trong phiên; hàm phân quyền thật chỉ được đọc.
// Không tạo/sửa/xóa đơn, giao dịch hay schema ứng dụng.
public class HuyDonSqlTests
{
    [SqlHuyDon]
    public async Task KhachHuyDonChoTiepNhanCoBangChung_GiuTienChoHoan()
    {
        await using var db = KetNoi();
        await db.OpenAsync();
        var ids = await TaoDuLieuTam(db, "CHO_XAC_NHAN");
        await ThucThi(db, "sp_HuyDonHang", new { MaDonHang = 1, MaNguoiHuy = ids.Khach, LyDo = "Không còn nhu cầu" });
        Assert.Equal("DA_HUY", await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #DonHang"));
        Assert.Null(await db.ExecuteScalarAsync<int?>("SELECT MaMayIn FROM #DonHang"));
        Assert.Equal("DA_THANH_TOAN", await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #ThanhToan"));
        Assert.Equal(2, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #ThongBao WHERE MaNguoiDung IN(@Khach,@Ceo)",ids));
        await ThucThi(db, "sp_HoanTien", new { MaDonHang = 1, MaNguoiDuyet = ids.Ceo });
        Assert.Equal("DA_HOAN_TIEN", await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #ThanhToan"));
        await Assert.ThrowsAsync<SqlException>(() => ThucThi(db, "sp_HoanTien", new { MaDonHang = 1, MaNguoiDuyet = ids.Ceo }));
    }

    [SqlHuyDon]
    public async Task CeoKhongHuyDuocBatKyTrangThaiNaoTuDangInTroDi()
    {
        foreach (string state in new[] { "DANG_IN", "DA_IN", "DANG_GIAO", "DA_GIAO", "GIAO_THAT_BAI", "HOAN_THANH", "DA_HUY" })
        {
            await using var db = KetNoi();
            await db.OpenAsync();
            var ids = await TaoDuLieuTam(db, state);
            await Assert.ThrowsAsync<SqlException>(() => ThucThi(db, "sp_HuyDonHang", new { MaDonHang = 1, MaNguoiHuy = ids.Ceo, LyDo = "Không được hủy sau in" }));
            Assert.Equal(state, await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #DonHang"));
            Assert.Equal("DA_THANH_TOAN", await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #ThanhToan"));
            Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #ThongBao"));
        }
    }

    [SqlHuyDon]
    public async Task DonHuyCuDaInCoMayIn_KhongDuocXacNhanHoanQuaLuongThongThuong()
    {
        await using var db = KetNoi();
        await db.OpenAsync();
        var ids = await TaoDuLieuTam(db, "DA_HUY");
        await Assert.ThrowsAsync<SqlException>(() => ThucThi(db, "sp_HoanTien", new { MaDonHang = 1, MaNguoiDuyet = ids.Ceo }));
        Assert.Equal("DA_THANH_TOAN", await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #ThanhToan"));
    }

    [SqlHuyDon]
    public async Task TienVeSauHuy_GiuDonDaHuyVaChiGhiNhanTienMotLan()
    {
        await using var db = KetNoi();
        await db.OpenAsync();
        await TaoDuLieuTam(db, "DA_HUY");
        await db.ExecuteAsync("UPDATE #DonHang SET MaMayIn=NULL; UPDATE #ThanhToan SET TrangThai='CHUA_THANH_TOAN'");
        for (int i = 0; i < 2; i++)
            await ThucThi(db, "sp_XacNhanChuyenKhoan", new { MaDonHang = 1, MaGiaoDich = "test-money-late" });
        Assert.Equal("DA_HUY", await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #DonHang"));
        Assert.Equal("DA_THANH_TOAN", await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #ThanhToan"));
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #ThongBao"));
        Assert.Contains("hoàn", await db.ExecuteScalarAsync<string>("SELECT NoiDung FROM #ThongBao"));
    }

    private static SqlConnection KetNoi()
    {
        var doc = typeof(DonHangController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv")!;
        var values = (Dictionary<string, string>)doc.GetMethod("Doc")!.Invoke(null,
            new object[] { Environment.GetEnvironmentVariable("PHOTOCOPY_CANCEL_SQL_ENV_FILE")! })!;
        return new SqlConnection(values["ConnectionStrings:PhotocopyChain"]);
    }

    private static async Task<Ids> TaoDuLieuTam(SqlConnection db, string state)
    {
        var ids = await db.QuerySingleAsync<Ids>(@"SELECT
            (SELECT TOP(1) MaNguoiDung FROM dbo.NguoiDung WHERE VaiTro='CUSTOMER' ORDER BY MaNguoiDung) AS Khach,
            (SELECT TOP(1) MaNguoiDung FROM dbo.NguoiDung WHERE VaiTro='SUPER_ADMIN' ORDER BY MaNguoiDung) AS Ceo,
            (SELECT TOP(1) MaChiNhanh FROM dbo.ChiNhanh ORDER BY MaChiNhanh) AS Nhanh");
        Assert.True(ids.Khach > 0 && ids.Ceo > 0 && ids.Nhanh > 0);
        await db.ExecuteAsync(@"CREATE TABLE #DonHang(MaDonHang INT PRIMARY KEY,MaChiNhanh INT,MaKhachHang INT,
                TrangThai VARCHAR(20) COLLATE DATABASE_DEFAULT,MaMayIn INT NULL,GhiChu NVARCHAR(255) COLLATE DATABASE_DEFAULT,NgayHenTra DATETIME NULL,BangChungHuyJson NVARCHAR(MAX) NULL);
            CREATE TABLE #ThanhToan(MaDonHang INT PRIMARY KEY,TrangThai VARCHAR(20) COLLATE DATABASE_DEFAULT,PhuongThuc VARCHAR(10) COLLATE DATABASE_DEFAULT,MaGiaoDich VARCHAR(50) COLLATE DATABASE_DEFAULT,NgayThanhToan DATETIME);
            CREATE TABLE #ThongBao(MaNguoiDung INT,MaDonHang INT,TieuDe NVARCHAR(255) COLLATE DATABASE_DEFAULT,NoiDung NVARCHAR(500) COLLATE DATABASE_DEFAULT);");
        await db.ExecuteAsync(@"INSERT #DonHang VALUES(1,@Nhanh,@Khach,@state,123,NULL,DATEADD(HOUR,1,GETDATE()),NULL);
            INSERT #ThanhToan VALUES(1,'DA_THANH_TOAN','VIETQR',NULL,NULL);", new { ids.Nhanh, ids.Khach, state });
        return ids;
    }

    private static Task<int> ThucThi(SqlConnection db, string name, object args)
    {
        string sql = ThuTuc(name);
        sql = sql[Regex.Match(sql, @"\bAS\s*\r?\nBEGIN").Index..];
        sql = Regex.Replace(sql, @"^AS\s*", "");
        foreach (string table in new[] { "DonHang", "ThanhToan", "ThongBao" })
            sql = Regex.Replace(sql, @"\b(?:dbo\.)?" + table + @"\b", "#" + table);
        var parameters=new DynamicParameters(args);
        if(name=="sp_HuyDonHang") {
            int actor=(int)args.GetType().GetProperty("MaNguoiHuy").GetValue(args);
            parameters.Add("BangChungJson",System.Text.Json.JsonSerializer.Serialize(new{Loai="THU_CONG",MaDonHang=1,MaNguoiGui=actor,Anh=new[]{new{MaAnh="fixture"}}}));
        }
        return db.ExecuteAsync(sql, parameters);
    }

    private static string ThuTuc(string name)
    {
        string root = Environment.GetEnvironmentVariable("PHOTOCOPY_SOURCE_ROOT") ?? Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(root);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Database", "04_StoredProcedure.sql"))) dir = dir.Parent;
        Assert.NotNull(dir);
        foreach(string file in new[]{"018-BangChungHuyDon.sql","014-HuyDonTruocIn_HoanTien.sql","04_StoredProcedure.sql"}) {
            string source=File.ReadAllText(Path.Combine(dir.FullName,"Database",file));
            var match=Regex.Match(source,@"^CREATE(?: OR ALTER)?\s+PROCEDURE (?:dbo\.)?"+name+@"\b.*?(?=^GO\s*$)",RegexOptions.Multiline|RegexOptions.Singleline);
            if(match.Success)return match.Value;
        }
        throw new InvalidDataException("Không tìm thấy thủ tục "+name);
    }

    private sealed class Ids { public int Khach { get; set; } public int Ceo { get; set; } public int Nhanh { get; set; } }
    private sealed class SqlHuyDonAttribute : FactAttribute
    {
        public SqlHuyDonAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHOTOCOPY_CANCEL_SQL_ENV_FILE")))
                Skip = "Chỉ chạy khi có cấu hình để đọc vai trò và kiểm thử bằng bảng # tạm.";
        }
    }
}
