using PDFtoImage;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PhotocopyApi.DichVu;

// Upload quản trị chỉ nhận PDF. Không chạy Office, JavaScript hay lệnh bên trong tài liệu.
public static class XemTruocMauDon
{
    public const int SoByteToiDa = 20 * 1024 * 1024;
    public const int SoTrangToiDa = 50;

    public static int KiemTra(byte[] bytes)
    {
        if (bytes.Length < 8 || bytes.Length > SoByteToiDa || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new ArgumentException("Chỉ nhận PDF hợp lệ, tối đa 20 MB.");
        try
        {
            using var pdf = PdfDocument.Open(bytes);
            if (pdf.Structure.Trailer.EncryptionToken != null)
                throw new ArgumentException("Hãy xuất bản PDF không khóa/mật khẩu trước khi tải mẫu lên.");
            if (pdf.NumberOfPages < 1 || pdf.NumberOfPages > SoTrangToiDa)
                throw new ArgumentException("Mỗi mẫu phải có từ 1 đến 50 trang.");
            KiemTraNoiDung(pdf);
            foreach (var trang in pdf.GetPages())
            {
                double rong = trang.Width, cao = trang.Height;
                if (!double.IsFinite(rong) || !double.IsFinite(cao) || rong < 20 || cao < 20 || rong > 2000 || cao > 2000)
                    throw new ArgumentException("Kích thước trang PDF không phù hợp với biểu mẫu in.");
            }
            return pdf.NumberOfPages;
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not OutOfMemoryException)
        { throw new ArgumentException("Không đọc được PDF. Hãy xuất lại PDF không khóa và thử lại.", ex); }
    }

    private static void KiemTraNoiDung(PdfDocument pdf)
    {
        var daDoc = new HashSet<IndirectReference>();
        var choDoc = new Stack<IToken>();
        choDoc.Push(pdf.Structure.Catalog.CatalogDictionary);
        int soToken = 0;
        while (choDoc.TryPop(out var token))
        {
            if (++soToken > 200000) throw new ArgumentException("PDF quá phức tạp; hãy xuất lại bản in đơn giản.");
            if (token is IndirectReferenceToken lienKet && daDoc.Add(lienKet.Data))
                choDoc.Push(pdf.Structure.GetObject(lienKet.Data).Data);
            else if (token is ObjectToken obj) choDoc.Push(obj.Data);
            else if (token is StreamToken stream) choDoc.Push(stream.StreamDictionary);
            else if (token is ArrayToken mang)
                foreach (var item in mang.Data) choDoc.Push(item);
            else if (token is DictionaryToken dict)
                foreach (var item in dict.Data)
                {
                    if (item.Key is "JS" or "JavaScript" or "EmbeddedFiles" or "EF"
                        || item.Value is NameToken name && name.Data is "JavaScript" or "Launch" or "RichMedia" or "GoToR" or "SubmitForm" or "ImportData")
                        throw new ArgumentException("PDF chứa mã lệnh hoặc tệp đính kèm. Hãy xuất lại bản PDF chỉ để in.");
                    choDoc.Push(item.Value);
                }
        }
    }

    public static void TaoAnh(byte[] bytes, string thuMuc, string tenFile, int soTrang)
    {
        using var input = new MemoryStream(bytes);
        // Giới hạn kích thước ở KiemTra; 120 dpi đủ đọc biểu mẫu.
        int trang = 0;
        foreach (var bitmap in Conversion.ToImages(input, options: new RenderOptions(Dpi: 120)))
        {
            using (bitmap)
            using (var anh = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
            using (var output = File.Create(Path.Combine(thuMuc, TenAnh(tenFile, ++trang))))
            {
                anh.SaveTo(output);
                if (output.Length > 4 * 1024 * 1024)
                    throw new ArgumentException("Trang PDF quá phức tạp để xem trên app. Hãy giảm độ phân giải ảnh trong PDF và thử lại.");
            }
        }
        if (trang != soTrang) throw new ArgumentException("Không tạo được bản xem trước đầy đủ của PDF.");
    }

    public static string TenAnh(string tenFile, int trang) => Path.GetFileNameWithoutExtension(tenFile) + "-trang-" + trang + ".png";
}
