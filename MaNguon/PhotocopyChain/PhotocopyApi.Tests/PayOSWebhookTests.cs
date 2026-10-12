using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PayOS;
using PayOS.Crypto;
using PayOS.Models.Webhooks;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public class PayOSWebhookTests
{
    [Fact]
    public async Task ChuKySai_KhongDuocGhiNhanThanhToan()
    {
        string khoaThu = Guid.NewGuid().ToString("N");
        var controller = TaoController(khoaThu);
        var webhook = TaoWebhook();
        webhook.Signature = "chu-ky-khong-hop-le";
        Assert.IsType<BadRequestObjectResult>(await controller.Webhook(webhook));
    }

    [Fact]
    public async Task ChuKyDung_NhungMaDonNgoaiPhamVi_KhongTruyCapDatabase()
    {
        string khoaThu = Guid.NewGuid().ToString("N");
        var controller = TaoController(khoaThu);
        var webhook = TaoWebhook();
        webhook.Signature = new CryptoProvider().CreateSignatureFromObject(webhook.Data, khoaThu);
        Assert.IsType<OkObjectResult>(await controller.Webhook(webhook));
    }

    private static ThanhToanController TaoController(string khoaThu)
    {
        // Chỉ dùng khóa ngẫu nhiên trong test; không gọi tạo link, thu tiền hoặc endpoint payOS thật.
        return new ThanhToanController(new ConfigurationBuilder().Build(), null,
            NullLogger<ThanhToanController>.Instance,
            new PayOSClient("unit-test-client", "unit-test-api", khoaThu), null);
    }

    private static Webhook TaoWebhook() => new()
    {
        Code = "00", Description = "unit test", Success = true,
        Data = new WebhookData
        {
            OrderCode = (long)int.MaxValue + 1, Amount = 1000, Code = "00",
            Description = "unit test", Currency = "VND", Reference = "unit-test-reference",
            AccountNumber = "000000", TransactionDateTime = "2026-10-07 10:00:00",
            PaymentLinkId = "unit-test-link"
        },
        Signature = ""
    };
}
