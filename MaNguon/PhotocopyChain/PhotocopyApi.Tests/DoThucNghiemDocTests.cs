using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PhotocopyApi.Auth;
using PhotocopyApi.BaoTri;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;
using PhotocopyPrinterSimulator;
using Xunit;
namespace PhotocopyApi.Tests;
// Opt-in đọc dữ liệu thật: không tạo ca/đơn, không gọi email/payOS/in/scan.
public class DoThucNghiemDocTests
{
    private sealed class ReadOnly:FactAttribute {public ReadOnly(){if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PHOTOCOPY_READ_BENCH_ENV_FILE")))Skip="Opt-in: localhost GET và đọc queue Windows.";}}
    [ReadOnly]
    public async Task LocalHttpAndInstalledPrinterQueues()
    {
        string output=Environment.GetEnvironmentVariable("PHOTOCOPY_BENCH_OUTPUT")??throw new InvalidOperationException("Cần thư mục kết quả riêng tư.");
        var reader=typeof(PhotocopyApi.Controllers.OcrController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var values=(Dictionary<string,string>)reader.GetMethod("Doc").Invoke(null,new object[]{Environment.GetEnvironmentVariable("PHOTOCOPY_READ_BENCH_ENV_FILE")});
        string source=Environment.GetEnvironmentVariable("PHOTOCOPY_SOURCE_ROOT");
        var config=new ConfigurationBuilder().AddJsonFile(Path.Combine(source,"MaNguon","PhotocopyChain","PhotocopyApi","appsettings.json")).AddInMemoryCollection(values).Build();
        using var db=new SqlConnection(config.GetConnectionString("PhotocopyChain"));
        var user=await db.QueryFirstAsync<NguoiDung>("SELECT TOP 1 MaNguoiDung,MatKhau,VaiTro,MaChiNhanh,TrangThai FROM NguoiDung WHERE VaiTro='SUPER_ADMIN' AND TrangThai=N'HOAT_DONG' ORDER BY MaNguoiDung");
        string runtime=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PhotocopyChain");
        var stamp=new PhienDangNhapStore(Path.Combine(runtime,"account-sessions.json"),config["Jwt:Key"]);
        var maintenance=new BaoTriStore(Path.Combine(runtime,"maintenance.json"));
        Assert.False(BaoTriStore.KhoaToanBo(maintenance.Read(),"web"),"Chỉ đo khi không bảo trì, không thay đổi cấu hình.");
        var claims=new[]{new Claim(TenClaim.MaNguoiDung,user.MaNguoiDung.ToString()),new Claim(TenClaim.VaiTro,user.VaiTro),new Claim("client_platform","web"),new Claim("maintenance_version",BaoTriStore.Version(maintenance.Read(),"web").ToString()),new Claim(PhienDangNhapStore.TenClaimDauPhien,stamp.TaoDauPhien(user))};
        var jwt=new JwtSecurityToken(config["Jwt:Issuer"],config["Jwt:Audience"],claims,expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"])),SecurityAlgorithms.HmacSha256));
        using var http=new HttpClient { BaseAddress=new Uri("http://localhost:5268"),Timeout=TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",new JwtSecurityTokenHandler().WriteToken(jwt));
        var rows=new List<object>();
        foreach(string route in new[]{"/api/don-hang/hang-doi","/api/quan-tri/bao-cao/hien-tai","/api/quan-tri/bao-cao/phan-tich?tuNgay="+DateTime.Today.AddDays(-6).ToString("yyyy-MM-dd")+"&denNgay="+DateTime.Today.ToString("yyyy-MM-dd")}) {
            using(var warm=await http.GetAsync(route))Assert.True(warm.IsSuccessStatusCode,$"Warmup HTTP {(int)warm.StatusCode}");
            var times=new List<double>();int length=0;
            for(int i=0;i<20;i++) {
                var watch=Stopwatch.StartNew();using var response=await http.GetAsync(route);var bytes=await response.Content.ReadAsByteArrayAsync();watch.Stop();
                Assert.True(response.IsSuccessStatusCode,$"GET HTTP {(int)response.StatusCode}");times.Add(watch.Elapsed.TotalMilliseconds);length=bytes.Length;await Task.Delay(100);
            }
            times.Sort();rows.Add(new {route,count=times.Count,p50_ms=times[9],p95_ms=times[18],max_ms=times[^1],last_bytes=length});
        }
        var deviceWatch=Stopwatch.StartNew();object devices;
        try {devices=new PhatHienMayInWindows().Quet().Select(x=>new{x.Ten,x.Driver,x.Cong,x.LaMayAo,x.SoJob,x.TrangThaiDriver,x.KhoGiayHoTro,x.InMau,x.InHaiMat}).ToArray();}
        catch(Exception ex){devices=new {error=ex.GetType().Name,message=ex.Message};}
        deviceWatch.Stop();Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"api-and-devices.json"),JsonSerializer.Serialize(new {at=DateTimeOffset.Now,machine=Environment.MachineName,runtime=Environment.Version.ToString(),mode="20 sequential GET per route, concurrency=1, warmed connection; not a capacity/load test",http=rows,devices,discovery_ms=deviceWatch.Elapsed.TotalMilliseconds,physical_print_tested=false,physical_scan_tested=false},new JsonSerializerOptions{WriteIndented=true}));
    }
}
