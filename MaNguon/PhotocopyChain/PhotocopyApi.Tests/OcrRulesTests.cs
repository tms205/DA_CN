using PhotocopyApi.Ocr;
using Xunit;
namespace PhotocopyApi.Tests;
public class OcrRulesTests
{
    [Theory]
    [InlineData("2b", "anh.jpg", 100, true)]
    [InlineData("4b", "anh.png", 100, true)]
    [InlineData("other", "anh.jpg", 100, false)]
    [InlineData("2b", "anh.pdf", 100, false)]
    [InlineData("2b", "anh.jpg", 0, false)]
    [InlineData("2b", "anh.jpg", 10485761, false)]
    public void ValidateModelAndFile(string model, string filename, long length, bool valid)
        => Assert.Equal(valid, OcrRules.Valid(model, filename, length));
}
