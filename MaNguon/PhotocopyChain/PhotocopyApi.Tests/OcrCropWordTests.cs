using System.IO.Compression;
using System.Xml.Linq;
using PhotocopyApi.Ocr;
using Xunit;
namespace PhotocopyApi.Tests;
public class OcrCropWordTests
{
    [Theory]
    [InlineData("[0.1,0.2,0.5,0.6]", true)]
    [InlineData("[0,0,1,1]", true)]
    [InlineData("[-0.1,0,1,1]", false)]
    [InlineData("[0.9,0,0.5,1]", false)]
    [InlineData("[0,0,0,1]", false)]
    [InlineData("[0,0,1]", false)]
    [InlineData("not json", false)]
    public void ValidatesCropBounds(string json, bool valid) => Assert.Equal(valid, OcrCrop.Valid(json));

    [Fact]
    public void WordPreservesEditedVietnameseAndParagraphs()
    {
        const string text = "Nội dung đã sửa: <đúng> & rõ\r\n\r\nDòng cuối";
        var bytes = OcrWord.Create(text);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("_rels/.rels"));
        using var stream = archive.GetEntry("word/document.xml")!.Open();
        var document = XDocument.Load(stream); XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = document.Descendants(w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))).ToList();
        Assert.Equal(new[] { "Nội dung đã sửa: <đúng> & rõ", "", "Dòng cuối" }, paragraphs);
    }
    [Fact]
    public void WordRejectsOversizedText() => Assert.Throws<ArgumentException>(() => OcrWord.Create(new string('a', 100001)));
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\u0001text")]
    public void WordRejectsInvalidInput(string text) => Assert.Throws<ArgumentException>(() => OcrWord.Create(text));
}
