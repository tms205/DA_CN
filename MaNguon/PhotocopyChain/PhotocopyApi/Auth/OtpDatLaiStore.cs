using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace PhotocopyApi.Auth;

// Dùng singleton: giữ OTP tạm trong bộ nhớ, khóa thao tác để mã không bị dùng hai lần.
// API khởi động lại thì mã cũ hết hiệu lực; không lưu OTP hoặc mật khẩu vào database.
public sealed class OtpDatLaiStore
{
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _dongHo;
    private readonly object _khoa = new();
    private readonly byte[] _khoaBam = RandomNumberGenerator.GetBytes(32);

    public OtpDatLaiStore(IMemoryCache cache, TimeProvider dongHo)
    {
        _cache = cache;
        _dongHo = dongHo;
    }

    // 0: được gửi; số dương: số giây còn phải đợi.
    public int ThuBatDauGui(string email)
    {
        lock (_khoa)
        {
            DateTimeOffset now = _dongHo.GetUtcNow();
            TrangThaiMa ma = Doc(email);
            DateTimeOffset choDen = ma.KhoaDen > ma.GuiLuc.AddSeconds(60)
                ? ma.KhoaDen : ma.GuiLuc.AddSeconds(60);
            if (choDen > now) return (int)Math.Ceiling((choDen - now).TotalSeconds);
            if (ma.DangGui) return 60;

            ma.GuiLuc = now;
            ma.DangGui = true;
            ma.NoiDung = null;
            ma.MaDaBam = null; // Gửi lại thì mã cũ không còn hiệu lực.
            ma.SoLanSai = 0;
            Luu(email, ma);
            return 0;
        }
    }

    public void LuuMa(string email, string maOtp, object noiDung = null, TimeSpan? thoiHan = null)
    {
        lock (_khoa)
        {
            TrangThaiMa ma = Doc(email);
            ma.MaDaBam = Bam(email, maOtp);
            ma.HetHanLuc = _dongHo.GetUtcNow().Add(thoiHan ?? TimeSpan.FromMinutes(10));
            ma.NoiDung = noiDung;
            ma.DangGui = false;
            Luu(email, ma);
        }
    }

    public void HuyMa(string email)
    {
        lock (_khoa)
        {
            TrangThaiMa ma = Doc(email);
            ma.MaDaBam = null;
            ma.NoiDung = null;
            ma.DangGui = false;
            Luu(email, ma);
        }
    }

    // Xác minh để sang bước nhập mật khẩu, chưa đánh dấu mã đã dùng.
    public bool KiemTraMa(string email, string maOtp) => KiemTra(email, maOtp, false, out _);

    public bool DungMa(string email, string maOtp) => KiemTra(email, maOtp, true, out _);

    public bool DungMa<T>(string email, string maOtp, out T noiDung) where T : class
    {
        bool dung = KiemTra(email, maOtp, true, out object payload);
        noiDung = payload as T;
        return dung && noiDung != null;
    }

    private bool KiemTra(string email, string maOtp, bool danhDauDaDung, out object noiDung)
    {
        lock (_khoa)
        {
            noiDung = null;
            TrangThaiMa ma = Doc(email);
            DateTimeOffset now = _dongHo.GetUtcNow();
            if (ma.MaDaBam == null || ma.HetHanLuc <= now || ma.KhoaDen > now)
                return false;

            if (!CryptographicOperations.FixedTimeEquals(ma.MaDaBam, Bam(email, maOtp)))
            {
                ma.SoLanSai++;
                if (ma.SoLanSai >= 5)
                {
                    ma.MaDaBam = null;
                    ma.NoiDung = null;
                    ma.KhoaDen = now.AddMinutes(15);
                }
                Luu(email, ma);
                return false;
            }

            noiDung = ma.NoiDung;
            if (danhDauDaDung)
            {
                ma.NoiDung = null;
                ma.MaDaBam = null; // Đánh dấu đã dùng ngay trong khóa, trước khi cập nhật mật khẩu.
                Luu(email, ma);
            }
            return true;
        }
    }

    private string KhoaCache(string email) => "otp-dat-lai:" + email.Trim().ToLowerInvariant();
    private TrangThaiMa Doc(string email) => _cache.Get<TrangThaiMa>(KhoaCache(email)) ?? new();
    private void Luu(string email, TrangThaiMa ma) => _cache.Set(KhoaCache(email), ma, TimeSpan.FromMinutes(25));
    private byte[] Bam(string email, string maOtp) => HMACSHA256.HashData(
        _khoaBam, Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant() + ":" + maOtp));

    private sealed class TrangThaiMa
    {
        public byte[] MaDaBam { get; set; }
        public object NoiDung { get; set; }
        public DateTimeOffset GuiLuc { get; set; }
        public DateTimeOffset HetHanLuc { get; set; }
        public DateTimeOffset KhoaDen { get; set; }
        public int SoLanSai { get; set; }
        public bool DangGui { get; set; }
    }
}
