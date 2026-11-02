using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.BaoTri;
using PhotocopyShared.Constants;

namespace PhotocopyApi.CaLamViec;

public static class LichCa
{
    public static DateTime GioKetThuc(DateTime gioMo)
    {
        TimeSpan t = gioMo.TimeOfDay;
        return t < new TimeSpan(7,30,0) || t >= new TimeSpan(21,30,0) ? gioMo
            : gioMo.Date.AddMinutes(t < new TimeSpan(12,30,0) ? 750 : t < new TimeSpan(17,30,0) ? 1050 : 1290);
    }
    public static bool TrongGio(DateTime ngay) => ngay.TimeOfDay >= new TimeSpan(7,30,0) && ngay.TimeOfDay < new TimeSpan(21,30,0);
    public static DateTime BayGio(TimeProvider clock) => TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime,
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
}

public sealed class LichCaService(IConfiguration config, TimeProvider clock)
{
    // Cập nhật có điều kiện: đã chuyển trạng thái thì lần chạy sau không sửa lại giờ/tiền.
    public const string SqlChot = @"
        UPDATE ca SET TrangThai='CHO_TONG_KET', GioDongCa=kt.KetThuc
        FROM CaLamViec ca
        CROSS APPLY (SELECT CASE
            WHEN CAST(ca.GioMoCa AS TIME)>='07:30' AND CAST(ca.GioMoCa AS TIME)<'12:30' THEN DATEADD(MINUTE,750,CAST(CAST(ca.GioMoCa AS DATE) AS DATETIME))
            WHEN CAST(ca.GioMoCa AS TIME)>='12:30' AND CAST(ca.GioMoCa AS TIME)<'17:30' THEN DATEADD(MINUTE,1050,CAST(CAST(ca.GioMoCa AS DATE) AS DATETIME))
            WHEN CAST(ca.GioMoCa AS TIME)>='17:30' AND CAST(ca.GioMoCa AS TIME)<'21:30' THEN DATEADD(MINUTE,1290,CAST(CAST(ca.GioMoCa AS DATE) AS DATETIME))
            ELSE ca.GioMoCa END AS KetThuc) kt
        WHERE ca.TrangThai='DANG_MO' AND @BayGio>=kt.KetThuc
          AND (@MaNhanVien IS NULL OR ca.MaNhanVien=@MaNhanVien);";

    public async Task ChotHetGioAsync(int? maNhanVien, CancellationToken dung)
    {
        using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        await conn.ExecuteAsync(new CommandDefinition(SqlChot,
            new { MaNhanVien = maNhanVien, BayGio = LichCa.BayGio(clock) }, cancellationToken: dung));
    }

    public async Task<bool> ConCaChuaTongKetAsync(int id, CancellationToken dung)
    {
        using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM CaLamViec WHERE MaNhanVien=@Id AND TrangThai='CHO_TONG_KET') THEN 1 ELSE 0 END AS BIT)",
            new { Id = id }, cancellationToken: dung));
    }
}

public sealed class ChotCaHetGioJob(LichCaService lich, BaoTriStore baoTri, ILogger<ChotCaHetGioJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try { if (!BaoTriStore.KhoaTacVuNen(baoTri.Read())) await lich.ChotHetGioAsync(null, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Chưa chốt được ca hết giờ. Sẽ thử lại ở nhịp kế tiếp."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class CaHetGioMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, LichCaService lich)
    {
        string role = context.User.FindFirst(TenClaim.VaiTro)?.Value;
        bool nhanSu = role is VaiTro.SuperAdmin or VaiTro.BranchAdmin or VaiTro.Staff;
        bool giaoDich = context.Request.Path.StartsWithSegments("/api/don-hang") || context.Request.Path.StartsWithSegments("/api/thanh-toan");
        if (nhanSu && giaoDich && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
            int.TryParse(context.User.FindFirst(TenClaim.MaNguoiDung)?.Value, out int id))
        {
            await lich.ChotHetGioAsync(id, context.RequestAborted);
            if (await lich.ConCaChuaTongKetAsync(id, context.RequestAborted))
            {
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new { message = "Ca đã hết giờ. Vui lòng vào Ca làm việc & két tiền để nhập tiền thực đếm và tổng kết trước khi xử lý giao dịch mới." });
                return;
            }
        }
        await next(context);
    }
}
