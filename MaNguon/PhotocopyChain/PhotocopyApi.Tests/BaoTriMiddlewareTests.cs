using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PhotocopyApi.BaoTri;
using Xunit;
namespace PhotocopyApi.Tests;
public class BaoTriMiddlewareTests
{
    [Theory]
    [InlineData("/api/don-hang", false, false)]
    [InlineData("/api/don-hang", true, false)]
    [InlineData("/api/quan-tri/bao-cao/hien-tai", true, false)]
    [InlineData("/api/quan-tri/bao-cao/hien-tai", false, false)]
    [InlineData("/hubs/cap-nhat", true, false)]
    [InlineData("/api/auth/login", false, false)]
    [InlineData("/api/bao-tri", false, false)]
    [InlineData("/api/bao-tri", true, true)]
    [InlineData("/api/bao-tri/trang-thai", false, true)]
    [InlineData("/api/auth/admin/login", false, true)]
    [InlineData("/api/thanh-toan/webhook", false, false)]
    public async Task MaintenanceRestrictsCeoToMaintenanceControls(string path, bool ceo, bool allow)
    {
        var dir = Path.Combine(Path.GetTempPath(), "BaoTriTest-" + Guid.NewGuid());
        try {
            var store = new BaoTriStore(Path.Combine(dir, "state.json")); store.Set(true, "Nâng cấp");
            bool called = false;
            var middleware = new BaoTriMiddleware(_ => { called = true; return Task.CompletedTask; });
            var context = new DefaultHttpContext(); context.Request.Path = path; context.Response.Body = new MemoryStream();
            if (ceo) context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "SUPER_ADMIN") }, "test"));
            await middleware.InvokeAsync(context, store);
            Assert.Equal(allow, called);
            if (!allow) Assert.Equal(503, context.Response.StatusCode);
        } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [Fact]
    public async Task UnauthenticatedRoleClaimCannotBypassMaintenance()
    {
        var dir = Path.Combine(Path.GetTempPath(), "BaoTriTest-" + Guid.NewGuid());
        try {
            var store = new BaoTriStore(Path.Combine(dir, "state.json")); store.Set(true, "Nâng cấp");
            bool called = false;
            var middleware = new BaoTriMiddleware(_ => { called = true; return Task.CompletedTask; });
            var context = new DefaultHttpContext(); context.Request.Path = "/api/don-hang"; context.Response.Body = new MemoryStream();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "SUPER_ADMIN") }));
            await middleware.InvokeAsync(context, store);
            Assert.False(called);
            Assert.Equal(503, context.Response.StatusCode);
        } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [Theory]
    [InlineData(0, 0, false, true)]
    [InlineData(0, 1, false, false)]
    [InlineData(1, 1, false, true)]
    [InlineData(0, 1, true, true)]
    public void OldSessionsCannotBeReused(long tokenVersion, long currentVersion, bool ceo, bool valid)
        => Assert.Equal(valid, BaoTriStore.SessionValid(tokenVersion, currentVersion, ceo));
}
