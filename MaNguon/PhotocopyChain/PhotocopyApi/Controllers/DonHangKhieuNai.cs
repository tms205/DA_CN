using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.HuyDon;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DonHang;

namespace PhotocopyApi.Controllers;

public partial class DonHangController
{
    private BangChungHuyStore KhoAnhKhieuNai() => new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string> { ["HuyDon:ThuMucBangChung"] = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotocopyChain", "RefundEvidence") }).Build());

    private int NguoiGuiKhieuNai => int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);

    private sealed class DonKhieuNai
    {
        public int MaKhachHang { get; set; }
        public int MaChiNhanh { get; set; }
        public int? MaMayIn { get; set; }
        public string BangChungHuyJson { get; set; }
        public string TrangThai { get; set; }
        public string ThanhToan { get; set; }
        public string YeuCauHoanTienJson { get; set; }
        public decimal SoTien { get; set; }
        public string HoTen { get; set; }
        public string TenChiNhanh { get; set; }
    }

    private const string SqlDonKhieuNai = @"SELECT dh.MaKhachHang,dh.MaChiNhanh,dh.MaMayIn,dh.BangChungHuyJson,dh.TrangThai,dh.YeuCauHoanTienJson,
        tt.TrangThai AS ThanhToan,tt.SoTien,nd.HoTen,cn.TenChiNhanh
        FROM DonHang dh WITH(UPDLOCK,HOLDLOCK)
        JOIN ThanhToan tt WITH(UPDLOCK,HOLDLOCK) ON tt.MaDonHang=dh.MaDonHang
        JOIN NguoiDung nd ON nd.MaNguoiDung=dh.MaKhachHang
        JOIN ChiNhanh cn ON cn.MaChiNhanh=dh.MaChiNhanh WHERE dh.MaDonHang=@id";

    private bool DuocXemKhieuNai(DonKhieuNai don) => don != null &&
        (User.IsInRole(VaiTro.SuperAdmin) || (User.IsInRole(VaiTro.Customer) && don.MaKhachHang == NguoiGuiKhieuNai));

    private static bool ChoGuiKhieuNai(DonKhieuNai don) => don != null &&
        don.TrangThai is "HOAN_THANH" or "DA_GIAO" && don.ThanhToan == "DA_THANH_TOAN" && don.YeuCauHoanTienJson == null;

    [HttpGet("{maDonHang:int:min(1)}/khieu-nai-hoan-tien")]
    [Authorize(Roles = "CUSTOMER,SUPER_ADMIN")]
    public async Task<IActionResult> XemKhieuNaiHoanTien(int maDonHang)
    {
        Response.Headers.CacheControl = "no-store";
        using var conn = new SqlConnection(_chuoiKetNoi);
        var don = await conn.QuerySingleOrDefaultAsync<DonKhieuNai>(SqlDonKhieuNai, new { id = maDonHang });
        if (!DuocXemKhieuNai(don)) return NotFound();
        return Ok(new { choPhepGui = ChoGuiKhieuNai(don), hoSo = don.YeuCauHoanTienJson == null ? null : JsonSerializer.Deserialize<HoSoKhieuNaiDto>(don.YeuCauHoanTienJson) });
    }

