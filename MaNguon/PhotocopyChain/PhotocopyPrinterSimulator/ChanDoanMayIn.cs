using System.Text;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyPrinterSimulator;

public static class ChanDoanMayIn
{
    // Tài liệu cố định, không đọc dữ liệu đơn/khách hàng, không tạo thanh toán hoặc tăng counter.
    public static byte[] TaoPdfThu()
    {
        const string content = "BT /F1 20 Tf 60 770 Td (PHOTOCOPYCHAIN - TEST PAGE) Tj "
            + "0 -45 Td /F1 12 Tf (A4 / Portrait / One-sided / Monochrome / One copy) Tj "
            + "0 -28 Td (This is a connector test, NOT a customer order.) Tj "
            + "0 -28 Td (Check paper size, orientation, text and printer queue.) Tj "
            + "0 -28 Td (Spooler success does not prove physical printing.) Tj ET\n";
        string[] objects = {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Length; i++)
        { offsets.Add(pdf.Length); pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets) pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture) + " 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    public static async Task<int> Chay(string[] args, CauHinhSimulator config)
    {
        try
        {
            if (args.Contains("--tao-pdf-thu"))
            {
                Console.WriteLine("Đã tạo PDF thử, KHÔNG gửi lệnh in: " + await LuuPdfThu(config));
                return 0;
            }
            var ds = new PhatHienMayInWindows().Quet();
            Console.WriteLine("Queue đã cài cho tài khoản Windows hiện tại (không khẳng định máy đang online):");
            foreach (var q in ds) Console.WriteLine($"- {q.Ten} | {q.Driver} | {q.Cong} | {(q.LaMayAo ? "ẢO / XUẤT FILE" : "CHƯA KIỂM CHỨNG VẬT LÝ")} | {q.SoJob} job");
            int index = Array.IndexOf(args, "--in-thu");
            if (index < 0) return 0;
            string ten = index + 1 < args.Length ? args[index + 1] : "";
            var queue = ds.FirstOrDefault(x => x.Ten == ten) ?? throw new InvalidOperationException("Truyền chính xác tên queue sau --in-thu.");
            if (queue.SoJob != 0) throw new InvalidOperationException("Queue đang có job. Không gửi trang thử lúc này.");
            Console.WriteLine("Sẽ gửi MỘT trang thử A4, không tạo đơn/thu tiền. Queue ảo có thể yêu cầu Save As ở Windows.");
            Console.Write("Gõ IN THU để xác nhận: ");
            if (Console.ReadLine() != "IN THU") { Console.WriteLine("Đã hủy, không gửi lệnh."); return 0; }
            string file = await LuuPdfThu(config);
            Console.WriteLine("PDF kiểm thử được giữ tại: " + file);
            await new MayInWindows().GuiPdf(config.SumatraPdfPath, ten, file,
                new DongLenhInDto { SoTrang = 1, SoBan = 1 }, CancellationToken.None);
            Console.WriteLine("Bộ gửi đã kết thúc. Kiểm tra queue và bản giấy/file đầu ra; chưa coi là đã in giấy thành công.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("Chưa kiểm tra được: " + ex.Message + "\nKiểm tra Print Spooler, driver, quyền Windows và Simulator__SumatraPdfPath."); return 1; }
    }

    private static async Task<string> LuuPdfThu(CauHinhSimulator config)
    {
        string dir = Path.Combine(config.ThuMucTrangThai ?? Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "PhotocopyChain", "PrinterSimulator"), "test-prints");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "TEST-" + Guid.NewGuid().ToString("N") + ".pdf");
        await File.WriteAllBytesAsync(file, TaoPdfThu());
        return file;
    }
}
