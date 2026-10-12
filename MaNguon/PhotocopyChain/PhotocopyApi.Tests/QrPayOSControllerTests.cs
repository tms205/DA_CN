using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PayOS;
using PayOS.Crypto;
using PayOS.Models.V2.PaymentRequests;
using PhotocopyApi.Controllers;
using PhotocopyApi.ThanhToan;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

// Chạy SDK payOS thật với HTTP giả; không gọi payOS, SQL hoặc chuyển tiền thật.
public sealed class QrPayOSControllerTests : IDisposable
{
    private const string KhoaThu = "checksum-chi-dung-trong-test";
    private readonly string _thuMuc = Path.Combine(Path.GetTempPath(), "QrControllerTest-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task TaoMoiVaMoLaiSauRestart_CungQrVaKhongTaoLinkLap()
    {
        using var mayChu = new PayOSGia();
        using var http = new HttpClient(mayChu);
        var client = new PayOSClient(new PayOSOptions
        {
            ClientId = "test-client", ApiKey = "test-api", ChecksumKey = KhoaThu,
            HttpClient = http, MaxRetries = 0
        });
        var tt = new ThanhToanDto
        {
            MaDonHang = 15, SoTien = 7400, HanThanhToan = DateTime.Now.AddMinutes(10)
        };

        var qrMoi = await LayQr(client, tt);
        var qrMoLai = await LayQr(client, tt); // Controller/store mới, chỉ dùng file đã lưu.
        Assert.Equal("qr-payOS-test", qrMoi.QrCode);
        Assert.Equal(qrMoi.QrCode, qrMoLai.QrCode);
        Assert.Equal(qrMoi.SoTaiKhoan, qrMoLai.SoTaiKhoan);
        Assert.Equal(1, mayChu.SoLanTao);
        Assert.Equal(2, mayChu.SoLanDoc);
    }

    [Fact]
    public async Task LinkCuKhongCoCache_TraCheckoutKhongTuTaoQr()
    {
        using var mayChu = new PayOSGia { DaCoLink = true };
        using var http = new HttpClient(mayChu);
        var client = new PayOSClient(new PayOSOptions
        {
            ClientId = "test-client", ApiKey = "test-api", ChecksumKey = KhoaThu,
            HttpClient = http, MaxRetries = 0
        });
        var data = await LayQr(client, new ThanhToanDto
        {
            MaDonHang = 15, SoTien = 7400, HanThanhToan = DateTime.Now.AddMinutes(10)
        });
        Assert.Equal("", data.QrCode);
        Assert.Equal("https://pay.payos.vn/web/test-link", data.CheckoutUrl);
        Assert.Equal(0, mayChu.SoLanTao);
    }

    private Task<LienKetPayOSDto> LayQr(PayOSClient client, ThanhToanDto tt)
    {
        var controller = new ThanhToanController(new ConfigurationBuilder().Build(),
            null, NullLogger<ThanhToanController>.Instance, client, null, new DuLieuQrPayOSStore(_thuMuc));
        var ham = typeof(ThanhToanController).GetMethod("TaoHoacLayLienKetPayOS",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return (Task<LienKetPayOSDto>)ham.Invoke(controller, new object[] { tt, "https://test.example", "mobile" });
    }

    private sealed class PayOSGia : HttpMessageHandler
    {
        public bool DaCoLink;
        public int SoLanTao;
        public int SoLanDoc;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            if (req.Method == HttpMethod.Get)
            {
                SoLanDoc++;
                Assert.EndsWith("/v2/payment-requests/15", req.RequestUri.AbsolutePath);
                if (!DaCoLink) return Json(new { code = "101", desc = "Chưa có link" });
                return Json(new
                {
                    code = "00", desc = "OK",
                    data = new { id = "test-link", orderCode = 15, amount = 7400,
                        amountPaid = 0, amountRemaining = 7400, status = "PENDING", createdAt = "2026-10-12" }
                });
            }
            Assert.Equal(HttpMethod.Post, req.Method);
            SoLanTao++;
            using var body = JsonDocument.Parse(await req.Content.ReadAsStringAsync(token));
            Assert.Equal(15, body.RootElement.GetProperty("orderCode").GetInt32());
            Assert.Equal(7400, body.RootElement.GetProperty("amount").GetInt32());
            Assert.Equal("DH00015", body.RootElement.GetProperty("description").GetString());
            var data = new CreatePaymentLinkResponse
            {
                Bin = "970422", AccountNumber = "0000000000", AccountName = "TEST", Amount = 7400,
                Description = "DH00015", OrderCode = 15, Currency = "VND", PaymentLinkId = "test-link",
                Status = PaymentLinkStatus.Pending, CheckoutUrl = "https://pay.payos.vn/web/test-link", QrCode = "qr-payOS-test"
            };
            DaCoLink = true;
            return Json(new { code = "00", desc = "OK", data,
                signature = new CryptoProvider().CreateSignatureFromObject(data, KhoaThu) });
        }

        private static HttpResponseMessage Json(object data) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_thuMuc)) Directory.Delete(_thuMuc, recursive: true);
    }
}
