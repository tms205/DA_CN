using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyApi.MayInAo;
using PhotocopyPrinterSimulator;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.MayInAo;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public sealed class MayInThietBiTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "photocopy-device-" + Guid.NewGuid());
    private const string Id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static HangDoiWindowsDto Queue(string papers = "[\"A4\"]", bool? color = false, bool? duplex = true) =>
        JsonSerializer.Deserialize<HangDoiWindowsDto>("{\"ten\":\"HP M236sdw fixture\",\"khoGiayHoTro\":" + papers
            + ",\"inMau\":" + JsonSerializer.Serialize(color) + ",\"inHaiMat\":" + JsonSerializer.Serialize(duplex) + "}", Json);

    private PhatHienMayInStore Store(HangDoiWindowsDto queue = null)
    {
        var store = new PhatHienMayInStore(_dir);
        store.CapNhat(Id, new() { TenMayTinh = "PC fixture", BoInPdfSanSang = true,
            MaMayCauHinh = new() { 1 }, HangDoi = new() { queue ?? Queue() } });
        Assert.Null(store.LienKet(1, 1, new() { ConnectorId = Id, CheDo = "THAT",
            TenHangDoi = "HP M236sdw fixture", DaKiemTraThietBi = true }, () => false));
        return store;
    }

    private static object Goi(PhatHienMayInStore store, string name, params object[] args)
    {
        var method = typeof(PhatHienMayInStore).GetMethod(name);
        Assert.NotNull(method);
        return method.Invoke(store, args);
    }

    [Fact]
    public void HienTenMayThat_GiuNguyenTenDanhMucVaMaMay()
    {
        var connection = Goi(Store(), "LayKetNoi", 1);
        string json = "{\"maMayIn\":1,\"tenMay\":\"Ricoh danh mục cũ\",\"ketNoi\":" + JsonSerializer.Serialize(connection, Json) + "}";
        foreach (var type in new[] { typeof(MayInDto), typeof(MayInQuanTriDto) })
        {
            var dto = JsonSerializer.Deserialize(json, type, Json);
            Assert.Equal("Ricoh danh mục cũ", type.GetProperty("TenMay")!.GetValue(dto));
            var display = type.GetProperty("TenHienThi");
            Assert.NotNull(display);
            Assert.Equal("HP M236sdw fixture", display.GetValue(dto));
        }
    }

    [Theory]
    [InlineData("A4", "DEN_TRANG", "1_MAT", null)]
    [InlineData("A4", "DEN_TRANG", "2_MAT", null)]
    [InlineData("A3", "DEN_TRANG", "1_MAT", "A3")]
    [InlineData("A4", "MAU", "1_MAT", "màu")]
    public void ChanQuyCachVuotNangLucDriver(string paper, string color, string side, string error)
    {
        var store = Store();
        var lines = new[] { new DongLenhInDto { KhoGiay = paper, MauSac = color, MatIn = side } };
        var loi = (string)Goi(store, "KiemTraQuyCach", 1, lines);
        if (error == null) Assert.Null(loi);
        else Assert.Contains(error, loi);
    }

    [Fact]
    public void DonNhieuFile_PhaiKiemTraCaFileA3PhiaSau()
    {
        var lines = new[] { new DongLenhInDto { KhoGiay = "A4" }, new DongLenhInDto { KhoGiay = "A3" } };
        Assert.Contains("A3", (string)Goi(Store(), "KiemTraQuyCach", 1, lines));
    }

    [Theory]
    [InlineData(null, true, "1_MAT")]
    [InlineData("[\"A4\"]", null, "2_MAT")]
    [InlineData("[\"A4\"]", false, "2_MAT")]
    public void DriverThieuThongTin_KhongDoanLaHoTro(string papers, bool? duplex, string side)
    {
        var store = Store(Queue(papers ?? "null", false, duplex));
        Assert.NotNull(Goi(store, "KiemTraQuyCach", 1, new[] { new DongLenhInDto { MatIn = side } }));
    }

    [Fact]
    public void MatInventorySauRestart_KhongChoMayThatNhanDonMoi_MayAoKhongBiAnhHuong()
    {
        Store();
        var restarted = new PhatHienMayInStore(_dir);
        Assert.NotNull(Goi(restarted, "KiemTraQuyCach", 1, new[] { new DongLenhInDto() }));
        Assert.Null(Goi(restarted, "KiemTraQuyCach", 2, new[] { new DongLenhInDto { KhoGiay = "A3", MauSac = "MAU" } }));
    }

    [Theory]
    [InlineData("BRANCH_ADMIN", 1, 1)]
    [InlineData("STAFF", 1, 1)]
    [InlineData("BRANCH_ADMIN", 2, 0)]
    [InlineData("STAFF", 2, 0)]
    public void NhanSuDungChiNhanh_XemDuocMayDaLienKet(string role, int branch, int count)
    {
        var controller = new PhatHienMayInController(new ConfigurationBuilder().Build(), Store(),
            new QuyenNhanLenhInStore(Path.Combine(_dir, "claims")), NullLogger<PhatHienMayInController>.Instance)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role),
            new Claim(TenClaim.MaChiNhanh, branch.ToString()) }, "fixture"));
        var data = Assert.IsType<List<ConnectorMayInDto>>(Assert.IsType<OkObjectResult>(controller.DanhSach()).Value);
        Assert.Equal(count, data.Count);
    }

    [Fact]
    public void DoiMaKhoGiayWindows_KhongTuGanA3ChoDriverA4()
    {
        var method = typeof(PhatHienMayInWindows).GetMethod("DoiKhoGiay");
        Assert.NotNull(method);
        var papers = Assert.IsType<List<string>>(method.Invoke(null, new object[] { new short[] { 9, 10, 1 } }));
        Assert.Equal(new[] { "A4" }, papers);
        Assert.Contains("A3", Assert.IsType<List<string>>(method.Invoke(null, new object[] { new short[] { 8, 9 } })));
    }

    [Fact]
    public void DriverIpp_DocPrintCapabilitiesXml_ChiNhanKhoVaNangLucDuocCongBo()
    {
        var type = typeof(PhatHienMayInWindows).Assembly.GetType("PhotocopyPrinterSimulator.NangLucPrintTicket");
        Assert.NotNull(type);
        const string xml = """
            <psf:PrintCapabilities xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
              xmlns:k="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords" version="1">
              <psf:Feature name="k:PageMediaSize"><psf:Option name="k:ISOA4" /></psf:Feature>
              <psf:Feature name="k:PageOutputColor"><psf:Option name="k:Monochrome" /></psf:Feature>
              <psf:Feature name="k:JobDuplexAllDocumentsContiguously"><psf:Option name="k:OneSided" /><psf:Option name="k:TwoSidedLongEdge" /></psf:Feature>
            </psf:PrintCapabilities>
            """;
        var data = Assert.IsType<HangDoiWindowsDto>(type.GetMethod("DocXml")!.Invoke(null, new object[] { xml }));
        var papers = typeof(HangDoiWindowsDto).GetProperty("KhoGiayHoTro")!.GetValue(data);
        Assert.Equal(new[] { "A4" }, Assert.IsType<List<string>>(papers));
        Assert.False(data.InMau);
        Assert.True(data.InHaiMat);
    }

    [Fact]
    public void DriverXml_KhongTinOptionNgoaiNamespaceHoacDocFileNgoai()
    {
        var type = typeof(PhatHienMayInWindows).Assembly.GetType("PhotocopyPrinterSimulator.NangLucPrintTicket");
        Assert.NotNull(type);
        const string xml = """
            <f:PrintCapabilities xmlns:f="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework" xmlns:fake="urn:fake">
              <f:Feature name="fake:PageMediaSize"><f:Option name="fake:ISOA3" /></f:Feature>
            </f:PrintCapabilities>
            """;
        var data = Assert.IsType<HangDoiWindowsDto>(type.GetMethod("DocXml")!.Invoke(null, new object[] { xml }));
        Assert.Null(typeof(HangDoiWindowsDto).GetProperty("KhoGiayHoTro")!.GetValue(data));
        Assert.Null(data.InHaiMat);
        Assert.Throws<TargetInvocationException>(() => type.GetMethod("DocXml")!.Invoke(null,
            new object[] { "<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///untrusted'>]><x>&a;</x>" }));
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
