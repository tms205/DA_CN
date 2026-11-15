using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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

    // PowerPoint không công bố HWND qua COM trên một số phiên bản.
    // Tên ngẫu nhiên của tài liệu vừa tạo xác định cửa sổ; NhanCuaSo vẫn kiểm PID/tuổi tiến trình.
    public bool NhanCuaSoTheoTieuDe(string tenDuyNhat)
    {
        if (string.IsNullOrWhiteSpace(tenDuyNhat)) throw new ArgumentException("Thiếu tên cửa sổ riêng.");
        for (int lan = 0; lan < 20 && !SoHuu; lan++)
        {
            bool timThay = false;
            EnumWindows((hwnd, _) =>
            {
                var title = new StringBuilder(1024);
                GetWindowText(hwnd, title, title.Capacity);
                if (!title.ToString().Contains(tenDuyNhat, StringComparison.Ordinal)) return true;
                timThay = true;
                NhanCuaSo(hwnd.ToInt64());
                return false;
            }, IntPtr.Zero);
            if (timThay) return true;
            Thread.Sleep(50);
        }
        return false;
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
    private delegate bool EnumWindowProc(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
}
