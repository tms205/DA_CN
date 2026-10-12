using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyApi.MayInAo;
using PhotocopyShared.Dtos.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public class MayInAoControllerTests
{
    [Fact]
    public async Task CallbackSaiMaLenh_KhongDuocChamVaoDatabase()
    {
        var (controller, store, id) = TaoController();
        store.NhanLenh(1, 6, id);
        var ketQua = await controller.HoanTat(6, new BaoCaoMayInRequest { MaMayIn = 1, MaLenh = "sai-token" });
        Assert.Equal(403, Assert.IsType<ObjectResult>(ketQua).StatusCode);
    }

    [Fact]
    public async Task ConnectorKhacHoiLenhDangGiu_Nhan409()
    {
        var (controller, store, _) = TaoController();
        store.NhanLenh(1, 6, Guid.NewGuid().ToString("N"));
        Assert.IsType<ConflictObjectResult>(await controller.LenhTiepTheo(1));
    }

    [Fact]
    public void BaoLoiDon0_BiTuChoi()
    {
        var (controller, _, _) = TaoController();
        Assert.Equal(403, Assert.IsType<ObjectResult>(controller.BaoLoi(0, new BaoCaoMayInRequest { MaMayIn = 1 })).StatusCode);
    }

    [Fact]
    public async Task KhoaConnectorSai_Nhan401()
    {
        var (controller, _, _) = TaoController();
        controller.Request.Headers["X-Printer-Api-Key"] = "khong-hop-le";
        Assert.IsType<UnauthorizedObjectResult>(await controller.LenhTiepTheo(1));
    }

    private static (MayInAoController, QuyenNhanLenhInStore, string) TaoController()
    {
        // Khóa chỉ dành cho unit test, không phải secret của kênh thật.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        { ["PrinterConnector:ApiKey"] = "test-only-connector-key" }).Build();
        var store = new QuyenNhanLenhInStore(Path.Combine(Path.GetTempPath(), "photocopy-claim-" + Guid.NewGuid()));
        var controller = new MayInAoController(config, new TrangThaiMayInAoStore(), null,
            NullLogger<MayInAoController>.Instance, store);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        string id = Guid.NewGuid().ToString("N");
        controller.Request.Headers["X-Printer-Api-Key"] = "test-only-connector-key";
        controller.Request.Headers["X-Printer-Connector-Id"] = id;
        return (controller, store, id);
    }
}
