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
using PhotocopyApi.Controllers;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;

// Opt-in sau migration: chỉ GET API và SELECT. Không đăng nhập/mở ca, tạo đơn, in hoặc chuyển tiền.
public class HoanTienHaiPhutLiveTests
{
    private sealed class LiveHaiPhutAttribute : FactAttribute
    {
        public LiveHaiPhutAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PHOTOCOPY_2MIN_LIVE_ENV_FILE")))
                Skip = "Chỉ bật sau khi được phép áp dụng migration/nạp API; kiểm tra đọc thực tế.";
        }
    }

    [LiveHaiPhut]
    public async Task LiveApi_ReadNewSchemaQueueComplaintAndReports_WithoutWritingOrders()
    {
        var reader=typeof(DonHangController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var values=(Dictionary<string,string>)reader.GetMethod("Doc").Invoke(null,
            new object[]{Environment.GetEnvironmentVariable("PHOTOCOPY_2MIN_LIVE_ENV_FILE")});
        var config=new ConfigurationBuilder().AddJsonFile(Path.Combine(
                Path.GetDirectoryName(Environment.GetEnvironmentVariable("PHOTOCOPY_2MIN_LIVE_ENV_FILE")),"PhotocopyApi","appsettings.json"))
            .AddInMemoryCollection(values).Build();
        using var db=new SqlConnection(config["ConnectionStrings:PhotocopyChain"]);
        var ceo=await db.QueryFirstAsync<NguoiDung>("SELECT TOP(1) MaNguoiDung,MatKhau,VaiTro,MaChiNhanh,TrangThai FROM NguoiDung WHERE VaiTro='SUPER_ADMIN' AND TrangThai=N'HOAT_DONG' ORDER BY MaNguoiDung");
        var store=new PhienDangNhapStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PhotocopyChain","account-sessions.json"),config["Jwt:Key"]);
        var claims=new[]{new Claim(TenClaim.MaNguoiDung,ceo.MaNguoiDung.ToString()),new Claim(TenClaim.VaiTro,ceo.VaiTro),new Claim(PhienDangNhapStore.TenClaimDauPhien,store.TaoDauPhien(ceo)),new Claim("platform","WEB")};
        var token=new JwtSecurityToken(config["Jwt:Issuer"],config["Jwt:Audience"],claims,DateTime.UtcNow.AddSeconds(-5),DateTime.UtcNow.AddMinutes(2),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"])),SecurityAlgorithms.HmacSha256));
        using var http=new HttpClient {BaseAddress=new Uri("http://localhost:5268"),Timeout=TimeSpan.FromSeconds(20)};
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",new JwtSecurityTokenHandler().WriteToken(token));
        int id=await db.ExecuteScalarAsync<int>("SELECT MAX(MaDonHang) FROM DonHang");
        foreach(var path in new[]{"/api/don-hang/khieu-nai-hoan-tien","/api/don-hang/hang-doi",$"/api/don-hang/{id}",
            $"/api/don-hang/{id}/khieu-nai-hoan-tien","/api/quan-tri/bao-cao/hien-tai","/api/quan-tri/bao-cao/yeu-cau-hoan-tien","/api/thong-bao","/api/quan-tri/bao-cao/phan-tich?tuNgay=2026-11-01&denNgay=2026-11-03"})
        {
            using var response=await http.GetAsync(path);
            Assert.True(response.IsSuccessStatusCode,$"GET {path}: HTTP {(int)response.StatusCode}");
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array);
        }
    }
}
