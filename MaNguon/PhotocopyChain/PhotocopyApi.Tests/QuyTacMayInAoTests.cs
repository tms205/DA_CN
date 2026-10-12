using PhotocopyShared.Dtos.MayInAo;
using PhotocopyShared.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuyTacMayInAoTests
{
    [Theory]
    [InlineData(12, 12, "SAN_SANG", true, true)]
    [InlineData(12, 11, "SAN_SANG", true, false)]
    [InlineData(12, 13, "SAN_SANG", true, false)]
    [InlineData(12, 12, "DA_XOA", true, false)]
    [InlineData(12, 12, "SAN_SANG", false, false)]
    [InlineData(0, 0, "SAN_SANG", true, false)]
    public void FilePhucHoi_PhaiDungSoTrangVaConFile(int soTrangCu, int soTrangMoi,
        string trangThai, bool conFile, bool mongDoi)
    {
        Assert.Equal(mongDoi, QuyTacMayInAo.FilePhucHoiHopLe(soTrangCu, soTrangMoi, trangThai, conFile));
    }

    [Fact]
    public void TinhSoGiay_CongThoiGianTrangDauVaTungDongIn()
    {
        var may = new CauHinhTocDoMayIn
        {
            TocDoA4 = 20,
            TocDoA3 = 10,
            TocDoHaiMat = 16,
            SoGiayRaTrangDau = 6.5
        };
        var dsDong = new List<DongLenhInDto>
        {
            new() { KhoGiay = "A4", MatIn = "1_MAT", SoTrang = 10, SoBan = 2 },
            new() { KhoGiay = "A3", MatIn = "1_MAT", SoTrang = 2, SoBan = 1 }
        };

        double ketQua = QuyTacMayInAo.TinhSoGiay(may, dsDong);

        Assert.Equal(78.5, ketQua, 1);
    }

    [Fact]
    public void TinhSoGiay_DungTocDoHaiMat()
    {
        var may = new CauHinhTocDoMayIn
        {
            TocDoA4 = 40,
            TocDoA3 = 20,
            TocDoHaiMat = 20,
            SoGiayRaTrangDau = 5
        };
        var dsDong = new List<DongLenhInDto>
        {
            new() { KhoGiay = "A4", MatIn = "2_MAT", SoTrang = 20, SoBan = 1 }
        };

        double ketQua = QuyTacMayInAo.TinhSoGiay(may, dsDong);

        Assert.Equal(65, ketQua, 1);
    }

    [Theory]
    [InlineData("A4", false, false, "A4", false, false, true)]
    [InlineData("A3", true, true, "A4", true, true, true)]
    [InlineData("A4", true, true, "A3", false, false, false)]
    [InlineData("A3", false, true, "A4", true, false, false)]
    [InlineData("A3", true, false, "A4", false, true, false)]
    public void DapUngQuyCach_KiemTraDongThoiBaTieuChi(
        string khoToiDa,
        bool mayInMau,
        bool mayInHaiMat,
        string khoCan,
        bool canMau,
        bool canHaiMat,
        bool mongDoi)
    {
        bool ketQua = QuyTacMayInAo.DapUngQuyCach(
            khoToiDa, mayInMau, mayInHaiMat, khoCan, canMau, canHaiMat);

        Assert.Equal(mongDoi, ketQua);
    }

    [Theory]
    [InlineData("SAN_SANG", "D:\\TepIn\\don.pdf", true, true)]
    [InlineData("DA_XOA", "D:\\TepIn\\don.pdf", true, false)]
    [InlineData("SAN_SANG", null, true, false)]
    [InlineData("SAN_SANG", "D:\\TepIn\\don.pdf", false, false)]
    public void TepSanSang_ChiChapNhanTepConTonTai(
        string trangThai,
        string duongDan,
        bool tepVatLyTonTai,
        bool mongDoi)
    {
        bool ketQua = QuyTacMayInAo.TepSanSang(trangThai, duongDan, tepVatLyTonTai);

        Assert.Equal(mongDoi, ketQua);
    }
}
