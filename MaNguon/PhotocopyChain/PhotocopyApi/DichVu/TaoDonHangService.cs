using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos.DonHang;

namespace PhotocopyApi.DichVu;

public sealed class YeuCauTaoDonTrungException(string message) : InvalidOperationException(message);
public sealed record KetQuaTaoDon(DatDonHangResponse Don, bool TaoMoi);

// Một transaction cho đơn, ghi chú và metadata chống gửi lặp. Không gửi sự kiện hoặc gọi cổng thanh toán trong transaction.
public sealed class TaoDonHangService
{
    public const string SqlTim = @"SELECT dh.MaDonHang,dh.TongTien,dh.DauYeuCauTao,tt.PhuongThuc AS PhuongThucThanhToan
        FROM dbo.DonHang dh JOIN dbo.ThanhToan tt ON tt.MaDonHang=dh.MaDonHang WHERE dh.MaYeuCauTao=@Key";
    public const string SqlLuu = @"UPDATE dbo.DonHang SET GhiChu=@Note,MaYeuCauTao=@Key,DauYeuCauTao=@Hash WHERE MaDonHang=@Id";
    private sealed class DonDaTao
    {
        public int MaDonHang { get; set; }
        public decimal TongTien { get; set; }
        public string DauYeuCauTao { get; set; }
        public string PhuongThucThanhToan { get; set; }
    }

    public static string DauYeuCau(int actor, string channel, int branch, object request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { actor, channel, branch, request })));

    public async Task<DatDonHangResponse> Tim(SqlConnection db, Guid? key, string hash, SqlTransaction tx = null)
    {
        if (key == Guid.Empty) throw new YeuCauTaoDonTrungException("Mã yêu cầu tạo đơn không hợp lệ.");
        if (key == null) return null; // Tương thích client cũ; client mới luôn gửi mã yêu cầu.
        var old = await db.QuerySingleOrDefaultAsync<DonDaTao>(SqlTim, new { Key = key }, tx);
        if (old == null) return null;
        if (!string.Equals(old.DauYeuCauTao, hash, StringComparison.Ordinal))
            throw new YeuCauTaoDonTrungException("Mã yêu cầu đã dùng cho nội dung khác. Kiểm tra đơn đã tạo trước khi lập đơn mới.");
        return new()
        {
            MaDonHang = old.MaDonHang,
            MaDonHangHienThi = $"DH{old.MaDonHang:D5}",
            TongTien = old.TongTien,
            PhuongThucThanhToan = old.PhuongThucThanhToan,
            Message = "Đơn đã được ghi nhận trước đó."
        };
    }

    public async Task<KetQuaTaoDon> Tao(SqlConnection db, DynamicParameters args, Guid? key, string hash,
        int customer, IEnumerable<int> files, string note, CancellationToken ct = default)
    {
        if (db.State != ConnectionState.Open) await db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(ct);
        if (key != null)
        {
            await KhoaGiaoDichSql.Lay(db, tx, "PhotocopyChain:CreateOrder:" + key.Value.ToString("N"), ct);
            var previous = await Tim(db, key, hash, tx);
            if (previous != null)
            {
                await tx.CommitAsync(ct);
                return new(previous, false);
            }
        }
        foreach (int id in files.Distinct().OrderBy(x => x))
        {
            await KhoaGiaoDichSql.File(db, tx, id, ct);
            var file = await db.QuerySingleOrDefaultAsync<FileCanTao>(@"SELECT DuongDan,TrangThai FROM dbo.FileIn WITH(UPDLOCK,HOLDLOCK)
                WHERE MaFile=@Id AND MaKhachHang=@Customer", new { Id = id, Customer = customer }, tx);
            if (file?.TrangThai != "SAN_SANG" || string.IsNullOrWhiteSpace(file.DuongDan) || !System.IO.File.Exists(file.DuongDan))
                throw new InvalidOperationException("File đã được xóa hoặc không còn sẵn sàng. Vui lòng tải lại trước khi tạo đơn.");
        }
        await db.ExecuteAsync(new CommandDefinition("sp_TaoDonHang", args, tx, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        int order = args.Get<int>("@MaDonHang");
        if (key.HasValue) await db.ExecuteAsync(SqlLuu, new { Id = order, Note = note, Key = key, Hash = hash }, tx);
        else if (note != null) await db.ExecuteAsync("UPDATE dbo.DonHang SET GhiChu=@Note WHERE MaDonHang=@Id", new { Id = order, Note = note }, tx);
        var response = await db.QuerySingleAsync<DatDonHangResponse>(@"SELECT dh.MaDonHang,'DH'+RIGHT('00000'+CAST(dh.MaDonHang AS VARCHAR(10)),5) AS MaDonHangHienThi,
            dh.TongTien,tt.PhuongThuc AS PhuongThucThanhToan FROM dbo.DonHang dh JOIN dbo.ThanhToan tt ON tt.MaDonHang=dh.MaDonHang WHERE dh.MaDonHang=@Id", new { Id = order }, tx);
        await tx.CommitAsync(ct);
        return new(response, true);
    }
    private sealed class FileCanTao
    {
        public string DuongDan { get; set; }
        public string TrangThai { get; set; }
    }
}
