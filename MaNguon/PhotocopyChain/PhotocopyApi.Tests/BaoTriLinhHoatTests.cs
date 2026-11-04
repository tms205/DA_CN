using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.BaoTri;
using PhotocopyApi.Controllers;
using Xunit;
namespace PhotocopyApi.Tests;

public class BaoTriLinhHoatTests
{
 [Theory]
 [InlineData("/api/thanh-toan/webhook")]
 [InlineData("/api/thanh-toan/payos/ket-qua")]
 public async Task BaoTriThanhToanRiengVanNhanCallbackDangCo(string path) {
  string dir=Path.Combine(Path.GetTempPath(),"PhotocopyMaintenance-"+Guid.NewGuid().ToString("N"));
  try{var store=new BaoTriStore(Path.Combine(dir,"state.json"));store.Set(true,"Thanh toán Web",new(false,false,new[]{"THANH_TOAN"},Array.Empty<string>(),1));
   var ctx=new DefaultHttpContext();ctx.Request.Path=path;ctx.Request.Method=path.EndsWith("webhook")?"POST":"GET";ctx.Response.Body=new MemoryStream();bool called=false;
   await new BaoTriMiddleware(_=>{called=true;return Task.CompletedTask;}).InvokeAsync(ctx,store);Assert.True(called);
  }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 [Fact] public async Task BaoTriRiengOCR_KhongDungJobDonHang_NhungKhoaDonCaHaiKenhThiDungXepIn() {
  var ocr=new TrangThaiBaoTri(true,"OCR",KhoaWeb:false,KhoaApp:false,KhoaTinhNangWeb:new[]{"OCR"});
  Assert.False(BaoTriStore.KhoaTacVuNen(ocr,"DON_HANG"));Assert.False(BaoTriStore.KhoaTacVuNen(ocr));
  string dir=Path.Combine(Path.GetTempPath(),"PhotocopyMaintenance-"+Guid.NewGuid().ToString("N"));
  try{var store=new BaoTriStore(Path.Combine(dir,"state.json"));store.Set(true,"Đơn hàng",new(false,false,new[]{"DON_HANG"},new[]{"DON_HANG"},1));
   Assert.True(BaoTriStore.KhoaTacVuNen(store.Read(),"DON_HANG"));
   var service=new PhotocopyApi.MayInAo.TuDongInService(new ConfigurationBuilder().Build(),null,Microsoft.Extensions.Logging.Abstractions.NullLogger<PhotocopyApi.MayInAo.TuDongInService>.Instance,null,baoTri:store);
   var result=await service.XepInSauThanhToan(1);Assert.False(result.DaBatDauIn);Assert.Contains("bảo trì",result.Message);
  }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 private sealed class Clock:TimeProvider {public DateTimeOffset Now=new(2026,11,1,4,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
 [Fact] public void HanTheoGioServer_TuMoLaiSauRestart_KhongKhoiPhucTokenCu() {
  string dir=Path.Combine(Path.GetTempPath(),"PhotocopyMaintenance-"+Guid.NewGuid().ToString("N"));
  try {var clock=new Clock();var store=new BaoTriStore(Path.Combine(dir,"state.json"),clock);
   var state=store.Set(true,"Đang nâng cấp",new(true,false,SoGio:2));
   Assert.Equal(clock.Now.AddHours(2),state.KetThucLuc);Assert.Contains("01/11/2026 13:00",BaoTriStore.ThongBao(state));
   Assert.Equal(1,BaoTriStore.Version(state,"web"));Assert.Equal(0,BaoTriStore.Version(state,"app"));
   clock.Now=clock.Now.AddHours(2);state=new BaoTriStore(Path.Combine(dir,"state.json"),clock).Read();
   Assert.False(state.DangBaoTri);Assert.False(BaoTriStore.SessionValid(0,BaoTriStore.Version(state,"web"),false));
  }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 [Fact] public void KhoaChucNang_KhongDangXuat_CauHinhSaiBiChan() {
  string dir=Path.Combine(Path.GetTempPath(),"PhotocopyMaintenance-"+Guid.NewGuid().ToString("N"));
  try {var store=new BaoTriStore(Path.Combine(dir,"state.json"));var state=store.Set(true,"OCR đang nâng cấp",new(false,false,new[]{"OCR"},Array.Empty<string>(),1));
   Assert.False(BaoTriStore.KhoaToanBo(state,"web"));Assert.Equal(0,BaoTriStore.Version(state,"web"));Assert.Empty(BaoTriStore.ChucNang(state,"app"));
   Assert.Throws<ArgumentException>(()=>store.Set(true,"Sai",new(false,false,new[]{"FAKE"})));
   Assert.Throws<ArgumentException>(()=>store.Set(true,"Sai",new(SoGio:double.NaN)));
  }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 [Theory]
 [InlineData("web","/api/ocr/nhan-dang",false)]
 [InlineData("web","/api/don-hang",true)]
 [InlineData("app","/api/ocr/nhan-dang",true)]
 public async Task BackendKiemChucNangTheoPlatformTrongToken_KhongTinHeader(string platform,string path,bool allow) {
  string dir=Path.Combine(Path.GetTempPath(),"PhotocopyMaintenance-"+Guid.NewGuid().ToString("N"));
  try {var store=new BaoTriStore(Path.Combine(dir,"state.json"));store.Set(true,"Nâng cấp OCR",new(false,false,new[]{"OCR"},Array.Empty<string>(),1));
   var ctx=new DefaultHttpContext();ctx.Request.Path=path;ctx.Request.Headers["X-Photocopy-Platform"]="app";ctx.Response.Body=new MemoryStream();
   ctx.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("client_platform",platform)},"test"));bool called=false;
   await new BaoTriMiddleware(_=>{called=true;return Task.CompletedTask;}).InvokeAsync(ctx,store);Assert.Equal(allow,called);
  }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 [Fact] public async Task BaoCao_ChanTrangThaiNgoaiWhitelistTruocDatabase() {
  var api=new QuanTriController(new ConfigurationBuilder().Build());
  Assert.IsType<BadRequestObjectResult>(await api.BaoCaoDoanhThu(null,null,"ngay","DA_HUY'; DROP TABLE DonHang--"));
 }
}
