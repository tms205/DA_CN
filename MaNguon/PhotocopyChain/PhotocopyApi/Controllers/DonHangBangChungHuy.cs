using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.HuyDon;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.GiaoHang;
namespace PhotocopyApi.Controllers;

public partial class DonHangController
{
 private async Task<string> DocBangChungHuy(int id) {
  int actor=int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
  using var conn=new SqlConnection(_chuoiKetNoi);
  var row=await conn.QueryFirstOrDefaultAsync("SELECT MaKhachHang,MaChiNhanh,BangChungHuyJson FROM DonHang WHERE MaDonHang=@id",new{id});
  if(row==null)return null;
  if(User.IsInRole(VaiTro.Customer)?(int)row.MaKhachHang!=actor:!DuocLamViecVoiChiNhanh((int)row.MaChiNhanh))return null;
  return (string)row.BangChungHuyJson;
 }
 [HttpPost("{maDonHang:int:min(1)}/bang-chung-huy")]
 [Authorize(Roles="CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN")]
 [RequestSizeLimit(16*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=16*1024*1024)]
 public async Task<IActionResult> TaiBangChungHuy(int maDonHang,[FromForm]AnhHuyRequest req) {
  int actor=int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
  using var conn=new SqlConnection(_chuoiKetNoi);
  var row=await conn.QueryFirstOrDefaultAsync("SELECT dh.KenhDatDon,tt.TrangThai AS ThanhToan,dh.MaKhachHang,dh.MaChiNhanh,dh.TrangThai,"+PhotocopyShared.Dtos.DonHang.ChinhSachDonApp.SqlChoHuy+" AS ChoHuyKhach FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang WHERE dh.MaDonHang=@maDonHang",new{maDonHang});
  if(row==null||(User.IsInRole(VaiTro.Customer)?(int)row.MaKhachHang!=actor:!DuocLamViecVoiChiNhanh((int)row.MaChiNhanh)))return NotFound(new{message="Không tìm thấy đơn trong phạm vi của bạn."});
  if(!TrangThaiDonHang.ChoPhepHuy((string)row.TrangThai))return Conflict(new{message="Đơn không còn được hủy; không tải ảnh lên."});
  if((User.IsInRole(VaiTro.Customer) || ((string)row.KenhDatDon=="APP" && (string)row.ThanhToan=="DA_THANH_TOAN"))&&!(bool)row.ChoHuyKhach)return Conflict(new{message="Đơn đã tiếp nhận hoặc hết hạn hủy; đơn app đã trả tiền chỉ có 2 phút sau thanh toán."});
  try {var proof=await _bangChungHuy.Luu(maDonHang,actor,req.Anh,HttpContext.RequestAborted);return Ok(new{maBangChung=proof.MaHoSo});}
  catch(ArgumentException ex){return BadRequest(new{message=ex.Message});}
  catch(IOException){return StatusCode(503,new{message="Chưa lưu được bằng chứng. Không hủy đơn; hãy thử lại."});}
 }
 [HttpGet("{maDonHang:int:min(1)}/bang-chung-huy")]
 [Authorize(Roles="CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN")]
 public async Task<IActionResult> XemBangChungHuy(int maDonHang) {
  Response.Headers.CacheControl="no-store";var json=await DocBangChungHuy(maDonHang);
  if(json==null)return NotFound(new{message="Đơn chưa có bằng chứng hủy hoặc ngoài phạm vi."});
  return Ok(JsonSerializer.Deserialize<HoSoHuyDon>(json));
 }
 [HttpGet("{maDonHang:int:min(1)}/bang-chung-huy/anh/{maAnh}")]
 [Authorize(Roles="CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN")]
 public async Task<IActionResult> XemAnhHuy(int maDonHang,string maAnh) {
  Response.Headers.CacheControl="no-store";Response.Headers["X-Content-Type-Options"]="nosniff";
  if(!Guid.TryParseExact(maAnh,"N",out _))return BadRequest();
  var json=await DocBangChungHuy(maDonHang);if(json==null)return NotFound();
  var item=JsonSerializer.Deserialize<HoSoHuyDon>(json)?.Anh.SingleOrDefault(x=>x.MaAnh==maAnh);if(item==null)return NotFound();
  FileStream stream=null;
  try {stream=new FileStream(_bangChungHuy.Anh.DuongDan(maDonHang,item),FileMode.Open,FileAccess.Read,FileShare.Read);
   var hash=await System.Security.Cryptography.SHA256.HashDataAsync(stream,HttpContext.RequestAborted);
   if(Convert.ToHexString(hash)!=item.Sha256){await stream.DisposeAsync();return Conflict(new{message="Ảnh không khớp mã kiểm tra."});}
   stream.Position=0;return File(stream,item.LoaiNoiDung);
  }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){if(stream!=null)await stream.DisposeAsync();return NotFound();}
  catch{if(stream!=null)await stream.DisposeAsync();throw;}
 }
}
public sealed class AnhHuyRequest {[MinLength(1,ErrorMessage="Cần ít nhất một ảnh bằng chứng."),MaxLength(3)]public List<IFormFile> Anh {get;set;}=new();}
