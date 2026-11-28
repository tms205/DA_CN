using System.Text.Json;
using PhotocopyApi.GiaoHang;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.InLai;

// =====================================================================================
// ẢNH BẰNG CHỨNG KHI IN LẠI DO LỖI QUẦY
//
// Vì sao cần: in lại là đơn 0đ, cửa hàng chịu giấy mực. Nếu chỉ ghi lý do bằng chữ thì nhân viên
// có thể bấm in lại để đưa thêm 1 bộ cho người quen. Bắt chụp 1–3 ảnh bản in lỗi thì quản lý
// xem lại được bản lỗi thật, giống cách làm với giao hàng thất bại.
//
// Cách làm giống bằng chứng hủy đơn (2 bước):
//   1) Web tải ảnh lên trước  → lưu ảnh + hồ sơ tạm, trả về MaBangChung (GUID).
//   2) Web gửi yêu cầu in lại kèm MaBangChung → API đọc lại hồ sơ, kiểm đúng đơn gốc + đúng người tải.
//      Tạo đơn in lại xong thì ghi thêm 1 file theo mã đơn in lại để báo cáo tra ra ảnh.
//
// Lưu ngoài SQL (thư mục trên máy chủ, ngoài wwwroot), không cần sửa CSDL:
//   <ThuMuc>/<MaDonGoc>/<MaAnh>.jpg|png     ảnh
//   <ThuMuc>/<MaDonGoc>/<MaBangChung>.json  hồ sơ tạm sau bước 1
//   <ThuMuc>/don-in-lai/<MaDonInLai>.json   hồ sơ đã gắn vào đơn in lại
// =====================================================================================
public sealed class HoSoInLai
{
    public string MaBangChung { get; set; } = "";
    public int MaDonHangGoc { get; set; }
    public int? MaDonInLai { get; set; }
    public int MaNguoiGui { get; set; }
    public DateTimeOffset GuiLuc { get; set; }
    public List<AnhBangChungDto> Anh { get; set; } = new();
}

public sealed class BangChungInLaiStore
{
    private readonly string _root;
    private readonly BangChungGiaoHangStore _anh;   // dùng lại phần kiểm ảnh JPG/PNG ≤ 5MB, tối đa 3 ảnh

    public BangChungInLaiStore(IConfiguration config)
    {
        _root = Path.GetFullPath(config["InLai:ThuMucBangChung"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotocopyChain", "ReprintEvidence"));
        _anh = new BangChungGiaoHangStore(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["GiaoHang:ThuMucBangChung"] = _root }).Build());
    }

    private string FileHoSoTam(int maDonGoc, string maBangChung)
    {
        if (maDonGoc < 1 || !Guid.TryParseExact(maBangChung, "N", out _))
            throw new ArgumentException("Mã bằng chứng in lại không hợp lệ.");
        return Path.Combine(_root, maDonGoc.ToString(), maBangChung + ".json");
    }

    private string FileHoSoDonInLai(int maDonInLai)
    {
        if (maDonInLai < 1) throw new ArgumentException("Mã đơn in lại không hợp lệ.");
        return Path.Combine(_root, "don-in-lai", maDonInLai + ".json");
    }

    // Bước 1: lưu 1–3 ảnh bản in lỗi.
    public async Task<HoSoInLai> Luu(int maDonGoc, int maNguoiGui, IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        List<AnhBangChungDto> anh = await _anh.Luu(maDonGoc, files, ct);
        var hoSo = new HoSoInLai
        {
            MaBangChung = Guid.NewGuid().ToString("N"),
            MaDonHangGoc = maDonGoc,
            MaNguoiGui = maNguoiGui,
            GuiLuc = DateTimeOffset.UtcNow,
            Anh = anh
        };
        try
        {
            await using var stream = new FileStream(FileHoSoTam(maDonGoc, hoSo.MaBangChung), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, hoSo, cancellationToken: ct);
            return hoSo;
        }
        catch
        {
            _anh.XoaTepCuaYeuCau(maDonGoc, anh);
            throw;
        }
    }

    // Bước 2: đọc lại hồ sơ, chỉ nhận đúng đơn gốc + đúng người đã tải ảnh + chưa dùng cho lần in lại khác.
    public HoSoInLai DocHoSoTam(int maDonGoc, int maNguoiGui, string maBangChung)
    {
        string path = FileHoSoTam(maDonGoc, maBangChung);
        if (!File.Exists(path))
            throw new ArgumentException("Vui lòng chụp ảnh bản in lỗi và tải lên trước khi tạo việc in lại.");
        HoSoInLai hoSo = JsonSerializer.Deserialize<HoSoInLai>(File.ReadAllText(path));
        if (hoSo == null || hoSo.MaDonHangGoc != maDonGoc || hoSo.MaNguoiGui != maNguoiGui || hoSo.Anh.Count is < 1 or > 3)
            throw new ArgumentException("Ảnh bằng chứng không thuộc đơn này hoặc không phải do bạn tải lên.");
        if (hoSo.MaDonInLai != null)
            throw new ArgumentException("Ảnh bằng chứng này đã dùng cho một lần in lại khác. Hãy chụp ảnh mới.");
        return hoSo;
    }

    // Sau khi đơn in lại đã ghi xong: đánh dấu hồ sơ đã dùng và lưu theo mã đơn in lại.
    public async Task GanVaoDonInLai(HoSoInLai hoSo, int maDonInLai)
    {
        hoSo.MaDonInLai = maDonInLai;
        string json = JsonSerializer.Serialize(hoSo);
        Directory.CreateDirectory(Path.Combine(_root, "don-in-lai"));
        await File.WriteAllTextAsync(FileHoSoDonInLai(maDonInLai), json);
        await File.WriteAllTextAsync(FileHoSoTam(hoSo.MaDonHangGoc, hoSo.MaBangChung), json);
    }

    // Báo cáo / hàng đợi: tìm ảnh của một đơn in lại (đơn cũ trước khi có tính năng này thì không có ảnh → null).
    public HoSoInLai DocTheoDonInLai(int maDonInLai)
    {
        string path = FileHoSoDonInLai(maDonInLai);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<HoSoInLai>(File.ReadAllText(path));
    }

    public string DuongDanAnh(int maDonGoc, AnhBangChungDto anh) => _anh.DuongDan(maDonGoc, anh);
}
