using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DonHang;

namespace PhotocopyApi.Controllers;

// Quầy đã in lại (từ lần 1, không cần chờ đủ 3 lần) mà tài liệu vẫn lỗi: nhân viên hủy đơn và lập hồ sơ hoàn tiền
// cho khách đã trả VietQR, để khỏi tốn thêm giấy mực in lại lần 2, lần 3.
// Hồ sơ dùng chung bảng khiếu nại hoàn tiền (YeuCauHoanTienJson) nên CEO duyệt + chuyển tiền + gửi biên lai
// ở đúng màn hình "Khiếu nại hoàn tiền" như các yêu cầu khác; nhân viên không tự chuyển tiền.
public partial class DonHangController
{
    private sealed class DonHoanTienInLoi
    {
        public int MaKhachHang { get; set; }
        public int MaChiNhanh { get; set; }
        public string TrangThai { get; set; }
        public string HinhThucNhan { get; set; }
        public int? MaDonHangGoc { get; set; }
        public string YeuCauHoanTienJson { get; set; }
        public string PhuongThuc { get; set; }
        public string ThanhToan { get; set; }
        public decimal SoTien { get; set; }
        public string HoTen { get; set; }
        public string TenChiNhanh { get; set; }
        public int SoLanInLai { get; set; }
        public int SoInLaiDangChay { get; set; }
    }

    // Khóa đơn gốc + thanh toán (cùng thứ tự Đơn -> Thanh toán như các chỗ hủy/hoàn khác) rồi đếm số lần in lại.
    private const string SqlDonHoanTienInLoi = @"
        SELECT dh.MaKhachHang, dh.MaChiNhanh, dh.TrangThai, dh.HinhThucNhan, dh.MaDonHangGoc, dh.YeuCauHoanTienJson,
               tt.PhuongThuc, tt.TrangThai AS ThanhToan, tt.SoTien, nd.HoTen, cn.TenChiNhanh,
               (SELECT COUNT(*) FROM DonHang il WHERE il.MaDonHangGoc = dh.MaDonHang
                  AND il.LoaiPhatSinh = 'IN_LAI_LOI_QUAY' AND il.TrangThai <> 'DA_HUY') AS SoLanInLai,
               (SELECT COUNT(*) FROM DonHang il WHERE il.MaDonHangGoc = dh.MaDonHang
                  AND il.LoaiPhatSinh = 'IN_LAI_LOI_QUAY' AND il.TrangThai NOT IN ('DA_IN','HOAN_THANH','DA_HUY')) AS SoInLaiDangChay
        FROM DonHang dh WITH (UPDLOCK, HOLDLOCK)
        JOIN ThanhToan tt WITH (UPDLOCK, HOLDLOCK) ON tt.MaDonHang = dh.MaDonHang
        JOIN NguoiDung nd ON nd.MaNguoiDung = dh.MaKhachHang
        JOIN ChiNhanh cn ON cn.MaChiNhanh = dh.MaChiNhanh
        WHERE dh.MaDonHang = @id";

