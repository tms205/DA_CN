using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;

namespace PhotocopyApi.Auth;

// Một API tại cửa hàng: phiên bản thu hồi được lưu bền vững như trạng thái bảo trì.
// Không lưu token/mật khẩu; thay đổi mật khẩu/quyền được đối chiếu trực tiếp với DB.
public sealed class PhienDangNhapStore
{
    public const string TenClaimDauPhien = "account_stamp";
    private readonly object _khoa = new();
    private readonly string _file;
    private readonly byte[] _key;
    private Dictionary<int, string> _versions;

    public PhienDangNhapStore(string file, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Thiếu khóa ký phiên đăng nhập.");
        _file = Path.GetFullPath(file);
        _key = Encoding.UTF8.GetBytes(key);
        _versions = File.Exists(_file)
            ? JsonSerializer.Deserialize<Dictionary<int, string>>(File.ReadAllText(_file)) ?? throw new InvalidDataException("Dữ liệu thu hồi phiên không hợp lệ.")
            : new();
    }

    public string TaoDauPhien(NguoiDung user)
    {
        lock (_khoa)
        {
            string text = JsonSerializer.Serialize(new object[] {
                user.MaNguoiDung, user.MatKhau, user.VaiTro, user.MaChiNhanh,
                _versions.GetValueOrDefault(user.MaNguoiDung, "0") });
            return Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(text)));
        }
    }

    public bool HopLe(ClaimsPrincipal principal, NguoiDung user)
    {
        if (user == null || user.TrangThai != "HOAT_DONG" ||
            !int.TryParse(principal?.FindFirst(TenClaim.MaNguoiDung)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id != user.MaNguoiDung)
            return false;
        string issued = principal.FindFirst(TenClaimDauPhien)?.Value;
        if (issued?.Length != 64) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(issued), Encoding.UTF8.GetBytes(TaoDauPhien(user)));
    }

    public void ThuHoi(int maNguoiDung)
    {
        lock (_khoa)
        {
            var next = new Dictionary<int, string>(_versions) { [maNguoiDung] = Guid.NewGuid().ToString("N") };
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            string temp = _file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(next));
            File.Move(temp, _file, true);
            _versions = next;
        }
    }
}
