using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyAdmin;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public class PheDuyetBadgeTests
{
    [Fact]
    public async Task KetQuaTaiCu_KhongGhiDeSoChoVuaCapNhatTuTrangPheDuyet()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var http = new HttpClient(new PendingHandler(response.Task)) { BaseAddress = new Uri("http://test.local") };
        var badge = new ThongBaoPheDuyet(http);
        var pending = badge.TaiAsync();
        badge.CapNhat(0);
        response.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { soPhieuChoDuyet = 1, soHoanTienChoDuyet = 0 }) });
        await pending;
        Assert.Equal(0, badge.SoCho);
    }

    [SqlBadge]
    public async Task DemHoanTien_ChiLayDonHuyTruocIn_DungPhamViChiNhanh()
    {
        var type = typeof(BaoCaoTongQuanController).Assembly.GetType("PhotocopyApi.DuLieu.BaoCaoTongQuanSql")!;
        var sql = (string)type.GetField("HienTai")!.GetRawConstantValue()!;
        sql = sql[sql.LastIndexOf("SELECT COUNT(*) FROM DonHang", StringComparison.Ordinal)..];
        sql = sql.Replace("FROM DonHang dh JOIN ThanhToan tt", "FROM #DonHang dh JOIN #ThanhToan tt");
        await using var db = new SqlConnection(Environment.GetEnvironmentVariable("PHOTOCOPY_APPROVAL_TEST_SQL"));
        await db.OpenAsync();
        await db.ExecuteAsync(@"
            CREATE TABLE #DonHang(MaDonHang INT, MaChiNhanh INT, MaMayIn INT NULL, TrangThai VARCHAR(30), YeuCauHoanTienJson NVARCHAR(MAX) NULL);
            CREATE TABLE #ThanhToan(MaDonHang INT, TrangThai VARCHAR(30));
            INSERT INTO #DonHang(MaDonHang,MaChiNhanh,MaMayIn,TrangThai) VALUES (1,1,NULL,'DA_HUY'),(2,1,1,'DA_HUY'),(3,1,NULL,'CHO_XAC_NHAN'),
              (4,1,NULL,'DA_HUY'),(5,2,NULL,'DA_HUY'),(6,1,NULL,'DA_HUY');
            INSERT INTO #ThanhToan VALUES (1,'DA_THANH_TOAN'),(2,'DA_THANH_TOAN'),(3,'DA_THANH_TOAN'),
              (4,'CHUA_THANH_TOAN'),(5,'DA_THANH_TOAN'),(6,'DA_HOAN_TIEN');");
        Assert.Equal(1, await db.ExecuteScalarAsync<int>(sql, new { MaChiNhanh = (int?)1 }));
        Assert.Equal(2, await db.ExecuteScalarAsync<int>(sql, new { MaChiNhanh = (int?)null }));
        Assert.Equal(0, await db.ExecuteScalarAsync<int>(sql, new { MaChiNhanh = (int?)3 }));
    }

    private sealed class PendingHandler(Task<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => response;
    }

    private sealed class SqlBadgeAttribute : FactAttribute
    {
        public SqlBadgeAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PHOTOCOPY_APPROVAL_TEST_SQL")))
                Skip = "Cần PHOTOCOPY_APPROVAL_TEST_SQL; kiểm tra trên bảng tạm riêng trong phiên, không ghi dữ liệu nghiệp vụ.";
        }
    }
}
