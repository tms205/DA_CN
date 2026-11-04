using Dapper;
using Microsoft.Data.SqlClient;

namespace PhotocopyApi.DichVu;

public enum LyDoDonFile { ChuaDat, BoDo, DonKetThuc, ThuLai }
public sealed record KetQuaDonFile(bool DaDanhDau, bool DaXoa);

// Mark trong SQL trước, xóa vật lý sau. Crash/I/O lỗi giữ đường dẫn DA_XOA để nhịp sau thử lại.
public sealed class FileInLifecycleService
{
    public const string SqlDoc = @"SELECT f.MaFile,f.MaKhachHang,f.DuongDan,f.TrangThai,f.NgayTaiLen,
        CAST(CASE WHEN f.NgayTaiLen<DATEADD(HOUR,-1,GETDATE()) THEN 1 ELSE 0 END AS BIT) AS QuaHanBoDo,
        CAST(CASE WHEN EXISTS(SELECT 1 FROM ChiTietDonHang c WHERE c.MaFile=f.MaFile) THEN 1 ELSE 0 END AS BIT) AS CoDon,
        CAST(CASE WHEN EXISTS(SELECT 1 FROM ChiTietDonHang c JOIN DonHang d ON d.MaDonHang=c.MaDonHang
            WHERE c.MaFile=f.MaFile AND d.TrangThai NOT IN ('HOAN_THANH','DA_HUY')) THEN 1 ELSE 0 END AS BIT) AS CoDonHoatDong,
        CAST(CASE WHEN EXISTS(SELECT 1 FROM ChiTietDonHang c JOIN DonHang d ON d.MaDonHang=c.MaDonHang
            WHERE c.MaFile=f.MaFile AND d.TrangThai='DA_HUY' AND d.GhiChu=N'Quá 15 phút chưa thanh toán'
              AND d.NgayDat>=DATEADD(HOUR,-24,GETDATE())) THEN 1 ELSE 0 END AS BIT) AS GiuDoiSoat
        FROM FileIn f WITH(UPDLOCK,HOLDLOCK) WHERE f.MaFile=@Id";
    public const string SqlDanhDau = "UPDATE FileIn SET TrangThai=N'DA_XOA' WHERE MaFile=@Id AND DuongDan=@Path";
    public const string SqlBoDuongDan = "UPDATE FileIn SET DuongDan=NULL WHERE MaFile=@Id AND TrangThai=N'DA_XOA' AND DuongDan=@Path";
    private readonly string _connection, _root;
    private readonly ILogger<FileInLifecycleService> _log;
    public FileInLifecycleService(IConfiguration config, ILogger<FileInLifecycleService> log = null)
    {
        _connection = config.GetConnectionString("PhotocopyChain"); _root = config["LuuTruFile:ThuMucGoc"]; _log = log;
    }
    private sealed class FileRow
    {
        public int MaFile { get; set; }
        public int MaKhachHang { get; set; }
        public string DuongDan { get; set; }
        public string TrangThai { get; set; }
        public bool QuaHanBoDo { get; set; }
        public bool CoDon { get; set; }
        public bool CoDonHoatDong { get; set; }
        public bool GiuDoiSoat { get; set; }
    }

