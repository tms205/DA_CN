using Microsoft.AspNetCore.Http;
using PhotocopyApi.BaoTri;
using Xunit;

namespace PhotocopyApi.Tests;

public class BaoTriThanhToanTests
{
    [Theory]
    [InlineData("POST","/api/thanh-toan/webhook",true)]
    [InlineData("GET","/api/thanh-toan/payos/ket-qua",true)]
    [InlineData("GET","/api/thanh-toan/webhook",false)]
    [InlineData("POST","/api/thanh-toan/webhook-gia",false)]
    [InlineData("POST","/api/thanh-toan/1/payos",false)]
    [InlineData("POST","/api/don-hang",false)]
    public async Task FullMaintenance_OnlyFinancialCallbacksRemainReachable(string method,string path,bool allowed)
    {
        string folder=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"PhotocopyMaintenanceTests",Guid.NewGuid().ToString("N")));
        try {
            var store=new BaoTriStore(Path.Combine(folder,"state.json"));store.Set(true,"Đang kiểm thử bảo trì");
            bool reached=false;var context=new DefaultHttpContext();context.Request.Method=method;context.Request.Path=path;
            context.Response.Body=new MemoryStream();
            await new BaoTriMiddleware(_=>{reached=true;return Task.CompletedTask;}).InvokeAsync(context,store);
            Assert.Equal(allowed,reached);Assert.Equal(allowed?200:503,context.Response.StatusCode);
        } finally {
            Assert.StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(),"PhotocopyMaintenanceTests"))+Path.DirectorySeparatorChar,folder);
            if(Directory.Exists(folder))Directory.Delete(folder,true);
        }
    }
}
