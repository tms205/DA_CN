using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.GiaoHang;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.Controllers;

public partial class GiaoHangController
{
    [HttpPost("vi-tri-shipper")]
    [Authorize(Roles = "SHIPPER")]
    public async Task<IActionResult> DinhViShipper(ViTriShipperRequest req)
    {
        if (KhongCoPhamVi) return Forbid();
        try { BangChungGiaoHangStore.KiemTraViTri(req, DateTimeOffset.UtcNow); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        string diaChi = await DiaChiBangChung(req, HttpContext.RequestAborted);
        return Ok(new { diaChi, daTraDuocDiaChi = diaChi != null,
            message = diaChi == null ? "Đã lấy GPS; chưa tra được địa chỉ. Vẫn có thể gửi báo cáo." : "Đã lấy vị trí hiện tại." });
    }

    [HttpPost("luot/{maGiaoHang:int:min(1)}/giao-that-bai")]
    [Authorize(Roles = "SHIPPER")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> BaoGiaoThatBai(int maGiaoHang, [FromForm] BaoGiaoThatBaiRequest req)
    {
        if (KhongCoPhamVi) return Forbid();
        HoSoBangChung hoSo = null;
        try
        {
            // Kiểm chủ sở hữu trước mọi I/O ảnh/Geoapify, không giữ transaction khi upload hay định vị.
            using (var conn = new SqlConnection(_chuoiKetNoi))
            {
                string trangThai = await conn.QueryFirstOrDefaultAsync<string>(@"SELECT gh.TrangThai FROM GiaoHang gh
                    JOIN DonHang dh ON dh.MaDonHang=gh.MaDonHang
                    WHERE gh.MaGiaoHang=@maGiaoHang AND gh.MaShipper=@MaNguoiDung AND dh.MaChiNhanh=@MaChiNhanh",
                    new { maGiaoHang, MaNguoiDung, MaChiNhanh = ChiNhanh });
                if (trangThai == null) return NotFound(new { message = "Không tìm thấy lượt giao của bạn." });
                if (trangThai == "THAT_BAI") return Ok(new { message = "Báo cáo lượt giao này đã được ghi nhận; không thay bằng chứng cũ." });
                if (trangThai != "DANG_GIAO") return Conflict(new { message = "Lượt giao không còn đang thực hiện. Hãy tải lại." });
            }
            var viTri = req.DocViTri();
            BangChungGiaoHangStore.KiemTraViTri(viTri, DateTimeOffset.UtcNow);
            BangChungGiaoHangStore.KiemTraDanhSach(req.Anh);
            if (string.IsNullOrWhiteSpace(req.LyDo) || req.LyDo.Trim().Length is < 5 or > 255)
                return BadRequest(new { message = "Nhập lý do từ 5 đến 255 ký tự." });
            hoSo = new HoSoBangChung { ViTri = viTri };
            hoSo.Anh = await _bangChung.Luu(maGiaoHang, req.Anh, HttpContext.RequestAborted, NguonAnhGiaoHang.Shipper);
            hoSo.DiaChi = await DiaChiBangChung(viTri, HttpContext.RequestAborted);
            return await GhiKetQua(maGiaoHang, new KetQuaGiaoRequest { ThanhCong = false, LyDo = req.LyDo }, hoSo);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { return new EmptyResult(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqlException)
        {
            _log.LogWarning("Chưa xác nhận lưu bằng chứng lượt giao {MaGiaoHang}; loại lỗi {LoaiLoi}.", maGiaoHang, ex.GetType().Name);
            return StatusCode(503, new { message = "Chưa xác nhận được báo cáo. Giữ ảnh và tải lại lịch sử trước khi gửi lại. Nếu vẫn lỗi, liên hệ quản lý cửa hàng." });
        }
        finally
        {
            // Commit mất phản hồi có thể đã thành công: giữ ảnh, không xóa mù làm mất bằng chứng.
            if (hoSo != null && !hoSo.DaLuu && !hoSo.BatDauCommit)
                _bangChung.XoaTepCuaYeuCau(maGiaoHang, hoSo.Anh, NguonAnhGiaoHang.Shipper);
        }
    }

    private async Task<string> DiaChiBangChung(ViTriShipperRequest req, CancellationToken ct)
    {
        try { return (await _banDo.TimDiaChi(null, req.ViDo, req.KinhDo, ct)).FirstOrDefault()?.DiaChi; }
        catch (InvalidOperationException)
        { _log.LogWarning("Chưa tra được địa chỉ bằng chứng giao; vẫn giữ tọa độ GPS."); return null; }
    }

    [HttpGet("luot/{maGiaoHang:int:min(1)}/bang-chung")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,SHIPPER")]
    public async Task<IActionResult> XemBangChung(int maGiaoHang)
    {
        Response.Headers.CacheControl = "no-store";
        if (KhongCoPhamVi) return Forbid();
        var row = await DocBangChung(maGiaoHang);
        if (row == null) return NotFound(new { message = "Không tìm thấy lượt giao trong phạm vi của bạn." });
        var anh = DocDanhSachAnh((string)row.AnhBangChungJson);
        return Ok(new BangChungGiaoDto { MaGiaoHang = row.MaGiaoHang, MaDonHang = row.MaDonHang,
            LanGiao = row.LanGiao, TenShipper = row.TenShipper, LyDo = row.LyDo,
            ViDo = row.ViDoBaoGiao, KinhDo = row.KinhDoBaoGiao, DoChinhXac = row.DoChinhXacGps,
            ThoiDiemGps = row.ThoiDiemGps, ThoiDiemBaoCao = row.ThoiDiemBaoCao, DiaChi = row.DiaChiBaoGiao, Anh = anh });
    }

    [HttpGet("luot/{maGiaoHang:int:min(1)}/bang-chung/anh/{maAnh}")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,SHIPPER")]
    public async Task<IActionResult> XemAnhBangChung(int maGiaoHang, string maAnh)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (KhongCoPhamVi) return Forbid();
        if (!Guid.TryParseExact(maAnh, "N", out _)) return BadRequest(new { message = "Mã ảnh không hợp lệ." });
        var row = await DocBangChung(maGiaoHang);
        if (row == null) return NotFound();
        return await TraAnhBangChung(maGiaoHang,maAnh,DocDanhSachAnh((string)row.AnhBangChungJson),NguonAnhGiaoHang.Shipper);
    }

    private async Task<IActionResult> TraAnhBangChung(int maGiaoHang,string maAnh,List<AnhBangChungDto> danhSach,NguonAnhGiaoHang nguon)
    {
        Response.Headers["X-Content-Type-Options"]="nosniff";
        var anh = danhSach.SingleOrDefault(x => x.MaAnh == maAnh);
        if (anh == null) return NotFound();
        string path = _bangChung.DuongDan(maGiaoHang, anh, nguon);
        FileStream stream = null;
        try
        {
            // Mở sau khi kiểm quyền; FileStream được ASP.NET giải phóng khi gửi xong.
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, HttpContext.RequestAborted);
            if (!string.Equals(Convert.ToHexString(hash), anh.Sha256, StringComparison.Ordinal))
            {
                await stream.DisposeAsync();
                return Conflict(new { message = "Ảnh không khớp mã kiểm tra đã lưu. Liên hệ quản lý đối chiếu bản sao lưu." });
            }
            stream.Position = 0;
            return File(stream, anh.LoaiNoiDung);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { if (stream != null) await stream.DisposeAsync(); return NotFound(new { message = "Ảnh chưa truy cập được. Liên hệ cửa hàng kiểm tra bản sao lưu." }); }
        catch { if (stream != null) await stream.DisposeAsync(); throw; }
    }

    private async Task<dynamic> DocBangChung(int maGiaoHang)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? shipper = User.IsInRole(VaiTro.Shipper) ? MaNguoiDung : null;
        return await conn.QueryFirstOrDefaultAsync(@"SELECT gh.MaGiaoHang,gh.MaDonHang,gh.LanGiao,gh.LyDo,
            sp.HoTen AS TenShipper,gh.ViDoBaoGiao,gh.KinhDoBaoGiao,gh.DoChinhXacGps,gh.ThoiDiemGps,
            gh.ThoiDiemBaoCao,gh.DiaChiBaoGiao,gh.AnhBangChungJson
            FROM GiaoHang gh JOIN DonHang dh ON dh.MaDonHang=gh.MaDonHang JOIN NguoiDung sp ON sp.MaNguoiDung=gh.MaShipper
            WHERE gh.MaGiaoHang=@maGiaoHang AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh)
                AND (@MaShipper IS NULL OR gh.MaShipper=@MaShipper)", new { maGiaoHang, MaChiNhanh = ChiNhanh, MaShipper = shipper });
    }

    private static List<AnhBangChungDto> DocDanhSachAnh(string json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<List<AnhBangChungDto>>(json) ?? new();

    private sealed class HoSoBangChung
    {
        public ViTriShipperRequest ViTri { get; init; }
        public string DiaChi { get; set; }
        public List<AnhBangChungDto> Anh { get; set; } = new();
        public bool BatDauCommit { get; set; }
        public bool DaLuu { get; set; }
    }
}

public sealed class BaoGiaoThatBaiRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do giao thất bại."), StringLength(255, MinimumLength = 5, ErrorMessage = "Lý do cần từ 5 đến 255 ký tự.")] public string LyDo { get; set; }
    // Multipart numbers luôn dùng dấu chấm; không phụ thuộc Windows đang vi-VN hay en-US.
    [Required(ErrorMessage = "Cần lấy vị trí GPS."), StringLength(60)] public string ViDo { get; set; }
    [Required(ErrorMessage = "Cần lấy vị trí GPS."), StringLength(60)] public string KinhDo { get; set; }
    [Required(ErrorMessage = "Thiếu độ chính xác GPS."), StringLength(60)] public string DoChinhXac { get; set; }
    [Required(ErrorMessage = "Thiếu thời điểm lấy GPS."), StringLength(60)] public string ThoiDiemGps { get; set; }
    public List<IFormFile> Anh { get; set; } = new();

    public ViTriShipperRequest DocViTri()
    {
        if (!decimal.TryParse(ViDo, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !decimal.TryParse(KinhDo, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
            !double.TryParse(DoChinhXac, NumberStyles.Float, CultureInfo.InvariantCulture, out var accuracy) ||
            !DateTimeOffset.TryParse(ThoiDiemGps, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ||
            !(ThoiDiemGps.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(ThoiDiemGps, @"[+-]\d{2}:\d{2}$")))
            throw new ArgumentException("Vị trí/thời điểm GPS không hợp lệ. Hãy lấy lại vị trí trên app.");
        return new() { ViDo=lat,KinhDo=lon,DoChinhXac=accuracy,ThoiDiemGps=time };
    }
}
