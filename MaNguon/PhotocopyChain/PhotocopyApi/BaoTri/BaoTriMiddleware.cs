using PhotocopyShared.Constants;
namespace PhotocopyApi.BaoTri;
public class BaoTriMiddleware
{
    private readonly RequestDelegate _next;
    public BaoTriMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context, BaoTriStore store)
    {
        var state = store.Read();
        string platform=BaoTriStore.NenTang(context.User);
        var path = context.Request.Path;
        if(platform==null && path.StartsWithSegments("/api/auth/admin"))platform="web";
        if(platform==null && path.Equals("/api/auth/login",StringComparison.OrdinalIgnoreCase))platform="app";
        if(platform==null && (path.StartsWithSegments("/api/auth/register")||path.Equals("/api/auth/google",StringComparison.OrdinalIgnoreCase)||path.StartsWithSegments("/api/auth/google/register")))platform="app";
        if(platform==null && path.StartsWithSegments("/api/auth") && context.Request.Headers["X-Photocopy-Platform"].ToString() is "web" or "app")
            platform=context.Request.Headers["X-Photocopy-Platform"].ToString();
        context.Items["maintenance_version"] = BaoTriStore.Version(state,platform??"web");
        bool full=BaoTriStore.KhoaToanBo(state,platform);
        if(platform==null && (path.StartsWithSegments("/api/may-in-ao")||path.StartsWithSegments("/api/printer-connector")||path.Equals("/api/thanh-toan/webhook",StringComparison.OrdinalIgnoreCase)||path.Equals("/api/thanh-toan/payos/ket-qua",StringComparison.OrdinalIgnoreCase)))full=state.DangBaoTri&&state.KhoaWeb&&state.KhoaApp;
        bool feature=BaoTriStore.ChucNang(state,platform).Contains(ChucNang(path));
        // Callback đã phát sinh từ cổng thanh toán phải được ghi nhận khi chỉ khóa một chức năng.
        if(!full && (path.Value?.Contains("webhook",StringComparison.OrdinalIgnoreCase)==true || path.Equals("/api/thanh-toan/payos/ket-qua",StringComparison.OrdinalIgnoreCase)))feature=false;
        if ((!full && !feature) || path.Equals("/api/bao-tri/trang-thai", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/bao-tri/phien", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/admin/login", StringComparison.OrdinalIgnoreCase) ||
            (path.Equals("/api/bao-tri", StringComparison.OrdinalIgnoreCase) && context.User.Identity?.IsAuthenticated == true && context.User.IsInRole(VaiTro.SuperAdmin)))
        { await _next(context); return; }
        context.Response.StatusCode = 503;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new { message = (full?"Hệ thống đang bảo trì.":"Chức năng đang bảo trì.")+"\n"+BaoTriStore.ThongBao(state), dangBaoTri=full, loiNhan=BaoTriStore.ThongBao(state),state.KetThucLuc });
    }
    public static string ChucNang(PathString path) {
        string p=path.Value?.ToLowerInvariant()??"";
        if(p.StartsWith("/api/auth"))return "DANG_NHAP";
        if(p.StartsWith("/api/quan-tri/bao-cao"))return "BAO_CAO";
        if(p.StartsWith("/api/don-hang"))return p.Contains("thanh-toan")||p.Contains("hoan-tien")||p.Contains("giao-tai-quay")?"THANH_TOAN":"DON_HANG";
        if(p.StartsWith("/api/file-in")||p.StartsWith("/api/quan-tri/file-in")||p.StartsWith("/api/mau-don"))return "FILE_IN";
        if(p.StartsWith("/api/thanh-toan"))return "THANH_TOAN";
        if(p.StartsWith("/api/giao-hang"))return "GIAO_HANG";
        if(p.StartsWith("/api/ocr"))return "OCR";
        if(p.StartsWith("/api/quan-tri")||p.StartsWith("/api/khach-hang")||p.StartsWith("/api/nhan-vien"))return "QUAN_LY";
        return "";
    }
}
