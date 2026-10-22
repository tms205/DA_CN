using System.ComponentModel;
using System.Runtime.InteropServices;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyPrinterSimulator;

/// <summary>Liệt kê queue đã cài cho tài khoản Windows hiện tại, không quét IP tùy ý trong LAN.</summary>
public class PhatHienMayInWindows
{
    public virtual List<HangDoiWindowsDto> Quet()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Phát hiện queue yêu cầu Windows.");
        bool ok = EnumPrinters(2 | 4, null, 2, IntPtr.Zero, 0, out uint size, out _);
        if (!ok && Marshal.GetLastWin32Error() != 122) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (size == 0) return new();
        var buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            if (!EnumPrinters(2 | 4, null, 2, buffer, size, out _, out uint count))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var ds = new List<HangDoiWindowsDto>();
            for (int i = 0; i < count; i++)
            {
                var p = Marshal.PtrToStructure<PrinterInfo2>(IntPtr.Add(buffer, checked(i * Marshal.SizeOf<PrinterInfo2>())));
                string ten = Chu(p.PrinterName), driver = Chu(p.DriverName), port = Chu(p.PortName);
                ds.Add(new() { Ten = ten, Driver = driver, Cong = port, TrangThaiDriver = p.Status,
                    SoJob = checked((int)p.Jobs), LaMayAo = LaQueueAo(ten, driver, port),
                    InMau = DocNangLuc(ten, port, 32), InHaiMat = DocNangLuc(ten, port, 7) });
            }
            return ds.OrderBy(x => x.Ten, StringComparer.OrdinalIgnoreCase).ToList();
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    public static bool LaQueueAo(string ten, string driver, string port)
    {
        string text = $"{ten} {driver} {port}".ToUpperInvariant();
        return new[] { "PRINT TO PDF", "MICROSOFT XPS", "ONENOTE", "FAX", "PORTPROMPT:", "FILE:", "PDFCREATOR", "PDF24" }
            .Any(text.Contains);
    }

    private static bool? DocNangLuc(string ten, string port, ushort loai)
    {
        int kq = DeviceCapabilities(ten, port, loai, IntPtr.Zero, IntPtr.Zero);
        return kq is 0 or 1 ? kq == 1 : null;
    }
    private static string Chu(IntPtr p) => Marshal.PtrToStringUni(p) ?? "";
    [StructLayout(LayoutKind.Sequential)]
    private struct PrinterInfo2
    {
        public IntPtr ServerName, PrinterName, ShareName, PortName, DriverName, Comment, Location,
            DevMode, SeparatorFile, PrintProcessor, DataType, Parameters, SecurityDescriptor;
        public uint Attributes, Priority, DefaultPriority, StartTime, UntilTime, Status, Jobs, AveragePpm;
    }
    [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumPrinters(uint flags, string? name, uint level, IntPtr buffer, uint size, out uint needed, out uint count);
    [DllImport("winspool.drv", EntryPoint = "DeviceCapabilitiesW", CharSet = CharSet.Unicode)]
    private static extern int DeviceCapabilities(string device, string port, ushort capability, IntPtr output, IntPtr devMode);
}
