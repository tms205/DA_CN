using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyApi.GiaoHang;
using PhotocopyShared.Dtos.GiaoHang;
using Xunit;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyShared.Constants;

namespace PhotocopyApi.Tests;

public class BangChungGiaoTests
{
    [Fact]
    public void MultipartGps_KhongDocSaiDauThapPhanTrenWindowsTiengViet()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try {
            System.Globalization.CultureInfo.CurrentCulture = new("vi-VN");
            var req = new BaoGiaoThatBaiRequest { ViDo="10.7654321",KinhDo="106.7654321",DoChinhXac="12.5",ThoiDiemGps="2026-11-01T02:30:00Z" };
            var gps = req.DocViTri();
            Assert.Equal(10.7654321m,gps.ViDo); Assert.Equal(106.7654321m,gps.KinhDo);
            Assert.Equal(12.5,gps.DoChinhXac);
            req.ViDo="10,7"; Assert.Throws<ArgumentException>(()=>req.DocViTri());
        } finally { System.Globalization.CultureInfo.CurrentCulture=old; }
    }
    [Fact]
    public async Task GeoapifyLoi_VanGiuGps_KhongTaoDiaChiGia()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var http = new HttpClient(new BanDoLoi());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string> {
            ["GiaoHang:GeoapifyApiKey"] = "fixture-not-real" }).Build();
        var controller = new GiaoHangController(config, cache, null, NullLogger<GiaoHangController>.Instance,
            new GeoapifyService(config, cache, http)) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] {new Claim(TenClaim.MaNguoiDung, "7"),
                    new Claim(TenClaim.MaChiNhanh, "1"), new Claim(TenClaim.VaiTro, VaiTro.Shipper)},
                    "fixture", ClaimTypes.Name, TenClaim.VaiTro)) }} };
        var request = new ViTriShipperRequest { ViDo=10.7m, KinhDo=106.7m, DoChinhXac=12, ThoiDiemGps=DateTimeOffset.UtcNow };
        var result = Assert.IsType<OkObjectResult>(await controller.DinhViShipper(request));
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(result.Value));
        Assert.False(json.RootElement.GetProperty("daTraDuocDiaChi").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("diaChi").ValueKind);
        Assert.Equal(10.7m, request.ViDo);
        Assert.Equal(106.7m, request.KinhDo);
    }
    private sealed class BanDoLoi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    [Theory]
    [InlineData(-6, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    public void Gps_KhongNhanViTriCuHoacTuongLai(int minutes, bool valid)
    {
        var now = DateTimeOffset.UtcNow;
        var req = new ViTriShipperRequest { ViDo = 10.7m, KinhDo = 106.7m,
            DoChinhXac = 15, ThoiDiemGps = now.AddMinutes(minutes) };
        if (valid) BangChungGiaoHangStore.KiemTraViTri(req, now);
        else Assert.Throws<ArgumentException>(() => BangChungGiaoHangStore.KiemTraViTri(req, now));
    }

    [Theory]
    [InlineData(91, 106, 10)]
    [InlineData(10, -181, 10)]
    [InlineData(10, 106, -1)]
    [InlineData(10, 106, double.NaN)]
    public void Gps_TuChoiToaDoSaiVaNaN(double lat, double lon, double accuracy) =>
        Assert.Throws<ArgumentException>(() => BangChungGiaoHangStore.KiemTraViTri(new() {
            ViDo = (decimal)lat, KinhDo = (decimal)lon, DoChinhXac = accuracy,
            ThoiDiemGps = DateTimeOffset.UtcNow }, DateTimeOffset.UtcNow));

    [Fact]
    public void FileGiaAnhQuaDungLuongVaThieuAnh_BiTuChoi()
    {
        Assert.Throws<ArgumentException>(() => BangChungGiaoHangStore.KiemTraAnh("not a png"u8.ToArray()));
        Assert.Throws<ArgumentException>(() => BangChungGiaoHangStore.KiemTraAnh(new byte[] {137,80,78,71,13,10,26,10}));
        Assert.Throws<ArgumentException>(() => BangChungGiaoHangStore.KiemTraDanhSach(Array.Empty<IFormFile>()));
        var fake = new FormFile(new MemoryStream(), 0, 6 * 1024 * 1024, "Anh", "x.png");
        Assert.Throws<ArgumentException>(() => BangChungGiaoHangStore.KiemTraDanhSach(new[] { fake }));
        var exe = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "Anh", "x.exe");
        Assert.Throws<ArgumentException>(() => BangChungGiaoHangStore.KiemTraDanhSach(new[] { exe }));
    }

    private static FormFile Anh(string name = "nhat-ky.png")
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var image = new Bitmap(8, 8);
        using var bytes = new MemoryStream(); image.Save(bytes, ImageFormat.Png);
        byte[] data = bytes.ToArray();
        return new FormFile(new MemoryStream(data), 0, data.Length, "Anh", name);
    }

    [Fact]
    public async Task LuuAnh_TenTrungKhongGhiDe_HashDung_DonDungFileCuaRequest()
    {
        string root = Path.Combine(Path.GetTempPath(), "PhotocopyEvidenceTests", Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string> {
            ["GiaoHang:ThuMucBangChung"] = root }).Build();
        var store = new BangChungGiaoHangStore(config);
        try
        {
            var results = await Task.WhenAll(
                store.Luu(10, new[] { Anh("../../same.png") }, default),
                store.Luu(10, new[] { Anh("../../same.png") }, default));
            var a = results[0].Single(); var b = results[1].Single();
            Assert.NotEqual(a.MaAnh, b.MaAnh);
            Assert.Equal("image/png", a.LoaiNoiDung);
            byte[] saved = await File.ReadAllBytesAsync(store.DuongDan(10, a));
            Assert.Equal(a.Sha256, Convert.ToHexString(SHA256.HashData(saved)));
            Assert.Equal(a.KichThuoc, saved.Length);
            store.XoaTepCuaYeuCau(10, results[0]);
            Assert.False(File.Exists(store.DuongDan(10, a)));
            Assert.True(File.Exists(store.DuongDan(10, b)));
            Assert.Throws<ArgumentException>(() => store.DuongDan(10, new AnhBangChungDto { MaAnh = "../../etc", LoaiNoiDung = "image/png" }));
            // Ảnh hợp lệ trước file hỏng phải được dọn, không đụng file request khác.
            var broken = new FormFile(new MemoryStream("fake"u8.ToArray()), 0, 4, "Anh", "bad.png");
            await Assert.ThrowsAsync<ArgumentException>(() => store.Luu(10, new[] { Anh(), broken }, default));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "10")));
            store.XoaTepCuaYeuCau(10, results[1]);
        }
        finally {
            Assert.StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PhotocopyEvidenceTests")) + Path.DirectorySeparatorChar,Path.GetFullPath(root));
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("BaoGiaoThatBai", "SHIPPER")]
    [InlineData("DinhViShipper", "SHIPPER")]
    [InlineData("XemBangChung", "SUPER_ADMIN,BRANCH_ADMIN,SHIPPER")]
    [InlineData("XemAnhBangChung", "SUPER_ADMIN,BRANCH_ADMIN,SHIPPER")]
    public void BangChung_KhongCapQuyenChoKhachHayNhanVienQuay(string name, string roles) =>
        Assert.Equal(roles, typeof(GiaoHangController).GetMethod(name)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
}
