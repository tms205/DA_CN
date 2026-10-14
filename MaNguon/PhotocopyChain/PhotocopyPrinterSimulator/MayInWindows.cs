using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyPrinterSimulator;

public class MayInWindows
{
    public static string[] TaoThamSo(string tenMay, string tep, DongLenhInDto dong)
    {
        if (string.IsNullOrWhiteSpace(tenMay) || dong.KhoGiay is not ("A4" or "A3")
            || dong.MatIn is not ("1_MAT" or "2_MAT") || dong.MauSac is not ("MAU" or "DEN_TRANG")
            || dong.HuongGiay is not ("DUNG" or "NGANG") || dong.SoBan < 1 || dong.SoTrang < 1)
            throw new InvalidOperationException("Tên queue hoặc quy cách máy thật không hợp lệ.");
        if (dong.TuTrang.HasValue != dong.DenTrang.HasValue || dong.TuTrang < 1 || dong.DenTrang < dong.TuTrang
            || (dong.TuTrang is int tu && dong.DenTrang is int den && (long)den - tu + 1 != dong.SoTrang))
            throw new InvalidOperationException("Phạm vi trang không hợp lệ.");
        var options = new List<string>();
        if (dong.TuTrang != null) options.Add($"{dong.TuTrang}-{dong.DenTrang}");
        options.AddRange(new[] { $"{dong.SoBan}x", "paper=" + dong.KhoGiay,
            dong.MauSac == "MAU" ? "color" : "monochrome", dong.MatIn == "2_MAT" ? "duplexlong" : "simplex",
            dong.HuongGiay == "NGANG" ? "landscape" : "portrait", "noscale", "collate" });
        return new[] { "-silent", "-print-to", tenMay, "-print-settings", string.Join(',', options), tep };
    }

    public virtual void KiemTraCauHinh(string engine, string tenMay)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Máy thật cần connector Windows.");
        if (!Path.IsPathFullyQualified(engine) || !File.Exists(engine)
            || !Path.GetFileName(engine).Equals("SumatraPDF.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cấu hình đường dẫn tuyệt đối tới SumatraPDF.exe trước khi bật chế độ máy thật.");
        if (string.IsNullOrWhiteSpace(tenMay)) throw new InvalidOperationException("Chưa cấu hình tên hàng đợi Windows.");
        _ = DocJob(tenMay);
    }

    public virtual async Task GuiPdf(string engine, string tenMay, string tep, DongLenhInDto dong, CancellationToken dung)
    {
        KiemTraCauHinh(engine, tenMay);
        var start = new ProcessStartInfo(engine) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (string arg in TaoThamSo(tenMay, tep, dong)) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Không mở được bộ in PDF.");
        using var han = CancellationTokenSource.CreateLinkedTokenSource(dung);
        han.CancelAfter(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(han.Token); }
        catch (OperationCanceledException)
        {
            // Chỉ dừng bộ gửi do mình tạo, không xóa job đã vào spooler.
            if (!process.HasExited) process.Kill();
            throw;
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Bộ in PDF trả mã lỗi {process.ExitCode}. Có thể đã gửi một phần; kiểm tra giấy và queue, không tự gửi lại.");
    }

    public virtual List<JobWindows> DocJob(string tenMay)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Hàng đợi in yêu cầu Windows.");
        if (!OpenPrinter(tenMay, out var handle, IntPtr.Zero)) throw LoiWindows();
        try
        {
            bool ok = EnumJobs(handle, 0, 1024, 1, IntPtr.Zero, 0, out uint size, out _);
            if (!ok && Marshal.GetLastWin32Error() != 122) throw LoiWindows();
            if (size == 0) return new();
            var buffer = Marshal.AllocHGlobal(checked((int)size));
            try
            {
                if (!EnumJobs(handle, 0, 1024, 1, buffer, size, out _, out uint count)) throw LoiWindows();
                var list = new List<JobWindows>();
                for (int i = 0; i < count; i++)
                {
                    var j = Marshal.PtrToStructure<JobInfo1>(IntPtr.Add(buffer, i * Marshal.SizeOf<JobInfo1>()));
                    list.Add(new() { Id = j.JobId, TenTaiLieu = Marshal.PtrToStringUni(j.Document) ?? "", Status = j.Status, TrangDaIn = (int)j.PagesPrinted });
                }
                return list;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { ClosePrinter(handle); }
    }

    public virtual void TamDungJob(string tenMay, JobWindows job, bool pause)
    {
        if (!OpenPrinter(tenMay, out var handle, IntPtr.Zero)) throw LoiWindows();
        try
        {
            // Kiểm tra lại ID lẫn document để không đụng job khác nếu queue tái sử dụng ID.
            if (!DocJob(tenMay).Any(x => x.Id == job.Id && x.TenTaiLieu == job.TenTaiLieu)) return;
            if (!SetJob(handle, job.Id, 0, IntPtr.Zero, pause ? 1u : 2u)) throw LoiWindows();
        }
        finally { ClosePrinter(handle); }
    }

    public static bool ThuocTep(JobWindows job, string tep) =>
        string.Equals(job.TenTaiLieu, Path.GetFileName(tep), StringComparison.OrdinalIgnoreCase)
        || string.Equals(job.TenTaiLieu, tep, StringComparison.OrdinalIgnoreCase);

    private static Win32Exception LoiWindows() => new(Marshal.GetLastWin32Error(), "Không truy cập được hàng đợi in. Kiểm tra Spooler, driver, tên queue và quyền Windows.");

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime { public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds; }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobInfo1
    {
        public uint JobId;
        public IntPtr PrinterName, MachineName, UserName, Document, DataType, StatusText;
        public uint Status, Priority, Position, TotalPages, PagesPrinted;
        public SystemTime Submitted;
    }
    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);
    [DllImport("winspool.drv", EntryPoint = "EnumJobsW", SetLastError = true)]
    private static extern bool EnumJobs(IntPtr handle, uint first, uint jobs, uint level, IntPtr buffer, uint size, out uint needed, out uint count);
    [DllImport("winspool.drv", EntryPoint = "SetJobW", SetLastError = true)]
    private static extern bool SetJob(IntPtr handle, uint job, uint level, IntPtr info, uint command);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr handle);
}

public sealed class JobWindows
{
    public uint Id { get; set; }
    public string TenTaiLieu { get; set; } = "";
    public uint Status { get; set; }
    public int TrangDaIn { get; set; }
}
