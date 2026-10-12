using System.Text.Json;

namespace PhotocopyApi.MayInAo;

// Không đổi schema: lưu chủ sở hữu lệnh ở ngoài source và khóa file khi đọc/ghi.
// Không tự hết hạn lệnh đang in: mất kết nối không có nghĩa là máy đã ngừng in.
public sealed class QuyenNhanLenhInStore
{
    private readonly string _thuMuc;

    public QuyenNhanLenhInStore(string thuMuc)
    {
        _thuMuc = Path.GetFullPath(thuMuc);
        Directory.CreateDirectory(_thuMuc);
    }

    public QuyenNhanLenhIn LayLenhDangIn(int maMayIn)
    {
        using var khoa = KhoaMay(maMayIn);
        var lenh = DocLenh(maMayIn);
        return lenh != null && !lenh.DaHoanTat ? lenh : null;
    }

    public QuyenNhanLenhIn NhanLenh(int maMayIn, int maDonHang, string connectorId)
    {
        if (maMayIn <= 0 || maDonHang <= 0 || string.IsNullOrWhiteSpace(connectorId)) return null;
        using var khoa = KhoaMay(maMayIn);
        var lenh = DocLenh(maMayIn);
        if (lenh != null && !lenh.DaHoanTat)
            return lenh.MaDonHang == maDonHang && lenh.ConnectorId == connectorId ? lenh : null;
        if (lenh?.MaDonHang == maDonHang) return null;

        lenh = new QuyenNhanLenhIn
        {
            MaDonHang = maDonHang, ConnectorId = connectorId,
            MaLenh = Guid.NewGuid().ToString("N")
        };
        LuuLenh(maMayIn, lenh);
        return lenh;
    }

    public bool CoQuyen(int maMayIn, int maDonHang, string connectorId, string maLenh)
    {
        using var khoa = KhoaMay(maMayIn);
        return DungChuSoHuu(DocLenh(maMayIn), maDonHang, connectorId, maLenh);
    }

    public bool HoanTat(int maMayIn, int maDonHang, string connectorId, string maLenh)
    {
        using var khoa = KhoaMay(maMayIn);
        var lenh = DocLenh(maMayIn);
        if (!DungChuSoHuu(lenh, maDonHang, connectorId, maLenh)) return false;
        lenh.DaHoanTat = true;
        LuuLenh(maMayIn, lenh);
        return true;
    }

    private static bool DungChuSoHuu(QuyenNhanLenhIn lenh, int maDonHang, string connectorId, string maLenh)
    {
        return lenh != null && !string.IsNullOrWhiteSpace(maLenh)
            && lenh.MaDonHang == maDonHang && lenh.ConnectorId == connectorId && lenh.MaLenh == maLenh;
    }

    private FileStream KhoaMay(int maMayIn)
    {
        string tepKhoa = Path.Combine(_thuMuc, $"may-{maMayIn}.lock");
        for (int lan = 0; ; lan++)
        {
            try { return new FileStream(tepKhoa, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (lan < 100) { Thread.Sleep(20); }
        }
    }

    private QuyenNhanLenhIn DocLenh(int maMayIn)
    {
        string tep = Path.Combine(_thuMuc, $"may-{maMayIn}.json");
        if (!File.Exists(tep)) return null;
        // File hỏng phải dừng nhận việc, không được âm thầm cấp quyền lại và in trùng.
        return JsonSerializer.Deserialize<QuyenNhanLenhIn>(File.ReadAllText(tep))
            ?? throw new InvalidDataException("File quyền nhận lệnh không hợp lệ.");
    }

    private void LuuLenh(int maMayIn, QuyenNhanLenhIn lenh)
    {
        string tep = Path.Combine(_thuMuc, $"may-{maMayIn}.json");
        string tam = tep + ".tmp";
        using (var file = new FileStream(tam, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, lenh);
            file.Flush(flushToDisk: true);
        }
        File.Move(tam, tep, overwrite: true);
    }
}

public sealed class QuyenNhanLenhIn
{
    public int MaDonHang { get; set; }
    public string ConnectorId { get; set; } = "";
    public string MaLenh { get; set; } = "";
    public bool DaHoanTat { get; set; }
}
