using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.JSInterop;
using PhotocopyApi.Ocr;
using Xunit;
namespace PhotocopyApi.Tests;
public class OcrExportWebTests
{
    [Fact]
    public async Task WordUsesCurrentEditedText()
    {
        const string edited = "Nội dung đã sửa\\nDòng thứ hai";
        using var http = new HttpClient(new Handler(edited)) { BaseAddress = new Uri("http://fixture.local/") };
        var module = new Module(); var page = new PhotocopyAdmin.Pages.NhanDangChu(); var type = page.GetType();
        type.GetProperty("Http", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, http);
        var images = (List<PhotocopyAdmin.Pages.NhanDangChu.AnhOcr>)type.GetField("_ds", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
        images.Add(new() { Name = "anh-1.jpg", Text = edited });
        images.Add(new() { Name = "anh-2.jpg", Text = "Trang thứ hai đã sửa" });
        type.GetField("_congCu", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, module);
        await (Task)type.GetMethod("XuatWordAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
        Assert.True(module.Downloaded);
        Assert.Null(type.GetField("_loi", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page));
    }
    private sealed class Handler(string expected) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.EndsWith("/api/ocr/xuat-word", request.RequestUri!.ToString());
            var json = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(new[] { expected, "Trang thứ hai đã sửa" }, json.GetProperty("pages").EnumerateArray().Select(x => x.GetString()));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(OcrWord.Create(expected)) };
        }
    }
    private sealed class Module : IJSObjectReference
    {
        public bool Downloaded;
        public ValueTask<T> InvokeAsync<T>(string id, object[] args)
        { Assert.Equal("downloadWord", id); Assert.IsType<DotNetStreamReference>(args[0]); Downloaded = true; return new(default(T)); }
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object[] args) => InvokeAsync<T>(id,args);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
