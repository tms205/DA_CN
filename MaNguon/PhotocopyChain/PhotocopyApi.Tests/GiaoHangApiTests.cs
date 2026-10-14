using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using PhotocopyApi.Controllers;
using PhotocopyApi.GiaoHang;
using PhotocopyApi.Models;
using PhotocopyShared.Dtos.GiaoHang;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PhotocopyApi.Tests;

public class GiaoHangApiTests
{
    [Fact]
    public void ViTri_PhaiGanDungKhachDiaChiVaToaDoKhongTheTuSua()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var geo = new GeoapifyService(new ConfigurationBuilder().Build(), cache);
        var ghi = geo.GetType().GetMethod("GhiNhanViTri");
        var doc = geo.GetType().GetMethod("KiemTraViTri");
        Assert.NotNull(ghi); Assert.NotNull(doc);
        var diem = new DiaChiGiaoDto { DiaChi = "140 Lê Trọng Tấn", ViDo = 10.7m, KinhDo = 106.7m };
        string ma = (string)ghi.Invoke(geo, new object[] { 1, diem });
        Assert.Null(doc.Invoke(geo, new object[] { ma, 1, diem.DiaChi, diem.ViDo, diem.KinhDo }));
        foreach (var args in new[] {
            new object[] { ma, 2, diem.DiaChi, diem.ViDo, diem.KinhDo },
            new object[] { ma, 1, "Địa chỉ xa khác", diem.ViDo, diem.KinhDo },
            new object[] { ma, 1, diem.DiaChi, 10.1m, diem.KinhDo },
            new object[] { "khong-ton-tai", 1, diem.DiaChi, diem.ViDo, diem.KinhDo } })
        {
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => doc.Invoke(geo, args));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }
    }

    [Fact]
    public void ApiGiaoHang_CoQuyenTheoTungThaoTac()
    {
        var type = typeof(DonHangController).Assembly.GetType("PhotocopyApi.Controllers.GiaoHangController");
        Assert.NotNull(type);
        Assert.Equal("SHIPPER", type.GetMethod("NhanDon").GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("SHIPPER", type.GetMethod("BaoKetQua").GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.DoesNotContain("SHIPPER", type.GetMethod("PhanCong").GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void GiaoThatBai_BatBuocGhiLyDo(string lyDo)
    {
        var req = new KetQuaGiaoRequest { ThanhCong = false, LyDo = lyDo };
        Assert.NotEmpty(req.Validate(new ValidationContext(req)));
    }

    [Fact]
    public void SnapshotPhi_KhongDungChoKhachChiNhanhDiaChiKhac()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var geo = new GeoapifyService(new ConfigurationBuilder().Build(), cache);
        var kq = new KetQuaPhiGiaoHang(3, 19000, 600);
        string ma = geo.GiuBaoGia(1, 2, "140 Lê Trọng Tấn", 10, 106, kq);
        Assert.Equal(kq, geo.DocBaoGia(ma, 1, 2, "140 Lê Trọng Tấn", 10, 106));
        Assert.Throws<InvalidOperationException>(() => geo.DocBaoGia(ma, 9, 2, "140 Lê Trọng Tấn", 10, 106));
        Assert.Throws<InvalidOperationException>(() => geo.DocBaoGia(ma, 1, 3, "140 Lê Trọng Tấn", 10, 106));
        Assert.Throws<InvalidOperationException>(() => geo.DocBaoGia(ma, 1, 2, "Địa chỉ khác", 10, 106));
        Assert.Throws<InvalidOperationException>(() => geo.DocBaoGia(ma, 1, 2, "140 Lê Trọng Tấn", 11, 106));
        cache.Remove("phi-giao:" + ma);
        Assert.Throws<InvalidOperationException>(() => geo.DocBaoGia(ma, 1, 2, "140 Lê Trọng Tấn", 10, 106));
    }
}
