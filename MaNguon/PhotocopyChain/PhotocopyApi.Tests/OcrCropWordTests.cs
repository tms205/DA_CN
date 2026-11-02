using System.IO.Compression;
using System.Xml.Linq;
using PhotocopyApi.Ocr;
using Xunit;
namespace PhotocopyApi.Tests;
public class OcrCropWordTests
{
    [Fact]
    public void MultipleImagesStartOnSeparatePagesAndPreserveOrder()
    {
        var bytes = OcrWord.CreatePages(new[] { "Ảnh đầu\nDòng hai", "Ảnh thứ hai", "Ảnh cuối" });
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var xml = XDocument.Load(stream); XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = xml.Descendants(w + "p").ToArray();
        Assert.Equal(new[] { "Ảnh đầu", "Dòng hai", "Ảnh thứ hai", "Ảnh cuối" }, paragraphs.Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))));
        Assert.Empty(paragraphs[0].Descendants(w + "pageBreakBefore"));
        Assert.Empty(paragraphs[1].Descendants(w + "pageBreakBefore"));
        Assert.Single(paragraphs[2].Descendants(w + "pageBreakBefore"));
        Assert.Single(paragraphs[3].Descendants(w + "pageBreakBefore"));
        Assert.Equal(2, xml.Descendants(w + "pageBreakBefore").Count());
    }
    [Fact]
    public void ExportRejectsMissingPagesAndOversizedCombinedText()
    {
        Assert.Throws<ArgumentException>(() => OcrWord.CreatePages(new[] { "Đã xong", " " }));
        Assert.Throws<ArgumentException>(() => OcrWord.CreatePages(Enumerable.Repeat("x", 11).ToArray()));
        Assert.Throws<ArgumentException>(() => OcrWord.CreatePages(new[] { new string('a', 60000), new string('b', 60000) }));
    }
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
