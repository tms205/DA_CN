using PhotocopyShared.CaLamViec;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class DoiSoatCaTests
{
    [Theory]
    [InlineData(250000, 250000, 0, 0, true)]
    [InlineData(240000, 250000, 0, 0, true)]
    [InlineData(240000, 240000, 0, 10000, true)]
    [InlineData(240000, 240000, 2000, 8000, true)]
    [InlineData(240000, 240000, 2000, 7000, false)]
    [InlineData(260000, 260000, 0, 0, true)]
    [InlineData(260000, 250000, 0, 0, true)]
    [InlineData(250000, 240000, 0, 0, false)]
    [InlineData(250000, 240000, 0, 10000, true)]
    [InlineData(250000, 240000, 2000, 8000, true)]
    [InlineData(250000, 260000, 0, 0, true)]
    [InlineData(250000, 250000, 0, 1000, false)]
    public void DoiSoat_DoiChieuSoBanGiaoVaTienBu(int nhanVien, int quanLy, int chi, int bu, bool hopLe)
    {
        var ca = new CaLamViecDto { TienDauCa = 200000, DoanhThuTrongCa = 50000,
            TongDoanhThuCuoiCa = nhanVien, GhiChu = "Lý do nhân viên" };
        var req = new XacNhanCaRequest { TienQuanLyDemCuoiCa = quanLy, TienChi = chi,
            TienBu = bu, GhiChu = "Đã kiểm tra nguyên nhân" };
        Assert.Equal(hopLe, QuyTacDoiSoatCa.KiemTra(ca, req) == null);
    }

    [Fact]
    public void ThieuTien_PhaiCoLyDoCuaCaHaiBen()
    {
        var ca = new CaLamViecDto { TienDauCa = 200000, TongDoanhThuCuoiCa = 190000 };
        var req = new XacNhanCaRequest { TienQuanLyDemCuoiCa = 190000, TienBu = 10000, GhiChu = "Đã đối soát" };
        Assert.NotNull(QuyTacDoiSoatCa.KiemTra(ca, req));
        ca.GhiChu = "Thu thiếu tiền khách";
        req.GhiChu = " ";
        Assert.NotNull(QuyTacDoiSoatCa.KiemTra(ca, req));
    }

    [Fact]
    public void ChuaNhapSoDem_KhongDuocTuDongXacNhan()
    {
        Assert.NotNull(QuyTacDoiSoatCa.KiemTra(new CaLamViecDto(), new XacNhanCaRequest()));
    }

    [Fact]
    public void NhanVienKhaiKhop_QuanLyDemThieu_PhaiCoLyDoVaBuDu()
    {
        var ca = new CaLamViecDto { TienDauCa = 200000, TongDoanhThuCuoiCa = 200000 };
        var req = new XacNhanCaRequest { TienQuanLyDemCuoiCa = 190000, TienBu = 10000 };
        Assert.NotNull(QuyTacDoiSoatCa.KiemTra(ca, req));
        req.GhiChu = "Kiểm đếm lại thiếu 10.000đ, đã nhận đủ tiền bù";
        Assert.Null(QuyTacDoiSoatCa.KiemTra(ca, req));
        Assert.Equal(200000m, ca.TongDoanhThuCuoiCa);
        req.TienBu = 9000;
        Assert.NotNull(QuyTacDoiSoatCa.KiemTra(ca, req));
    }
}
