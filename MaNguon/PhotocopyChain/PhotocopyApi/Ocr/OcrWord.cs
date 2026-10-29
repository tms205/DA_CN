using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
namespace PhotocopyApi.Ocr;

// DOCX chuẩn Open XML, chỉ chứa văn bản do người dùng gửi, không cần cài Office để xuất.
public static class OcrWord
{
    public const string Mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public static byte[] Create(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 100000) throw new ArgumentException("Nội dung Word phải có từ 1 đến 100.000 ký tự.");
        try { XmlConvert.VerifyXmlChars(text); } catch (XmlException) { throw new ArgumentException("Văn bản chứa ký tự không hợp lệ. Hãy xóa ký tự đặc biệt và thử lại."); }
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body");
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var run = new XElement(w + "r", new XElement(w + "rPr",
                new XElement(w + "rFonts", new XAttribute(w + "ascii", "Times New Roman"), new XAttribute(w + "hAnsi", "Times New Roman")),
                new XElement(w + "sz", new XAttribute(w + "val", "26")), new XElement(w + "lang", new XAttribute(w + "val", "vi-VN"))));
            var parts = line.Split('\t');
            for (int i = 0; i < parts.Length; i++) {
                if (i > 0) run.Add(new XElement(w + "tab"));
                run.Add(new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), parts[i]));
            }
            body.Add(new XElement(w + "p", new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "after", "0"), new XAttribute(w + "line", "360"), new XAttribute(w + "lineRule", "auto"))), run));
        }
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", "11906"), new XAttribute(w + "h", "16838")),
            new XElement(w + "pgMar", new XAttribute(w + "top", "1440"), new XAttribute(w + "bottom", "1440"), new XAttribute(w + "left", "1440"), new XAttribute(w + "right", "1440"))));
        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body));
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""");
            Write(zip, "_rels/.rels", """<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""");
            using var entry = zip.CreateEntry("word/document.xml").Open();
            document.Save(entry);
        }
        return memory.ToArray();
    }
    private static void Write(ZipArchive zip, string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(content); }
}
