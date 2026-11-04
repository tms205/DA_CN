using PhotocopyShared.CaLamViec;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;
public class CaThongDemTests
{
    [Theory]
    [InlineData("2026-11-03T21:30:00","2026-11-04T12:30:00")]
    [InlineData("2026-11-03T23:59:00","2026-11-04T12:30:00")]
    [InlineData("2026-11-04T00:00:00","2026-11-04T12:30:00")]
    [InlineData("2026-11-04T07:29:59","2026-11-04T12:30:00")]
    [InlineData("2026-11-04T07:30:00","2026-11-04T12:30:00")]
    [InlineData("2026-11-04T12:30:00","2026-11-04T17:30:00")]
    public void ShiftEndAndBusinessDate(string open,string end)
    {
        var start=DateTime.Parse(open);var expected=DateTime.Parse(end);
        Assert.Equal(expected,LichCaLamViec.GioKetThuc(start));
        Assert.Equal(expected.Date,LichCaLamViec.NgayLamViec(start));
        Assert.Equal(expected,PhotocopyApi.CaLamViec.LichCa.GioKetThuc(start));
    }
    [Theory]
    [InlineData("CEO đã xử lý yêu cầu hoàn tiền","DA_XU_LY")]
    [InlineData("Phản hồi giao hàng đã được đối chiếu","DA_XU_LY")]
    [InlineData("Đã tiếp nhận yêu cầu hoàn tiền","CHO_XU_LY")]
    [InlineData("In xong","")]
    public void NotificationProcessingStatusIsSeparateFromReadFlag(string title,string status)
    {
        Assert.Equal(status,new ThongBaoDto{TieuDe=title,DaDoc=true}.TrangThaiXuLy);
        Assert.Equal(status,new ThongBaoDto{TieuDe=title,DaDoc=false}.TrangThaiXuLy);
    }
}
