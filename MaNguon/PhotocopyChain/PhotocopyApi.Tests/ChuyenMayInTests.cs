using System.Reflection;
using System.Text.Json;
using PhotocopyApi.Controllers;
using PhotocopyShared.Dtos.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public sealed class ChuyenMayInTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "photocopy-transfer-test-" + Guid.NewGuid());
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private Type TypeStore()
    {
        var t = typeof(MayInAoController).Assembly.GetType("PhotocopyApi.MayInAo.ChuyenMayInStore");
        Assert.NotNull(t);
        return t;
    }
    private object Store() => Activator.CreateInstance(TypeStore(), _dir);
    private static object Call(object o, string method, params object[] args) =>
        o.GetType().GetMethod(method)!.Invoke(o, args);
    private static T Prop<T>(object o, string name) => (T)o.GetType().GetProperty(name)!.GetValue(o);

    [Fact]
    public void YeuCauDung_PersistVaKhongTuXacNhanDaDung()
    {
        var store = Store();
        var x = Call(store, "YeuCauDung", 57, 1, 1, "token-a", "connector-a", 8, "Máy hết giấy");
        Assert.Equal("DANG_DUNG", Prop<string>(x, "TrangThai"));
        var saved = Call(Store(), "LayHienTai", 57);
        Assert.Equal(Prop<string>(x, "MaYeuCau"), Prop<string>(saved, "MaYeuCau"));
        Assert.True((bool)Call(store, "ChanLenhCu", 57, 1, "token-a"));
    }

    [Fact]
    public void DungChiNhanDungTokenVaConnector_ReplyLapIdempotent()
    {
        var store = Store();
        Call(store, "YeuCauDung", 57, 1, 1, "token-a", "connector-a", 8, "Hết giấy");
        Assert.False((bool)Call(store, "XacNhanDaDung", 57, 1, "token-sai", "connector-a"));
        Assert.False((bool)Call(store, "XacNhanDaDung", 57, 1, "token-a", "connector-khac"));
        Assert.True((bool)Call(store, "XacNhanDaDung", 57, 1, "token-a", "connector-a"));
        Assert.True((bool)Call(Store(), "XacNhanDaDung", 57, 1, "token-a", "connector-a"));
        Assert.Equal("DA_DUNG", Prop<string>(Call(store, "LayHienTai", 57), "TrangThai"));
    }

    [Fact]
    public void YeuCauLap_KhongTaoHaiPhien_ThayTokenCuBiChan()
    {
        var store = Store();
        var a = Call(store, "YeuCauDung", 57, 1, 1, "token-a", "connector-a", 8, "Hết giấy");
        var b = Call(store, "YeuCauDung", 57, 1, 1, "token-a", "connector-a", 8, "Bấm lần nữa");
        Assert.Equal(Prop<string>(a, "MaYeuCau"), Prop<string>(b, "MaYeuCau"));
        var ex = Assert.Throws<TargetInvocationException>(() => Call(store, "YeuCauDung", 57, 1, 1, "token-b", "connector-a", 8, "Đổi token sai"));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Theory]
    [InlineData(1, 5, 1, true)]
    [InlineData(0, 5, 1, false)]
    [InlineData(5, 4, 1, false)]
    [InlineData(1, 6, 1, false)]
    [InlineData(1, 5, 2, false)]
    public void PhamViConLai_KhongVuotFileHaySoBan(int tu, int den, int ban, bool expected)
    {
        var rule = typeof(LenhInAoDto).Assembly.GetType("PhotocopyShared.MayInAo.QuyTacChuyenMay");
        Assert.NotNull(rule);
        var rowType = typeof(LenhInAoDto).Assembly.GetType("PhotocopyShared.Dtos.MayInAo.DongChuyenMayRequest");
        Assert.NotNull(rowType);
        object request = JsonSerializer.Deserialize($"[{{\"maChiTiet\":10,\"tuTrang\":{tu},\"denTrang\":{den},\"soBan\":{ban}}}]", rowType.MakeArrayType(), Json);
        var lines = new[] { new DongLenhInDto { MaChiTiet = 10, MaFile = 1, SoTrang = 5, SoBan = 1 } };
        string error = (string)rule.GetMethod("KiemTra").Invoke(null, new object[] { lines, request });
        Assert.Equal(expected, error == null);
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
