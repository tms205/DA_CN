using PhotocopyApi.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public class TrangThaiMayInAoTests
{
    [Fact]
    public void Qua30GiayKhongHeartbeat_MayMatKetNoi()
    {
        var gio = new DongHoThu();
        var store = new TrangThaiMayInAoStore(gio);
        store.CapNhat(1, "SAN_SANG");
        Assert.Equal("SAN_SANG", Assert.Single(store.LayTatCa()).TrangThai);
        gio.Gio = gio.Gio.AddSeconds(31);
        Assert.Equal("MAT_KET_NOI", Assert.Single(store.LayTatCa()).TrangThai);
        store.CapNhat(1, "DANG_IN", 6);
        Assert.Equal(6, Assert.Single(store.LayTatCa()).MaDonHangDangIn);
    }

    private sealed class DongHoThu : TimeProvider
    {
        public DateTimeOffset Gio { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Gio;
    }
}
