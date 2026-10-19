using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyApi.MayQuet;

public static class AnhQuetXuLy
{
    public const int DungLuongToiDa = 10 * 1024 * 1024;

    public static AnhQuetDto ChuanHoa(byte[] duLieu, QuyCachQuet qc)
    {
        if (duLieu == null || duLieu.Length == 0 || duLieu.Length > DungLuongToiDa)
            throw new ArgumentException("Mỗi mặt cần ảnh JPG/PNG từ 1 byte đến 10 MB.");
        if (qc == null || !new[] { qc.CatTrai, qc.CatTren, qc.CatRong, qc.CatCao }.All(double.IsFinite) ||
            qc.CatTrai < 0 || qc.CatTren < 0 || qc.CatRong <= 0 || qc.CatCao <= 0 ||
            qc.CatTrai + qc.CatRong > 100 || qc.CatTren + qc.CatCao > 100 ||
            qc.GocXoay is not (0 or 90 or 180 or 270))
            throw new ArgumentException("Vùng cắt phải nằm trong ảnh (0–100%); góc xoay là 0, 90, 180 hoặc 270 độ.");

        try
        {
            using var stream = new MemoryStream(duLieu);
            using var anh = Image.FromStream(stream, false, true);
            if (anh.RawFormat.Guid != ImageFormat.Jpeg.Guid && anh.RawFormat.Guid != ImageFormat.Png.Guid)
                throw new ArgumentException("Nội dung phải là ảnh JPG hoặc PNG, không chỉ đổi tên file.");
            if ((long)anh.Width * anh.Height > 20000000)
                throw new ArgumentException("Mỗi ảnh tối đa 20 megapixel. Hãy giảm kích thước ảnh nguồn.");
            ApDungExif(anh);

            var vung = new Rectangle((int)Math.Round(anh.Width * qc.CatTrai / 100),
                (int)Math.Round(anh.Height * qc.CatTren / 100),
                (int)Math.Round(anh.Width * qc.CatRong / 100), (int)Math.Round(anh.Height * qc.CatCao / 100));
            if (vung.Width < 1 || vung.Height < 1 || vung.Right > anh.Width || vung.Bottom > anh.Height)
                throw new ArgumentException("Vùng cắt quá nhỏ hoặc vượt ngoài ảnh. Hãy điều chỉnh phần trăm cắt.");

            // Bitmap mới không mang EXIF/GPS của ảnh gốc. Nền trong suốt được thay bằng trắng.
            using var cat = new Bitmap(vung.Width, vung.Height);
            using (var ve = Graphics.FromImage(cat))
            {
                ve.Clear(Color.White);
                ve.DrawImage(anh, new Rectangle(0, 0, cat.Width, cat.Height), vung, GraphicsUnit.Pixel);
            }
            cat.RotateFlip(qc.GocXoay switch {
                90 => RotateFlipType.Rotate90FlipNone, 180 => RotateFlipType.Rotate180FlipNone,
                270 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone
            });
            double tiLe = Math.Min(1, 1600.0 / Math.Max(cat.Width, cat.Height));
            using var ketQua = new Bitmap(Math.Max(1, (int)Math.Round(cat.Width * tiLe)), Math.Max(1, (int)Math.Round(cat.Height * tiLe)));
            using (var ve = Graphics.FromImage(ketQua))
            {
                ve.InterpolationMode = InterpolationMode.HighQualityBicubic;
                ve.DrawImage(cat, 0, 0, ketQua.Width, ketQua.Height);
            }
            using var jpeg = new MemoryStream();
            ketQua.Save(jpeg, ImageFormat.Jpeg);
            return new AnhQuetDto { AnhBase64 = Convert.ToBase64String(jpeg.ToArray()), RongPixel = ketQua.Width, CaoPixel = ketQua.Height };
        }
        catch (Exception ex) when (ex is ExternalException or OutOfMemoryException)
        {
            throw new ArgumentException("Không đọc được ảnh. Hãy chọn ảnh JPG/PNG hợp lệ.");
        }
    }

    private static void ApDungExif(Image anh)
    {
        if (!anh.PropertyIdList.Contains(0x112)) return;
        var value = anh.GetPropertyItem(0x112)?.Value;
        if (value == null || value.Length < 2) return;
        anh.RotateFlip(BitConverter.ToUInt16(value, 0) switch {
            2 => RotateFlipType.RotateNoneFlipX, 3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX, 5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone, 7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone
        });
    }
}
