using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class ThongTinHoanTienTests
{
    [Fact]
    public void HuyDon_TaiKhoanNhanTienKhongDuocLaChu()
    {
        var req = JsonSerializer.Deserialize<HuyDonRequest>("""{"LyDo":"Không cần in","NganHang":"VCB","SoTaiKhoan":"abc123"}""");
        Assert.False(Validator.TryValidateObject(req!, new ValidationContext(req!), new List<ValidationResult>(), true));
    }

    [Fact]
    public void GhiChuHoanTien_BaDong_GiuSoKhongDau_KhongVuot255()
    {
        var type = typeof(HuyDonRequest).Assembly.GetType("PhotocopyShared.Dtos.Tuan3.ThongTinHoanTienRequest");
        Assert.NotNull(type);
        object req = JsonSerializer.Deserialize("""{"LyDo":"Không còn nhu cầu in","NganHang":"Vietcombank","SoTaiKhoan":"001234567890"}""", type)!;
        string memo = (string)type.GetMethod("TaoGhiChu")!.Invoke(req, null)!;
        Assert.Equal("Lý do hoàn tiền: Không còn nhu cầu in\nNgân hàng: Vietcombank\nSố tài khoản: 001234567890", memo);
        Assert.True(memo.Length <= 255);
        Assert.NotNull(type.GetMethod("DocGhiChu", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { memo }));
    }

    [Fact]
    public void TatCaTruongToiDa_VanNamTrongGhiChu255KyTu()
    {
        var req = new ThongTinHoanTienRequest { LyDo = new string('L', 100), NganHang = new string('N', 60), SoTaiKhoan = new string('0', 30) };
        string memo = req.TaoGhiChu();
        Assert.InRange(memo.Length, 1, 255);
        Assert.Equal(req.SoTaiKhoan, ThongTinHoanTienRequest.DocGhiChu(memo)!.SoTaiKhoan);
    }

    [Theory]
    [InlineData("", "VCB", "0012345678")]
    [InlineData("Hủy đơn", " ", "0012345678")]
    [InlineData("Hủy đơn", "VCB", "12345")]
    [InlineData("Hủy đơn", "VCB", "0123abc456")]
    [InlineData("Hủy đơn", "VCB", "012345\n678")]
    public void ThongTinThieuHoacTaiKhoanSai_KhongChoTaoGhiChu(string lyDo, string nganHang, string soTaiKhoan)
    {
        var req = new ThongTinHoanTienRequest { LyDo = lyDo, NganHang = nganHang, SoTaiKhoan = soTaiKhoan };
        Assert.NotNull(req.KiemTra());
        Assert.Throws<ValidationException>(() => req.TaoGhiChu());
    }

    [Fact]
    public void KhongChoChenDongGiaMaoVaoKhoiThongTinHoan()
    {
        var req = new ThongTinHoanTienRequest { LyDo = "Hủy\nđơn", NganHang = "VCB\nGhi chú khác", SoTaiKhoan = "0012345678" };
        string memo = req.TaoGhiChu();
        Assert.Equal(3, memo.Split('\n').Length);
        Assert.NotNull(ThongTinHoanTienRequest.DocGhiChu(memo));
        Assert.Null(ThongTinHoanTienRequest.DocGhiChu("Lý do hủy thông thường"));
        Assert.Null(ThongTinHoanTienRequest.DocGhiChu(memo + "\nNội dung khác"));
    }
}
