using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PhotocopyApi.Controllers;
using PhotocopyApi.DichVu;
using Xunit;

namespace PhotocopyApi.Tests;

// Mỗi bài có thư mục riêng, không kết nối SQL/PayOS hay dùng thư viện thật của người dùng.
public class QuanLyMauDonTests
{
    [TrinhDuyetMauDonFact]
    public async Task TrinhDuyet_QuanLyMauVaResponsive_KhongGhiDb()
    {
        await using var host = await MayChu.Tao();
        string goc = GocMaNguon();
        var start = new System.Diagnostics.ProcessStartInfo("node") {
            WorkingDirectory = goc, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        start.ArgumentList.Add(Path.Combine(goc, "verification", "quan-ly-mau-don.cjs"));
        start.Environment["PHOTOCOPY_MAU_TEST_API"] = host.Http.BaseAddress.ToString().TrimEnd('/');
        start.Environment["PHOTOCOPY_MAU_SAMPLE"] = Path.Combine(AppContext.BaseDirectory, "TaiNguyen", "MauDon", "xin-viec.pdf");
        using var process = System.Diagnostics.Process.Start(start);
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await output + "\n" + await error);
    }

    private static string GocMaNguon([System.Runtime.CompilerServices.CallerFilePath] string file = "")
        => Directory.GetParent(Path.GetDirectoryName(file)).FullName;

    [Fact]
    public async Task CeoTaiLenSuaThayAnHienVaGoMau_KhongCanDatabase()
    {
        await using var host = await MayChu.Tao();
        using var http = host.Http;
        byte[] pdf = MauDonPdf.Tao("DON_XIN_VIEC");
        var mau = await Them(http, pdf);
        string ma = mau.GetProperty("maMau").GetString();
        long phienBan = mau.GetProperty("phienBan").GetInt64();
        Assert.Equal(1, mau.GetProperty("soTrang").GetInt32());
        Assert.Contains(await DanhSach(http), x => x.GetProperty("maMau").GetString() == ma);
        Assert.Equal(pdf, await http.GetByteArrayAsync($"api/file-in/mau/{ma}/pdf"));
        Assert.True((await http.GetAsync("api/file-in/mau")).Headers.CacheControl?.NoStore);
        byte[] anhCu = await http.GetByteArrayAsync($"api/file-in/mau/{ma}/trang/1");
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, anhCu.Take(8));

