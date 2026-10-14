using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyApi.DichVu;

// Chỉ đọc PDF tải sẵn; không tự vẽ hoặc điền thông tin vào mẫu đơn.
public static class MauDonPdf
{
    private static readonly string ThuMuc = Path.Combine(AppContext.BaseDirectory, "TaiNguyen", "MauDon");
    public static IReadOnlyList<MauDonDto> DanhSach { get; } = DocDanhMuc();

    private static IReadOnlyList<MauDonDto> DocDanhMuc()
    {
        var danhSach = JsonSerializer.Deserialize<List<MauDonDto>>(
            File.ReadAllText(Path.Combine(ThuMuc, "catalog.json")))
            ?? throw new InvalidDataException("Thiếu danh mục mẫu đơn.");
        var maDaCo = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mau in danhSach)
        {
            if (mau == null || string.IsNullOrWhiteSpace(mau.MaMau) || !maDaCo.Add(mau.MaMau)
                || string.IsNullOrWhiteSpace(mau.TenMau) || string.IsNullOrWhiteSpace(mau.PhamVi)
                || string.IsNullOrWhiteSpace(mau.Nguon) || mau.SoTrang < 1
                || !Regex.IsMatch(mau.TenFile ?? "", @"^[a-z0-9-]+\.pdf$")
                || !Regex.IsMatch(mau.Sha256 ?? "", @"^[a-f0-9]{64}$")
                || !Uri.TryCreate(mau.UrlNguon, UriKind.Absolute, out var url) || url.Scheme != "https")
                throw new InvalidDataException("Danh mục mẫu đơn không hợp lệ.");
        }
        return danhSach.AsReadOnly();
    }

    public static byte[] Tao(string maMau)
    {
        var mau = DanhSach.FirstOrDefault(x => x.MaMau == maMau)
            ?? throw new ArgumentException("Mẫu đơn không tồn tại.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(ThuMuc, mau.TenFile));
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (hash != mau.Sha256)
            throw new InvalidDataException("File mẫu đã thay đổi. Vui lòng kiểm tra lại thư viện mẫu đơn.");
        return bytes;
    }

    public static byte[] DocAnhTrang(string maMau, int soTrang)
    {
        var mau = DanhSach.FirstOrDefault(x => x.MaMau == maMau);
        if (mau == null || soTrang < 1 || soTrang > mau.SoTrang)
            throw new ArgumentException("Trang mẫu đơn không tồn tại.");
        string tenAnh = Path.GetFileNameWithoutExtension(mau.TenFile) + "-trang-" + soTrang + ".png";
        return File.ReadAllBytes(Path.Combine(ThuMuc, "XemTruoc", tenAnh));
    }
}
