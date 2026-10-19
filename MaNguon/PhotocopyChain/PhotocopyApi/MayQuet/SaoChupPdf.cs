using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using PhotocopyShared.Dtos.DatIn;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace PhotocopyApi.MayQuet;

public static class SaoChupPdf
{
    // Hai khung 86×54 mm, xếp dọc trên một mặt A4. Giữ tỉ lệ ảnh trong khung,
    // không tự kéo giãn hoặc tự nhận dạng/cắt nội dung giấy tờ.
    private const double Rong = 86, Cao = 54, Trai = 62, Tren = 30, Cach = 12;

    public static BanSaoChupDto Tao(byte[] matTruoc, byte[] matSau)
    {
        byte[][] ds = { KhungThe(matTruoc), KhungThe(matSau) };
        using var pdf = new PdfDocumentBuilder();
        var trang = pdf.AddPage(PageSize.A4);
        const double mm = 72 / 25.4;
        // PNG xem trước dùng đúng vị trí và ảnh đã lót nền của PDF, không dựng bố cục khác trên web.
        using var xem = new Bitmap(840, 1188);
        using (var ve = Graphics.FromImage(xem))
        {
            ve.Clear(Color.White);
            ve.InterpolationMode = InterpolationMode.HighQualityBicubic;
            for (int i = 0; i < ds.Length; i++)
            {
                double tren = Tren + i * (Cao + Cach);
                double duoi = trang.PageSize.Height - (tren + Cao) * mm;
                trang.AddJpeg(ds[i], new PdfRectangle(Trai * mm, duoi, (Trai + Rong) * mm, duoi + Cao * mm));
                using var stream = new MemoryStream(ds[i]);
                using var anh = Image.FromStream(stream);
                ve.DrawImage(anh, (float)(Trai * 4), (float)(tren * 4), (float)(Rong * 4), (float)(Cao * 4));
            }
        }
        using var png = new MemoryStream();
        xem.Save(png, ImageFormat.Png);
        return new BanSaoChupDto { PdfBase64 = Convert.ToBase64String(pdf.Build()), AnhXemTruocBase64 = Convert.ToBase64String(png.ToArray()) };
    }

    private static byte[] KhungThe(byte[] duLieu)
    {
        var ketQua = AnhQuetXuLy.ChuanHoa(duLieu, new QuyCachQuet());
        using var stream = new MemoryStream(Convert.FromBase64String(ketQua.AnhBase64));
        using var anh = Image.FromStream(stream);
        using var khung = new Bitmap((int)Math.Round(Rong / 25.4 * 300), (int)Math.Round(Cao / 25.4 * 300));
        using (var ve = Graphics.FromImage(khung))
        {
            ve.Clear(Color.White);
            ve.InterpolationMode = InterpolationMode.HighQualityBicubic;
            double tiLe = Math.Min((double)khung.Width / anh.Width, (double)khung.Height / anh.Height);
            float rong = (float)(anh.Width * tiLe), cao = (float)(anh.Height * tiLe);
            ve.DrawImage(anh, (khung.Width - rong) / 2, (khung.Height - cao) / 2, rong, cao);
        }
        using var jpeg = new MemoryStream();
        khung.Save(jpeg, ImageFormat.Jpeg);
        return jpeg.ToArray();
    }

    public static byte[] AnhMinhHoa(bool matSau)
    {
        using var anh = new Bitmap(860, 540);
        using (var ve = Graphics.FromImage(anh))
        using (var tieuDe = new Font(FontFamily.GenericSansSerif, 34, FontStyle.Bold))
        using (var noiDung = new Font(FontFamily.GenericSansSerif, 22))
        using (var mau = new SolidBrush(Color.FromArgb(20, 76, 120)))
        {
            ve.Clear(Color.FromArgb(237, 244, 250));
            ve.DrawString("THE MINH HOA", tieuDe, mau, 50, 70);
            ve.DrawString(matSau ? "MAT SAU" : "MAT TRUOC", tieuDe, mau, 50, 165);
            ve.DrawString("DEMO - KHONG PHAI GIAY TO THAT", noiDung, mau, 50, 300);
            ve.DrawString("PhotocopyChain / May quet ao", noiDung, mau, 50, 370);
        }
        using var png = new MemoryStream();
        anh.Save(png, ImageFormat.Png);
        return png.ToArray();
    }
}