        using var sua = await http.PutAsJsonAsync($"api/quan-tri/mau-don/{ma}", new { tenMau = "Mẫu đã sửa", nhom = "Loại mới", moTa = "Bản trống", phienBan });
        Assert.Equal(HttpStatusCode.OK, sua.StatusCode);
        var daSua = await sua.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Mẫu đã sửa", daSua.GetProperty("tenMau").GetString());
        using var trungPhien = await http.PutAsJsonAsync($"api/quan-tri/mau-don/{ma}", new { tenMau = "Không được ghi đè", nhom = "A", phienBan });
        Assert.Equal(HttpStatusCode.Conflict, trungPhien.StatusCode);
        phienBan = daSua.GetProperty("phienBan").GetInt64();
        string khac = MauDonPdf.DanhSach.First(x => x.MaMau != "DON_XIN_VIEC").MaMau;
        byte[] pdfMoi = MauDonPdf.Tao(khac);
        using var form = FileForm(pdfMoi, phienBan: phienBan);
        using var thay = await http.PutAsync($"api/quan-tri/mau-don/{ma}/file", form);
        Assert.Equal(HttpStatusCode.OK, thay.StatusCode);
        var daThay = await thay.Content.ReadFromJsonAsync<JsonElement>();
        phienBan = daThay.GetProperty("phienBan").GetInt64();
        Assert.Equal(pdfMoi, await http.GetByteArrayAsync($"api/file-in/mau/{ma}/pdf"));
        Assert.Equal(HttpStatusCode.Conflict, (await http.GetAsync($"api/file-in/mau/{ma}/pdf?phienBan=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsync($"api/file-in/mau/{ma}?phienBan=1", null)).StatusCode);
        Assert.NotEqual(anhCu, await http.GetByteArrayAsync($"api/file-in/mau/{ma}/trang/1"));

        using var an = await http.PutAsJsonAsync($"api/quan-tri/mau-don/{ma}/trang-thai", new { hien = false, phienBan });
        Assert.Equal(HttpStatusCode.OK, an.StatusCode);
        phienBan = (await an.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("phienBan").GetInt64();
        Assert.DoesNotContain(await DanhSach(http), x => x.GetProperty("maMau").GetString() == ma);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"api/file-in/mau/{ma}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsync($"api/file-in/mau/{ma}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync($"api/quan-tri/mau-don/{ma}/trang/1")).StatusCode);
        using var hien = await http.PutAsJsonAsync($"api/quan-tri/mau-don/{ma}/trang-thai", new { hien = true, phienBan });
        phienBan = (await hien.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("phienBan").GetInt64();
        using var xoa = await http.DeleteAsync($"api/quan-tri/mau-don/{ma}?phienBan={phienBan}");
        Assert.Equal(HttpStatusCode.NoContent, xoa.StatusCode);
        Assert.DoesNotContain(await DanhSach(http), x => x.GetProperty("maMau").GetString() == ma);
        Assert.True(Directory.EnumerateFiles(host.ThuMuc, "*.pdf", SearchOption.AllDirectories).Count() >= 20);
        await host.Restart();
        Assert.DoesNotContain(await DanhSach(host.Http), x => x.GetProperty("maMau").GetString() == ma);
        Assert.Equal(18, (await DanhSach(host.Http)).Length);
    }

    [Theory]
    [InlineData("", 401)]
    [InlineData("CUSTOMER", 403)]
    [InlineData("STAFF", 403)]
    [InlineData("BRANCH_ADMIN", 403)]
    [InlineData("SHIPPER", 403)]
    public async Task ChiCeoQuanLy(string role, int status)
    {
        await using var host = await MayChu.Tao();
        host.Http.DefaultRequestHeaders.Remove("X-Test-Role");
        if (role != "") host.Http.DefaultRequestHeaders.Add("X-Test-Role", role);
        foreach (var method in new[] { "GET", "POST", "PUT", "DELETE" })
        {
            string path = method is "PUT" or "DELETE" ? "/bat-ky" : "";
            using var request = new HttpRequestMessage(new HttpMethod(method), "api/quan-tri/mau-don" + path);
            using var res = await host.Http.SendAsync(request);
            Assert.Equal(status, (int)res.StatusCode);
        }
    }

    [Theory]
    [InlineData("", "Loại", "mau.pdf", true)]
    [InlineData("Mẫu", "  ", "mau.pdf", true)]
    [InlineData("Mẫu", "Loại", "mau.exe", true)]
    [InlineData("Mẫu", "Loại", "mau.pdf", false)]
    public async Task ChanDuLieuSai(string ten, string nhom, string file, bool pdfDung)
    {
        await using var host = await MayChu.Tao();
        using var form = FileForm(pdfDung ? MauDonPdf.Tao("DON_XIN_VIEC") : "%PDF-1.7\nFake"u8.ToArray(), file);
        form.Add(new StringContent(ten), "tenMau"); form.Add(new StringContent(nhom), "nhom");
        using var res = await host.Http.PostAsync("api/quan-tri/mau-don", form);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(18, (await DanhSach(host.Http)).Length);
    }

    [Fact]
    public async Task GoMauGocKhongTuPhucHoi_VaPdfDaLayKhongDoi()
    {
        await using var host = await MayChu.Tao();
        byte[] fileDon = await host.Http.GetByteArrayAsync("api/file-in/mau/DON_XIN_VIEC/pdf");
        string fileDocLap = Path.Combine(host.ThuMuc, "ban-sao-cua-don.pdf");
        await File.WriteAllBytesAsync(fileDocLap, fileDon);
        using var go = await host.Http.DeleteAsync("api/quan-tri/mau-don/DON_XIN_VIEC?phienBan=1");
        Assert.Equal(HttpStatusCode.NoContent, go.StatusCode);
        await host.Restart();
        Assert.Equal(17, (await DanhSach(host.Http)).Length);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Http.GetAsync("api/file-in/mau/DON_XIN_VIEC/pdf")).StatusCode);
        Assert.Equal(fileDon, await File.ReadAllBytesAsync(fileDocLap));
    }

