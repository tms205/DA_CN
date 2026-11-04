using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.GiaoHang;

public enum NguonAnhGiaoHang { Shipper, KhachHang }

// File riêng tư: chỉ controller đã kiểm quyền mới được đọc. Không dùng tên/path do client gửi.
public sealed class BangChungGiaoHangStore
{
    public const int ToiDaAnh = 3;
    public const int ToiDaByteMoiAnh = 5 * 1024 * 1024;
    private readonly string _root;

    public BangChungGiaoHangStore(IConfiguration config)
    {
        _root = Path.GetFullPath(config["GiaoHang:ThuMucBangChung"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotocopyChain", "DeliveryEvidence"));
    }

    public static void KiemTraViTri(ViTriShipperRequest req, DateTimeOffset now)
    {
        if (req?.ViDo == null || req.KinhDo == null || req.DoChinhXac == null || req.ThoiDiemGps == null ||
            req.ViDo is < -90 or > 90 || req.KinhDo is < -180 or > 180 ||
            !double.IsFinite(req.DoChinhXac.Value) || req.DoChinhXac is < 0 or > 100000)
            throw new ArgumentException("Cần vị trí GPS và độ chính xác hợp lệ. Hãy lấy lại vị trí hiện tại.");
        if (req.ThoiDiemGps < now.AddMinutes(-5) || req.ThoiDiemGps > now.AddMinutes(2))
            throw new ArgumentException("Vị trí đã quá 5 phút hoặc giờ điện thoại chưa đúng. Bật giờ tự động và lấy lại vị trí.");
    }

    public static void KiemTraDanhSach(IReadOnlyList<IFormFile> files)
    {
        if (files == null || files.Count is < 1 or > ToiDaAnh)
            throw new ArgumentException("Cần từ 1 đến 3 ảnh nhật ký cuộc gọi.");
        foreach (var file in files)
            if (file == null || file.Length is <= 0 or > ToiDaByteMoiAnh ||
                Path.GetExtension(file.FileName).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png"))
                throw new ArgumentException("Chỉ nhận ảnh JPG/PNG, mỗi ảnh tối đa 5 MB.");
    }

    public async Task<List<AnhBangChungDto>> Luu(int maGiao, IReadOnlyList<IFormFile> files, CancellationToken ct, NguonAnhGiaoHang? nguon = null)
    {
        KiemTraDanhSach(files);
        var saved = new List<AnhBangChungDto>();
        try
        {
            foreach (var file in files)
            {
                using var input = file.OpenReadStream();
                using var bytes = new MemoryStream();
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    if (bytes.Length + read > ToiDaByteMoiAnh) throw new ArgumentException("Ảnh vượt quá 5 MB.");
                    await bytes.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                byte[] data = bytes.ToArray();
                string mime = KiemTraAnh(data);
                var item = new AnhBangChungDto { MaAnh = Guid.NewGuid().ToString("N"), LoaiNoiDung = mime,
                    KichThuoc = data.LongLength, Sha256 = Convert.ToHexString(SHA256.HashData(data)) };
                string path = Tep(maGiao, item, nguon);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                saved.Add(item); // Kể cả write dở cũng chỉ dọn file do chính request này tạo.
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await output.WriteAsync(data, ct);
            }
            return saved;
        }
        catch { XoaTepCuaYeuCau(maGiao, saved, nguon); throw; }
    }

    public static string KiemTraAnh(byte[] data)
    {
        bool png = data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        bool jpg = data.Length >= 3 && data[0] == 255 && data[1] == 216 && data[2] == 255;
        if (!png && !jpg) throw new ArgumentException("Nội dung file không phải ảnh JPG/PNG.");
        try
        {
            using var stream = new MemoryStream(data);
            using var image = Image.FromStream(stream, false, true);
            if (image.Width <= 0 || image.Height <= 0 || image.Width > 10000 || image.Height > 10000 ||
                (long)image.Width * image.Height > 24_000_000 ||
                !(image.RawFormat.Equals(ImageFormat.Png) || image.RawFormat.Equals(ImageFormat.Jpeg)))
                throw new ArgumentException("Ảnh quá lớn hoặc không hợp lệ. Chọn ảnh chụp màn hình tối đa 24 megapixel.");
            // Buộc giải mã pixel để từ chối file có header ảnh nhưng dữ liệu bị hỏng.
            using var decoded = new Bitmap(image);
            _ = decoded.GetPixel(decoded.Width - 1, decoded.Height - 1);
            return png ? "image/png" : "image/jpeg";
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
        { throw new ArgumentException("Ảnh hỏng hoặc không đọc được. Chọn lại ảnh JPG/PNG."); }
    }

    public string DuongDan(int maGiao, AnhBangChungDto anh, NguonAnhGiaoHang? nguon = null)
    {
        string path = Tep(maGiao, anh, nguon);
        // Ảnh cũ nằm trực tiếp trong thư mục lượt giao; không tìm sang nguồn của bên kia.
        return nguon.HasValue && !File.Exists(path) ? Tep(maGiao, anh, null) : path;
    }

    private string Tep(int maGiao, AnhBangChungDto anh, NguonAnhGiaoHang? nguon)
    {
        if (maGiao <= 0 || !Guid.TryParseExact(anh?.MaAnh, "N", out _) || anh.LoaiNoiDung is not ("image/png" or "image/jpeg"))
            throw new ArgumentException("Mã ảnh không hợp lệ.");
        string folder = Path.Combine(_root, maGiao.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (nguon.HasValue) folder = Path.Combine(folder, nguon.Value switch {
            NguonAnhGiaoHang.Shipper => "Shipper", NguonAnhGiaoHang.KhachHang => "KhachHang",
            _ => throw new ArgumentException("Nguồn ảnh giao hàng không hợp lệ.")
        });
        return Path.Combine(folder, anh.MaAnh + (anh.LoaiNoiDung == "image/png" ? ".png" : ".jpg"));
    }

    public void XoaTepCuaYeuCau(int maGiao, IEnumerable<AnhBangChungDto> files, NguonAnhGiaoHang? nguon = null)
    {
        foreach (var file in files)
            // Chỉ dọn đúng nơi request đã ghi, không dùng đường dẫn fallback của ảnh lịch sử.
            try { File.Delete(Tep(maGiao, file, nguon)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Giữ orphan thay vì đụng file khác. */ }
    }
}
