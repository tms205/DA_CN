using Dapper;
using Microsoft.Data.SqlClient;

namespace PhotocopyApi.DichVu;

// Marker sống qua crash. Khóa handle ngăn job dọn lượt upload đang chuyển đổi/ghi SQL.
public sealed class LuotTaiFile : IDisposable
{
    public const string TenMarker = ".upload-pending";
    private readonly string _marker;
    private FileStream _lease;
    public LuotTaiFile(string directory)
    {
        _marker = Path.Combine(directory, TenMarker);
        _lease = new FileStream(_marker, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    }
    public void HoanTat() { Dispose(); System.IO.File.Delete(_marker); }
    public void Dispose() { _lease?.Dispose(); _lease = null; }

    public static async Task DonBoDo(string root, string connection, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;
        using var db = new SqlConnection(connection);
        // Chỉ định dạng thư mục upload yyyy/MM/GUID, không duyệt/xóa thư mục tùy ý.
        foreach (var year in Directory.EnumerateDirectories(root).Where(x => Path.GetFileName(x).Length == 4 && int.TryParse(Path.GetFileName(x), out _)))
            foreach (var month in Directory.EnumerateDirectories(year).Where(x => int.TryParse(Path.GetFileName(x), out int m) && m is >= 1 and <= 12))
                foreach (var dir in Directory.EnumerateDirectories(month).Where(x => Guid.TryParseExact(Path.GetFileName(x), "N", out _)))
                {
                    ct.ThrowIfCancellationRequested();
                    string marker = Path.Combine(dir, TenMarker);
                    if (!System.IO.File.Exists(marker) || System.IO.File.GetLastWriteTimeUtc(marker) > DateTime.UtcNow.AddHours(-1)) continue;
                    try
                    {
                        using (var lease = new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            var files = Directory.GetFiles(dir, "tai-lieu.*", SearchOption.TopDirectoryOnly);
                            // SQL lỗi/không xác định kết quả => giữ file. Không đoán INSERT đã rollback.
                            int used = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                                "SELECT COUNT(*) FROM FileIn WHERE DuongDan IN @Paths", new { Paths = files }, cancellationToken: ct));
                            if (used == 0) foreach (string file in files) System.IO.File.Delete(file);
                        }
                        System.IO.File.Delete(marker);
                        if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir, false);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Còn khóa: nhịp sau thử lại. */ }
                }
    }
}
