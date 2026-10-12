using Dapper;
using Microsoft.Data.SqlClient;

namespace PhotocopyApi;

// mỗi phút hủy đơn VietQR quá hạn và dọn file của mọi đơn đã kết thúc.
// File chỉ bị xóa khi đơn HOAN_THANH / DA_HUY và không còn đơn đang xử lý nào khác dùng file đó.
// Đơn VietQR tự hủy do quá hạn được giữ file thêm 24 giờ để còn đối soát khi webhook tới muộn.
//
// Đây KHÔNG phải lớp Service kiểu kiến trúc nhiều tầng — chỉ là "hẹn giờ chạy ngầm" mà ASP.NET Core bắt buộc
// phải viết thành class (đăng ký bằng AddHostedService trong Program.cs).
public class DonQuaHanJob : BackgroundService
{
    private readonly string _chuoiKetNoi;
    private readonly ILogger<DonQuaHanJob> _logger;

    public DonQuaHanJob(IConfiguration config, ILogger<DonQuaHanJob> logger)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
        _logger = logger;
    }

    // Hàm này chạy suốt từ lúc API bật tới lúc tắt
    protected override async Task ExecuteAsync(CancellationToken dungLai)
    {
        while (!dungLai.IsCancellationRequested)
        {
            try
            {
                using var conn = new SqlConnection(_chuoiKetNoi);
                await conn.ExecuteAsync("sp_HuyDonQuaHan", commandType: System.Data.CommandType.StoredProcedure);
                await XoaFileCuaDonDaKetThuc(conn);
                await XoaFileTaiLenBiBoDo(conn);
            }
            catch (Exception ex)
            {
                // Mất kết nối CSDL thì chỉ ghi ra cửa sổ console rồi phút sau thử lại, không được làm sập API
                _logger.LogWarning("Không chạy được job hủy đơn quá hạn / dọn file: {LoiChiTiet}", ex.Message);
            }

            // Nghỉ 1 phút rồi chạy tiếp. Lúc tắt API thì Task.Delay bị hủy → thoát vòng lặp.
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), dungLai);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    // File tải lên nhưng không gắn vào đơn nào (đóng trang, đổi khách, mất mạng...)
    // được giữ 1 giờ để người dùng còn thời gian thao tác, sau đó tự dọn.
    private async Task XoaFileTaiLenBiBoDo(SqlConnection conn)
    {
        var dsFile = await conn.QueryAsync<FileCanXoa>(
            @"SELECT f.MaFile, f.DuongDan FROM FileIn f
              WHERE f.TrangThai = N'SAN_SANG' AND f.DuongDan IS NOT NULL
                AND f.NgayTaiLen < DATEADD(HOUR, -1, GETDATE())
                AND NOT EXISTS (SELECT 1 FROM ChiTietDonHang ct WHERE ct.MaFile = f.MaFile)");

        foreach (FileCanXoa file in dsFile)
        {
            try
            {
                if (File.Exists(file.DuongDan)) File.Delete(file.DuongDan);
                if (!File.Exists(file.DuongDan))
                    await conn.ExecuteAsync(
                        "UPDATE FileIn SET TrangThai = N'DA_XOA', DuongDan = NULL WHERE MaFile = @MaFile",
                        new { file.MaFile });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Chưa xóa được file tải lên bị bỏ dở {DuongDan}: {LoiChiTiet}", file.DuongDan, ex.Message);
            }
        }
    }

    private async Task XoaFileCuaDonDaKetThuc(SqlConnection conn)
    {
        // Bước 1: lấy các file chỉ còn thuộc đơn đã hoàn tất / đã hủy.
        // Nếu cùng một file vẫn đang được một đơn khác sử dụng thì chưa xóa.
        var dsFile = await conn.QueryAsync<FileCanXoa>(
            @"SELECT f.MaFile, f.DuongDan
              FROM FileIn f
              WHERE f.TrangThai = N'SAN_SANG' AND f.DuongDan IS NOT NULL
                AND EXISTS (
                    SELECT 1 FROM ChiTietDonHang ct JOIN DonHang dh ON dh.MaDonHang = ct.MaDonHang
                    WHERE ct.MaFile = f.MaFile AND dh.TrangThai IN ('HOAN_THANH','DA_HUY')
                )
                AND NOT EXISTS (
                    SELECT 1 FROM ChiTietDonHang ct JOIN DonHang dh ON dh.MaDonHang = ct.MaDonHang
                    WHERE ct.MaFile = f.MaFile AND dh.TrangThai NOT IN ('HOAN_THANH','DA_HUY')
                )
                AND NOT EXISTS (
                    SELECT 1 FROM ChiTietDonHang ct JOIN DonHang dh ON dh.MaDonHang = ct.MaDonHang
                    WHERE ct.MaFile = f.MaFile AND dh.TrangThai = 'DA_HUY'
                      AND dh.GhiChu = N'Quá 15 phút chưa thanh toán'
                      AND dh.NgayDat >= DATEADD(HOUR, -24, GETDATE())
                )");

        foreach (FileCanXoa file in dsFile)
        {
            try
            {
                // Bước 2: xóa file PDF thật trên ổ D.
                if (File.Exists(file.DuongDan)) File.Delete(file.DuongDan);

                // Bước 3: file đã mất khỏi ổ đĩa thì bỏ đường dẫn, nhưng vẫn giữ tên + số trang để tra cứu.
                await conn.ExecuteAsync(
                    "UPDATE FileIn SET TrangThai = N'DA_XOA', DuongDan = NULL WHERE MaFile = @MaFile",
                    new { file.MaFile });
            }
            catch (Exception ex)
            {
                // Không xóa được (file đang bị chương trình in giữ...) thì giữ nguyên đường dẫn để phút sau thử lại.
                _logger.LogWarning("Chưa xóa được file {DuongDan}: {LoiChiTiet}", file.DuongDan, ex.Message);
            }
        }
    }

    private class FileCanXoa
    {
        public int MaFile { get; set; }
        public string DuongDan { get; set; }
    }
}
