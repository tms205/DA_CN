#nullable enable
using System.Text;
using System.Text.RegularExpressions;

namespace PhotocopyChain.CauHinh;

// Chỉ biên dịch vào API và máy in, không đưa vào Web/mobile.
internal static class DocFileEnv
{
    public static string LayDuongDan(string? duongDan, string thuMucChay)
    {
        if (string.IsNullOrWhiteSpace(duongDan))
        {
            // Chạy từ project hoặc bin đều lấy cùng file ở cạnh solution.
            var thuMuc = new DirectoryInfo(thuMucChay);
            while (thuMuc != null)
            {
                if (File.Exists(Path.Combine(thuMuc.FullName, "PhotocopyChain.sln")))
                    return Path.Combine(thuMuc.FullName, ".env");
                thuMuc = thuMuc.Parent;
            }
            throw new DirectoryNotFoundException("Không tìm thấy PhotocopyChain.sln. Khi triển khai riêng, hãy đặt PHOTOCOPY_ENV_FILE tới file .env.");
        }
        if (!Path.IsPathFullyQualified(duongDan))
            throw new ArgumentException("PHOTOCOPY_ENV_FILE phải là đường dẫn tuyệt đối.");
        return Path.GetFullPath(duongDan);
    }

    public static Dictionary<string, string?> Doc(string duongDan)
    {
        if (!File.Exists(duongDan))
            throw new FileNotFoundException("Thiếu file .env riêng. Hãy khôi phục file cấu hình tại MaNguon/PhotocopyChain/.env.", duongDan);

        var config = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        int soDong = 0;
        foreach (string dong in File.ReadLines(duongDan, Encoding.UTF8))
        {
            soDong++;
            string text = dong.Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;
            int dauBang = text.IndexOf('=');
            if (dauBang < 1) throw Loi(soDong);
            string key = text[..dauBang].Trim();
            if (!Regex.IsMatch(key, @"^[A-Za-z_][A-Za-z0-9_]*$")) throw Loi(soDong);
            key = key.Replace("__", ":");
            string value = DocGiaTri(text[(dauBang + 1)..].Trim(), soDong);
            if (!config.TryAdd(key, value)) throw Loi(soDong);
        }
        return config;
    }

    private static string DocGiaTri(string value, int soDong)
    {
        if (value.Length == 0) return "";
        if (value[0] is '\'' or '"')
        {
            char nhay = value[0];
            int cuoi = 1;
            while (cuoi < value.Length && (value[cuoi] != nhay || value[cuoi - 1] == '\\')) cuoi++;
            if (cuoi == value.Length) throw Loi(soDong);
            string phanSau = value[(cuoi + 1)..].Trim();
            if (phanSau.Length > 0 && !phanSau.StartsWith('#')) throw Loi(soDong);
            // Không mở rộng $VAR hay chạy lệnh; chỉ gỡ escape dấu nháy.
            return value[1..cuoi].Replace("\\" + nhay, nhay.ToString());
        }
        for (int i = 0; i < value.Length; i++)
            if (value[i] == '#' && (i == 0 || char.IsWhiteSpace(value[i - 1])))
                return value[..i].TrimEnd();
        return value;
    }

    private static FormatException Loi(int soDong) => new(
        $"File .env không hợp lệ hoặc có khóa trùng tại dòng {soDong}. Không hiển thị giá trị để bảo vệ bí mật.");
}
