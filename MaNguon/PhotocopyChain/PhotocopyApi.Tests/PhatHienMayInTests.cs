using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyApi.MayInAo;
using PhotocopyPrinterSimulator;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public sealed class PhatHienMayInTests
{
    private static string ThuMuc() => Path.Combine(Path.GetTempPath(), "photocopy-discovery-" + Guid.NewGuid());
    private static readonly string Id = new('a', 32);
    private static BaoCaoPhatHienRequest BaoCao() => new() { TenMayTinh = "PC fixture", BoInPdfSanSang = true,
        MaMayCauHinh = new() { 1, 2 }, HangDoi = new() {
            new() { Ten = "HP fixture", Driver = "HP", Cong = "USB001" },
            new() { Ten = "Microsoft Print to PDF", LaMayAo = true, Cong = "PORTPROMPT:" }
        }};
    private static LienKetMayInRequest That() => new() { ConnectorId = Id, CheDo = "THAT", TenHangDoi = "HP fixture", DaKiemTraThietBi = true };
    private static PhatHienMayInStore Tao() { var s = new PhatHienMayInStore(ThuMuc()); s.CapNhat(Id, BaoCao()); return s; }

    [Fact]
    public void LuuDocLai_GiuLienKetNhungKhongGiaOnlineSauRestart()
    {
        string dir = ThuMuc();
        var s = new PhatHienMayInStore(dir); s.CapNhat(Id, BaoCao());
        Assert.Null(s.LienKet(1, 3, That(), () => false));
        var cu = s.LayLienKet(1);
        var moi = new PhatHienMayInStore(dir);
        Assert.Equal(cu.PhienBan, moi.LayLienKet(1).PhienBan);
        Assert.Empty(moi.LayTatCa(null)); // Cần heartbeat mới, không coi persisted inventory là online.
        Assert.False(moi.DuocNhanLenh(1, Id, "cu"));
        Assert.False(moi.DuocNhanLenh(1, new string('b', 32), cu.PhienBan));
        Assert.True(moi.DuocNhanLenh(1, Id, cu.PhienBan));
    }

    [Fact]
    public void MayDangGiuLenh_KhongDoiQueueHoacCheDo()
    {
        var s = Tao(); Assert.Contains("lệnh", s.LienKet(1, 1, That(), () => true));
        Assert.Null(s.LayLienKet(1));
    }

    [Fact]
    public void QueueAo_KhongDuocNhanDonSanXuat()
    {
        var s = Tao(); var req = That(); req.TenHangDoi = "Microsoft Print to PDF";
        Assert.Contains("ảo", s.LienKet(1, 1, req, () => false));
    }

    [Fact]
    public void KhongXacNhanThietBi_HoacThieuBoGui_KhongKichHoat()
    {
        var s = Tao(); var req = That(); req.DaKiemTraThietBi = false;
        Assert.Contains("xác nhận", s.LienKet(1, 1, req, () => false));
        var report = BaoCao(); report.BoInPdfSanSang = false; s.CapNhat(Id, report);
        Assert.Contains("Sumatra", s.LienKet(1, 1, That(), () => false));
    }

    [Fact]
    public void MotQueue_KhongHaiMayLogic_MotConnectorKhongHaiChiNhanh()
    {
        var s = Tao(); Assert.Null(s.LienKet(1, 3, That(), () => false));
        Assert.Contains("Queue đã", s.LienKet(2, 3, That(), () => false));
        Assert.Contains("chi nhánh khác", s.LienKet(2, 4, That(), () => false));
        Assert.Empty(s.LayTatCa(4)); Assert.Single(s.LayTatCa(3));
    }

    [Fact]
    public void KhongLuuQueueKhongTonTai_MaMayKhongDuocCauHinh_HoacScannerLoi()
    {
        var s = Tao(); var req = That(); req.TenHangDoi = "khong-co";
        Assert.NotNull(s.LienKet(1, 1, req, () => false));
        Assert.NotNull(s.LienKet(99, 1, That(), () => false));
        var report = BaoCao(); report.Loi = "Spooler stopped"; s.CapNhat(Id, report);
        Assert.NotNull(s.LienKet(1, 1, That(), () => false));
    }

    [Fact]
    public void DuLieuQuaHan_KhongChoKichHoat()
    {
        var gio = new DongHo(); var s = new PhatHienMayInStore(ThuMuc(), gio); s.CapNhat(Id, BaoCao());
        gio.Tang = TimeSpan.FromSeconds(91);
        Assert.False(Assert.Single(s.LayTatCa(null)).TrucTuyen);
        Assert.NotNull(s.LienKet(1, 1, That(), () => false));
    }
    private sealed class DongHo : TimeProvider { public TimeSpan Tang; public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch + Tang; }

    [Fact]
    public void SnapshotKhongChoCallerSuaStore_VaDoiCheDoDoiPhienBan()
    {
        var s = Tao(); Assert.Null(s.LienKet(1, 1, That(), () => false));
        string old = s.LayLienKet(1).PhienBan;
        s.LayLienKet(1).CheDo = "AO"; s.LayTatCa(null)[0].LienKet[0].CheDo = "AO";
        Assert.Equal("THAT", s.LayLienKet(1).CheDo);
        Assert.Null(s.LienKet(1, 1, new() { ConnectorId = Id, CheDo = "AO" }, () => false));
        Assert.NotEqual(old, s.LayLienKet(1).PhienBan);
        Assert.False(s.DuocNhanLenh(1, Id, old));
    }

    [Theory]
    [InlineData("Microsoft Print to PDF", "Microsoft", "PORTPROMPT:", true)]
    [InlineData("Printer renamed", "Microsoft XPS Document Writer", "local", true)]
    [InlineData("HP", "HP", "USB001", false)]
    [InlineData("Canon", "Canon", "IP_192.168.1.12", false)]
    public void PhanLoaiAo_DuaTenDriverCong_KhongChiTen(string ten, string driver, string port, bool ao) =>
        Assert.Equal(ao, PhatHienMayInWindows.LaQueueAo(ten, driver, port));

    [Fact]
    public void ApDungLienKet_KhongSuaCauHinhGoc_HoacTocDo()
    {
        var goc = new CauHinhTocDoMayIn { MaMayIn = 1, CheDo = "AO", TocDoA4 = 20 };
        var moi = MayInSimulator.ApDungLienKet(goc, new() { MaMayIn = 1, CheDo = "THAT", TenHangDoi = "HP" });
        Assert.Equal("AO", goc.CheDo); Assert.Equal("THAT", moi.CheDo); Assert.Equal(20, moi.TocDoA4);
        Assert.Throws<InvalidDataException>(() => MayInSimulator.ApDungLienKet(goc, new() { MaMayIn = 2 }));
    }

    [Fact]
    public void PdfThu_CoCauTrucTrangA4VaKhongDuLieuKhach()
    {
        byte[] bytes = ChanDoanMayIn.TaoPdfThu();
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        Assert.Equal(1, pdf.NumberOfPages);
        Assert.Equal(595, pdf.GetPage(1).Width);
        Assert.Contains("TEST PAGE", pdf.GetPage(1).Text);
        string text = System.Text.Encoding.ASCII.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", text); Assert.Contains("NOT a customer order", text);
        Assert.Contains("/MediaBox [0 0 595 842]", text); Assert.Contains("%%EOF", text);
    }

    [Fact]
    public void ChanQueueLoiVaQueueDaDuocCauHinhCu_TranhGuiTrung()
    {
        var s = Tao(); var report = BaoCao(); report.HangDoi[0].TrangThaiDriver = 128;
        s.CapNhat(Id, report); Assert.Contains("offline", s.LienKet(1, 1, That(), () => false));
        report.HangDoi[0].TrangThaiDriver = 0;
        report.KetNoiCauHinh.Add(new() { MaMayIn = 2, CheDo = "THAT", TenHangDoi = "HP fixture" });
        s.CapNhat(Id, report); Assert.Contains("máy khác", s.LienKet(1, 1, That(), () => false));
    }

    [Fact]
    public void BaoCaoKhoaSai_KhongGhiStore_NhanVienKhongChiNhanhKhongXemInventory()
    {
        var s = Tao(); var c = Controller(s);
        c.Request.Headers["X-Printer-Api-Key"] = "sai";
        Assert.IsType<UnauthorizedResult>(c.BaoCao(BaoCao()));
        c.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "STAFF") }, "test"));
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(c.DanhSach()).StatusCode);
        var role = typeof(PhatHienMayInController).GetMethod("LienKet").GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("SUPER_ADMIN", role.Roles);
    }

    [Fact]
    public async Task ConnectorSaiVaPhienCu_BiChanTruocKhiHoiDatabase()
    {
        var s = Tao(); Assert.Null(s.LienKet(1, 1, That(), () => false));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> {
            ["PrinterConnector:ApiKey"] = "fixture-key" }).Build();
        var c = new MayInAoController(config, new TrangThaiMayInAoStore(), null,
            NullLogger<MayInAoController>.Instance, new QuyenNhanLenhInStore(ThuMuc()), s)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        c.Request.Headers["X-Printer-Api-Key"] = "fixture-key";
        c.Request.Headers["X-Printer-Connector-Id"] = Id;
        c.Request.Headers["X-Printer-Binding-Version"] = "phien-cu";
        Assert.IsType<ConflictObjectResult>(await c.LenhTiepTheo(1));
        c.Request.Headers["X-Printer-Connector-Id"] = new string('b', 32);
        Assert.IsType<ConflictResult>(c.LayDieuKhien(1));
        Assert.IsType<ConflictObjectResult>(await c.Heartbeat(1, new()));
    }

    private static PhatHienMayInController Controller(PhatHienMayInStore s)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["PrinterConnector:ApiKey"] = "fixture-key" }).Build();
        var c = new PhatHienMayInController(config, s, new QuyenNhanLenhInStore(ThuMuc()), NullLogger<PhatHienMayInController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        c.Request.Headers["X-Printer-Api-Key"] = "fixture-key"; c.Request.Headers["X-Printer-Connector-Id"] = Id;
        return c;
    }
}
