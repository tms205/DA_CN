using System.Text.Json;
using PhotocopyShared.Dtos.DonHang;

namespace PhotocopyApi.ThanhToan;

// API lấy link cũ của payOS không trả QR. Lưu dữ liệu hiển thị ngoài source
// để mở lại đơn sau khi API khởi động lại. Đây KHÔNG phải dữ liệu xác nhận tiền.
public sealed class DuLieuQrPayOSStore
{
    private readonly string _thuMuc;

    public DuLieuQrPayOSStore(string thuMuc)
    {
        _thuMuc = Path.GetFullPath(thuMuc);
    }

    public void Luu(int maDonHang, string paymentLinkId, LienKetPayOSDto duLieu)
    {
        string file = LayFile(maDonHang);
        Directory.CreateDirectory(_thuMuc);
        string fileTam = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var banLuu = new BanLuu { PaymentLinkId = paymentLinkId, DuLieu = duLieu };
            File.WriteAllText(fileTam, JsonSerializer.Serialize(banLuu));
            File.Move(fileTam, file, overwrite: true);
        }
        finally
        {
            if (File.Exists(fileTam)) File.Delete(fileTam);
        }
    }

    public LienKetPayOSDto Doc(int maDonHang, string paymentLinkId, decimal soTien)
    {
        string file = LayFile(maDonHang);
        try
        {
            if (!File.Exists(file)) return null;
            var banLuu = JsonSerializer.Deserialize<BanLuu>(File.ReadAllText(file));
            var data = banLuu?.DuLieu;
            if (banLuu?.PaymentLinkId != paymentLinkId || data == null
                || data.MaDonHang != maDonHang || data.SoTien != soTien
                || string.IsNullOrWhiteSpace(data.QrCode))
                return null;
            return data;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private string LayFile(int maDonHang)
    {
        if (maDonHang <= 0) throw new ArgumentOutOfRangeException(nameof(maDonHang));
        return Path.Combine(_thuMuc, "don-" + maDonHang + ".json");
    }

    public sealed class BanLuu
    {
        public string PaymentLinkId { get; set; } = "";
        public LienKetPayOSDto DuLieu { get; set; }
    }
}