    [Fact]
    public async Task KhongDocDuongDanNgoaiThuVien_HoacBoQuaHash()
    {
        await using var host = await MayChu.Tao();
        await DanhSach(host.Http);
        string catalog = Path.Combine(host.ThuMuc, "catalog.json");
        string goc = await File.ReadAllTextAsync(catalog);
        await File.WriteAllTextAsync(catalog, goc.Replace("xin-viec.pdf", "../../.env"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await host.Http.GetAsync("api/file-in/mau/DON_XIN_VIEC/pdf")).StatusCode);
        await File.WriteAllTextAsync(catalog, goc);
        await File.WriteAllBytesAsync(Path.Combine(host.ThuMuc, "xin-viec.pdf"), "%PDF-1.7 fake"u8.ToArray());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await host.Http.GetAsync("api/file-in/mau/DON_XIN_VIEC/pdf")).StatusCode);
    }

    [Fact]
    public void ChanQuaDungLuongQuaTrangVaPdfKhoa_ChoPhepPdfTrangTrang()
    {
        Assert.Throws<ArgumentException>(() => XemTruocMauDon.KiemTra(new byte[20 * 1024 * 1024 + 1]));
        var nhieuTrang = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        for (int i = 0; i < 51; i++) nhieuTrang.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        Assert.Throws<ArgumentException>(() => XemTruocMauDon.KiemTra(nhieuTrang.Build()));
        var trangTrang = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        trangTrang.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        Assert.Equal(1, XemTruocMauDon.KiemTra(trangTrang.Build()));
        // Một số tài sản gốc chỉ cho in, cấm sửa. Giữ nguyên seed nhưng không upload PDF khóa mới.
        foreach (var mau in MauDonPdf.DanhSach)
        {
            byte[] bytes = MauDonPdf.Tao(mau.MaMau);
            using var doc = UglyToad.PdfPig.PdfDocument.Open(bytes);
            if (doc.Structure.Trailer.EncryptionToken != null)
                Assert.Throws<ArgumentException>(() => XemTruocMauDon.KiemTra(bytes));
        }
    }

    [Fact]
    public async Task HaiMayChuKhongGhiDeDanhMuc_JsonHongKhongTuTaoLai()
    {
        await using var host = await MayChu.Tao();
        await using var host2 = await MayChu.Tao(host.ThuMuc);
        await Task.WhenAll(Them(host.Http, MauDonPdf.Tao("DON_XIN_VIEC")), Them(host2.Http, MauDonPdf.Tao("DON_XIN_VIEC")));
        Assert.Equal(20, (await DanhSach(host.Http)).Length);
        await File.WriteAllTextAsync(Path.Combine(host.ThuMuc, "catalog.json"), "{hong");
        using var res = await host.Http.GetAsync("api/quan-tri/mau-don");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        Assert.Equal("{hong", await File.ReadAllTextAsync(Path.Combine(host.ThuMuc, "catalog.json")));
    }

    private static async Task<JsonElement[]> DanhSach(HttpClient http) => await http.GetFromJsonAsync<JsonElement[]>("api/file-in/mau");
    private static async Task<JsonElement> Them(HttpClient http, byte[] pdf)
    {
        using var form = FileForm(pdf);
        form.Add(new StringContent("Đơn kiểm thử"), "tenMau"); form.Add(new StringContent("Mẫu QA"), "nhom");
        using var res = await http.PostAsync("api/quan-tri/mau-don", form);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static MultipartFormDataContent FileForm(byte[] pdf, string name = "mau.pdf", long? phienBan = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(pdf), "file", name);
        if (phienBan != null) form.Add(new StringContent(phienBan.ToString()), "phienBan");
        return form;
    }

    private sealed class MayChu : IAsyncDisposable
    {
        public string ThuMuc { get; private set; }
        public HttpClient Http { get; private set; }
        private WebApplication _app;
        private bool _soHuu;
        public static async Task<MayChu> Tao(string thuMuc = null)
        {
            var host = new MayChu { ThuMuc = thuMuc ?? Path.Combine(Path.GetTempPath(), "PhotocopyMauQA-" + Guid.NewGuid().ToString("N")), _soHuu = thuMuc == null };
            await host.Start(); return host;
        }
        private async Task Start()
        {
            var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            b.Logging.ClearProviders(); b.WebHost.UseUrls("http://127.0.0.1:0");
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string> { ["MauDon:ThuMucGoc"] = ThuMuc });
            b.Services.AddAuthentication("mau-test").AddScheme<AuthenticationSchemeOptions, MauDonHttpTests.PhienMau>("mau-test", _ => { });
            b.Services.AddAuthorization();
            b.Services.AddSingleton<ThuVienMauDon>();
            b.Services.AddControllers().AddApplicationPart(typeof(FileInController).Assembly);
            _app = b.Build(); _app.UseAuthentication(); _app.UseAuthorization(); _app.MapControllers(); await _app.StartAsync();
            var url = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
            Http = new HttpClient { BaseAddress = new Uri(url) }; Http.DefaultRequestHeaders.Add("X-Test-Role", "SUPER_ADMIN");
        }
        public async Task Restart() { Http.Dispose(); await _app.DisposeAsync(); await Start(); }
        public async ValueTask DisposeAsync()
        {
            Http.Dispose(); await _app.DisposeAsync();
            if (_soHuu && Directory.Exists(ThuMuc)) Directory.Delete(ThuMuc, true);
        }
    }
}

public sealed class TrinhDuyetMauDonFactAttribute : FactAttribute
{
    public TrinhDuyetMauDonFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PHOTOCOPY_RUN_MAU_BROWSER") != "1")
            Skip = "Cần PHOTOCOPY_RUN_MAU_BROWSER=1, Web QA, Node và Playwright; không ghi DB thật.";
    }
}
