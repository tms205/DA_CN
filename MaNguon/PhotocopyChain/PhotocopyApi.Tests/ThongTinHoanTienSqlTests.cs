using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Controllers;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class ThongTinHoanTienSqlTests
{
    [SqlHoan]
    public async Task ChiChuDonHuyTruocInDangChoHoanDuocSuaGhiChu()
    {
        foreach (var test in new (string State, string Payment, int Owner, int? Printer, bool Allowed)[] {
            ("DA_HUY", "DA_THANH_TOAN", 1, null, true),
            ("DA_HUY", "DA_THANH_TOAN", 2, null, false),
            ("DA_HUY", "DA_HOAN_TIEN", 1, null, false),
            ("DA_HUY", "CHUA_THANH_TOAN", 1, null, false),
            ("DA_HUY", "DA_THANH_TOAN", 1, (int?)4, false),
            ("DANG_IN", "DA_THANH_TOAN", 1, (int?)4, false),
            ("CHO_XAC_NHAN", "DA_THANH_TOAN", 1, null, false)
        }) {
            await using var db = KetNoi();
            await db.OpenAsync();
            await db.ExecuteAsync(@"CREATE TABLE #DonHang(MaDonHang INT,MaKhachHang INT,MaMayIn INT NULL,TrangThai VARCHAR(20),GhiChu NVARCHAR(255));
                CREATE TABLE #ThanhToan(MaDonHang INT,TrangThai VARCHAR(20));");
            await db.ExecuteAsync(@"INSERT #DonHang VALUES(1,1,@Printer,@State,N'Ghi chú cũ');
                INSERT #ThanhToan VALUES(1,@Payment)", new { test.Printer, test.State, test.Payment });
            string memo = new ThongTinHoanTienRequest { LyDo = "Không cần in", NganHang = "VCB", SoTaiKhoan = "0012345678" }.TaoGhiChu();
            var field = typeof(DonHangController).GetField("SqlLuuThongTinHoanTien", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            string sql = ((string)field.GetRawConstantValue()!).Replace("dbo.DonHang", "#DonHang").Replace("dbo.ThanhToan", "#ThanhToan");
            int changed = await db.ExecuteAsync(sql, new { MaDonHang = 1, MaKhachHang = test.Owner, GhiChu = memo });
            Assert.Equal(test.Allowed ? 1 : 0, changed);
            Assert.Equal(test.Allowed ? memo : "Ghi chú cũ", await db.ExecuteScalarAsync<string>("SELECT GhiChu FROM #DonHang"));
            Assert.Equal(test.Payment, await db.ExecuteScalarAsync<string>("SELECT TrangThai FROM #ThanhToan"));
        }
    }

    private static SqlConnection KetNoi()
    {
        var doc = typeof(DonHangController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv")!;
        var values = (Dictionary<string, string>)doc.GetMethod("Doc")!.Invoke(null,
            new object[] { Environment.GetEnvironmentVariable("PHOTOCOPY_CANCEL_SQL_ENV_FILE")! })!;
        return new SqlConnection(values["ConnectionStrings:PhotocopyChain"]);
    }

    private sealed class SqlHoanAttribute : FactAttribute
    {
        public SqlHoanAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHOTOCOPY_CANCEL_SQL_ENV_FILE")))
                Skip = "Chỉ kiểm thử bảng # tạm khi có cấu hình SQL riêng; không ghi đơn thật.";
        }
    }
}
