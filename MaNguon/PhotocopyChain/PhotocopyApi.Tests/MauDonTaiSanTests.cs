using System.Security.Cryptography;
using System.Text.Json;
using PhotocopyApi.DichVu;
using Xunit;

namespace PhotocopyApi.Tests;

public class MauDonTaiSanTests
{
    [Fact]
    public void ThuVienDungPdfTaiSanVaCoNguonPhamViSoTrang()
    {
        Assert.True(MauDonPdf.DanhSach.Count >= 10);
        string folder = Path.Combine(AppContext.BaseDirectory, "TaiNguyen", "MauDon");
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "catalog.json")));
        Assert.Equal(MauDonPdf.DanhSach.Count, catalog.RootElement.GetArrayLength());
        foreach (var item in catalog.RootElement.EnumerateArray())
        {
            string id = item.GetProperty("MaMau").GetString();
            byte[] bytes = MauDonPdf.Tao(id);
            Assert.Equal(File.ReadAllBytes(Path.Combine(folder, item.GetProperty("TenFile").GetString())), bytes);
            Assert.Equal(item.GetProperty("Sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
            Assert.Equal(item.GetProperty("SoTrang").GetInt32(), pdf.NumberOfPages);
            Assert.All(pdf.GetPages(), p => Assert.False(string.IsNullOrWhiteSpace(p.Text)));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("Nguon").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("PhamVi").GetString()));
            Assert.Equal("https", new Uri(item.GetProperty("UrlNguon").GetString()).Scheme);
        }
    }

    [Theory]
    [InlineData("KHONG_CO")]
    [InlineData("../catalog.json")]
    [InlineData("../../.env")]
    public void KhongChoDocFileNgoaiDanhMuc(string id)
    {
        Assert.Throws<ArgumentException>(() => MauDonPdf.Tao(id));
    }
}
