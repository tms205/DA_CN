using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyPrinterSimulator;

// Driver IPP/v4 có thể không trả DC_PAPERS. PrintTicket chỉ đọc năng lực,
// không tạo job và không thay đổi cấu hình máy. Provider luôn dùng trên cùng thread.
public static class NangLucPrintTicket
{
    private const int GioiHanXml = 4 * 1024 * 1024;
    private static readonly XNamespace Framework = "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework";
    private static readonly XNamespace Keywords = "http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords";

    public static HangDoiWindowsDto DocXml(string xml)
    {
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = GioiHanXml });
        var doc = XDocument.Load(reader);
        List<string>? Options(params string[] names)
        {
            var features = doc.Descendants(Framework + "Feature").Where(x => names.Contains(Ten(x))).ToList();
            return features.Count == 0 ? null : features.SelectMany(x => x.Elements(Framework + "Option"))
                .Select(Ten).Where(x => x != null).Select(x => x!).Distinct().ToList();
        }
        var papers = Options("PageMediaSize");
        var color = Options("PageOutputColor");
        var duplex = Options("JobDuplexAllDocumentsContiguously", "DocumentDuplex");
        return new() {
            KhoGiayHoTro = papers == null ? null : papers.Where(x => x is "ISOA3" or "ISOA4")
                .Select(x => x == "ISOA3" ? "A3" : "A4").Distinct().Order().ToList(),
            InMau = color == null || color.Count == 0 ? null : color.Contains("Color"),
            InHaiMat = duplex == null || duplex.Count == 0 ? null : duplex.Any(x => x is "TwoSidedLongEdge" or "TwoSidedShortEdge")
        };
    }

    private static string? Ten(XElement element)
    {
        string[] parts = ((string?)element.Attribute("name") ?? "").Split(':', 2);
        var ns = parts.Length == 2 ? element.GetNamespaceOfPrefix(parts[0]) : element.GetDefaultNamespace();
        return ns == Keywords ? parts[^1] : null;
    }

    public static HangDoiWindowsDto? Doc(string tenMay)
    {
        if (!OperatingSystem.IsWindows()) return null;
        IntPtr provider = IntPtr.Zero;
        IStream? stream = null;
        int init = CoInitializeEx(IntPtr.Zero, 0);
        try
        {
            if (PTOpenProvider(tenMay, 1, out provider) < 0) return null;
            if (CreateStreamOnHGlobal(IntPtr.Zero, true, out stream) < 0) return null;
            if (PTGetPrintCapabilities(provider, IntPtr.Zero, stream, out _) < 0) return null;
            stream.Stat(out var stat, 1);
            if (stat.cbSize <= 0 || stat.cbSize > GioiHanXml) return null;
            byte[] bytes = new byte[(int)stat.cbSize];
            stream.Seek(0, 0, IntPtr.Zero);
            IntPtr read = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                stream.Read(bytes, bytes.Length, read);
                if (Marshal.ReadInt32(read) != bytes.Length) return null;
            }
            finally { Marshal.FreeHGlobal(read); }
            // XML reader phát hiện BOM/encoding do driver khai báo.
            using var buffer = new MemoryStream(bytes);
            using var reader = new StreamReader(buffer, Encoding.UTF8, true);
            return DocXml(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is COMException or XmlException or DllNotFoundException or EntryPointNotFoundException)
        { return null; } // Không đoán năng lực khi Windows/driver không cung cấp.
        finally
        {
            if (stream != null) Marshal.ReleaseComObject(stream);
            if (provider != IntPtr.Zero) PTCloseProvider(provider);
            if (init >= 0) CoUninitialize();
        }
    }

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CreateStreamOnHGlobal(IntPtr memory, [MarshalAs(UnmanagedType.Bool)] bool free, out IStream stream);
    [DllImport("prntvpt.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int PTOpenProvider(string printer, uint version, out IntPtr provider);
    [DllImport("prntvpt.dll", ExactSpelling = true)] private static extern int PTCloseProvider(IntPtr provider);
    [DllImport("prntvpt.dll", ExactSpelling = true)]
    private static extern int PTGetPrintCapabilities(IntPtr provider, IntPtr ticket, IStream capabilities, [MarshalAs(UnmanagedType.BStr)] out string error);
}
