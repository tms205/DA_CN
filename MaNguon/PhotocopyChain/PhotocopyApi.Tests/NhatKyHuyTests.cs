using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.HuyDon;
using Xunit;
namespace PhotocopyApi.Tests;
public class NhatKyHuyTests
{
 [Fact] public async Task YeuCauBiChanVanGhiNhatKy_KhongLuuToken_VaTraDungNoiDung() {
  string dir=Path.Combine(Path.GetTempPath(),"CancelAuditTest-"+Guid.NewGuid().ToString("N"));
  try {var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string>{{"HuyDon:ThuMucNhatKy",dir}}).Build();
   var ctx=new DefaultHttpContext();ctx.Request.Path="/api/don-hang/17/huy";ctx.Request.Method="POST";ctx.Request.Headers.Authorization="Bearer SECRET-DO-NOT-LOG";ctx.Response.Body=new MemoryStream();
   await new NhatKyHuyMiddleware(async c=>{c.Response.StatusCode=401;await c.Response.WriteAsJsonAsync(new{message="Phiên không hợp lệ"});}).InvokeAsync(ctx,config,NullLogger<NhatKyHuyMiddleware>.Instance);
   string text=await File.ReadAllTextAsync(Directory.GetFiles(dir,"*.json").Single());Assert.DoesNotContain("SECRET",text);
   using var json=JsonDocument.Parse(text);Assert.Equal(17,json.RootElement.GetProperty("MaDonHang").GetInt32());Assert.Equal(401,json.RootElement.GetProperty("MaHttp").GetInt32());
   ctx.Response.Body.Position=0;using var body=await JsonDocument.ParseAsync(ctx.Response.Body);Assert.Equal("Phiên không hợp lệ",body.RootElement.GetProperty("message").GetString());
  }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 [Fact] public async Task KhongGhiDuocNhatKy_ThiKhongGoiLenhHuy() {
  string file=Path.Combine(Path.GetTempPath(),"CancelAuditTest-"+Guid.NewGuid().ToString("N"));
  try {await File.WriteAllTextAsync(file,"fixture");var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string>{{"HuyDon:ThuMucNhatKy",file}}).Build();
   bool called=false;var ctx=new DefaultHttpContext();ctx.Request.Path="/api/don-hang/17/huy";ctx.Request.Method="POST";ctx.Response.Body=new MemoryStream();
   await new NhatKyHuyMiddleware(_=>{called=true;return Task.CompletedTask;}).InvokeAsync(ctx,config,NullLogger<NhatKyHuyMiddleware>.Instance);
   Assert.False(called);Assert.Equal(503,ctx.Response.StatusCode);
  }finally{File.Delete(file);}
 }
}
