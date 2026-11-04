using System.ComponentModel.DataAnnotations;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class HoanTienHaiPhutTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(119, true)]
    [InlineData(120, false)]
    [InlineData(121, false)]
    [InlineData(420, false)]
    public void PaidApp_DeadlineIsExactly120Seconds(int elapsed, bool expected)
    {
        var paid = new DateTime(2026, 11, 3, 8, 0, 0);
        Assert.Equal(expected, ChinhSachDonApp.ChoHuy("APP", "CHO_XAC_NHAN", "DA_THANH_TOAN", paid, paid, paid.AddSeconds(elapsed)));
    }

    [Theory]
    [InlineData("DA_XAC_NHAN")]
    [InlineData("DANG_IN")]
    [InlineData("DA_IN")]
    [InlineData("HOAN_THANH")]
    [InlineData("DA_HUY")]
    public void ReceivedOrderCannotUseOrdinaryCancellation(string state)
    {
        var now = DateTime.Now;
        Assert.False(ChinhSachDonApp.ChoHuy("APP", state, "DA_THANH_TOAN", now, now.AddHours(1), now));
    }

    [Fact]
    public void MissingPaymentTimestampFailsClosed()
        => Assert.False(ChinhSachDonApp.ChoHuy("APP", "CHO_XAC_NHAN", "DA_THANH_TOAN", null, null, DateTime.Now));

    [Fact]
    public void LegacyPaidKeepsAppointmentRule_UnpaidCanCancelImmediately()
    {
        var now = DateTime.Now;
        Assert.True(ChinhSachDonApp.ChoHuy("LEGACY", "CHO_XAC_NHAN", "DA_THANH_TOAN", now.AddDays(-1), now.AddHours(1), now));
        Assert.True(ChinhSachDonApp.ChoHuy("APP", "CHO_XAC_NHAN", "CHUA_THANH_TOAN", null, now.AddHours(1), now));
        Assert.True(ChinhSachDonApp.ChoHuy("APP", "CHO_XAC_NHAN", "CHUA_THANH_TOAN", null, now.AddMinutes(15), now));
    }

    [Theory]
    [InlineData("CHO_DUYET", "CHAP_THUAN", true)]
    [InlineData("CHO_DUYET", "TU_CHOI", true)]
    [InlineData("CHO_DUYET", "DA_HOAN_TIEN", false)]
    [InlineData("CHAP_THUAN", "DA_HOAN_TIEN", true)]
    [InlineData("CHAP_THUAN", "CHAP_THUAN", false)]
    [InlineData("TU_CHOI", "CHAP_THUAN", false)]
    [InlineData("DA_HOAN_TIEN", "DA_HOAN_TIEN", false)]
    public void CeoDecisionRequiresApprovalBeforeTransferAndCannotRepeat(string old, string next, bool expected)
        => Assert.Equal(expected, new HoSoKhieuNaiDto { TrangThai = old }.ChoQuyetDinh(next));

    [Theory]
    [InlineData("short", "123456", false)]
    [InlineData("Mô tả sản phẩm bị thiếu một số trang cần đối chiếu", "abc123", false)]
    [InlineData("Mô tả sản phẩm bị thiếu một số trang cần đối chiếu", "123456", true)]
    public void ComplaintRequiresDetailAndNumericBankAccount(string text, string account, bool valid)
    {
        var req = new KhieuNaiHoanTienRequest { MoTa=text,NganHang="Ngân hàng",SoTaiKhoan=account,ChuTaiKhoan="Người nhận",MaBangChung=Guid.NewGuid().ToString("N") };
        Assert.Equal(valid, Validator.TryValidateObject(req,new ValidationContext(req),new List<ValidationResult>(),true));
    }
}