    // POST api/don-hang/5/hoan-tien-in-loi
    [HttpPost("{maDonHang:int:min(1)}/hoan-tien-in-loi")]
    [Authorize(Roles = "BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> LapHoanTienInLoi(int maDonHang, HoanTienInLoiRequest req)
    {
        string nganHang = (req.NganHang ?? "").Trim();
        string soTaiKhoan = (req.SoTaiKhoan ?? "").Trim();
        string chuTaiKhoan = (req.ChuTaiKhoan ?? "").Trim();
        string ghiChu = (req.GhiChu ?? "").Trim();
        if (nganHang.Length < 2 || chuTaiKhoan.Length < 2 || soTaiKhoan.Length < 6 || !soTaiKhoan.All(char.IsAsciiDigit))
            return BadRequest(new { message = "Nhập đủ ngân hàng, số tài khoản (6–30 chữ số) và tên chủ tài khoản của khách." });

        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var don = await conn.QuerySingleOrDefaultAsync<DonHoanTienInLoi>(SqlDonHoanTienInLoi, new { id = maDonHang }, tx);
        if (don == null || !DuocLamViecVoiChiNhanh(don.MaChiNhanh))
            return NotFound(new { message = "Không tìm thấy đơn trong chi nhánh của bạn." });
        string loi = QuyTacHoanTienInLoi.LoiKhongChoHoan(don.TrangThai, don.HinhThucNhan, don.MaDonHangGoc,
            don.PhuongThuc, don.ThanhToan, don.YeuCauHoanTienJson != null, don.SoLanInLai, don.SoInLaiDangChay);
        if (loi != null) return Conflict(new { message = loi });

        var luc = DateTimeOffset.UtcNow;
        var hoSo = new HoSoKhieuNaiDto
        {
            MaDonHang = maDonHang, MaKhachHang = don.MaKhachHang, TenKhachHang = don.HoTen, TenChiNhanh = don.TenChiNhanh,
            TrangThai = "CHO_DUYET", Nguon = QuyTacHoanTienInLoi.Nguon, SoTien = don.SoTien, GuiLuc = luc,
            MoTa = $"Quầy đã in lại {don.SoLanInLai} lần nhưng tài liệu vẫn lỗi; cửa hàng hủy đơn và hoàn lại tiền đã chuyển khoản VietQR."
                   + (ghiChu.Length > 0 ? " Ghi chú của quầy: " + ghiChu : ""),
            NganHang = nganHang, SoTaiKhoan = soTaiKhoan, ChuTaiKhoan = chuTaiKhoan
        };
        // Nhật ký hủy để Web/App hiện lý do hủy giống các đơn hủy khác.
        var nhatKyHuy = new PhotocopyApi.HuyDon.HoSoHuyDon
        {
            Loai = QuyTacHoanTienInLoi.Nguon, MaHoSo = Guid.NewGuid().ToString("N"), MaDonHang = maDonHang,
            MaNguoiGui = maNhanVien, GuiLuc = luc, TrangThaiThanhToan = don.ThanhToan,
            LyDo = $"In lại {don.SoLanInLai} lần vẫn lỗi. Đơn đã hủy, chờ CEO hoàn tiền chuyển khoản cho khách."
        };

        // Lần in lại cuối (0đ) vẫn đang chờ bàn giao thì hủy theo đơn gốc, để không còn nằm ở trang Bàn giao.
        var dsInLai = (await conn.QueryAsync<int>(
            @"SELECT MaDonHang FROM DonHang WITH (UPDLOCK, HOLDLOCK)
              WHERE MaDonHangGoc = @id AND LoaiPhatSinh = 'IN_LAI_LOI_QUAY' AND TrangThai = 'DA_IN'",
            new { id = maDonHang }, tx)).ToList();

        int soDong = await conn.ExecuteAsync(
            @"UPDATE DonHang SET TrangThai = 'DA_HUY', YeuCauHoanTienJson = @hoSo, BangChungHuyJson = @huy
              WHERE MaDonHang = @id AND TrangThai = 'DA_IN'",
            new { id = maDonHang, hoSo = JsonSerializer.Serialize(hoSo), huy = JsonSerializer.Serialize(nhatKyHuy) }, tx);
        if (soDong != 1) return Conflict(new { message = "Đơn vừa thay đổi trạng thái. Tải lại trang rồi thử lại." });
        if (dsInLai.Count > 0)
            await conn.ExecuteAsync("UPDATE DonHang SET TrangThai = 'DA_HUY' WHERE MaDonHang IN @ds", new { ds = dsInLai }, tx);

        string soTienHienThi = don.SoTien.ToString("#,0", new System.Globalization.CultureInfo("vi-VN")) + "₫";
        await conn.ExecuteAsync(@"INSERT ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
            SELECT MaNguoiDung, @id, N'Hoàn tiền do in lỗi', @noiDung
            FROM NguoiDung WHERE TrangThai = N'HOAT_DONG' AND (VaiTro = 'SUPER_ADMIN' OR (VaiTro = 'BRANCH_ADMIN' AND MaChiNhanh = @cn))",
            new { id = maDonHang, cn = don.MaChiNhanh,
                  noiDung = $"Quầy in lại {don.SoLanInLai} lần vẫn lỗi nên đã hủy đơn. CEO cần duyệt và chuyển hoàn {soTienHienThi} cho khách." }, tx);
        await conn.ExecuteAsync(@"INSERT ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
            VALUES(@kh, @id, N'Đã tiếp nhận yêu cầu hoàn tiền', @noiDung)",
            new { kh = don.MaKhachHang, id = maDonHang,
                  noiDung = $"Cửa hàng in lỗi nhiều lần nên đã hủy đơn và lập hồ sơ hoàn {soTienHienThi} về tài khoản bạn cung cấp. Hồ sơ đang chờ CEO chuyển khoản." }, tx);
        await tx.CommitAsync();

        await BaoHoanTienDoiAsync(maDonHang, don.MaChiNhanh);
        await BaoDonDoiTrangThai(conn, maDonHang, "Đơn in lỗi đã hủy, chờ CEO hoàn tiền");
        foreach (int maInLai in dsInLai)
            await BaoDonDoiTrangThai(conn, maInLai, "Hủy lần in lại vì đơn gốc đã chuyển sang hoàn tiền");
        await XoaFileCuaDonDaKetThuc(conn, maDonHang);
        foreach (int maInLai in dsInLai)
            await XoaFileCuaDonDaKetThuc(conn, maInLai);

        return Ok(new { message = $"Đã hủy đơn và gửi hồ sơ hoàn {soTienHienThi} cho CEO. Tiền chỉ được chuyển khi CEO xác nhận." });
    }
}
