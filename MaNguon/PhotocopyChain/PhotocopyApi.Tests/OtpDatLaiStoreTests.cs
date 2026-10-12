using Microsoft.Extensions.Caching.Memory;
using PhotocopyApi.Auth;
using Xunit;

namespace PhotocopyApi.Tests;

public class OtpDatLaiStoreTests
{
    private static OtpDatLaiStore TaoStore(MemoryCache cache, TimeProvider clock)
    {
        return new OtpDatLaiStore(cache, clock);
    }

    [Fact]
    public void MaDung_ChiDungMotLan()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = TaoStore(cache, TimeProvider.System);
        Assert.Equal(0, store.ThuBatDauGui(" TEST@example.com "));
        store.LuuMa("test@example.com", "123456");
        Assert.True(store.DungMa("test@example.com", "123456"));
        Assert.False(store.DungMa("test@example.com", "123456"));
    }

    [Fact]
    public void GuiLai_PhaiCho60Giay_VaMaMoiThayMaCu()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new DongHo();
        var store = TaoStore(cache, clock);
        Assert.Equal(0, store.ThuBatDauGui("test@example.com"));
        store.LuuMa("test@example.com", "123456");
        Assert.Equal(60, store.ThuBatDauGui("test@example.com"));
        clock.Tang(TimeSpan.FromSeconds(60));
        Assert.Equal(0, store.ThuBatDauGui("test@example.com"));
        store.LuuMa("test@example.com", "654321");
        Assert.False(store.DungMa("test@example.com", "123456"));
        Assert.True(store.DungMa("test@example.com", "654321"));
    }

    [Fact]
    public void Sai5Lan_Khoa15Phut_KeCaGuiLai()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new DongHo();
        var store = TaoStore(cache, clock);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        for (int i = 0; i < 5; i++) Assert.False(store.DungMa("test@example.com", "000000"));
        Assert.False(store.DungMa("test@example.com", "123456"));
        Assert.Equal(900, store.ThuBatDauGui("test@example.com"));
        clock.Tang(TimeSpan.FromMinutes(15));
        Assert.Equal(0, store.ThuBatDauGui("test@example.com"));
    }

    [Fact]
    public void MaQua10Phut_HoacGuiEmailLoi_KhongDatLaiDuoc()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new DongHo();
        var store = TaoStore(cache, clock);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        clock.Tang(TimeSpan.FromMinutes(10));
        Assert.False(store.DungMa("test@example.com", "123456"));
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "654321");
        store.HuyMa("test@example.com");
        Assert.False(store.DungMa("test@example.com", "654321"));
    }

    [Fact]
    public async Task HaiRequestDongThoi_ChiMotRequestDungDuocMa()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = TaoStore(cache, TimeProvider.System);
        store.ThuBatDauGui("test@example.com");
        store.LuuMa("test@example.com", "123456");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => store.DungMa("test@example.com", "123456"))));
        Assert.Single(results, x => x);
    }

    [Fact]
    public void MaCuaEmailKhac_KhongDuocDung_VaDangGuiKhongDuocGuiTrung()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = TaoStore(cache, TimeProvider.System);
        Assert.Equal(0, store.ThuBatDauGui("a@example.com"));
        Assert.True(store.ThuBatDauGui("a@example.com") > 0);
        store.LuuMa("a@example.com", "123456");
        Assert.False(store.DungMa("b@example.com", "123456"));
        Assert.True(store.DungMa("a@example.com", "123456"));
    }

    private sealed class DongHo : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Tang(TimeSpan duration) => _now += duration;
    }
}
