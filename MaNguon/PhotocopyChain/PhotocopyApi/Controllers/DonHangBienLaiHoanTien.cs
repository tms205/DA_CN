using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos.DonHang;

namespace PhotocopyApi.Controllers;

public partial class DonHangController
{
    private const string SqlLuuBienLaiHoanTien = @"UPDATE DonHang SET BangChungHuyJson=JSON_MODIFY(
        COALESCE(BangChungHuyJson,N'{}'),'$.BienLaiHoanTien',JSON_QUERY(@json)) WHERE MaDonHang=@id";
    // Báo sau COMMIT bằng payload nhỏ, không chờ dựng lại toàn bộ chi tiết/file của đơn.
    private async Task BaoHoanTienDoiAsync(int id, int chiNhanh)
    {
        if (_hub == null) return;
        try { await PhotocopyApi.Hubs.CapNhatHub.GuiChoChiNhanh(_hub,chiNhanh,
            PhotocopyShared.Constants.HubEvents.RefundChanged,new { MaDonHang=id,MaChiNhanh=chiNhanh }); }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            HttpContext.RequestServices?.GetService<ILogger<DonHangController>>()?.LogWarning(
                "Hoàn tiền đơn {MaDonHang} đã lưu; chưa gửi được tín hiệu realtime.",id);
        }
    }

    [HttpPost("{maDonHang:int:min(1)}/bien-lai-hoan-tien")]
    [Authorize(Roles = "SUPER_ADMIN")]
    [RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> TaiBienLaiHoanTien(int maDonHang, [FromForm] AnhBienLaiRequest req)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        var don = await conn.QuerySingleOrDefaultAsync<DonKhieuNai>(SqlDonKhieuNai, new { id=maDonHang });
        if (don == null || !DuocLamViecVoiChiNhanh(don.MaChiNhanh)) return NotFound();
        var khieuNai = don.YeuCauHoanTienJson == null ? null : JsonSerializer.Deserialize<HoSoKhieuNaiDto>(don.YeuCauHoanTienJson);
        bool daHuyTruocIn=don.TrangThai=="DA_HUY" && don.MaMayIn==null;
        bool thieuBienLai=don.BangChungHuyJson==null || JsonSerializer.Deserialize<PhotocopyApi.HuyDon.HoSoHuyDon>(don.BangChungHuyJson)?.BienLaiHoanTien==null;
        bool choChuyen=don.ThanhToan=="DA_THANH_TOAN" && (daHuyTruocIn || khieuNai?.TrangThai=="CHAP_THUAN");
        bool boSungCu=daHuyTruocIn && don.ThanhToan=="DA_HOAN_TIEN" && thieuBienLai;
        if (!choChuyen && !boSungCu)
            return Conflict(new { message="Đơn không còn ở bước chờ chuyển hoàn tiền." });
        if (req.Anh == null) return BadRequest(new { message="Chọn một ảnh biên lai JPG/PNG, tối đa 5 MB." });
        try
        {
            var proof = await _khoBienLai.Luu(maDonHang, NguoiGuiKhieuNai, new[] {req.Anh}, HttpContext.RequestAborted);
            return Ok(new { maBangChung=proof.MaHoSo });
        }
        catch (ArgumentException ex) { return BadRequest(new { message=ex.Message }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return StatusCode(503,new { message="Chưa lưu được biên lai; chưa xác nhận hoàn tiền." }); }
    }

    [HttpPost("{maDonHang:int:min(1)}/bien-lai-hoan-tien/bo-sung")]
    [Authorize(Roles="SUPER_ADMIN")]
    public async Task<IActionResult> BoSungBienLaiHoanTien(int maDonHang, XacNhanHoanTienRequest req)
    {
        if((req.LyDo?.Trim().Length??0)<11 || string.IsNullOrWhiteSpace(req.MaChuyenKhoan) || string.IsNullOrWhiteSpace(req.MaBienLai))
            return BadRequest(new {message="Cần lời nhắn trên 10 ký tự, mã chuyển tiền và ảnh biên lai."});
        using var conn=new SqlConnection(_chuoiKetNoi);await conn.OpenAsync();
        await using var tx=await conn.BeginTransactionAsync();
        var don=await conn.QuerySingleOrDefaultAsync<DonKhieuNai>(SqlDonKhieuNai,new {id=maDonHang},tx);
        if(don==null || !DuocLamViecVoiChiNhanh(don.MaChiNhanh))return NotFound();
        if(don.TrangThai!="DA_HUY" || don.MaMayIn!=null || don.ThanhToan!="DA_HOAN_TIEN")
            return Conflict(new {message="Chỉ bổ sung biên lai cho đơn hủy đã ghi nhận hoàn tiền."});
        if(don.BangChungHuyJson!=null && JsonSerializer.Deserialize<PhotocopyApi.HuyDon.HoSoHuyDon>(don.BangChungHuyJson)?.BienLaiHoanTien!=null)
            return Conflict(new {message="Đơn đã có biên lai; không ghi đè lịch sử xác nhận."});
        BienLaiHoanTienDto bill;
        try {bill=await DocBienLaiHoanTien(maDonHang,req.MaBienLai,req.MaChuyenKhoan,req.LyDo);}
        catch(ArgumentException ex){return BadRequest(new {message=ex.Message});}
        catch(IOException){return StatusCode(503,new {message="Không đọc được biên lai. Chưa bổ sung hồ sơ."});}
        // Chỉ bổ sung JSON và thông báo; không gọi sp_HoanTien, không cập nhật ThanhToan/số tiền.
        await conn.ExecuteAsync(SqlLuuBienLaiHoanTien,new {id=maDonHang,json=JsonSerializer.Serialize(bill)},tx);
        await conn.ExecuteAsync(@"INSERT ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
            VALUES(@kh,@id,N'CEO đã xử lý yêu cầu hoàn tiền',N'CEO đã bổ sung biên lai và lời nhắn. Mở chi tiết đơn để xem.')",
            new {kh=don.MaKhachHang,id=maDonHang},tx);
        await tx.CommitAsync();
        await BaoHoanTienDoiAsync(maDonHang,don.MaChiNhanh);
        await BaoDonDoiTrangThai(conn,maDonHang,"CEO đã bổ sung biên lai hoàn tiền cho đơn");
        return Ok(new {message="Đã gửi biên lai và lời nhắn cho khách."});
    }

    private async Task<BienLaiHoanTienDto> DocBienLaiHoanTien(int id, string ma, string maChuyen, string lyDo)
    {
        if (string.IsNullOrWhiteSpace(ma)) throw new ArgumentException("Vui lòng đính kèm ảnh biên lai chuyển khoản.");
        var proof = _khoBienLai.Doc(id, NguoiGuiKhieuNai, ma);
        if (proof.Anh.Count != 1) throw new ArgumentException("Cần đúng một ảnh biên lai chuyển khoản.");
        var anh = proof.Anh[0];
        var bytes = await System.IO.File.ReadAllBytesAsync(_khoBienLai.Anh.DuongDan(id,anh), HttpContext.RequestAborted);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != anh.Sha256)
            throw new ArgumentException("Biên lai không khớp mã kiểm tra. Hãy tải lại ảnh.");
        return new BienLaiHoanTienDto { Anh=anh, MaCeo=NguoiGuiKhieuNai, Luc=DateTimeOffset.UtcNow,
            MaChuyenKhoan=maChuyen.Trim(), LyDo=lyDo.Trim() };
    }

    [HttpGet("{maDonHang:int:min(1)}/bien-lai-hoan-tien/{maAnh}")]
    [Authorize(Roles="CUSTOMER,SUPER_ADMIN")]
    public async Task<IActionResult> XemBienLaiHoanTien(int maDonHang, string maAnh)
    {
        Response.Headers.CacheControl="no-store";
        Response.Headers["X-Content-Type-Options"]="nosniff";
        if (!Guid.TryParseExact(maAnh,"N",out _)) return BadRequest();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var don = await conn.QuerySingleOrDefaultAsync<DonKhieuNai>(SqlDonKhieuNai,new {id=maDonHang});
        if (!DuocXemKhieuNai(don) || (User.IsInRole("SUPER_ADMIN") && !DuocLamViecVoiChiNhanh(don.MaChiNhanh))) return NotFound();
        BienLaiHoanTienDto bienLai = null;
        if (don.YeuCauHoanTienJson != null)
            bienLai = JsonSerializer.Deserialize<HoSoKhieuNaiDto>(don.YeuCauHoanTienJson)?.LichSu
                .Select(x=>x.BienLai).FirstOrDefault(x=>x?.Anh.MaAnh==maAnh);
        if (bienLai == null && don.BangChungHuyJson != null)
            bienLai = JsonSerializer.Deserialize<PhotocopyApi.HuyDon.HoSoHuyDon>(don.BangChungHuyJson)?.BienLaiHoanTien;
        if (bienLai?.Anh.MaAnh != maAnh) return NotFound();
        try
        {
            var data = await System.IO.File.ReadAllBytesAsync(_khoBienLai.Anh.DuongDan(maDonHang,bienLai.Anh),HttpContext.RequestAborted);
            if (Convert.ToHexString(SHA256.HashData(data)) != bienLai.Anh.Sha256)
                return Conflict(new {message="Biên lai không khớp mã kiểm tra."});
            return File(data,bienLai.Anh.LoaiNoiDung);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return NotFound(); }
    }
}
public sealed class AnhBienLaiRequest
{
    [Required] public IFormFile Anh { get; set; }
}
