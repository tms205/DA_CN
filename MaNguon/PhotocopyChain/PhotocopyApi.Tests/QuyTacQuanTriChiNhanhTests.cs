using PhotocopyApi;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuyTacQuanTriChiNhanhTests
{
    [Fact]
    public void KiemTraDichVu_ChuaChonChiNhanh_ThongBaoLoi()
    {
        var yeuCau = new LuuDichVuRequest { TenDichVu = "In A4", DonGia = 500, TrangThai = "HIEN" };

        string loi = QuyTacQuanTriChiNhanh.KiemTraDichVu(yeuCau, null);

        Assert.Equal("Hãy chọn một chi nhánh trước khi cập nhật bảng giá.", loi);
    }

    [Fact]
    public void KiemTraDichVu_GiaAm_ThongBaoLoi()
    {
        var yeuCau = new LuuDichVuRequest { TenDichVu = "In A4", DonGia = -1, TrangThai = "HIEN" };

        string loi = QuyTacQuanTriChiNhanh.KiemTraDichVu(yeuCau, 1);

        Assert.Equal("Đơn giá không được âm.", loi);
    }

    [Fact]
    public void KiemTraKhuyenMai_ChuaChonChiNhanh_ThongBaoLoi()
    {
        var yeuCau = KhuyenMaiHopLe();

        string loi = QuyTacQuanTriChiNhanh.KiemTraKhuyenMai(yeuCau, null);

        Assert.Equal("Hãy chọn một chi nhánh trước khi cập nhật khuyến mãi.", loi);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void KiemTraKhuyenMai_PhanTramKhongHopLe_ThongBaoLoi(int phanTram)
    {
        var yeuCau = KhuyenMaiHopLe();
        yeuCau.PhanTramGiam = phanTram;

        string loi = QuyTacQuanTriChiNhanh.KiemTraKhuyenMai(yeuCau, 1);

        Assert.Equal("Phần trăm giảm phải từ 1 đến 100.", loi);
    }

    [Fact]
    public void KiemTraKhuyenMai_NgayKetThucTruocNgayBatDau_ThongBaoLoi()
    {
        var yeuCau = KhuyenMaiHopLe();
        yeuCau.NgayKetThuc = yeuCau.NgayBatDau.AddDays(-1);

        string loi = QuyTacQuanTriChiNhanh.KiemTraKhuyenMai(yeuCau, 1);

        Assert.Equal("Ngày kết thúc phải sau ngày bắt đầu.", loi);
    }

    [Fact]
    public void KiemTraKhuyenMai_DuLieuHopLe_KhongCoLoi()
    {
        string loi = QuyTacQuanTriChiNhanh.KiemTraKhuyenMai(KhuyenMaiHopLe(), 1);

        Assert.Null(loi);
    }

    private static LuuKhuyenMaiRequest KhuyenMaiHopLe() => new()
    {
        MaCode = "GIAM10",
        PhanTramGiam = 10,
        DonToiThieu = 10000,
        GiamToiDa = 20000,
        SoLuong = 100,
        NgayBatDau = new DateTime(2026, 10, 1),
        NgayKetThuc = new DateTime(2026, 10, 31),
        TrangThai = "HOAT_DONG"
    };
}
