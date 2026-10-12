using Dapper;
using Microsoft.Data.SqlClient;

namespace PhotocopyApi.MayInAo;

// Máy bật lại hoặc nhân viên nhập thêm vật tư: thử xếp lại các đơn đã trả tiền đang chờ.
public sealed class XepLaiDonChoInJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _chuoiKetNoi;
    private readonly ILogger<XepLaiDonChoInJob> _logger;

    public XepLaiDonChoInJob(IServiceScopeFactory scopeFactory, IConfiguration config,
        ILogger<XepLaiDonChoInJob> logger)
    {
        _scopeFactory = scopeFactory;
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken dung)
    {
        using var nhip = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await nhip.WaitForNextTickAsync(dung))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    using var conn = new SqlConnection(_chuoiKetNoi);
                    var ds = await conn.QueryAsync<int>(
                        @"SELECT dh.MaDonHang FROM DonHang dh
                          JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
                          WHERE dh.TrangThai IN ('CHO_XAC_NHAN', 'DA_XAC_NHAN')
                            AND tt.PhuongThuc='VIETQR' AND tt.TrangThai='DA_THANH_TOAN'
                          ORDER BY dh.InGap DESC, dh.NgayDat, dh.MaDonHang");
                    var xepIn = scope.ServiceProvider.GetRequiredService<TuDongInService>();
                    foreach (int maDon in ds)
                    {
                        if (dung.IsCancellationRequested) return;
                        await xepIn.XepInSauThanhToan(maDon);
                    }
                }
                catch (Exception ex) when (!dung.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Chưa thể xếp lại đơn chờ in; sẽ thử ở nhịp kế tiếp.");
                }
            }
        }
        catch (OperationCanceledException) when (dung.IsCancellationRequested) { }
    }
}
