using System.Text.Json;
using PhotocopyApi.MayInAo;
using PhotocopyShared.Dtos.MayInAo;
using PhotocopyPrinterSimulator;
using Xunit;

namespace PhotocopyApi.Tests;

public class DieuKhienMayInTests
{
    [Fact]
    public void CapNhatTrangThaiTuApi_KhongLamMayThatBienThanhMayAo()
    {
        var store = new TrangThaiMayInAoStore();
        store.CapNhat(1, "SAN_SANG", baoCao: new() { CheDo = "THAT", TenHangDoi = "Ricoh USB" });
        store.CapNhat(1, "DANG_IN", 6);
        Assert.Equal("THAT", Assert.Single(store.LayTatCa()).CheDo);
        Assert.Equal("Ricoh USB", Assert.Single(store.LayTatCa()).TenHangDoi);
    }
    [Fact]
    public void Heartbeat_GiuTienDoVaCheDo_KhongLoMaLenh()
    {
        var store = new TrangThaiMayInAoStore();
        var req = JsonSerializer.Deserialize<BaoCaoMayInRequest>("""
            {"MaMayIn":1,"TrangThai":"DANG_IN","CheDo":"THAT","TenHangDoi":"Ricoh USB","TongTrang":100,"TrangDaIn":20,"MaLenh":"secret"}
            """);
        store.CapNhat(1, "DANG_IN", 6, null, req);
        var json = JsonSerializer.SerializeToElement(Assert.Single(store.LayTatCa()));
        Assert.Equal("THAT", json.GetProperty("CheDo").GetString());
        Assert.Equal(20, json.GetProperty("TrangDaIn").GetInt32());
        Assert.False(json.TryGetProperty("MaLenh", out _));
    }

    [Fact]
    public void DieuKhien_LuuQuaRestart_KhoiPhucKhongXoaPause()
    {
        string dir = Path.Combine(Path.GetTempPath(), "photocopy-control-" + Guid.NewGuid());
        var store = new TrangThaiMayInAoStore(thuMucDieuKhien: dir);
        Assert.Null(store.DieuKhien(1, new() { ThaoTac = "TAM_DUNG" }));
        Assert.Null(store.DieuKhien(1, new() { ThaoTac = "HET_GIAY" }));
        store = new TrangThaiMayInAoStore(thuMucDieuKhien: dir);
        Assert.True(store.LayDieuKhien(1).TamDung);
        Assert.Equal("HET_GIAY", store.LayDieuKhien(1).LoiMoPhong);
        Assert.Null(store.DieuKhien(1, new() { ThaoTac = "KHOI_PHUC" }));
        Assert.True(store.LayDieuKhien(1).TamDung);
        Assert.Null(store.LayDieuKhien(1).LoiMoPhong);
    }

    [Theory]
    [InlineData("THAT", "CHO_XAC_NHAN", 6, true)]
    [InlineData("THAT", "DANG_IN", 6, false)]
    [InlineData("AO", "CHO_XAC_NHAN", 6, false)]
    [InlineData("THAT", "CHO_XAC_NHAN", 7, false)]
    public void XacNhanBanGiay_PhaiDungDonDangChoVaLaMayThat(string mode, string state, int order, bool ok)
    {
        var store = new TrangThaiMayInAoStore();
        store.CapNhat(1, state, 6, null, new() { CheDo = mode });
        string error = store.DieuKhien(1, new() { ThaoTac = "XAC_NHAN_BAN_GIAY", MaDonHang = order }, "job-6");
        Assert.Equal(ok, error == null);
        Assert.Equal(ok ? "job-6" : null, store.LayDieuKhien(1).MaLenhXacNhan);
    }

    [Fact]
    public void MayThat_KhongChoGiaLapLoi()
    {
        var store = new TrangThaiMayInAoStore();
        store.CapNhat(1, "SAN_SANG", baoCao: new() { CheDo = "THAT" });
        Assert.NotNull(store.DieuKhien(1, new() { ThaoTac = "KET_GIAY" }));
        Assert.NotNull(store.DieuKhien(1, new() { ThaoTac = "PURGE" }));
    }

    [Fact]
    public void PauseVaLoi_KhongTangTienDoAo()
    {
        var p = new TienTrinhInDaLuu { TongSoGiay = 100, SoGiayDaIn = 30 };
        p.TangTienDoAo(5, true);
        Assert.Equal(30, p.SoGiayDaIn);
        p.TangTienDoAo(5, false);
        Assert.Equal(35, p.SoGiayDaIn);
        p.TangTienDoAo(1000, false);
        Assert.Equal(100, p.SoGiayDaIn);
    }

    [Fact]
    public void InThat_ThamSoDayDuVaKhongDungShell()
    {
        var dong = new DongLenhInDto { KhoGiay = "A3", HuongGiay = "NGANG", MatIn = "2_MAT", MauSac = "MAU", SoBan = 3, SoTrang = 4, TuTrang = 2, DenTrang = 5 };
        var args = MayInWindows.TaoThamSo("Máy \"USB\"", "C:/test folder/lenh.pdf", dong);
        Assert.Equal(new[] { "-silent", "-print-to", "Máy \"USB\"", "-print-settings", "2-5,3x,paper=A3,color,duplexlong,landscape,noscale,collate", "C:/test folder/lenh.pdf" }, args);
        dong.MatIn = "SAI";
        Assert.Throws<InvalidOperationException>(() => MayInWindows.TaoThamSo("USB", "x.pdf", dong));
    }

    [Fact]
    public void PhamViTrangPhaiKhopSoTrangTinhTien()
    {
        Assert.Throws<InvalidOperationException>(() => MayInWindows.TaoThamSo("USB", "x.pdf", new() { SoTrang = 2, SoBan = 1, TuTrang = 1, DenTrang = 5 }));
    }

    [Fact]
    public void ChuaCoBoInPdf_KhongChoGuiMayThat()
    {
        Assert.Throws<InvalidOperationException>(() => new MayInWindows().KiemTraCauHinh("", "USB"));
    }

    [Fact]
    public void KhongGanHaiMayLogicCungMotQueueWindows()
    {
        var config = new CauHinhSimulator { MayIn = new() {
            new() { MaMayIn = 1, CheDo = "THAT", TenHangDoiWindows = "USB" },
            new() { MaMayIn = 2, CheDo = "THAT", TenHangDoiWindows = "usb" }
        }};
        Assert.Throws<InvalidOperationException>(() => config.KiemTraDanhSachMay());
    }
}
