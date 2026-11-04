using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyApi.HuyDon;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class BienLaiHoanTienTests
{
    [Theory]
    [InlineData(10,false)] [InlineData(11,true)] [InlineData(19,true)]
    public void CustomerAndCeoReasonsRequireMoreThanTenCharacters(int n,bool ok)
    {
        var request=new KhieuNaiHoanTienRequest { MoTa=new string('x',n),NganHang="VCB",SoTaiKhoan="123456",ChuTaiKhoan="Khách thử",MaBangChung=Guid.NewGuid().ToString("N") };
        var decision=new QuyetDinhHoanTienRequest { TrangThai="CHAP_THUAN",LyDo=new string('x',n) };
        Assert.Equal(ok,Validator.TryValidateObject(request,new(request),new List<ValidationResult>(),true));
        Assert.Equal(ok,Validator.TryValidateObject(decision,new(decision),new List<ValidationResult>(),true));
    }
    private static DonHangController Controller(string folder)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string> {
            ["HoanTien:ThuMucBienLai"]=folder }).Build();
        var controller=new DonHangController(config,new MemoryCache(new MemoryCacheOptions()),null);
        controller.ControllerContext=new ControllerContext { HttpContext=new DefaultHttpContext { User=new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(TenClaim.MaNguoiDung,"1"),new Claim(ClaimTypes.Role,"SUPER_ADMIN") },"test")) } };
        return controller;
    }
    [Fact]
    public async Task MissingBillOrWhitespaceReasonRejectedBeforeAnyDatabaseCall()
    {
        var c=Controller(Path.GetTempPath());
        Assert.IsType<BadRequestObjectResult>(await c.QuyetDinhKhieuNai(15,new() { TrangThai="DA_HOAN_TIEN",LyDo="Đã chuyển tiền thực tế",MaChuyenKhoan="TEST" }));
        Assert.IsType<BadRequestObjectResult>(await c.QuyetDinhKhieuNai(15,new() { TrangThai="CHAP_THUAN",LyDo="            " }));
        Assert.IsType<BadRequestObjectResult>(await c.HoanTien(15,new() { LyDo="Đã chuyển tiền thực tế",MaChuyenKhoan="TEST" }));
        Assert.IsType<BadRequestObjectResult>(await c.BoSungBienLaiHoanTien(15,new() { LyDo="Đã chuyển tiền thực tế",MaChuyenKhoan="TEST" }));
    }
    [Fact]
    public async Task BillBoundToOrderAndCeo_AndTamperingRejected()
    {
        if(!OperatingSystem.IsWindows())return;
        var folder=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"photocopy-receipt-test-"+Guid.NewGuid().ToString("N")));
        var c=Controller(folder);
        var store=(BangChungHuyStore)typeof(DonHangController).GetField("_khoBienLai",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c)!;
        try {
            using var image=new Bitmap(2,2);using var stream=new MemoryStream();image.Save(stream,ImageFormat.Png);stream.Position=0;
            var proof=await store.Luu(15,1,new[] {new FormFile(stream,0,stream.Length,"Anh","bill.png")},default);
            var method=typeof(DonHangController).GetMethod("DocBienLaiHoanTien",BindingFlags.Instance|BindingFlags.NonPublic)!;
            Task<BienLaiHoanTienDto> Read(int id,string ma)=>(Task<BienLaiHoanTienDto>)method.Invoke(c,new object[] {id,ma,"TEST-ONLY","Đã chuyển tiền thực tế"})!;
            var bill=await Read(15,proof.MaHoSo);
            Assert.Equal(proof.Anh[0].MaAnh,bill.Anh.MaAnh);Assert.Equal(1,bill.MaCeo);
            await Assert.ThrowsAsync<ArgumentException>(()=>Read(16,proof.MaHoSo));
            c.User.FindFirst(TenClaim.MaNguoiDung)!.Subject!.RemoveClaim(c.User.FindFirst(TenClaim.MaNguoiDung)!);
            ((ClaimsIdentity)c.User.Identity!).AddClaim(new(TenClaim.MaNguoiDung,"2"));
            await Assert.ThrowsAsync<ArgumentException>(()=>Read(15,proof.MaHoSo));
            ((ClaimsIdentity)c.User.Identity!).RemoveClaim(c.User.FindFirst(TenClaim.MaNguoiDung)!);
            ((ClaimsIdentity)c.User.Identity!).AddClaim(new(TenClaim.MaNguoiDung,"1"));
            await File.WriteAllTextAsync(store.Anh.DuongDan(15,proof.Anh[0]),"tampered");
            await Assert.ThrowsAsync<ArgumentException>(()=>Read(15,proof.MaHoSo));
        } finally { if(Directory.Exists(folder))Directory.Delete(folder,true); }
    }
    [Fact]
    public void ImageEndpointsArePrivateAndOldRecordsRemainReadable()
    {
        var write=typeof(DonHangController).GetMethod(nameof(DonHangController.TaiBienLaiHoanTien))!;
        Assert.Equal("SUPER_ADMIN",write.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("SUPER_ADMIN",typeof(DonHangController).GetMethod(nameof(DonHangController.BoSungBienLaiHoanTien))!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        var read=typeof(DonHangController).GetMethod(nameof(DonHangController.XemBienLaiHoanTien))!;
        Assert.Equal("CUSTOMER,SUPER_ADMIN",read.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Null(JsonSerializer.Deserialize<QuyetDinhHoanTienDto>("{\"TrangThai\":\"DA_HOAN_TIEN\"}")!.BienLai);
        Assert.Null(JsonSerializer.Deserialize<HoSoHuyDon>("{\"Loai\":\"HUY_TRUOC_TIEP_NHAN\"}")!.BienLaiHoanTien);
    }
}
