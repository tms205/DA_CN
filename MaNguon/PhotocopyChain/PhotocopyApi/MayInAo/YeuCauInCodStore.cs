using System.Text.Json;

namespace PhotocopyApi.MayInAo;

/// <summary>Chỉ nhận COD đã được quầy gửi rõ ràng; không tự in lại các đơn DANG_IN từ phiên bản thủ công cũ.</summary>
public sealed class YeuCauInCodStore
{
    private readonly object _khoa = new();
    private Dictionary<int, int> _ds = new();
    private readonly string _tep;
    public YeuCauInCodStore(string thuMuc = null)
    {
        if (thuMuc == null) return;
        Directory.CreateDirectory(thuMuc);
        _tep = Path.Combine(thuMuc, "yeu-cau-in-cod.json");
        if (File.Exists(_tep)) _ds = JsonSerializer.Deserialize<Dictionary<int, int>>(File.ReadAllText(_tep))
            ?? throw new InvalidDataException("File yêu cầu in COD bị hỏng; không tự cấp lại lệnh.");
    }

    public bool DangKy(int don, int may)
    {
        if (don <= 0 || may <= 0) return false;
        lock (_khoa)
        {
            if (_ds.TryGetValue(don, out int cu)) return cu == may;
            var moi = new Dictionary<int, int>(_ds) { [don] = may };
            if (_tep != null)
            {
                using (var file = new FileStream(_tep + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                { JsonSerializer.Serialize(file, moi); file.Flush(true); }
                File.Move(_tep + ".tmp", _tep, true);
            }
            _ds = moi;
            return true;
        }
    }
    public bool DaDangKy(int don, int may) { lock (_khoa) return _ds.TryGetValue(don, out int m) && m == may; }
    public int[] DonChoMay(int may) { lock (_khoa) return _ds.Where(x => x.Value == may).Select(x => x.Key).ToArray(); }
}
