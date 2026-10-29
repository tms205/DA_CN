using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;

public class HuyDonTruocInTests
{
    [Theory]
    [InlineData("CHO_XAC_NHAN", true)]
    [InlineData("DA_XAC_NHAN", true)]
    [InlineData("DANG_IN", false)]
    [InlineData("DA_IN", false)]
    [InlineData("DANG_GIAO", false)]
    [InlineData("DA_GIAO", false)]
    [InlineData("GIAO_THAT_BAI", false)]
    [InlineData("HOAN_THANH", false)]
    [InlineData("DA_HUY", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ChiHaiTrangThaiTruocInDuocHuy(string trangThai, bool duocHuy)
    {
        Assert.Equal(duocHuy, TrangThaiDonHang.ChoPhepHuy(trangThai));
    }
}
