using PayOS.Models.V2.PaymentRequests;
using PhotocopyApi.ThanhToan;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class DuLieuQrPayOSTests : IDisposable
{
    private readonly string _thuMuc = Path.Combine(Path.GetTempPath(), "PhotocopyQrTest-" + Guid.NewGuid().ToString("N"));

    private static CreatePaymentLinkResponse PhanHoi() => new()
    {
        OrderCode = 15, Amount = 7400, QrCode = "qr-payOS-test",
        Bin = "970422", AccountNumber = "0000000000", AccountName = "TEST",
        Description = "DH00015", PaymentLinkId = "test-link", CheckoutUrl = "https://pay.payos.vn/web/test-link"
    };

    private static LienKetPayOSDto TaoQr(CreatePaymentLinkResponse link) =>
        QuyTacPayOS.TaoDuLieuQr(link, 15, 7400m, DateTime.Now.AddMinutes(10));

    private DuLieuQrPayOSStore TaoStore() => new(_thuMuc);

    [Fact]
    public void Qr_LayNguyenVanTuPayOSVaDungThongTinDon()
    {
        var data = TaoQr(PhanHoi());
        Assert.Equal("qr-payOS-test", data.QrCode);
        Assert.Equal(15, data.MaDonHang);
        Assert.Equal(7400m, data.SoTien);
        Assert.Equal("0000000000", data.SoTaiKhoan);
        Assert.Equal("DH00015", data.NoiDungChuyenKhoan);
        Assert.Equal("970422", data.MaNganHang);
        Assert.Equal("TEST", data.ChuTaiKhoan);
    }

    [Theory]
    [InlineData(14, 7400, "qr")]
    [InlineData(15, 7300, "qr")]
    [InlineData(15, 7400, "")]
    public void Qr_SaiDonSaiTienHoacRong_KhongCapMa(long don, long tien, string qr)
    {
        var link = PhanHoi();
        link.OrderCode = don; link.Amount = tien; link.QrCode = qr;
        Assert.Throws<InvalidOperationException>(() => TaoQr(link));
    }

    [Fact]
    public void Cache_KhoiDongLaiVanLayDuocQrCungLink()
    {
        var store = TaoStore();
        store.Luu(15, "test-link", TaoQr(PhanHoi()));
        var storeMoi = TaoStore();
        var data = storeMoi.Doc(15, "test-link", 7400m);
        Assert.Equal("qr-payOS-test", data.QrCode);
    }

    [Theory]
    [InlineData("link-khac", 7400)]
    [InlineData("test-link", 7500)]
    public void Cache_SaiLinkHoacTien_KhongTraQr(string link, decimal tien)
    {
        var store = TaoStore();
        store.Luu(15, "test-link", TaoQr(PhanHoi()));
        Assert.Null(store.Doc(15, link, tien));
    }

    [Fact]
    public void Cache_FileHong_KhongTuTaoQrKhac()
    {
        var store = TaoStore();
        store.Luu(15, "test-link", TaoQr(PhanHoi()));
        File.WriteAllText(Directory.GetFiles(_thuMuc, "*.json").Single(), "{hong");
        Assert.Null(store.Doc(15, "test-link", 7400m));
    }

    public void Dispose()
    {
        if (Directory.Exists(_thuMuc)) Directory.Delete(_thuMuc, true);
    }
}
