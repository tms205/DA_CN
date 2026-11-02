using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;
namespace PhotocopyApi.Tests;

public class HuyBangChungCSDLTests
{
 [KiemThuGiaoHangGhi]
 public async Task DonTest_HuyCoAnh_GiuThanhToan_ChanSauTiepNhan_VaBaoCaoDuTrangThai() {
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  var reader=typeof(DonHangController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
  var values=(Dictionary<string,string>)reader.GetMethod("Doc").Invoke(null,new object[]{Environment.GetEnvironmentVariable("PHOTOCOPY_DELIVERY_ENV_FILE")});
  var config=new ConfigurationBuilder().AddInMemoryCollection(values).Build();
  string root=Path.Combine(Path.GetTempPath(),"PhotocopyCancelTests",Guid.NewGuid().ToString("N"));config["HuyDon:ThuMucBangChung"]=root;
  using var conn=new SqlConnection(config.GetConnectionString("PhotocopyChain"));await conn.OpenAsync();using var cache=new MemoryCache(new MemoryCacheOptions());
  var customers=(await conn.QueryAsync<int>("SELECT TOP 2 MaNguoiDung FROM NguoiDung WHERE VaiTro='CUSTOMER' AND TrangThai=N'HOAT_DONG' ORDER BY MaNguoiDung")).ToList();Assert.Equal(2,customers.Count);
  int branch=await conn.ExecuteScalarAsync<int>("SELECT TOP 1 MaChiNhanh FROM ChiNhanh ORDER BY MaChiNhanh");
  var ids=new List<int>();string marker="TEST_HUY018_"+Guid.NewGuid().ToString("N");
  ClaimsPrincipal User(string role,int actor)=>new(new ClaimsIdentity(new[]{new Claim(TenClaim.MaNguoiDung,actor.ToString()),new Claim(TenClaim.VaiTro,role)},"fixture",ClaimTypes.Name,TenClaim.VaiTro));
  DonHangController Api(int actor)=>new(config,cache,null){ControllerContext=new(){HttpContext=new DefaultHttpContext{User=User(VaiTro.Customer,actor)}}};
  try {
   foreach(string status in new[]{"CHO_XAC_NHAN","DA_XAC_NHAN"}){
    int id=await conn.ExecuteScalarAsync<int>(@"SET QUOTED_IDENTIFIER ON;SET ANSI_NULLS ON;
     INSERT DonHang(MaKhachHang,MaChiNhanh,HinhThucNhan,TienHang,TongTien,TrangThai,NgayHenTra,GhiChu)
     VALUES(@customer,@branch,'TAI_QUAY',39000,39000,@status,DATEADD(HOUR,1,GETDATE()),@marker);SELECT CAST(SCOPE_IDENTITY() AS INT)",new{customer=customers[0],branch,status,marker});ids.Add(id);
    await conn.ExecuteAsync("INSERT ThanhToan(MaDonHang,PhuongThuc,SoTien,TrangThai,MaGiaoDich) VALUES(@id,'VIETQR',39000,'DA_THANH_TOAN',@marker)",new{id,marker});
   }
   using var image=new System.Drawing.Bitmap(8,8);using var bytes=new MemoryStream();image.Save(bytes,System.Drawing.Imaging.ImageFormat.Png);byte[] data=bytes.ToArray();
   var upload=Assert.IsType<OkObjectResult>(await Api(customers[0]).TaiBangChungHuy(ids[0],new(){Anh=new(){new FormFile(new MemoryStream(data),0,data.Length,"Anh","proof.png")}}));
   using var receipt=JsonDocument.Parse(JsonSerializer.Serialize(upload.Value));string code=receipt.RootElement.GetProperty("maBangChung").GetString();
   var request=new HuyDonRequest{LyDo="Khách yêu cầu hủy thử nghiệm",NganHang="Ngân hàng test",SoTaiKhoan="001234567890",MaBangChung=code};
   Assert.IsType<BadRequestObjectResult>(await Api(customers[0]).HuyDon(ids[1],request));
   Assert.IsType<OkObjectResult>(await Api(customers[0]).HuyDon(ids[0],request));
   Assert.Equal("DA_HUY",await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM DonHang WHERE MaDonHang=@id",new{id=ids[0]}));
   Assert.Equal("DA_XAC_NHAN",await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM DonHang WHERE MaDonHang=@id",new{id=ids[1]}));
   Assert.Equal(78000m,await conn.ExecuteScalarAsync<decimal>("SELECT SUM(SoTien) FROM ThanhToan WHERE MaDonHang IN @ids AND TrangThai='DA_THANH_TOAN'",new{ids}));
   Assert.IsType<NotFoundObjectResult>(await Api(customers[1]).XemBangChungHuy(ids[0]));
   var proofResult=Assert.IsType<OkObjectResult>(await Api(customers[0]).XemBangChungHuy(ids[0]));
   var proof=Assert.IsType<PhotocopyApi.HuyDon.HoSoHuyDon>(proofResult.Value);Assert.Single(proof.Anh);Assert.Equal(customers[0],proof.MaNguoiGui);
   var file=Assert.IsType<FileStreamResult>(await Api(customers[0]).XemAnhHuy(ids[0],proof.Anh[0].MaAnh));await file.FileStream.DisposeAsync();
   DateTime today=await conn.ExecuteScalarAsync<DateTime>("SELECT CAST(GETDATE() AS DATE)");
   var report=new QuanTriController(config){ControllerContext=new(){HttpContext=new DefaultHttpContext{User=User(VaiTro.SuperAdmin,1)}}};
   var rows=Assert.IsAssignableFrom<IEnumerable<BaoCaoDoanhThuDto>>(Assert.IsType<OkObjectResult>(await report.BaoCaoDoanhThu(today,today,"ngay","DA_HUY")).Value).ToList();
   Assert.NotEmpty(rows);Assert.All(rows,x=>{Assert.Equal("DA_HUY",x.TrangThai);Assert.Equal(0,x.DoanhThu);});
  } finally {
   foreach(int id in ids){
    Assert.Equal(1,await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DonHang dh WHERE dh.MaDonHang=@id AND dh.MaKhachHang=@customer AND (dh.GhiChu=@marker OR EXISTS(SELECT 1 FROM ThanhToan tt WHERE tt.MaDonHang=dh.MaDonHang AND tt.MaGiaoDich=@marker))",new{id,customer=customers[0],marker}));
    await conn.ExecuteAsync(@"SET QUOTED_IDENTIFIER ON;SET ANSI_NULLS ON;SET XACT_ABORT ON;BEGIN TRAN;
     DELETE ThongBao WHERE MaDonHang=@id;DELETE ThanhToan WHERE MaDonHang=@id AND MaGiaoDich=@marker;
     DELETE DonHang WHERE MaDonHang=@id AND MaKhachHang=@customer;COMMIT;",new{id,marker,customer=customers[0]});
   }
   Assert.Equal(0,await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ThanhToan WHERE MaGiaoDich=@marker",new{marker}));
   Assert.StartsWith(Path.Combine(Path.GetTempPath(),"PhotocopyCancelTests")+Path.DirectorySeparatorChar,root);
   if(Directory.Exists(root))Directory.Delete(root,true);
  }
 }
}
