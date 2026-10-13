using System.Reflection;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class HangDoiNoiBatTests
{
    [Theory]
    [InlineData("CHO_XAC_NHAN", "COD", "CHUA_THANH_TOAN", "Cần tiếp nhận", "hd-cho-tiep-nhan")]
    [InlineData("CHO_XAC_NHAN", "TIEN_MAT", "DA_THANH_TOAN", "Cần tiếp nhận", "hd-cho-tiep-nhan")]
    [InlineData("CHO_XAC_NHAN", "VIETQR", "CHUA_THANH_TOAN", "Chờ thanh toán QR", "hd-cho-thanh-toan")]
    [InlineData("CHO_XAC_NHAN", "VIETQR", "DA_THANH_TOAN", "Chờ hệ thống tiếp nhận", "hd-cho-tu-dong")]
    [InlineData("DANG_IN", "COD", "CHUA_THANH_TOAN", "", "")]
    public void ChoTiepNhan_PhanBietViecNhanVienVaThanhToan(string trangThai, string cach, string tien, string nhan, string lopCss)
    {
        var lop = typeof(PhamViChiNhanh).Assembly.GetType("PhotocopyAdmin.QuyTacHangDoi");
        Assert.NotNull(lop);
        var don = new HangDoiDonDto { TrangThaiDonHang = trangThai, PhuongThucThanhToan = cach, TrangThaiThanhToan = tien };
        Assert.Equal(nhan, lop.GetMethod("NhanCho").Invoke(null, new object[] { don }));
        Assert.Equal(lopCss, lop.GetMethod("LopCho").Invoke(null, new object[] { don }));
    }

    [Fact]
    public void XemDonCho_XoaTuKhoaCu_VeTrangMot_KhongDoiDon()
    {
        const BindingFlags rieng = BindingFlags.Instance | BindingFlags.NonPublic;
        var trang = new HangDoiDon();
        var ds = new List<HangDoiDonDto> { new() { MaDonHang = 1, TrangThaiDonHang = "CHO_XAC_NHAN" } };
        typeof(HangDoiDon).GetField("_ds", rieng).SetValue(trang, ds);
        typeof(HangDoiDon).GetField("_tuKhoa", rieng).SetValue(trang, "khong-co-don-nay");
        typeof(HangDoiDon).GetField("_trangDon", rieng).SetValue(trang, 3);
        var ham = typeof(HangDoiDon).GetMethod("XemDonCho", rieng);
        Assert.NotNull(ham);
        ham.Invoke(trang, null);
        Assert.Equal("", typeof(HangDoiDon).GetField("_tuKhoa", rieng).GetValue(trang));
        Assert.Equal("CHO_XAC_NHAN", typeof(HangDoiDon).GetField("_loc", rieng).GetValue(trang));
        Assert.Equal(1, typeof(HangDoiDon).GetField("_trangDon", rieng).GetValue(trang));
        Assert.Equal("CHO_XAC_NHAN", ds[0].TrangThaiDonHang);
    }
}
