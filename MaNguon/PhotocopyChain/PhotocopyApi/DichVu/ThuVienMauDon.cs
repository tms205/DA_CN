using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyApi.DichVu;

// Một thư viện dùng chung toàn chuỗi. Không dùng SQL và không nằm trong thư mục upload của đơn.
public sealed class ThuVienMauDon
{
    private readonly string _thuMuc;
    private readonly object _khoa = new();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public ThuVienMauDon(IConfiguration config)
    {
        _thuMuc = Path.GetFullPath(config["MauDon:ThuMucGoc"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotocopyChain", "MauDon"));
    }

    public IReadOnlyList<MauDonDto> DanhSach(bool caMauAn = false) => TrongKhoa(ds =>
        ds.Where(x => caMauAn || x.TrangThai == "HIEN").OrderBy(x => x.Nhom).ThenBy(x => x.TenMau).ToArray());

    public (MauDonDto Mau, byte[] Bytes) DocPdf(string ma, bool caMauAn = false, long? phienBan = null) => TrongKhoa(ds =>
    {
        var mau = Tim(ds, ma, caMauAn, phienBan);
        byte[] bytes = File.ReadAllBytes(Path.Combine(_thuMuc, mau.TenFile));
        if (Hash(bytes) != mau.Sha256) throw new InvalidDataException("PDF trong thư viện đã bị thay đổi ngoài hệ thống.");
        return (mau, bytes);
    });

    public byte[] DocAnh(string ma, int trang, bool caMauAn = false, long? phienBan = null) => TrongKhoa(ds =>
    {
        var mau = Tim(ds, ma, caMauAn, phienBan);
        if (trang < 1 || trang > mau.SoTrang) throw new KeyNotFoundException("Trang mẫu không tồn tại.");
        return File.ReadAllBytes(Path.Combine(_thuMuc, "XemTruoc", XemTruocMauDon.TenAnh(mau.TenFile, trang)));
    });

    public MauDonDto Them(string ten, string nhom, string moTa, byte[] bytes)
    {
        var mau = new MauDonDto("MAU_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), "", "");
        mau = ThongTin(mau, ten, nhom, moTa) with {
            Nguon = "Mẫu do cửa hàng cung cấp", PhamVi = "Biểu mẫu do cửa hàng quản lý; kiểm tra yêu cầu nơi tiếp nhận trước khi in."
        };
        return LuuPdf(mau, bytes, laThem: true);
    }

    public MauDonDto Sua(string ma, LuuMauDonRequest req) => TrongKhoa(ds =>
    {
        var mau = ThongTin(Tim(ds, ma, true, req.PhienBan), req.TenMau, req.Nhom, req.MoTa);
        ds[ds.FindIndex(x => x.MaMau == ma)] = CapNhat(mau);
        Ghi(ds); return ds.First(x => x.MaMau == ma);
    });

    public MauDonDto ThayFile(string ma, long phienBan, byte[] bytes)
    {
        var cu = TrongKhoa(ds => Tim(ds, ma, true, phienBan));
        return LuuPdf(cu, bytes, laThem: false);
    }

    public MauDonDto Hien(string ma, HienMauDonRequest req) => TrongKhoa(ds =>
    {
        var mau = CapNhat(Tim(ds, ma, true, req.PhienBan) with { TrangThai = req.Hien ? "HIEN" : "AN" });
        ds[ds.FindIndex(x => x.MaMau == ma)] = mau;
        Ghi(ds); return mau;
    });

    public void Go(string ma, long phienBan) => TrongKhoa(ds =>
    {
        var mau = Tim(ds, ma, true, phienBan);
        ds.Remove(mau); Ghi(ds);
        // Chỉ gỡ danh mục. Không xóa PDF cũ hay tài liệu đã sao chép cho đơn hàng.
        return true;
    });

    private MauDonDto LuuPdf(MauDonDto mau, byte[] bytes, bool laThem)
    {
        int soTrang = XemTruocMauDon.KiemTra(bytes);
        string tenFile = Guid.NewGuid().ToString("N") + ".pdf";
        Directory.CreateDirectory(Path.Combine(_thuMuc, "XemTruoc"));
        // File có tên bất biến; chỉ xuất hiện khi catalog đã được ghi thành công.
        // Nếu render/ghi JSON lỗi, giữ bản chưa publish để kiểm tra; không xóa nhầm một commit đã lưu.
        File.WriteAllBytes(Path.Combine(_thuMuc, tenFile), bytes);
        XemTruocMauDon.TaoAnh(bytes, Path.Combine(_thuMuc, "XemTruoc"), tenFile, soTrang);
        return TrongKhoa(ds =>
        {
            if (laThem) ds.Add(mau);
            else Tim(ds, mau.MaMau, true, mau.PhienBan); // Đọc lại dưới khóa, tránh ghi đè người khác.
            var moi = mau with { TenFile = tenFile, Sha256 = Hash(bytes), SoTrang = soTrang,
                Nguon = "Mẫu do cửa hàng cung cấp", UrlNguon = "", UrlTaiGoc = "",
                PhamVi = "Biểu mẫu do cửa hàng quản lý; kiểm tra yêu cầu nơi tiếp nhận trước khi in.",
                PhienBan = laThem ? 1 : mau.PhienBan + 1, CapNhatLuc = DateTimeOffset.UtcNow };
            ds[ds.FindIndex(x => x.MaMau == mau.MaMau)] = moi;
            Ghi(ds); return moi;
        });
    }

    private T TrongKhoa<T>(Func<List<MauDonDto>, T> viec)
    {
        lock (_khoa)
        {
            Directory.CreateDirectory(_thuMuc);
            using var khoa = MoKhoaFile();
            string path = Path.Combine(_thuMuc, "catalog.json");
            if (!File.Exists(path)) KhoiTao();
            List<MauDonDto> ds;
            try { ds = JsonSerializer.Deserialize<List<MauDonDto>>(File.ReadAllText(path), Json); }
            catch (JsonException ex) { throw new InvalidDataException("Danh mục mẫu đơn bị lỗi. Hãy phục hồi bản sao lưu, không tạo lại dữ liệu.", ex); }
            KiemTraDanhMuc(ds);
            return viec(ds);
        }
    }

    private FileStream MoKhoaFile()
    {
        var batDau = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            try { return new FileStream(Path.Combine(_thuMuc, "catalog.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (batDau.Elapsed < TimeSpan.FromSeconds(10)) { Thread.Sleep(25); }
        }
    }

    private void KhoiTao()
    {
        if (File.Exists(Path.Combine(_thuMuc, "catalog.json.bak")))
            throw new InvalidDataException("Thiếu catalog.json nhưng còn bản sao lưu. Hãy phục hồi thư viện trước khi tiếp tục.");
        Directory.CreateDirectory(Path.Combine(_thuMuc, "XemTruoc"));
        foreach (var mau in MauDonPdf.DanhSach)
        {
            File.WriteAllBytes(Path.Combine(_thuMuc, mau.TenFile), MauDonPdf.Tao(mau.MaMau));
            for (int i = 1; i <= mau.SoTrang; i++)
                File.WriteAllBytes(Path.Combine(_thuMuc, "XemTruoc", XemTruocMauDon.TenAnh(mau.TenFile, i)), MauDonPdf.DocAnhTrang(mau.MaMau, i));
        }
        Ghi(MauDonPdf.DanhSach.ToList());
    }

    private void Ghi(List<MauDonDto> ds)
    {
        string path = Path.Combine(_thuMuc, "catalog.json");
        string tam = Path.Combine(_thuMuc, "catalog-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(tam, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, ds, Json); file.Flush(true); }
            if (File.Exists(path)) File.Replace(tam, path, path + ".bak");
            else File.Move(tam, path);
        }
        finally { if (File.Exists(tam)) File.Delete(tam); }
    }

    private static void KiemTraDanhMuc(List<MauDonDto> ds)
    {
        var ma = new HashSet<string>(StringComparer.Ordinal);
        if (ds == null || ds.Any(x => x == null || !ma.Add(x.MaMau)
            || !Regex.IsMatch(x.MaMau ?? "", "^[A-Z0-9_-]{1,80}$")
            || !Regex.IsMatch(x.TenFile ?? "", @"^[a-z0-9-]+\.pdf$")
            || !Regex.IsMatch(x.Sha256 ?? "", "^[a-f0-9]{64}$")
            || string.IsNullOrWhiteSpace(x.TenMau) || string.IsNullOrWhiteSpace(x.Nhom)
            || x.SoTrang < 1 || x.SoTrang > 50 || x.PhienBan < 1 || x.TrangThai is not ("HIEN" or "AN")))
            throw new InvalidDataException("Danh mục mẫu đơn không hợp lệ. Không ghi đè; cần phục hồi bản sao lưu.");
    }

    private static MauDonDto Tim(List<MauDonDto> ds, string ma, bool caMauAn, long? phienBan)
    {
        var mau = ds.FirstOrDefault(x => x.MaMau == ma && (caMauAn || x.TrangThai == "HIEN"))
            ?? throw new KeyNotFoundException("Mẫu đơn không còn trong thư viện.");
        if (phienBan != null && mau.PhienBan != phienBan)
            throw new MauDonDaDoiException();
        return mau;
    }

    private static MauDonDto ThongTin(MauDonDto mau, string ten, string nhom, string moTa)
    {
        ten = ten?.Trim() ?? ""; nhom = nhom?.Trim() ?? ""; moTa = moTa?.Trim() ?? "";
        if (ten.Length is < 1 or > 150 || nhom.Length is < 1 or > 80 || moTa.Length > 1000
            || ten.Any(char.IsControl) || nhom.Any(char.IsControl) || moTa.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
            throw new ArgumentException("Nhập tên mẫu (1–150 ký tự), loại mẫu (1–80 ký tự) và mô tả tối đa 1.000 ký tự.");
        return mau with { TenMau = ten, Nhom = nhom, MoTa = moTa };
    }
    private static MauDonDto CapNhat(MauDonDto mau) => mau with { PhienBan = mau.PhienBan + 1, CapNhatLuc = DateTimeOffset.UtcNow };
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public sealed class MauDonDaDoiException : Exception
{
    public MauDonDaDoiException() : base("Mẫu vừa được thay đổi. Hãy làm mới thư viện rồi thử lại.") { }
}
