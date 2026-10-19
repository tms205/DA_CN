using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

// Chỉ ghi vào bảng # tạm riêng của kết nối, không thay dữ liệu nghiệp vụ thật.
public class TachVatTuGiayTests
{
    [KiemThuTachGiay]
    public async Task TachGiay_GiuTonChuaPhanLoai_GiaVaDonCu_ChayLaiKhongTrung()
    {
        using var conn = KetNoi();
        await conn.OpenAsync();
        await TaoBangTam(conn);
        await conn.ExecuteAsync(@"
            INSERT #VatTu VALUES (N'Giấy A4',N'Tờ',120),(N'Giấy A3',N'Tờ',240),(N'Giấy ảnh A4',N'tờ',0);
            INSERT #TonKho VALUES (1,1,1234,100),(2,1,456,50),(1,2,789,40),(1,3,20,5);
            INSERT #DichVu (TenDichVu,LoaiDichVu,KhoGiay,LoaiGiay,MatIn,MauSac,DonGia,MaVatTu,TrangThai)
            VALUES (N'A4 70','IN_AN','A4','70gsm','1_MAT','DEN_TRANG',300,1,N'HIEN'),
                   (N'A4 80','IN_AN','A4','80gsm','2_MAT','DEN_TRANG',350,1,N'HIEN'),
                   (N'A3 70','IN_AN','A3','70gsm','1_MAT','DEN_TRANG',600,2,N'HIEN'),
                   (N'Ảnh thẻ','IN_AN','A4','GIAY_ANH','1_MAT','MAU',5000,3,N'HIEN');
            INSERT #BangGiaChiNhanh VALUES (1,1,321,N'HIEN'),(2,1,345,N'AN'),(1,2,456,N'HIEN'),(1,3,678,N'HIEN'),(1,4,5000,N'HIEN');
            INSERT #ChiTietDonHang VALUES (9,1,100,3,321);");

        await conn.ExecuteAsync(SqlMigration());
        var maGiay = (await conn.QueryAsync<int>(@"SELECT DISTINCT dv.MaVatTu FROM #DichVu dv
            WHERE dv.TrangThai=N'HIEN' AND dv.LoaiGiay IN ('70gsm','80gsm') ORDER BY dv.MaVatTu")).ToArray();
        Assert.Equal(3, maGiay.Length);
        Assert.DoesNotContain(1, maGiay);
        Assert.DoesNotContain(2, maGiay);
        Assert.Equal(1234, await conn.ExecuteScalarAsync<int>("SELECT SoLuong FROM #TonKho WHERE MaChiNhanh=1 AND MaVatTu=1"));
        Assert.Equal(456, await conn.ExecuteScalarAsync<int>("SELECT SoLuong FROM #TonKho WHERE MaChiNhanh=2 AND MaVatTu=1"));
        Assert.Equal(789, await conn.ExecuteScalarAsync<int>("SELECT SoLuong FROM #TonKho WHERE MaChiNhanh=1 AND MaVatTu=2"));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT SUM(SoLuong) FROM #TonKho WHERE MaVatTu IN @maGiay", new { maGiay }));
        Assert.Equal(6, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #TonKho WHERE MaVatTu IN @maGiay", new { maGiay }));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT SUM(DonGia) FROM #VatTu WHERE MaVatTu IN @maGiay", new { maGiay }));
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT MaVatTu FROM #DichVu WHERE MaDichVu=1"));
        Assert.Equal(321m, await conn.ExecuteScalarAsync<decimal>("SELECT DonGia FROM #ChiTietDonHang WHERE MaDonHang=9"));
        Assert.Equal(321m, await conn.ExecuteScalarAsync<decimal>(@"SELECT bg.DonGia FROM #BangGiaChiNhanh bg JOIN #DichVu dv ON dv.MaDichVu=bg.MaDichVu
            WHERE bg.MaChiNhanh=1 AND dv.LoaiGiay='70gsm' AND dv.KhoGiay='A4' AND dv.TrangThai=N'HIEN'"));
        Assert.Equal("AN", await conn.ExecuteScalarAsync<string>(@"SELECT bg.TrangThai FROM #BangGiaChiNhanh bg JOIN #DichVu dv ON dv.MaDichVu=bg.MaDichVu
            WHERE bg.MaChiNhanh=2 AND dv.LoaiGiay='70gsm' AND dv.KhoGiay='A4' AND dv.TrangThai=N'HIEN'"));
        Assert.Equal("AN", await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM #BangGiaChiNhanh WHERE MaChiNhanh=1 AND MaDichVu=1"));
        Assert.Equal(3, await conn.ExecuteScalarAsync<int>("SELECT MaVatTu FROM #DichVu WHERE MaDichVu=4"));
        var truoc = await conn.QuerySingleAsync("SELECT (SELECT COUNT(*) FROM #VatTu) AS VatTu,(SELECT COUNT(*) FROM #DichVu) AS DichVu,(SELECT COUNT(*) FROM #TonKho) AS TonKho");
        await conn.ExecuteAsync(SqlMigration());
        var sau = await conn.QuerySingleAsync("SELECT (SELECT COUNT(*) FROM #VatTu) AS VatTu,(SELECT COUNT(*) FROM #DichVu) AS DichVu,(SELECT COUNT(*) FROM #TonKho) AS TonKho");
        Assert.Equal((int)truoc.VatTu, (int)sau.VatTu);
        Assert.Equal((int)truoc.DichVu, (int)sau.DichVu);
        Assert.Equal((int)truoc.TonKho, (int)sau.TonKho);
    }

    [KiemThuTachGiay]
    public async Task DuLieuDaTach_GiuNguyenTonKhoVaLienKet()
    {
        using var conn = KetNoi();
        await conn.OpenAsync();
        await TaoBangTam(conn);
        await conn.ExecuteAsync(@"
            INSERT #VatTu VALUES (N'Giấy A4 70gsm',N'Tờ',150);
            INSERT #TonKho VALUES (1,1,777,100);
            INSERT #DichVu(TenDichVu,LoaiDichVu,KhoGiay,LoaiGiay,MatIn,MauSac,DonGia,MaVatTu,TrangThai)
                VALUES (N'A4 70','IN_AN','A4','70gsm','1_MAT','DEN_TRANG',300,1,N'HIEN');
            INSERT #BangGiaChiNhanh VALUES (1,1,321,N'HIEN');");
        await conn.ExecuteAsync(SqlMigration());
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #VatTu"));
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #DichVu"));
        Assert.Equal(777, await conn.ExecuteScalarAsync<int>("SELECT SoLuong FROM #TonKho WHERE MaChiNhanh=1 AND MaVatTu=1"));
        Assert.Equal(150m, await conn.ExecuteScalarAsync<decimal>("SELECT DonGia FROM #VatTu WHERE MaVatTu=1"));
    }

    private static SqlConnection KetNoi()
    {
        var doc = typeof(QuanTriController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var config = (Dictionary<string, string>)doc.GetMethod("Doc").Invoke(null,
            new object[] { Environment.GetEnvironmentVariable("PHOTOCOPY_REPORT_ENV_FILE") });
        return new SqlConnection(config["ConnectionStrings:PhotocopyChain"]);
    }

    private static string SqlMigration()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Database"))) dir = dir.Parent;
        Assert.NotNull(dir);
        string file = Path.Combine(dir.FullName, "Database", "013-TachVatTuGiayTheoQuyCach.sql");
        string sql = File.Exists(file) ? File.ReadAllText(file) : "SELECT 1;";
        sql = Regex.Replace(sql, @"(?im)^USE PhotocopyChain;\s*\r?\nGO\s*$", "");
        return Regex.Replace(sql, @"\bdbo\.(VatTu|DichVu|TonKho|ChiNhanh|BangGiaChiNhanh|ChiTietDonHang)\b", "#$1");
    }

    private static Task TaoBangTam(SqlConnection conn) => conn.ExecuteAsync(@"
        CREATE TABLE #VatTu(MaVatTu INT IDENTITY PRIMARY KEY,TenVatTu NVARCHAR(100) COLLATE DATABASE_DEFAULT,DonViTinh NVARCHAR(20) COLLATE DATABASE_DEFAULT,DonGia DECIMAL(12,0));
        CREATE TABLE #ChiNhanh(MaChiNhanh INT PRIMARY KEY);
        CREATE TABLE #TonKho(MaChiNhanh INT,MaVatTu INT,SoLuong INT,MucToiThieu INT,PRIMARY KEY(MaChiNhanh,MaVatTu));
        CREATE TABLE #DichVu(MaDichVu INT IDENTITY PRIMARY KEY,TenDichVu NVARCHAR(100) COLLATE DATABASE_DEFAULT,LoaiDichVu VARCHAR(10) COLLATE DATABASE_DEFAULT,KhoGiay VARCHAR(5) COLLATE DATABASE_DEFAULT,
            LoaiGiay VARCHAR(20) COLLATE DATABASE_DEFAULT,MatIn VARCHAR(10) COLLATE DATABASE_DEFAULT,MauSac VARCHAR(10) COLLATE DATABASE_DEFAULT,NhomGiaCong VARCHAR(20) COLLATE DATABASE_DEFAULT,KichThuocAnh VARCHAR(10) COLLATE DATABASE_DEFAULT,
            DonGia DECIMAL(12,0),MaVatTu INT,TrangThai NVARCHAR(20) COLLATE DATABASE_DEFAULT);
        CREATE TABLE #BangGiaChiNhanh(MaChiNhanh INT,MaDichVu INT,DonGia DECIMAL(12,0),TrangThai NVARCHAR(20) COLLATE DATABASE_DEFAULT,PRIMARY KEY(MaChiNhanh,MaDichVu));
        CREATE TABLE #ChiTietDonHang(MaDonHang INT,MaDichVu INT,SoTrang INT,SoBan INT,DonGia DECIMAL(12,0));
        INSERT #ChiNhanh VALUES (1),(2);");
}

public sealed class KiemThuTachGiayAttribute : FactAttribute
{
    public KiemThuTachGiayAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PHOTOCOPY_REPORT_ENV_FILE")))
            Skip = "Cần cấu hình kết nối để chạy migration trên các bảng # tạm, không sửa dữ liệu thật.";
    }
}
