using PhotocopyApi.CaLamViec;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuyTacCaDangNhapTests
{
    [Fact]
    public void NhanVienDangNhapWeb_ThiTuDongMoCa()
    {
        Assert.True(QuyTacCaDangNhap.CanTuDongMoCa(true, VaiTro.Staff));
    }

    [Theory]
    [InlineData(false, VaiTro.Staff)]
    [InlineData(true, VaiTro.BranchAdmin)]
    [InlineData(true, VaiTro.SuperAdmin)]
    [InlineData(true, VaiTro.Customer)]
    public void TaiKhoanKhac_HoacDangNhapApp_ThiKhongTuDongMoCa(bool laWeb, string vaiTro)
    {
        Assert.False(QuyTacCaDangNhap.CanTuDongMoCa(laWeb, vaiTro));
    }
}
