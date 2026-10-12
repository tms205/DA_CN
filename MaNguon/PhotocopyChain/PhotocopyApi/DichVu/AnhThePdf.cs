using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using PhotocopyShared.QuyCachIn;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace PhotocopyApi.DichVu;

[SupportedOSPlatform("windows")]
public static class AnhThePdf
{
    // Một ảnh gốc được lặp đúng số lượng, không in thêm ảnh cho đầy tờ.
    // Căn vừa khung, giữ tỉ lệ và nền trắng; không tự cắt khuôn mặt của khách.
    public static byte[] Tao(byte[] duLieuAnh, string kichThuoc, int soLuong)
    {
        var quyCach = QuyCachAnhThe.Lay(kichThuoc)
            ?? throw new ArgumentException("Kích thước ảnh thẻ không hợp lệ.");
        if (soLuong is < 1 or > 100)
            throw new ArgumentException("Số lượng phải từ 1 đến 100 ảnh mỗi file.");

        using var stream = new MemoryStream(duLieuAnh);
        using var anh = Image.FromStream(stream);
        if ((long)anh.Width * anh.Height > 20000000)
            throw new ArgumentException("Ảnh tối đa 20 megapixel. Vui lòng giảm kích thước ảnh rồi tải lại.");

        // EXIF có thể xoay ảnh chụp điện thoại dù pixel gốc đang nằm ngang.
        if (anh.PropertyIdList.Contains(0x112))
        {
            int huong = BitConverter.ToUInt16(anh.GetPropertyItem(0x112).Value, 0);
            var xoay = huong switch
            {
                2 => RotateFlipType.RotateNoneFlipX, 3 => RotateFlipType.Rotate180FlipNone,
                4 => RotateFlipType.Rotate180FlipX, 5 => RotateFlipType.Rotate90FlipX,
                6 => RotateFlipType.Rotate90FlipNone, 7 => RotateFlipType.Rotate270FlipX,
                8 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone
            };
            anh.RotateFlip(xoay);
        }

        int rongPixel = (int)Math.Round(quyCach.RongCm / 2.54 * 300);
        int caoPixel = (int)Math.Round(quyCach.CaoCm / 2.54 * 300);
        using var khungAnh = new Bitmap(rongPixel, caoPixel);
        using (var ve = Graphics.FromImage(khungAnh))
        {
            ve.Clear(Color.White);
            ve.InterpolationMode = InterpolationMode.HighQualityBicubic;
            double tiLe = Math.Min((double)rongPixel / anh.Width, (double)caoPixel / anh.Height);
            float rong = (float)(anh.Width * tiLe), cao = (float)(anh.Height * tiLe);
            ve.DrawImage(anh, (rongPixel - rong) / 2, (caoPixel - cao) / 2, rong, cao);
        }
        using var jpeg = new MemoryStream();
        khungAnh.Save(jpeg, ImageFormat.Jpeg);

        const double cm = 72 / 2.54, le = cm, cach = 0.3 * cm;
        double rongAnh = quyCach.RongCm * cm, caoAnh = quyCach.CaoCm * cm;
        using var pdf = new PdfDocumentBuilder();
        int conLai = soLuong;
        while (conLai > 0)
        {
            var trang = pdf.AddPage(PageSize.A4);
            int soCot = (int)Math.Floor((trang.PageSize.Width - 2 * le + cach) / (rongAnh + cach));
            int soHang = (int)Math.Floor((trang.PageSize.Height - 2 * le + cach) / (caoAnh + cach));
            PdfPageBuilder.AddedImage hinh = null;
            for (int i = 0; i < Math.Min(conLai, soCot * soHang); i++)
            {
                double x = le + i % soCot * (rongAnh + cach);
                double y = trang.PageSize.Height - le - caoAnh - i / soCot * (caoAnh + cach);
                var khung = new PdfRectangle(x, y, x + rongAnh, y + caoAnh);
                if (hinh == null) hinh = trang.AddJpeg(jpeg.ToArray(), khung);
                else trang.AddJpeg(hinh, khung);
                trang.DrawRectangle(new PdfPoint(x, y), rongAnh, caoAnh, 0.25); // đường cắt đúng kích thước
            }
            conLai -= Math.Min(conLai, soCot * soHang);
        }
        return pdf.Build();
    }
}
