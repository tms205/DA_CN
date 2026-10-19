using PhotocopyApi.CaLamViec;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuyTacCaDangNhapTests
{
    [Theory]
    [InlineData(VaiTro.Staff)]
    [InlineData(VaiTro.BranchAdmin)]
    public void NhanSuCoChiNhanhDangNhapWeb_ThiTuDongMoCa(string vaiTro)
    {
        Assert.True(QuyTacCaDangNhap.CanTuDongMoCa(true, vaiTro));
    }

    [Theory]
    [InlineData(false, VaiTro.Staff)]
    [InlineData(false, VaiTro.BranchAdmin)]
    [InlineData(false, VaiTro.SuperAdmin)]
    [InlineData(true, VaiTro.SuperAdmin)]
    [InlineData(true, VaiTro.Customer)]
    [InlineData(true, VaiTro.Shipper)]
    public void TaiKhoanKhac_HoacDangNhapApp_ThiKhongTuDongMoCa(bool laWeb, string vaiTro)
    {
        Assert.False(QuyTacCaDangNhap.CanTuDongMoCa(laWeb, vaiTro));
    }
}
