using PhotocopyShared.Constants;
namespace PhotocopyApi.BaoTri;
public class BaoTriMiddleware
{
    private readonly RequestDelegate _next;
    public BaoTriMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context, BaoTriStore store)
    {
        var state = store.Read();
        context.Items["maintenance_version"] = state.PhienBanPhien;
        var path = context.Request.Path;
        if (!state.DangBaoTri || path.Equals("/api/bao-tri/trang-thai", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/bao-tri/phien", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/admin/login", StringComparison.OrdinalIgnoreCase) ||
            (path.Equals("/api/bao-tri", StringComparison.OrdinalIgnoreCase) && context.User.Identity?.IsAuthenticated == true && context.User.IsInRole(VaiTro.SuperAdmin)))
        { await _next(context); return; }
        context.Response.StatusCode = 503;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new { message = "Hệ thống đang bảo trì.\n" + state.LoiNhan, dangBaoTri = true, loiNhan = state.LoiNhan });
    }
}
