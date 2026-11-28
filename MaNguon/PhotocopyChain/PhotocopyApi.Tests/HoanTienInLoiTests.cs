using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotocopyApi.Controllers;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

// Hoàn tiền đơn VietQR khi quầy đã in lại đủ 3 lần mà vẫn lỗi.
public class HoanTienInLoiTests
{
    [Theory]
    [InlineData(null, "VIETQR", "DA_THANH_TOAN", 3, true)]
    [InlineData(null, "VIETQR", "DA_THANH_TOAN", 2, false)]   // chưa in lại đủ 3 lần
    [InlineData(null, "COD", "DA_THANH_TOAN", 3, false)]      // tiền mặt không hoàn qua chuyển khoản
    [InlineData(null, "VIETQR", "CHUA_THANH_TOAN", 3, false)]
    [InlineData(null, "VIETQR", "DA_HOAN_TIEN", 3, false)]
    [InlineData(12, "VIETQR", "DA_THANH_TOAN", 3, false)]     // dòng in lại 0đ, không phải đơn gốc
    public void NutHoanTien_ChiHienChoDonGocVietQrDaInLaiDu3Lan(int? goc, string pt, string tt, int soLan, bool hien)
        => Assert.Equal(hien, QuyTacHoanTienInLoi.HienNut(goc, pt, tt, soLan));

    [Fact]
    public void DuDieuKien_KhongCoLoi()
        => Assert.Null(QuyTacHoanTienInLoi.LoiKhongChoHoan("DA_IN", "TAI_QUAY", null, "VIETQR", "DA_THANH_TOAN", false, 3, 0));

    [Theory]
    [InlineData("HOAN_THANH", "TAI_QUAY", null, "VIETQR", "DA_THANH_TOAN", false, 3, 0, "bàn giao")]
    [InlineData("DA_IN", "GIAO_TAN_NOI", null, "VIETQR", "DA_THANH_TOAN", false, 3, 0, "bàn giao")]
    [InlineData("DA_IN", "TAI_QUAY", 5, "VIETQR", "DA_THANH_TOAN", false, 3, 0, "đơn gốc")]
    [InlineData("DA_IN", "TAI_QUAY", null, "COD", "DA_THANH_TOAN", false, 3, 0, "VietQR")]
    [InlineData("DA_IN", "TAI_QUAY", null, "VIETQR", "DA_HOAN_TIEN", false, 3, 0, "đã hoàn tiền")]
    [InlineData("DA_IN", "TAI_QUAY", null, "VIETQR", "DA_THANH_TOAN", true, 3, 0, "không lập trùng")]
    [InlineData("DA_IN", "TAI_QUAY", null, "VIETQR", "DA_THANH_TOAN", false, 2, 0, "2/3")]
    [InlineData("DA_IN", "TAI_QUAY", null, "VIETQR", "DA_THANH_TOAN", false, 3, 1, "chưa in xong")]
    public void KhongDuDieuKien_BaoLyDo(string don, string nhan, int? goc, string pt, string tt, bool coHoSo,
        int soLan, int dangChay, string chua)
    {
        string loi = QuyTacHoanTienInLoi.LoiKhongChoHoan(don, nhan, goc, pt, tt, coHoSo, soLan, dangChay);
        Assert.NotNull(loi);
        Assert.Contains(chua, loi);
    }

    [Fact]
    public void HoSoCu_KhongCoNguon_VanLaKhachGui()
    {
        var hoSo = JsonSerializer.Deserialize<HoSoKhieuNaiDto>("""{"MaDonHang":7,"TrangThai":"CHO_DUYET"}""");
        Assert.Equal("KHACH_GUI", hoSo!.Nguon);
        var moi = JsonSerializer.Deserialize<HoSoKhieuNaiDto>(JsonSerializer.Serialize(
            new HoSoKhieuNaiDto { Nguon = QuyTacHoanTienInLoi.Nguon }));
        Assert.Equal("IN_LOI_3_LAN", moi!.Nguon);
    }

    [Theory]
    [InlineData("12345", false)]
    [InlineData("12345a7", false)]
    [InlineData("0123456789", true)]
    public void SoTaiKhoan_ChiNhanChuSo(string soTk, bool hopLe)
    {
        var req = new HoanTienInLoiRequest { NganHang = "Vietcombank", SoTaiKhoan = soTk, ChuTaiKhoan = "Nguyen Van A" };
        Assert.Equal(hopLe, Validator.TryValidateObject(req, new ValidationContext(req), null, true));
    }

    [Fact]
    public void Api_ChoNhanVienVaQuanLy_KhongChoKhachHang()
    {
        MethodInfo m = typeof(DonHangController).GetMethod(nameof(DonHangController.LapHoanTienInLoi))!;
        Assert.Equal("{maDonHang:int:min(1)}/hoan-tien-in-loi", m.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal("BRANCH_ADMIN,STAFF", m.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public void ThongBaoChoCeo_NamTrongNhomChoXuLy()
        => Assert.Equal("CHO_XU_LY", QuyTacThongBaoXuLy.TrangThai("Hoàn tiền do in lỗi 3 lần"));
}
