using PhotocopyShared.Dtos.DatIn;
using PhotocopyShared.Dtos.DanhMuc;
using PhotocopyShared.Dtos.DonHang;
using System.Reflection;
using Xunit;

namespace PhotocopyApi.Tests;

public class AnhTheTests
{
    [Fact]
    public void MuaThemAnhThe_TinhTheoSoAnhKhongTheoSoTrangPdf()
    {
        var trang = new PhotocopyAdmin.Pages.BanGiao();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        void Gan(string ten, object giaTri) => trang.GetType().GetField(ten, flags).SetValue(trang, giaTri);
        Gan("_donPhatSinh", new DonHangChiTietDto {
            DongIn = new List<DongInChiTietDto> { new() {
                MaChiTiet = 1, MaDichVu = 2, SoTrang = 1, TongSoTrangFile = 1, SoBan = 1,
                KichThuocAnh = "3X4", SoLuongAnh = 8
            } }
        });
        Gan("_maChiTietPhatSinh", 1);
        Gan("_maDichVuBoSung", 2);
        Gan("_dsDichVu", new List<DichVuDto> { new() { MaDichVu = 2, DonGia = 5000, KichThuocAnh = "3X4" } });
        Assert.Equal(40000m, (decimal)trang.GetType().GetProperty("TienDuKienBoSung", flags).GetValue(trang));
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [Theory]
    [InlineData("3X4", 8)]
    [InlineData("2X3", 8)]
    [InlineData("35X45", 8)]
    [InlineData("4X6", 100)]
    [InlineData("5X5", 1)]
    public void PdfGhepDungSoAnhVaKichThuoc(string ma, int soAnh)
    {
        using var image = new System.Drawing.Bitmap(60, 80);
        using var stream = new MemoryStream();
        image.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
        byte[] bytes = PhotocopyApi.DichVu.AnhThePdf.Tao(stream.ToArray(), ma, soAnh);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var images = pdf.GetPages().SelectMany(p => p.GetImages()).ToList();
        Assert.Equal(soAnh, images.Count);
        Assert.All(pdf.GetPages(), p => Assert.InRange(p.Width, 595, 596));
        var qc = PhotocopyShared.QuyCachIn.QuyCachAnhThe.Lay(ma);
        Assert.All(images, i => {
            Assert.InRange(i.BoundingBox.Width, qc.RongCm * 72 / 2.54 - 0.01, qc.RongCm * 72 / 2.54 + 0.01);
            Assert.InRange(i.BoundingBox.Height, qc.CaoCm * 72 / 2.54 - 0.01, qc.CaoCm * 72 / 2.54 + 0.01);
            Assert.InRange(i.BoundingBox.Left, 28, 595);
            Assert.InRange(i.BoundingBox.Bottom, 28, 842);
        });
    }

    [Fact]
    public void MauDonTrongCoChuTiengVietVaKhoangVietTay()
    {
        byte[] bytes = PhotocopyApi.DichVu.MauDonPdf.Tao("DON_XIN_VIEC");
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        Assert.Equal(1, pdf.NumberOfPages);
        Assert.Contains("ĐƠN XIN VIỆC", pdf.GetPage(1).Text);
        Assert.Contains("Tên tôi là", pdf.GetPage(1).Text);
        string output = Environment.GetEnvironmentVariable("PHOTOCOPY_PDF_TEST_OUTPUT");
        if (output != null)
        {
            Directory.CreateDirectory(output);
            foreach (var mau in PhotocopyApi.DichVu.MauDonPdf.DanhSach)
                File.WriteAllBytes(Path.Combine(output, mau.MaMau + ".pdf"), PhotocopyApi.DichVu.MauDonPdf.Tao(mau.MaMau));
        }
    }

    [Fact]
    public void FileLuuSoAnhRiengVoiSoTrangPdf()
    {
        Assert.NotNull(typeof(FileInDto).GetProperty("SoLuongAnh"));
        Assert.NotNull(typeof(FileInDto).GetProperty("KichThuocAnh"));
        Assert.NotNull(typeof(DichVuDto).GetProperty("KichThuocAnh"));
    }

    [Theory]
    [InlineData("2X3", 2, 3)]
    [InlineData("3X4", 3, 4)]
    [InlineData("4X6", 4, 6)]
    [InlineData("35X45", 3.5, 4.5)]
    [InlineData("5X5", 5, 5)]
    public void DanhMucCoDungKichThuocCentimet(string ma, double rong, double cao)
    {
        var qc = PhotocopyShared.QuyCachIn.QuyCachAnhThe.Lay(ma);
        Assert.NotNull(qc);
        Assert.Equal(rong, qc.RongCm);
        Assert.Equal(cao, qc.CaoCm);
    }
}
