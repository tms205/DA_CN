using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhotocopyApi.DichVu;

// Quyền kết thúc tiến trình chỉ được cấp từ HWND của đúng COM instance mới.
public sealed class TienTrinhOffice
{
    private readonly object _khoa = new();
    private readonly HashSet<int> _daCo;
    private readonly string _ten;
    private readonly DateTime _batDau = DateTime.UtcNow;
    private int? _pid;
    private DateTime _gioTao;
    public bool SoHuu { get { lock (_khoa) return _pid != null; } }

    public TienTrinhOffice(string loai)
    {
        _ten = loai == "Word" ? "WINWORD" : loai == "Excel" ? "EXCEL" : "POWERPNT";
        _daCo = new();
        foreach (var p in Process.GetProcessesByName(_ten))
        { using (p) _daCo.Add(p.Id); }
    }

    public static bool CoTheNhan(int pid, bool daTonTai, string tenThuc, string tenCan, DateTime gioTao, DateTime batDau) =>
        pid > 0 && !daTonTai && string.Equals(tenThuc, tenCan, StringComparison.OrdinalIgnoreCase) && gioTao >= batDau.AddSeconds(-1);

    public void NhanCuaSo(long hwnd)
    {
        if (hwnd == 0) return;
        GetWindowThreadProcessId(new IntPtr(hwnd), out uint pid);
        if (pid == 0) return;
        using var process = Process.GetProcessById(checked((int)pid));
        DateTime start = process.StartTime.ToUniversalTime();
        if (!CoTheNhan(process.Id, _daCo.Contains(process.Id), process.ProcessName, _ten, start, _batDau)) return;
        lock (_khoa) { _pid = process.Id; _gioTao = start; }
    }

    public void TatNeuSoHuu()
    {
        lock (_khoa)
        {
            if (_pid == null) return;
            try
            {
                using var process = Process.GetProcessById(_pid.Value);
                if (!process.HasExited && process.StartTime.ToUniversalTime() == _gioTao &&
                    string.Equals(process.ProcessName, _ten, StringComparison.OrdinalIgnoreCase)) process.Kill();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
}
