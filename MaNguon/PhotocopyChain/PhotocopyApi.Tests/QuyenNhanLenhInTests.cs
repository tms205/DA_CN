using PhotocopyApi.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuyenNhanLenhInTests
{
    [Fact]
    public async Task HaiConnectorCungNhan_MotConnectorDuocNhan()
    {
        string thuMuc = Path.Combine(Path.GetTempPath(), "photocopy-claim-" + Guid.NewGuid());
        var ketQua = await Task.WhenAll(Enumerable.Range(1, 10)
            .Select(i => Task.Run<object>(() => TaoStore(thuMuc).NhanLenh(1, 6, "connector-" + i))));
        Assert.Single(ketQua, x => x != null);
    }

    [Fact]
    public void KhoiDongLai_GiuChuSoHuuVaMaLenh()
    {
        string thuMuc = Path.Combine(Path.GetTempPath(), "photocopy-claim-" + Guid.NewGuid());
        var store = TaoStore(thuMuc);
        var lenh = store.NhanLenh(1, 6, "connector-a");
        var sauKhoiDong = TaoStore(thuMuc);
        Assert.Null(sauKhoiDong.NhanLenh(1, 6, "connector-b"));
        Assert.Equal((string)lenh.MaLenh, (string)sauKhoiDong.NhanLenh(1, 6, "connector-a").MaLenh);
        Assert.False(sauKhoiDong.CoQuyen(1, 6, "connector-a", "sai-token"));
    }

    [Fact]
    public void ChuaHoanTat_KhongDuocNhanDonKhac()
    {
        var store = TaoStore(Path.Combine(Path.GetTempPath(), "photocopy-claim-" + Guid.NewGuid()));
        var lenh = store.NhanLenh(1, 6, "connector-a");
        Assert.Null(store.NhanLenh(1, 7, "connector-a"));
        store.HoanTat(1, 6, "connector-a", (string)lenh.MaLenh);
        Assert.True(store.CoQuyen(1, 6, "connector-a", (string)lenh.MaLenh));
        Assert.NotNull(store.NhanLenh(1, 7, "connector-b"));
    }

    private static QuyenNhanLenhInStore TaoStore(string thuMuc) => new(thuMuc);
}
