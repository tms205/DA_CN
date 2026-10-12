using PhotocopyApi.ThanhToan;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuyTacPayOSTests
{
    [Theory]
    [InlineData(12, "DH00012")]
    [InlineData(123456, "DH123456")]
    public void TaoMoTaDon_DungDinhDangNganGon(int maDonHang, string mongDoi)
    {
        Assert.Equal(mongDoi, QuyTacPayOS.TaoMoTaDon(maDonHang));
    }

    [Fact]
    public void ChuanHoaDiaChiGoc_BoKhoangTrangVaDauGachCuoi()
    {
        string ketQua = QuyTacPayOS.ChuanHoaDiaChiGoc(" https://demo.trycloudflare.com/ ");

        Assert.Equal("https://demo.trycloudflare.com", ketQua);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://localhost:5268")]
    [InlineData("khong-phai-dia-chi")]
    public void ChuanHoaDiaChiGoc_KhongPhaiHttps_TraVeNull(string diaChi)
    {
        Assert.Null(QuyTacPayOS.ChuanHoaDiaChiGoc(diaChi));
    }

    [Fact]
    public void TaoCheckoutUrl_GhepDungPaymentLinkId()
    {
        string ketQua = QuyTacPayOS.TaoCheckoutUrl("abc123");

        Assert.Equal("https://pay.payos.vn/web/abc123", ketQua);
    }

    [Fact]
    public void TaoDiaChiTrangChuWeb_KhongPhuThuocTunnel()
    {
        string ketQua = QuyTacPayOS.TaoDiaChiTrangChuWeb("http://localhost:5064/");

        Assert.Equal("http://localhost:5064/", ketQua);
    }

    [Theory]
    [InlineData("")]
    [InlineData("khong-phai-dia-chi")]
    [InlineData("ftp://localhost")]
    public void TaoDiaChiTrangChuWeb_DiaChiKhongHopLe_TraVeNull(string diaChi)
    {
        Assert.Null(QuyTacPayOS.TaoDiaChiTrangChuWeb(diaChi));
    }

    [Theory]
    [InlineData(50000, 50000, true)]
    [InlineData(50000, 49999, false)]
    public void SoTienKhop_ChiNhanDungSoTien(decimal canTra, long thucNhan, bool mongDoi)
    {
        Assert.Equal(mongDoi, QuyTacPayOS.SoTienKhop(canTra, thucNhan));
    }

    [Theory]
    [InlineData(200, "101", true)]
    [InlineData(404, null, true)]
    [InlineData(200, "20", false)]
    [InlineData(500, null, false)]
    public void ChuaCoLienKet_NhanDungPhanHoiPayOS(int statusCode, string errorCode, bool mongDoi)
    {
        Assert.Equal(mongDoi, QuyTacPayOS.ChuaCoLienKet(statusCode, errorCode));
    }
}