    public async Task<KetQuaDonFile> Don(int id, LyDoDonFile reason, int? customer = null, CancellationToken ct = default)
    {
        using var db = new SqlConnection(_connection); await db.OpenAsync(ct); string path;
        await using (var tx = (SqlTransaction)await db.BeginTransactionAsync(ct))
        {
            await KhoaGiaoDichSql.File(db, tx, id, ct);
            var file = await db.QuerySingleOrDefaultAsync<FileRow>(SqlDoc, new { Id = id }, tx);
            if (file == null || (customer.HasValue && file.MaKhachHang != customer) || file.CoDonHoatDong || file.GiuDoiSoat)
                return new(false, false);
            if (reason is LyDoDonFile.ChuaDat or LyDoDonFile.BoDo && file.CoDon) return new(false, false);
            if (reason == LyDoDonFile.BoDo && !file.QuaHanBoDo) return new(false, false);
            if (reason == LyDoDonFile.DonKetThuc && !file.CoDon) return new(false, false);
            if (reason == LyDoDonFile.ThuLai && file.TrangThai != "DA_XOA") return new(false, false);
            if (file.TrangThai is not ("SAN_SANG" or "DA_XOA")) return new(false, false);
            path = file.DuongDan;
            if (string.IsNullOrEmpty(path)) { await tx.CommitAsync(ct); return new(file.TrangThai == "DA_XOA", true); }
            if (string.IsNullOrWhiteSpace(_root) || !Path.GetFullPath(path).StartsWith(
                Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("File nằm ngoài thư mục lưu tài liệu đã cấu hình; chưa xóa.");
            await db.ExecuteAsync(SqlDanhDau, new { Id = id, Path = path }, tx); await tx.CommitAsync(ct);
        }
        try
        {
            System.IO.File.Delete(path);
            await db.ExecuteAsync(SqlBoDuongDan, new { Id = id, Path = path }); return new(true, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.LogWarning("File {MaFile} đã được đánh dấu xóa, đang chờ dọn lại ({LoaiLoi}).", id, ex.GetType().Name);
            return new(true, false);
        }
    }
    public async Task DonTheoDon(int order, CancellationToken ct = default)
    {
        try
        {
            using var db = new SqlConnection(_connection);
            var ids = await db.QueryAsync<int>("SELECT DISTINCT MaFile FROM ChiTietDonHang WHERE MaDonHang=@Order", new { Order = order });
            foreach (int id in ids) try { await Don(id, LyDoDonFile.DonKetThuc, ct: ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { _log?.LogWarning("Chưa dọn file {MaFile} của đơn {Don} ({LoaiLoi}).", id, order, ex.GetType().Name); }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { _log?.LogWarning("Đơn {Don} đã lưu; job sẽ dọn file sau ({LoaiLoi}).", order, ex.GetType().Name); }
    }
    public async Task Quet(CancellationToken ct = default)
    {
        await LuotTaiFile.DonBoDo(_root, _connection, ct);
        using var db = new SqlConnection(_connection);
        var rows = await db.QueryAsync<FileRow>(@"SELECT TOP(200) f.MaFile,f.TrangThai FROM FileIn f
            WHERE f.DuongDan IS NOT NULL
              AND NOT EXISTS(SELECT 1 FROM ChiTietDonHang c JOIN DonHang d ON d.MaDonHang=c.MaDonHang WHERE c.MaFile=f.MaFile
                  AND d.TrangThai='DA_HUY' AND d.GhiChu=N'Quá 15 phút chưa thanh toán' AND d.NgayDat>=DATEADD(HOUR,-24,GETDATE()))
              AND NOT EXISTS(SELECT 1 FROM ChiTietDonHang c JOIN DonHang d ON d.MaDonHang=c.MaDonHang WHERE c.MaFile=f.MaFile AND d.TrangThai NOT IN ('HOAN_THANH','DA_HUY'))
              AND (f.TrangThai=N'DA_XOA' OR f.NgayTaiLen<DATEADD(HOUR,-1,GETDATE())
                OR EXISTS(SELECT 1 FROM ChiTietDonHang c JOIN DonHang d ON d.MaDonHang=c.MaDonHang WHERE c.MaFile=f.MaFile AND d.TrangThai IN ('HOAN_THANH','DA_HUY')))
            ORDER BY CASE WHEN f.TrangThai=N'DA_XOA' THEN 0 ELSE 1 END,f.MaFile");
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (row.TrangThai == "DA_XOA") await Don(row.MaFile, LyDoDonFile.ThuLai, ct: ct);
                else { await Don(row.MaFile, LyDoDonFile.DonKetThuc, ct: ct); await Don(row.MaFile, LyDoDonFile.BoDo, ct: ct); }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log?.LogWarning("Chưa dọn file {MaFile} ({LoaiLoi}).", row.MaFile, ex.GetType().Name); }
        }
    }
}
