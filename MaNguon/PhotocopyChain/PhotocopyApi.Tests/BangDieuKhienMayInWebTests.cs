using Xunit;

namespace PhotocopyApi.Tests;

public class BangDieuKhienMayInWebTests
{
    [Fact]
    public void Web_CoRoutePhanQuyenVaXacNhanBanGiayKhongLoKhoa()
    {
        var type = typeof(PhotocopyAdmin.Pages.MayIn);
        var route = (Microsoft.AspNetCore.Components.RouteAttribute)Attribute.GetCustomAttribute(type, typeof(Microsoft.AspNetCore.Components.RouteAttribute));
        var quyen = Attribute.GetCustomAttributes(type, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.Equal("/may-in", route.Template);
        Assert.All(quyen, x => Assert.Equal("SUPER_ADMIN,BRANCH_ADMIN,STAFF", x.Roles));
    }
}
