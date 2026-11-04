using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotocopyAdmin.Components;
using PhotocopyShared.Dtos.DonHang;
using Xunit;
namespace PhotocopyApi.Tests;
public class BangHangDoiComponentTests
{
    [Fact]
    public async Task ControlledTablePreservesSelectionPriorityStatusAndKeyboardAccess()
    {
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var rows=new[]{new HangDoiDonDto {MaDonHang=123,MaDonHangHienThi="DH00123",TenKhachHang="Khách thử",TrangThaiDonHang="DA_IN",TrangThaiThanhToan="DA_THANH_TOAN",PhuongThucThanhToan="COD",HinhThucNhan="TAI_QUAY",InHoaToc=true,QuyCach="A4 · 2 mặt",NgayDat=DateTime.Today,SoFile=1,TongSoTrang=2}};
        var parameters=ParameterView.FromDictionary(new Dictionary<string,object> {
            ["Don"]=rows,["MaDonDangChon"]=123,["MauNhan"]=(Func<string,string>)(_=>"nhan-xanh"),
            ["TenPhuongThuc"]=(Func<string,string>)(_=>"COD · trả khi nhận")
        });
        string raw=await renderer.Dispatcher.InvokeAsync(async()=>(await renderer.RenderComponentAsync<BangHangDoiDon>(parameters)).ToHtmlString());
        string html=WebUtility.HtmlDecode(raw);
        Assert.Contains("dong-chon",html);Assert.Contains("Hỏa tốc",html);Assert.Contains("Đã thanh toán",html);Assert.Contains("Đã in xong",html);
        Assert.Contains("tabindex=\"0\"",html);Assert.Contains("Mở chi tiết DH00123",html);
        Assert.Contains("data-nhan=\"Thanh toán\"",html);
        string output=Environment.GetEnvironmentVariable("PHOTOCOPY_UI_FIXTURE");
        if(!string.IsNullOrEmpty(output)){
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output,"hang-doi-component.html"),
                "<!doctype html><html lang=\"vi\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><link rel=\"stylesheet\" href=\"http://localhost:5064/css/app.css\"><body><main class=\"than-trang\"><div class=\"the the-bang\">"+raw+"</div></main></body></html>");
        }
    }
}
