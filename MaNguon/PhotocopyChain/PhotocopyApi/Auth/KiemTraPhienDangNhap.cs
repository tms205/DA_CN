using System.Collections.Concurrent;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;

namespace PhotocopyApi.Auth;

public sealed class KiemTraPhienDangNhap
{
    private readonly string _connection;
    private readonly PhienDangNhapStore _store;
    private readonly ConcurrentDictionary<string, HubCallerContext> _ketNoi = new();
    private const string SqlNguoiDung = "SELECT MaNguoiDung, MatKhau, VaiTro, MaChiNhanh, TrangThai FROM NguoiDung";

    public KiemTraPhienDangNhap(IConfiguration config, PhienDangNhapStore store) =>
        (_connection, _store) = (config.GetConnectionString("PhotocopyChain"), store);

    public async Task<bool> HopLeAsync(ClaimsPrincipal principal, CancellationToken dung)
    {
        if (!int.TryParse(principal?.FindFirst(TenClaim.MaNguoiDung)?.Value, out int id)) return false;
        using var conn = new SqlConnection(_connection);
        var user = await conn.QuerySingleOrDefaultAsync<NguoiDung>(new CommandDefinition(
            SqlNguoiDung + " WHERE MaNguoiDung=@Id", new { Id = id }, cancellationToken: dung));
        return _store.HopLe(principal, user);
    }

    public void Them(HubCallerContext context) => _ketNoi[context.ConnectionId] = context;
    public void Xoa(string id) => _ketNoi.TryRemove(id, out _);
    public void Ngat(int id)
    {
        foreach (var context in _ketNoi.Values)
            if (context.User?.FindFirst(TenClaim.MaNguoiDung)?.Value == id.ToString()) context.Abort();
    }

    public async Task KiemTraKetNoiAsync(CancellationToken dung)
    {
        var contexts = _ketNoi.Values.ToArray();
        int[] ids = contexts.Select(c => int.TryParse(c.User?.FindFirst(TenClaim.MaNguoiDung)?.Value, out int id) ? id : 0).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0) return;
        using var conn = new SqlConnection(_connection);
        var users = (await conn.QueryAsync<NguoiDung>(new CommandDefinition(
            SqlNguoiDung + " WHERE MaNguoiDung IN @Ids", new { Ids = ids }, cancellationToken: dung))).ToDictionary(u => u.MaNguoiDung);
        foreach (var context in contexts)
        {
            int.TryParse(context.User?.FindFirst(TenClaim.MaNguoiDung)?.Value, out int id);
            if (!_store.HopLe(context.User, users.GetValueOrDefault(id))) context.Abort();
        }
    }
}

public sealed class KiemTraKetNoiPhienJob(KiemTraPhienDangNhap phien, ILogger<KiemTraKetNoiPhienJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await phien.KiemTraKetNoiAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Chưa kiểm tra được các kết nối phiên đăng nhập."); }
        }
    }
}
