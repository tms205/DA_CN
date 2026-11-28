using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyApi.InLai;
using Xunit;

namespace PhotocopyApi.Tests;

// 28/11: in lại do lỗi quầy bắt buộc 1–3 ảnh bản in lỗi + báo cáo in lại theo nhân viên.
public class BangChungInLaiTests
{
    private static FormFile Anh(string ten = "ban-loi.png")
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var image = new Bitmap(8, 8);
        using var bytes = new MemoryStream(); image.Save(bytes, ImageFormat.Png);
        byte[] data = bytes.ToArray();
        return new FormFile(new MemoryStream(data), 0, data.Length, "Anh", ten);
    }

    private static (BangChungInLaiStore Store, string Root) TaoStore()
    {
        string root = Path.Combine(Path.GetTempPath(), "PhotocopyReprintTests", Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> {
            ["InLai:ThuMucBangChung"] = root }).Build();
        return (new BangChungInLaiStore(config), root);
    }

    [Fact]
    public async Task LuuAnh_DocLai_GanVaoDonInLai_RoiTraTheoMaDon()
    {
        var (store, root) = TaoStore();
        try
        {
            HoSoInLai hoSo = await store.Luu(15, 7, new[] { Anh(), Anh("2.png") }, CancellationToken.None);
            Assert.Equal(2, hoSo.Anh.Count);

            HoSoInLai doc = store.DocHoSoTam(15, 7, hoSo.MaBangChung);
            await store.GanVaoDonInLai(doc, 18);

            HoSoInLai theoDon = store.DocTheoDonInLai(18);
            Assert.NotNull(theoDon);
            Assert.Equal(15, theoDon.MaDonHangGoc);
            Assert.Equal(18, theoDon.MaDonInLai);
            Assert.True(File.Exists(store.DuongDanAnh(15, theoDon.Anh[0])));
            Assert.Null(store.DocTheoDonInLai(19));   // đơn in lại cũ không có ảnh
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task HoSoAnh_KhongDungDuocChoDonKhac_NguoiKhac_HoacLanThuHai()
    {
        var (store, root) = TaoStore();
        try
        {
            HoSoInLai hoSo = await store.Luu(15, 7, new[] { Anh() }, CancellationToken.None);
            Assert.Throws<ArgumentException>(() => store.DocHoSoTam(16, 7, hoSo.MaBangChung));  // đơn gốc khác
            Assert.Throws<ArgumentException>(() => store.DocHoSoTam(15, 8, hoSo.MaBangChung));  // người khác
            Assert.Throws<ArgumentException>(() => store.DocHoSoTam(15, 7, ""));                 // không gửi ảnh
            Assert.Throws<ArgumentException>(() => store.DocHoSoTam(15, 7, Guid.NewGuid().ToString("N")));

            await store.GanVaoDonInLai(store.DocHoSoTam(15, 7, hoSo.MaBangChung), 18);
            // Một bộ ảnh chỉ dùng cho 1 lần in lại, không lấy lại để in lại tiếp.
            var loi = Assert.Throws<ArgumentException>(() => store.DocHoSoTam(15, 7, hoSo.MaBangChung));
            Assert.Contains("đã dùng", loi.Message);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TaiAnh_TuChoiKhongCoAnh_QuaBaAnh_FileKhongPhaiAnh()
    {
        var (store, root) = TaoStore();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => store.Luu(15, 7, Array.Empty<IFormFile>(), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => store.Luu(15, 7, new[] { Anh(), Anh(), Anh(), Anh() }, CancellationToken.None));
            var gia = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "Anh", "gia.png");
            await Assert.ThrowsAsync<ArgumentException>(() => store.Luu(15, 7, new[] { gia }, CancellationToken.None));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Api_AnhInLai_ChiNhanVienQuanLyTaiLen_QuanLyCeoXem()
    {
        MethodInfo tai = typeof(DonHangController).GetMethod(nameof(DonHangController.TaiBangChungInLai))!;
        Assert.Equal("{maDonHang:int:min(1)}/bang-chung-in-lai", tai.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal("BRANCH_ADMIN,STAFF", tai.GetCustomAttribute<AuthorizeAttribute>()!.Roles);

        MethodInfo baoCao = typeof(QuanTriController).GetMethod(nameof(QuanTriController.BaoCaoInLai))!;
        Assert.Equal("bao-cao-in-lai", baoCao.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("SUPER_ADMIN,BRANCH_ADMIN", baoCao.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public void AnhInLaiRequest_BatBuocTuMotDenBaAnh()
    {
        var rong = new AnhInLaiRequest();
        Assert.False(Validator.TryValidateObject(rong, new ValidationContext(rong), null, true));
    }
}
