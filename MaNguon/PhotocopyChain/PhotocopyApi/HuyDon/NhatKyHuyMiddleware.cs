using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Constants;
namespace PhotocopyApi.HuyDon;

// Nhật ký riêng cho mọi request hủy, kể cả lỗi model/quyền/bảo trì. Không ghi token hoặc ảnh vào log.
public sealed class NhatKyHuyMiddleware
{
 private readonly RequestDelegate _next;
 public NhatKyHuyMiddleware(RequestDelegate next)=>_next=next;
 public async Task InvokeAsync(HttpContext context,IConfiguration config,ILogger<NhatKyHuyMiddleware> log) {
  var match=Regex.Match(context.Request.Path.Value??"",@"^/api/don-hang/([^/]+)/huy$",RegexOptions.IgnoreCase);
  if(context.Request.Method!="POST"||!match.Success){await _next(context);return;}
  int.TryParse(match.Groups[1].Value,out int order);int.TryParse(context.User.FindFirst(TenClaim.MaNguoiDung)?.Value,out int actor);
  string root=config["HuyDon:ThuMucNhatKy"]??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PhotocopyChain","CancellationAudit");
  string path=Path.Combine(root,Guid.NewGuid().ToString("N")+".json");var start=DateTimeOffset.UtcNow;
  try {Directory.CreateDirectory(root);await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{MaDonHang=order,MaNguoiGui=actor,GuiLuc=start,KetQua="DANG_XU_LY"}));}
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){context.Response.StatusCode=503;await context.Response.WriteAsJsonAsync(new{message="Chưa ghi được nhật ký yêu cầu hủy. Đơn chưa được hủy."});return;}
  var original=context.Response.Body;await using var buffer=new MemoryStream();context.Response.Body=buffer;
  try {
   await _next(context);
   string message="";buffer.Position=0;
   try {using var json=await JsonDocument.ParseAsync(buffer);if(json.RootElement.TryGetProperty("message",out var v))message=v.GetString()??"";}catch(JsonException){}
   try {string temp=path+".tmp";await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(new{MaDonHang=order,MaNguoiGui=actor,GuiLuc=start,HoanTatLuc=DateTimeOffset.UtcNow,MaHttp=context.Response.StatusCode,ThongBao=message}));File.Move(temp,path,true);
    if(actor>0&&context.Response.StatusCode>=400) {
     using var conn=new SqlConnection(config.GetConnectionString("PhotocopyChain"));
     await conn.ExecuteAsync(@"INSERT ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
      SELECT nd.MaNguoiDung,dh.MaDonHang,N'Yêu cầu hủy đơn bị từ chối',@message
      FROM DonHang dh JOIN NguoiDung actor ON actor.MaNguoiDung=@actor
      JOIN NguoiDung nd ON nd.TrangThai=N'HOAT_DONG' AND (nd.VaiTro='SUPER_ADMIN' OR(nd.VaiTro='BRANCH_ADMIN' AND nd.MaChiNhanh=dh.MaChiNhanh))
      WHERE dh.MaDonHang=@order AND (dh.MaKhachHang=@actor OR actor.VaiTro='SUPER_ADMIN' OR(actor.VaiTro='BRANCH_ADMIN' AND actor.MaChiNhanh=dh.MaChiNhanh))",new{order,actor,message=message.Length>250?message[..250]:message});
    }
   }catch(Exception ex) when(ex is IOException or SqlException or UnauthorizedAccessException){log.LogError("Yêu cầu hủy {Order} đã có nhật ký bắt đầu, cần đối chiếu kết quả; lỗi {Type}",order,ex.GetType().Name);}
   buffer.Position=0;await buffer.CopyToAsync(original);
  }finally{context.Response.Body=original;}
 }
}
