using Microsoft.Extensions.Caching.Memory;
using PhotocopyApi.Auth;
using Xunit;
namespace PhotocopyApi.Tests;
public class OtpDangKyDongThoiTests
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow()=>Now; }
    [Fact]
    public async Task GuiDongThoi_ChiMotLan_VaPayloadChiDuocLayMotLan()
    {
        using var cache=new MemoryCache(new MemoryCacheOptions()); var store=new OtpDatLaiStore(cache,TimeProvider.System);
        var admission=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>Task.Run(()=>store.ThuBatDauGui("register:a@gmail.com"))));
        Assert.Single(admission,x=>x==0);
        store.LuuMa("register:a@gmail.com","123456","payload",TimeSpan.FromMinutes(5));
        Assert.False(store.DungMa("a@gmail.com","123456"));
        var consumed=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>Task.Run(()=>store.DungMa<string>("register:a@gmail.com","123456",out var payload)?payload:null)));
        Assert.Single(consumed,x=>x=="payload");
    }
    [Fact]
    public void HetHanNamPhut_HuyGui_VaGioiHanSaiCungMotCoChe()
    {
        using var cache=new MemoryCache(new MemoryCacheOptions()); var clock=new Clock();var store=new OtpDatLaiStore(cache,clock);
        string key="register:a@gmail.com";store.ThuBatDauGui(key);store.LuuMa(key,"123456","first",TimeSpan.FromMinutes(5));
        clock.Now+=TimeSpan.FromMinutes(5);Assert.False(store.DungMa<string>(key,"123456",out _));
        store.ThuBatDauGui(key);store.LuuMa(key,"654321","second",TimeSpan.FromMinutes(5));
        for(int i=0;i<5;i++)Assert.False(store.DungMa<string>(key,"000000",out _));
        Assert.Equal(900,store.ThuBatDauGui(key));clock.Now+=TimeSpan.FromMinutes(15);
        store.ThuBatDauGui(key);store.LuuMa(key,"111111","third",TimeSpan.FromMinutes(5));store.HuyMa(key);
        Assert.False(store.DungMa<string>(key,"111111",out _));
    }
}
