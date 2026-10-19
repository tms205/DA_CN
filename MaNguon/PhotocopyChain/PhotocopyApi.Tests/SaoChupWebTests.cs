using System.Reflection;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.DanhMuc;
using PhotocopyShared.Dtos.DatIn;
using Xunit;

namespace PhotocopyApi.Tests;

public class SaoChupWebTests
{
    private const BindingFlags Rieng = BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    public void SaoChup_ChiChonGiaA4MotMat_KhongDungGiaAnhThe()
    {
        var trang = new LapDonTaiQuay();
        typeof(LapDonTaiQuay).GetField("_loaiTai", Rieng).SetValue(trang, "SAO_CHUP");
        typeof(LapDonTaiQuay).GetField("_dsDichVu", Rieng).SetValue(trang, new List<DichVuDto> {
            new() { MaDichVu = 1, LoaiDichVu = "IN_AN", KhoGiay = "A3", MatIn = "1_MAT" },
            new() { MaDichVu = 2, LoaiDichVu = "IN_AN", KhoGiay = "A4", MatIn = "2_MAT" },
            new() { MaDichVu = 3, LoaiDichVu = "IN_AN", KhoGiay = "A4", MatIn = "1_MAT", MauSac = "DEN_TRANG" },
            new() { MaDichVu = 4, LoaiDichVu = "IN_AN", KhoGiay = "A4", MatIn = "1_MAT", MauSac = "MAU" },
            new() { MaDichVu = 5, LoaiDichVu = "IN_AN", KhoGiay = "A4", MatIn = "1_MAT", KichThuocAnh = "3X4" },
        });
        var ds = (IEnumerable<DichVuDto>)typeof(LapDonTaiQuay).GetMethod("DichVuInHopLe", Rieng).Invoke(trang, null);
        Assert.Equal(new[] { 3, 4 }, ds.Select(x => x.MaDichVu));
    }

    [Fact]
    public void SaoChup_LuonHuongDung_NhungVanTinhTheoSoBan()
    {
        var trang = new LapDonTaiQuay();
        void Gan(string ten, object value) => typeof(LapDonTaiQuay).GetField(ten, Rieng).SetValue(trang, value);
        Gan("_loaiTai", "SAO_CHUP");
        Gan("_dsFile", new List<FileInDto> { new() { MaFileIn = 25, SoTrang = 1 } });
        Gan("_maDichVu", 3);
        Gan("_soBan", 4);
        Gan("_huongGiay", "NGANG");
        var ds = (List<DongInRequest>)typeof(LapDonTaiQuay).GetMethod("TaoDongIn", Rieng).Invoke(trang, null);
        var dong = Assert.Single(ds);
        Assert.Equal("DUNG", dong.HuongGiay);
        Assert.Equal(4, dong.SoBanIn);
        Assert.Equal(25, dong.MaFileIn);
    }
}
