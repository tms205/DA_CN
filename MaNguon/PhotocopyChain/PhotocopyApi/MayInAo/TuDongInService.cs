using System.Data;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Hubs;
using PhotocopyShared.Constants;
using PhotocopyShared.MayInAo;

namespace PhotocopyApi.MayInAo;

/// <summary>
/// Đưa đơn VietQR đã nhận đủ tiền vào máy in bằng các bảng và thủ tục sẵn có.
/// Thanh toán luôn được ghi nhận trước; nếu chưa xếp được máy thì đơn dừng ở DA_XAC_NHAN.
/// </summary>
public sealed class TuDongInService
{
    private readonly string _chuoiKetNoi;
    private readonly IHubContext<CapNhatHub> _hub;
    private readonly ILogger<TuDongInService> _logger;
    private readonly TrangThaiMayInAoStore _trangThaiMay;

    public TuDongInService(
        IConfiguration config,
        IHubContext<CapNhatHub> hub,
        ILogger<TuDongInService> logger,
        TrangThaiMayInAoStore trangThaiMay)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain")!;
        _hub = hub;
        _logger = logger;
        _trangThaiMay = trangThaiMay;
    }

    public async Task<KetQuaXepIn> XepInSauThanhToan(int maDonHang)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = (SqlTransaction)await conn.BeginTransactionAsync();
        DonCanXepIn don = await conn.QueryFirstOrDefaultAsync<DonCanXepIn>(
            @"SELECT dh.MaDonHang, dh.MaChiNhanh, dh.MaKhachHang, dh.MaMayIn,
                     dh.TrangThai, tt.PhuongThuc, tt.TrangThai AS TrangThaiThanhToan
              FROM DonHang dh WITH (UPDLOCK, HOLDLOCK)
              JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
              WHERE dh.MaDonHang = @MaDonHang",
            new { MaDonHang = maDonHang }, giaoDich);

        if (don == null || don.PhuongThuc != "VIETQR" || don.TrangThaiThanhToan != "DA_THANH_TOAN")
            return new KetQuaXepIn(false, "Đơn chưa đủ điều kiện tự động in.");

        if (don.TrangThai is "DA_IN" or "DANG_GIAO" or "DA_GIAO" or "HOAN_THANH")
            return new KetQuaXepIn(true, "Đơn đã được đưa vào quy trình in.");
        if (don.TrangThai == "DA_HUY")
            return new KetQuaXepIn(false, "Đơn đã bị hủy.");
        if (don.TrangThai == "DANG_IN")
            return new KetQuaXepIn(true, "Đơn đã được đưa vào quy trình in.");
        if (don.TrangThai is not ("CHO_XAC_NHAN" or "DA_XAC_NHAN"))
            return new KetQuaXepIn(false, "Đơn không còn ở bước chờ in.");

        bool vuaXacNhan = don.TrangThai == "CHO_XAC_NHAN";
        if (vuaXacNhan)
        {
            await conn.ExecuteAsync(
                "UPDATE DonHang SET TrangThai='DA_XAC_NHAN' WHERE MaDonHang=@MaDonHang AND TrangThai='CHO_XAC_NHAN'",
                new { MaDonHang = maDonHang }, giaoDich);
            don.TrangThai = "DA_XAC_NHAN";
        }

        List<FileCanIn> dsFile = (await conn.QueryAsync<FileCanIn>(
            @"SELECT f.TrangThai, f.DuongDan
              FROM ChiTietDonHang ct
              JOIN FileIn f ON f.MaFile = ct.MaFile
              WHERE ct.MaDonHang = @MaDonHang",
            new { MaDonHang = maDonHang }, giaoDich)).ToList();
        bool fileSanSang = dsFile.Count > 0 && dsFile.All(file =>
            QuyTacMayInAo.TepSanSang(
                file.TrangThai,
                file.DuongDan,
                !string.IsNullOrWhiteSpace(file.DuongDan) && File.Exists(file.DuongDan)));

        if (!fileSanSang)
        {
            const string thongBao = "Đã thanh toán nhưng file in không còn trên máy chủ. Vui lòng tải lại file.";
            int soDong = await conn.ExecuteAsync(
                @"UPDATE DonHang
                  SET GhiChu = CONCAT(NULLIF(GhiChu, ''), CHAR(10), @ThongBao)
                  WHERE MaDonHang = @MaDonHang AND TrangThai='DA_XAC_NHAN'
                    AND LEN(ISNULL(GhiChu, '')) + LEN(@ThongBao) + 1 <= 255
                    AND ISNULL(GhiChu, '') NOT LIKE N'%Đã thanh toán nhưng file in không còn trên máy chủ.%'",
                new { MaDonHang = maDonHang, ThongBao = thongBao }, giaoDich);
            await giaoDich.CommitAsync();
            if (soDong > 0 || vuaXacNhan)
                await GuiCapNhat(conn, maDonHang, "Đơn đã thanh toán nhưng cần tải lại file trước khi in");
            return new KetQuaXepIn(false, "File in không còn sẵn sàng; đơn chưa được đưa vào máy in.");
        }

        int[] mayDangKetNoi = _trangThaiMay.LayTatCa()
            .Where(x => x.TrangThai is "SAN_SANG" or "DANG_IN").Select(x => x.MaMayIn).ToArray();
        int? maMayIn = mayDangKetNoi.Length == 0 ? null
            : await ChonMayIn(conn, giaoDich, maDonHang, don.MaMayIn, mayDangKetNoi);
        if (maMayIn == null)
        {
            await giaoDich.CommitAsync();
            if (vuaXacNhan) await GuiCapNhat(conn, maDonHang, "Đơn đã thanh toán, đang chờ máy in kết nối");
            return new KetQuaXepIn(false, "Đang chờ máy in phù hợp kết nối; hệ thống sẽ tự xếp lại.");
        }

        try
        {
            await conn.ExecuteAsync(
                "sp_PhanCongMayIn",
                new { MaDonHang = maDonHang, MaMayIn = maMayIn.Value },
                transaction: giaoDich,
                commandType: CommandType.StoredProcedure);

            await conn.ExecuteAsync(
                "sp_CapNhatTrangThaiIn",
                new { MaDonHang = maDonHang, TrangThaiMoi = "DANG_IN" },
                transaction: giaoDich,
                commandType: CommandType.StoredProcedure);
            await giaoDich.CommitAsync();
        }
        catch (SqlException ex)
        {
            if (giaoDich.Connection != null) await giaoDich.RollbackAsync();
            // Thanh toán đã ghi nhận; thiếu kho hoặc lỗi xếp máy vẫn phải giữ trạng thái đã xác nhận.
            await conn.ExecuteAsync("UPDATE DonHang SET TrangThai='DA_XAC_NHAN' WHERE MaDonHang=@MaDonHang AND TrangThai='CHO_XAC_NHAN'",
                new { MaDonHang = maDonHang });
            _logger.LogWarning(ex, "Không thể tự xếp máy in cho đơn {MaDonHang}.", maDonHang);
            if (vuaXacNhan) await GuiCapNhat(conn, maDonHang, "Đơn đã thanh toán nhưng chưa thể bắt đầu in");
            return new KetQuaXepIn(false, ex.Message);
        }

        await GuiCapNhat(conn, maDonHang, "Đơn đã thanh toán và đang được in tự động");
        return new KetQuaXepIn(true, "Đơn đã được chuyển sang đang in.");
    }

    private static async Task<int?> ChonMayIn(SqlConnection conn, SqlTransaction giaoDich,
        int maDonHang, int? maMayDaGan, int[] mayDangKetNoi)
    {
        const string sql = @"
            SELECT TOP 1 m.MaMayIn
            FROM MayIn m
            JOIN DonHang dh ON dh.MaChiNhanh = m.MaChiNhanh
            WHERE dh.MaDonHang = @MaDonHang
              AND m.TrangThai = N'HOAT_DONG'
              AND m.MaMayIn IN @MayDangKetNoi
              AND NOT EXISTS (
                  SELECT 1
                  FROM ChiTietDonHang ct
                  JOIN DichVu dv ON dv.MaDichVu = ct.MaDichVu
                  WHERE ct.MaDonHang = dh.MaDonHang
                    AND ((dv.KhoGiay = 'A3' AND m.KhoGiay <> 'A3')
                      OR (dv.MauSac = 'MAU' AND m.InMau = 0)
                      OR (dv.MatIn = '2_MAT' AND m.In2Mat = 0))
              )
            ORDER BY CASE WHEN m.MaMayIn = @MaMayDaGan THEN 0 ELSE 1 END,
                     (SELECT COUNT(*) FROM DonHang d WITH (READPAST)
                      WHERE d.MaMayIn = m.MaMayIn AND d.TrangThai = 'DANG_IN'),
                     m.MaMayIn";

        return await conn.ExecuteScalarAsync<int?>(sql, new
        {
            MaDonHang = maDonHang,
            MaMayDaGan = maMayDaGan,
            MayDangKetNoi = mayDangKetNoi
        }, giaoDich);
    }

    private async Task GuiCapNhat(SqlConnection conn, int maDonHang, string noiDung)
    {
        DonBaoTrangThai don = await conn.QueryFirstOrDefaultAsync<DonBaoTrangThai>(
            @"SELECT MaDonHang,
                     'DH' + RIGHT('00000' + CAST(MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     MaChiNhanh, MaKhachHang, TrangThai
              FROM DonHang WHERE MaDonHang = @MaDonHang",
            new { MaDonHang = maDonHang });
        if (don == null) return;

        var tin = new TinCapNhat
        {
            MaDonHang = don.MaDonHang,
            MaDonHangHienThi = don.MaDonHangHienThi,
            MaChiNhanh = don.MaChiNhanh,
            TrangThai = don.TrangThai,
            NoiDung = noiDung
        };
        await CapNhatHub.GuiChoChiNhanh(_hub, don.MaChiNhanh, HubEvents.OrderStatusChanged, tin);
        await CapNhatHub.GuiChoNguoiDung(_hub, don.MaKhachHang, HubEvents.OrderStatusChanged, tin);
    }

    private sealed class DonCanXepIn
    {
        public int MaDonHang { get; set; }
        public int MaChiNhanh { get; set; }
        public int MaKhachHang { get; set; }
        public int? MaMayIn { get; set; }
        public string TrangThai { get; set; } = "";
        public string PhuongThuc { get; set; } = "";
        public string TrangThaiThanhToan { get; set; } = "";
    }

    private sealed class DonBaoTrangThai
    {
        public int MaDonHang { get; set; }
        public string MaDonHangHienThi { get; set; } = "";
        public int MaChiNhanh { get; set; }
        public int MaKhachHang { get; set; }
        public string TrangThai { get; set; } = "";
    }

    private sealed class FileCanIn
    {
        public string TrangThai { get; set; } = "";
        public string DuongDan { get; set; } = "";
    }
}

public sealed record KetQuaXepIn(bool DaBatDauIn, string Message);
