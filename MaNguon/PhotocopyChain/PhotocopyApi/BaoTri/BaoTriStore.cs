using System.Text.Json;
namespace PhotocopyApi.BaoTri;
public record TrangThaiBaoTri(bool DangBaoTri = false, string LoiNhan = "", long PhienBanPhien = 0);
public sealed class BaoTriStore
{
    private readonly object _lock = new();
    private readonly string _file;
    private TrangThaiBaoTri _state;
    public BaoTriStore(string file)
    {
        _file = file;
        _state = File.Exists(file) ? JsonSerializer.Deserialize<TrangThaiBaoTri>(File.ReadAllText(file)) ?? throw new InvalidDataException("Trạng thái bảo trì không hợp lệ.") : new();
    }
    public static bool SessionValid(long issued, long current, bool ceo) => ceo || issued == current;
    public TrangThaiBaoTri Read() { lock (_lock) return _state; }
    public TrangThaiBaoTri Set(bool enabled, string message)
    {
        message = (message ?? "").Trim();
        if (enabled && (message.Length == 0 || message.Length > 500)) throw new ArgumentException("Lời nhắn phải có từ 1 đến 500 ký tự.");
        lock (_lock)
        {
            var next = new TrangThaiBaoTri(enabled, enabled ? message : _state.LoiNhan,
                _state.PhienBanPhien + (enabled && !_state.DangBaoTri ? 1 : 0));
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            var temp = _file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(next));
            File.Move(temp, _file, true);
            return _state = next;
        }
    }
}