    [HttpPost("{maDonHang:int:min(1)}/khieu-nai-hoan-tien/anh")]
    [Authorize(Roles = "CUSTOMER")]
    [RequestSizeLimit(16 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> TaiAnhKhieuNai(int maDonHang, [FromForm] AnhHuyRequest req)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        var don = await conn.QuerySingleOrDefaultAsync<DonKhieuNai>(SqlDonKhieuNai, new { id = maDonHang });
        if (!DuocXemKhieuNai(don)) return NotFound();
        if (!ChoGuiKhieuNai(don)) return Conflict(new { message = "Chỉ gửi yêu cầu cho đơn đã nhận, đã trả tiền và chưa có hồ sơ khiếu nại." });
        try
        {
            var proof = await KhoAnhKhieuNai().Luu(maDonHang, NguoiGuiKhieuNai, req.Anh, HttpContext.RequestAborted);
            return Ok(new { maBangChung = proof.MaHoSo });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (IOException) { return StatusCode(503, new { message = "Chưa lưu được ảnh; chưa gửi yêu cầu hoàn tiền." }); }
    }

    [HttpPost("{maDonHang:int:min(1)}/khieu-nai-hoan-tien")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> GuiKhieuNaiHoanTien(int maDonHang, KhieuNaiHoanTienRequest req)
    {
        if ((req.MoTa?.Trim().Length ?? 0) < 11 || string.IsNullOrWhiteSpace(req.NganHang) || req.ChuTaiKhoan.Trim().Length < 2)
            return BadRequest(new { message = "Nhập mô tả chi tiết trên 10 ký tự và đầy đủ thông tin nhận tiền." });
        HoSoHuyDon proof;
        try { proof = KhoAnhKhieuNai().Doc(maDonHang, NguoiGuiKhieuNai, req.MaBangChung); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var don = await conn.QuerySingleOrDefaultAsync<DonKhieuNai>(SqlDonKhieuNai, new { id = maDonHang }, tx);
        if (!DuocXemKhieuNai(don)) return NotFound();
        if (!ChoGuiKhieuNai(don)) return Conflict(new { message = "Đơn chưa nhận, chưa trả tiền hoặc đã có hồ sơ; không gửi trùng." });
        var hoSo = new HoSoKhieuNaiDto { MaDonHang = maDonHang, MaKhachHang = don.MaKhachHang,
            TenKhachHang = don.HoTen, TenChiNhanh = don.TenChiNhanh, SoTien = don.SoTien,
            MoTa = req.MoTa.Trim(), NganHang = req.NganHang.Trim(), SoTaiKhoan = req.SoTaiKhoan,
            ChuTaiKhoan = req.ChuTaiKhoan.Trim(), Anh = proof.Anh, GuiLuc = DateTimeOffset.UtcNow };
        await conn.ExecuteAsync("UPDATE DonHang SET YeuCauHoanTienJson=@json WHERE MaDonHang=@id",
            new { id = maDonHang, json = JsonSerializer.Serialize(hoSo) }, tx);
        await conn.ExecuteAsync(@"INSERT ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
            SELECT MaNguoiDung,@id,N'Yêu cầu hoàn tiền do sản phẩm',N'Khách đã gửi mô tả, bằng chứng và tài khoản ngân hàng. CEO cần xem xét.'
            FROM NguoiDung WHERE TrangThai=N'HOAT_DONG' AND (VaiTro='SUPER_ADMIN' OR (VaiTro='BRANCH_ADMIN' AND MaChiNhanh=@cn))", new { id = maDonHang, cn=don.MaChiNhanh }, tx);
        await conn.ExecuteAsync(@"INSERT ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung) VALUES(@kh,@id,N'Đã tiếp nhận yêu cầu hoàn tiền',N'Hồ sơ đang chờ CEO xem xét. Mở đơn để theo dõi kết quả.')", new {kh=don.MaKhachHang,id=maDonHang},tx);
        await tx.CommitAsync();
        await BaoHoanTienDoiAsync(maDonHang, don.MaChiNhanh);
        await BaoDonDoiTrangThai(conn, maDonHang, "Đã gửi hồ sơ yêu cầu hoàn tiền, chờ CEO xem xét");
        return Ok(new { message = "Đã gửi yêu cầu tới CEO. Chưa có khoản hoàn tiền nào được thực hiện." });
    }

    [HttpGet("khieu-nai-hoan-tien")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> DanhSachKhieuNaiHoanTien()
    {
        Response.Headers.CacheControl = "no-store";
        using var conn = new SqlConnection(_chuoiKetNoi);
        var json = await conn.QueryAsync<string>(@"SELECT YeuCauHoanTienJson FROM DonHang
            WHERE YeuCauHoanTienJson IS NOT NULL AND (@cn IS NULL OR MaChiNhanh=@cn) ORDER BY MaDonHang DESC",
            new { cn = int.TryParse(User.FindFirst(TenClaim.MaChiNhanh)?.Value, out int maCn) ? (int?)maCn : null });
        return Ok(json.Select(x => JsonSerializer.Deserialize<HoSoKhieuNaiDto>(x)));
    }

    [HttpPost("{maDonHang:int:min(1)}/khieu-nai-hoan-tien/quyet-dinh")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> QuyetDinhKhieuNai(int maDonHang, QuyetDinhHoanTienRequest req)
    {
        if ((req.LyDo?.Trim().Length ?? 0) < 11 || (req.TrangThai == "DA_HOAN_TIEN" && string.IsNullOrWhiteSpace(req.MaChuyenKhoan)))
            return BadRequest(new { message = "Nhập lý do trên 10 ký tự; khi đã hoàn phải có mã đối chiếu chuyển khoản thực tế." });
        if(req.TrangThai == "DA_HOAN_TIEN" && string.IsNullOrWhiteSpace(req.MaBienLai))
            return BadRequest(new { message="Vui lòng đính kèm ảnh biên lai chuyển khoản." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var don = await conn.QuerySingleOrDefaultAsync<DonKhieuNai>(SqlDonKhieuNai, new { id = maDonHang }, tx);
        if (don == null || !DuocLamViecVoiChiNhanh(don.MaChiNhanh) || don.YeuCauHoanTienJson == null) return NotFound();
        var hoSo = JsonSerializer.Deserialize<HoSoKhieuNaiDto>(don.YeuCauHoanTienJson);
        if (!hoSo.ChoQuyetDinh(req.TrangThai) || don.ThanhToan != "DA_THANH_TOAN")
            return Conflict(new { message = "Hồ sơ hoặc thanh toán đã thay đổi, không quyết định/hoàn trùng." });
        BienLaiHoanTienDto bienLai = null;
        if (req.TrangThai == "DA_HOAN_TIEN")
        {
            try { bienLai = await DocBienLaiHoanTien(maDonHang, req.MaBienLai, req.MaChuyenKhoan, req.LyDo); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (IOException) { return StatusCode(503, new { message = "Không đọc được biên lai. Chưa xác nhận hoàn tiền." }); }
        }
        hoSo.TrangThai = req.TrangThai;
        hoSo.LichSu.Add(new() { MaCeo = NguoiGuiKhieuNai, TrangThai = req.TrangThai,
            LyDo = req.LyDo.Trim(), MaChuyenKhoan = req.TrangThai == "DA_HOAN_TIEN" ? req.MaChuyenKhoan.Trim() : null, Luc = DateTimeOffset.UtcNow, BienLai = bienLai });
        if (req.TrangThai == "DA_HOAN_TIEN")
        {
            int n = await conn.ExecuteAsync("UPDATE ThanhToan SET TrangThai='DA_HOAN_TIEN' WHERE MaDonHang=@id AND TrangThai='DA_THANH_TOAN'", new { id = maDonHang }, tx);
            if (n != 1) return Conflict(new { message = "Khoản thanh toán không còn đủ điều kiện hoàn." });
        }
        await conn.ExecuteAsync("UPDATE DonHang SET YeuCauHoanTienJson=@json WHERE MaDonHang=@id", new { id = maDonHang, json = JsonSerializer.Serialize(hoSo) }, tx);
        await conn.ExecuteAsync(@"INSERT ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
            VALUES(@kh,@id,N'CEO đã xử lý yêu cầu hoàn tiền',@noidung)",
            new { kh = don.MaKhachHang, id = maDonHang, noidung = "Kết quả: " + req.TrangThai + ". Mở chi tiết đơn để xem lý do và lịch sử xử lý." }, tx);
        await tx.CommitAsync();
        await BaoHoanTienDoiAsync(maDonHang, don.MaChiNhanh);
        await BaoDonDoiTrangThai(conn, maDonHang, "CEO đã xử lý hồ sơ hoàn tiền");
        return Ok(new { message = req.TrangThai == "DA_HOAN_TIEN" ? "Đã ghi nhận CEO chuyển hoàn tiền; giữ nguyên lịch sử hoàn tất đơn." : "Đã lưu quyết định và thông báo khách hàng." });
    }

    [HttpGet("{maDonHang:int:min(1)}/khieu-nai-hoan-tien/anh/{maAnh}")]
    [Authorize(Roles = "CUSTOMER,SUPER_ADMIN")]
    public async Task<IActionResult> XemAnhKhieuNai(int maDonHang, string maAnh)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (!Guid.TryParseExact(maAnh, "N", out _)) return BadRequest();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var don = await conn.QuerySingleOrDefaultAsync<DonKhieuNai>(SqlDonKhieuNai, new { id = maDonHang });
        if (!DuocXemKhieuNai(don) || don.YeuCauHoanTienJson == null) return NotFound();
        var anh = JsonSerializer.Deserialize<HoSoKhieuNaiDto>(don.YeuCauHoanTienJson).Anh.SingleOrDefault(x => x.MaAnh == maAnh);
        if (anh == null) return NotFound();
        try
        {
            var data = await System.IO.File.ReadAllBytesAsync(KhoAnhKhieuNai().Anh.DuongDan(maDonHang, anh), HttpContext.RequestAborted);
            if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)) != anh.Sha256)
                return Conflict(new { message = "Ảnh không khớp mã kiểm tra." });
            return File(data, anh.LoaiNoiDung);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return NotFound(); }
    }
}
