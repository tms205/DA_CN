using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using Xunit;
namespace PhotocopyApi.Tests;

public class OcrControllerTests
{
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private static OcrController Create(string root) => new(new EnvironmentStub { ContentRootPath = root }, NullLogger<OcrController>.Instance)
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    [Fact]
    public async Task RejectsUnsupportedModelBeforeStartingOcr()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var file = new FormFile(stream, 0, 1, "file", "test.png");
        var result = await Create("unused").NhanDang(file, "other", CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RejectsInvalidCropBeforeStartingModel()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var file = new FormFile(stream, 0, 1, "file", "test.png");
        Assert.IsType<BadRequestObjectResult>(await Create("unused").NhanDang(file, "2b", CancellationToken.None, "[0.8,0,0.5,1]"));
    }

    [Fact]
    public void ExportReturnsEditableWordWithCorrectMime()
    {
        var result = Assert.IsType<FileContentResult>(Create("unused").XuatWord(new XuatWordRequest { Text = "Nội dung đã chỉnh sửa\nDòng mới" }));
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.ContentType);
        Assert.EndsWith(".docx", result.FileDownloadName);
        Assert.True(result.FileContents.Length > 100);
    }

    [Fact]
    public async Task LocalModelCanTranscribeWhenIntegrationSampleConfigured()
    {
        var image = Environment.GetEnvironmentVariable("PHOTOCOPY_OCR_TEST_IMAGE");
        var root = Environment.GetEnvironmentVariable("PHOTOCOPY_OCR_TEST_API_ROOT");
        if (string.IsNullOrEmpty(image) || string.IsNullOrEmpty(root)) return;
        await using var stream = File.OpenRead(image);
        var file = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(image));
        var result = await Create(root).NhanDang(file, "2b", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.False(string.IsNullOrWhiteSpace((string)ok.Value.GetType().GetProperty("text")!.GetValue(ok.Value)!));
    }
}
