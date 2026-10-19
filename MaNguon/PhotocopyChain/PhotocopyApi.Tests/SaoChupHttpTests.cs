using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotocopyApi.Controllers;
using UglyToad.PdfPig;
using Xunit;

namespace PhotocopyApi.Tests;

// HTTP thật với ảnh tổng hợp, không đọc .env, không nối DB và không tạo lệnh in.
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class SaoChupHttpTests
{
    [Fact]
    public async Task MayQuet_ChiNhanSuDuocQuetVaXemAnhMinhHoa()
    {
        await using var mayChu = await MayChu.Tao();
        foreach (string role in new[] { "", "CUSTOMER", "SHIPPER", "STAFF", "BRANCH_ADMIN", "SUPER_ADMIN" })
        {
            mayChu.DoiVaiTro(role);
            var status = role == "" ? HttpStatusCode.Unauthorized : role is "CUSTOMER" or "SHIPPER" ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
            using var form = FormQuet(Anh(Color.Red));
            using var quet = await mayChu.Http.PostAsync("api/may-quet/quet", form);
            Assert.Equal(status, quet.StatusCode);
            using var mau = await mayChu.Http.GetAsync("api/may-quet/anh-minh-hoa/TRUOC");
            Assert.Equal(status, mau.StatusCode);
            if (status == HttpStatusCode.OK)
                Assert.Equal("no-store", mau.Headers.CacheControl?.ToString());
            else
            {
                using var ghepForm = FormGhep(Anh(Color.Red), Anh(Color.Blue));
                using var ghep = await mayChu.Http.PostAsync("api/may-quet/ghep", ghepForm);
                Assert.Equal(status, ghep.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Quet_XoayVaCatDungKichThuoc_BoMetadata()
    {
        await using var mayChu = await MayChu.Tao();
        using var form = FormQuet(Anh(Color.Red, 400, 200));
        form.Add(new StringContent("90"), "gocXoay");
        form.Add(new StringContent("50"), "catRong");
        using var res = await mayChu.Http.PostAsync("api/may-quet/quet", form);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var kq = await res.Content.ReadFromJsonAsync<AnhNhan>();
        Assert.Equal("AO", kq.CheDo);
        // Cắt trước, xoay sau: 200×200.
        Assert.Equal(200, kq.RongPixel);
        Assert.Equal(200, kq.CaoPixel);
        using var stream = new MemoryStream(Convert.FromBase64String(kq.AnhBase64));
        using var image = Image.FromStream(stream);
        Assert.Equal(ImageFormat.Jpeg.Guid, image.RawFormat.Guid);
        Assert.DoesNotContain(0x112, image.PropertyIdList);
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("gocXoay", "45")]
    [InlineData("catTrai", "-1")]
    [InlineData("catTrai", "100")]
    [InlineData("catRong", "0")]
    [InlineData("catCao", "101")]
    [InlineData("catRong", "NaN")]
    public async Task Quet_ChanQuyCachSai(string ten, string giaTri)
    {
        await using var mayChu = await MayChu.Tao();
        using var form = FormQuet(Anh(Color.Red));
        form.Add(new StringContent(giaTri), ten);
        using var res = await mayChu.Http.PostAsync("api/may-quet/quet", form);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Theory]
    [InlineData("gia.jpg")]
    [InlineData("anh.svg")]
    public async Task Quet_ChanNoiDungKhongPhaiAnh(string ten)
    {
        await using var mayChu = await MayChu.Tao();
        using var form = FormQuet("khong-phai-anh"u8.ToArray(), ten);
        using var res = await mayChu.Http.PostAsync("api/may-quet/quet", form);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Quet_ChanVungCatVuotBienVaAnhQuaLon()
    {
        await using var mayChu = await MayChu.Tao();
        using var cat = FormQuet(Anh(Color.Red));
        cat.Add(new StringContent("50"), "catTrai");
        cat.Add(new StringContent("60"), "catRong");
        using var res = await mayChu.Http.PostAsync("api/may-quet/quet", cat);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        using var lon = FormQuet(new byte[10 * 1024 * 1024 + 1]);
        using var quaLon = await mayChu.Http.PostAsync("api/may-quet/quet", lon);
        Assert.Equal(HttpStatusCode.BadRequest, quaLon.StatusCode);
    }

    [Fact]
    public async Task Ghep_HaiMatCungMotTrangA4_VaPreviewCungBoCuc()
    {
        await using var mayChu = await MayChu.Tao();
        using var form = FormGhep(Anh(Color.Red), Anh(Color.Blue));
        using var res = await mayChu.Http.PostAsync("api/may-quet/ghep", form);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var kq = await res.Content.ReadFromJsonAsync<BanGhepNhan>();
        Assert.Equal(1, kq.SoTrang);
        using var pdf = PdfDocument.Open(Convert.FromBase64String(kq.PdfBase64));
        Assert.Equal(1, pdf.NumberOfPages);
        var page = pdf.GetPage(1);
        Assert.InRange(page.Width, 595, 596);
        Assert.InRange(page.Height, 841, 843);
        var images = page.GetImages().ToList();
        Assert.Equal(2, images.Count);
        Assert.All(images, i => {
            Assert.InRange(i.BoundingBox.Width, 86 * 72 / 25.4 - .01, 86 * 72 / 25.4 + .01);
            Assert.InRange(i.BoundingBox.Height, 54 * 72 / 25.4 - .01, 54 * 72 / 25.4 + .01);
        });
        Assert.True(images[0].BoundingBox.Bottom > images[1].BoundingBox.Top);
        using var stream = new MemoryStream(Convert.FromBase64String(kq.AnhXemTruocBase64));
        using var preview = new Bitmap(stream);
        Assert.InRange((double)preview.Width / preview.Height, .70, .71);
        Assert.True(preview.GetPixel(preview.Width / 2, (int)(preview.Height * 57.0 / 297)).R > 200);
        Assert.True(preview.GetPixel(preview.Width / 2, (int)(preview.Height * 123.0 / 297)).B > 200);
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Ghep_ThieuMotMatKhongTraPdf()
    {
        await using var mayChu = await MayChu.Tao();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Anh(Color.Red)), "matTruoc", "truoc.png");
        using var res = await mayChu.Http.PostAsync("api/may-quet/ghep", form);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.DoesNotContain("pdfBase64", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HopDongNguonQuet_KhongBatConnectorPhaiGuiAnhMoPhong()
    {
        // Chỉ kiểm tra hợp đồng adapter, KHÔNG phải kiểm thử WIA hay máy thật.
        var nguon = new NguonAnhKiemThu();
        await using var mayChu = await MayChu.Tao(nguon);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("0"), "gocXoay");
        using var res = await mayChu.Http.PostAsync("api/may-quet/quet", form);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        Assert.Null(nguon.LenhDaNhan.AnhMoPhong);
        var kq = await res.Content.ReadFromJsonAsync<AnhNhan>();
        Assert.Equal(430, kq.RongPixel);
    }

    [Fact]
    public async Task MayQuetAo_VanBatBuocCoAnhNguon()
    {
        await using var mayChu = await MayChu.Tao();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("0"), "gocXoay");
        using var res = await mayChu.Http.PostAsync("api/may-quet/quet", form);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [TrinhDuyetScanFact]
    public async Task TrinhDuyet_QuetLoiKhoiPhucXemTruocVaLapDon_KhongGhiDb()
    {
        await using var mayChu = await MayChu.Tao();
        string thuMuc = GocMaNguon();
        var cauHinh = new System.Diagnostics.ProcessStartInfo("node") {
            WorkingDirectory = thuMuc, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        cauHinh.ArgumentList.Add(Path.Combine(thuMuc, "verification", "may-quet-ao.cjs"));
        cauHinh.Environment["PHOTOCOPY_SCAN_TEST_API"] = mayChu.Http.BaseAddress.ToString().TrimEnd('/');
        using var tienTrinh = System.Diagnostics.Process.Start(cauHinh);
        var output = tienTrinh.StandardOutput.ReadToEndAsync();
        var error = tienTrinh.StandardError.ReadToEndAsync();
        using var cho = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        try { await tienTrinh.WaitForExitAsync(cho.Token); }
        catch (OperationCanceledException) { tienTrinh.Kill(true); throw; }
        Assert.True(tienTrinh.ExitCode == 0, await output + "\n" + await error);
    }

    private static string GocMaNguon([System.Runtime.CompilerServices.CallerFilePath] string file = "")
        => Directory.GetParent(Path.GetDirectoryName(file)).FullName;

    internal static byte[] Anh(Color mau, int rong = 430, int cao = 270)
    {
        using var image = new Bitmap(rong, cao);
        using (var g = Graphics.FromImage(image)) g.Clear(mau);
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static MultipartFormDataContent FormQuet(byte[] anh, string ten = "the.png")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(anh), "file", ten);
        return form;
    }

    private static MultipartFormDataContent FormGhep(byte[] truoc, byte[] sau)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(truoc), "matTruoc", "truoc.png");
        form.Add(new ByteArrayContent(sau), "matSau", "sau.png");
        return form;
    }

    private sealed class AnhNhan
    {
        public string CheDo { get; set; }
        public string AnhBase64 { get; set; }
        public int RongPixel { get; set; }
        public int CaoPixel { get; set; }
    }
    private sealed class BanGhepNhan
    {
        public string PdfBase64 { get; set; }
        public string AnhXemTruocBase64 { get; set; }
        public int SoTrang { get; set; }
    }

    private sealed class MayChu : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public HttpClient Http { get; }
        private MayChu(WebApplication app, HttpClient http) { _app = app; Http = http; }
        public static async Task<MayChu> Tao(PhotocopyApi.MayQuet.IMayQuet nguon = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddAuthentication("scan-test").AddScheme<AuthenticationSchemeOptions, PhienQuet>("scan-test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<PhotocopyApi.MayQuet.IMayQuet>(nguon ?? new PhotocopyApi.MayQuet.MayQuetAo());
            builder.Services.AddControllers().AddApplicationPart(typeof(FileInController).Assembly);
            var app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
            var may = new MayChu(app, new HttpClient { BaseAddress = new Uri(url) });
            may.DoiVaiTro("STAFF");
            return may;
        }
        public void DoiVaiTro(string role)
        {
            Http.DefaultRequestHeaders.Remove("X-Test-Role");
            if (role != "") Http.DefaultRequestHeaders.Add("X-Test-Role", role);
        }
        public async ValueTask DisposeAsync() { Http.Dispose(); await _app.StopAsync(); await _app.DisposeAsync(); }
    }

    private sealed class NguonAnhKiemThu : PhotocopyApi.MayQuet.IMayQuet
    {
        public PhotocopyApi.MayQuet.LenhQuet LenhDaNhan { get; private set; }
        public Task<PhotocopyShared.Dtos.DatIn.AnhQuetDto> QuetAsync(PhotocopyApi.MayQuet.LenhQuet lenh, CancellationToken dung)
        {
            LenhDaNhan = lenh;
            return Task.FromResult(PhotocopyApi.MayQuet.AnhQuetXuLy.ChuanHoa(Anh(Color.Red), lenh.QuyCach));
        }
    }

    public sealed class PhienQuet : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public PhienQuet(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string role = Request.Headers["X-Test-Role"];
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name)));
        }
    }
}

public sealed class TrinhDuyetScanFactAttribute : FactAttribute
{
    public TrinhDuyetScanFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PHOTOCOPY_RUN_SCAN_BROWSER") != "1")
            Skip = "Cần PHOTOCOPY_RUN_SCAN_BROWSER=1, Web đang chạy, Node và Playwright để kiểm thử UI không ghi DB.";
    }
}
