namespace PhotocopyApi.DichVu;

// Tên khách gửi được giữ trong FileIn.TenFile; tên vật lý riêng cho từng lượt tải.
public record DuongDanTaiLen(string ThuMuc, string Goc, string Pdf)
{
    public static DuongDanTaiLen Tao(string thuMucGoc, DateTime ngay, string duoi)
    {
        if (string.IsNullOrWhiteSpace(thuMucGoc)) throw new InvalidOperationException("Chưa cấu hình thư mục lưu tài liệu.");
        if (string.IsNullOrWhiteSpace(duoi) || duoi.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Đuôi file không hợp lệ.");
        string thuMuc = Path.Combine(Path.GetFullPath(thuMucGoc), ngay.ToString("yyyy"), ngay.ToString("MM"), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(thuMuc);
        return new(thuMuc, Path.Combine(thuMuc, "tai-lieu." + duoi.ToLowerInvariant()), Path.Combine(thuMuc, "tai-lieu.pdf"));
    }
}
