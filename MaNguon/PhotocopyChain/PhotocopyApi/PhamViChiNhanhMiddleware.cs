using System.Security.Claims;
using PhotocopyShared.Constants;

namespace PhotocopyApi;

// CEO có thể xem toàn chuỗi hoặc làm việc trong phạm vi một chi nhánh.
// Chỉ request của SUPER_ADMIN mới được dùng header; các vai trò khác luôn giữ chi nhánh trong JWT.
public sealed class PhamViChiNhanhMiddleware
{
    private readonly RequestDelegate _xuLyTiep;

    public PhamViChiNhanhMiddleware(RequestDelegate xuLyTiep)
    {
        _xuLyTiep = xuLyTiep;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.User.IsInRole(VaiTro.SuperAdmin) ||
            !context.Request.Headers.TryGetValue(TenHeader.MaChiNhanh, out var giaTriHeader))
        {
            await _xuLyTiep(context);
            return;
        }

        if (!int.TryParse(giaTriHeader, out int maChiNhanh) || maChiNhanh <= 0)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Mã chi nhánh được chọn không hợp lệ." });
            return;
        }

        var identity = context.User.Identities.First(x => x.IsAuthenticated && x.HasClaim(TenClaim.VaiTro, VaiTro.SuperAdmin));
        foreach (Claim claim in identity.FindAll(TenClaim.VaiTro).ToList())
            identity.RemoveClaim(claim);
        foreach (Claim claim in identity.FindAll(TenClaim.MaChiNhanh).ToList())
            identity.RemoveClaim(claim);

        // BRANCH_ADMIN đứng trước để code cũ dùng FindFirst(...) xử lý CEO giống quản lý chi nhánh.
        // Vẫn giữ SUPER_ADMIN để các endpoint cấp chuỗi như phê duyệt tiếp tục hoạt động.
        identity.AddClaim(new Claim(TenClaim.VaiTro, VaiTro.BranchAdmin));
        identity.AddClaim(new Claim(TenClaim.VaiTro, VaiTro.SuperAdmin));
        identity.AddClaim(new Claim(TenClaim.MaChiNhanh, maChiNhanh.ToString()));

        await _xuLyTiep(context);
    }
}
