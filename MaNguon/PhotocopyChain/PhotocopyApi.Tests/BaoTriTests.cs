using PhotocopyApi.BaoTri;
using Xunit;
namespace PhotocopyApi.Tests;
public class BaoTriTests
{
    [Fact]
    public void EnabledStateAndSessionGenerationSurviveRestart()
    {
        var dir = Path.Combine(Path.GetTempPath(), "BaoTriTest-" + Guid.NewGuid());
        try {
            var store = new BaoTriStore(Path.Combine(dir, "state.json"));
            Assert.False(store.Read().DangBaoTri);
            store.Set(true, "Nâng cấp hệ thống");
            var loaded = new BaoTriStore(Path.Combine(dir, "state.json"));
            Assert.True(loaded.Read().DangBaoTri);
            Assert.Equal("Nâng cấp hệ thống", loaded.Read().LoiNhan);
            Assert.Equal(1, loaded.Read().PhienBanPhien);
            loaded.Set(false, "");
            Assert.Equal(1, loaded.Read().PhienBanPhien);
            loaded.Set(true, "Lần hai");
            Assert.Equal(2, loaded.Read().PhienBanPhien);
        } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [Fact]
    public void RequiresMessageForEnabling() {
        var store = new BaoTriStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".json"));
        Assert.Throws<ArgumentException>(() => store.Set(true, " "));
    }
}
