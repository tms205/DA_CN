using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public class GiaoHangQuyTacTests
{
    // Reflection ở bước RED để test thực sự thất bại vì thiếu chức năng, không lỗi biên dịch.
    private static Type QuyTac()
    {
        Type type = typeof(DonHangController).Assembly.GetType("PhotocopyApi.GiaoHang.QuyTacGiaoHang");
        Assert.NotNull(type);
        return type;
    }

    [Theory]
    [InlineData(0, 15000)]
    [InlineData(2, 15000)]
    [InlineData(2.01, 16000)]
    [InlineData(3, 19000)]
    [InlineData(5, 27000)]
    [InlineData(10, 47000)]
    public void PhiNoiBo_TinhQuangDuongVaLamTronTongPhi(decimal km, decimal phi)
    {
        var config = new ConfigurationBuilder().Build();
        Assert.Equal(phi, (decimal)QuyTac().GetMethod("TinhPhi").Invoke(null, new object[] { km, config }));
    }

    [Theory]
    [InlineData("DA_IN", "DA_THANH_TOAN", 0, true)]
    [InlineData("GIAO_THAT_BAI", "DA_THANH_TOAN", 2, true)]
    [InlineData("DA_IN", "CHUA_THANH_TOAN", 0, false)]
    [InlineData("DANG_IN", "DA_THANH_TOAN", 0, false)]
    [InlineData("DANG_GIAO", "DA_THANH_TOAN", 1, false)]
    [InlineData("GIAO_THAT_BAI", "DA_THANH_TOAN", 3, false)]
    [InlineData("DA_IN", "DA_HOAN_TIEN", 0, false)]
    public void NhanDon_ChiKhiDaInDaTraTienVaChuaQuaBaLuot(string trangThai, string thanhToan, int soLan, bool duocNhan)
    {
        var loi = (string)QuyTac().GetMethod("LoiNhanDon").Invoke(null, new object[] { trangThai, thanhToan, soLan });
        Assert.Equal(duocNhan, loi == null);
    }
}
