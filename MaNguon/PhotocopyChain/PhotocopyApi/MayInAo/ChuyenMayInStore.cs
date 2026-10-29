using System.Text.Json;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyApi.MayInAo;

/// <summary>Nhật ký/intent bền vững. Intent chỉ được dùng khi SQL đã trỏ đúng máy mới.
/// SQL khóa đơn trước khi đổi intent, ACK hoặc claim để không có hai máy cùng nhận.</summary>
public sealed class ChuyenMayInStore
{
    private readonly string _dir;
    public ChuyenMayInStore(string dir) { _dir = Path.GetFullPath(dir); Directory.CreateDirectory(_dir); }
    private string Tep(int don) => Path.Combine(_dir, $"don-{don}.json");
    private FileStream Khoa(int don)
    {
        for (int i = 0; ; i++)
        {
            try { return new FileStream(Tep(don) + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (i < 100) { Thread.Sleep(20); }
        }
    }
    private List<PhienChuyenMay> Doc(int don) => !File.Exists(Tep(don)) ? new() :
        JsonSerializer.Deserialize<List<PhienChuyenMay>>(File.ReadAllText(Tep(don))) ?? throw new InvalidDataException("Nhật ký chuyển máy không hợp lệ.");
    private void Luu(int don, List<PhienChuyenMay> ds)
    {
        using (var f = new FileStream(Tep(don) + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(f, ds); f.Flush(true); }
        File.Move(Tep(don) + ".tmp", Tep(don), true);
    }
    public List<PhienChuyenMay> LichSu(int don) { using var k = Khoa(don); return Doc(don); }
    public PhienChuyenMay LayHienTai(int don) => LichSu(don).LastOrDefault();

    public PhienChuyenMay YeuCauDung(int don, int nhanh, int may, string token, string connector, int nguoi, string lyDo)
    {
        if (don <= 0 || nhanh <= 0 || may <= 0 || nguoi <= 0 || string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(connector) || string.IsNullOrWhiteSpace(lyDo) || lyDo.Trim().Length is < 3 or > 255)
            throw new InvalidOperationException("Thiếu thông tin hoặc lý do dừng lệnh.");
        using var k = Khoa(don);
        var ds = Doc(don);
        var cu = ds.LastOrDefault();
        if (cu != null && cu.TrangThai != "CHO_AP_DUNG")
        {
            if (cu.MaMayCu != may || cu.MaLenhCu != token || cu.ConnectorCu != connector)
                throw new InvalidOperationException("Đang có yêu cầu chuyển khác; tải lại đơn.");
            return cu;
        }
        if (cu != null && cu.MaLenhCu == token) return cu;
        var x = new PhienChuyenMay { MaYeuCau = Guid.NewGuid().ToString("N"), MaDonHang = don,
            MaChiNhanh = nhanh, MaMayCu = may, MaLenhCu = token, ConnectorCu = connector,
            MaNguoiYeuCau = nguoi, LyDo = lyDo.Trim(), LucYeuCau = DateTimeOffset.UtcNow };
        ds.Add(x); Luu(don, ds); return x;
    }

    public bool XacNhanDaDung(int don, int may, string token, string connector)
    {
        using var k = Khoa(don);
        var ds = Doc(don);
        // ACK lặp của lần dừng cũ là hợp lệ nhưng không thay đổi lần chuyển mới.
        var x = ds.LastOrDefault(x => x.MaMayCu == may && x.MaLenhCu == token && x.ConnectorCu == connector);
        if (x == null) return false;
        if (x.TrangThai != "DANG_DUNG") return true;
        x.TrangThai = "DA_DUNG"; x.LucDaDung = DateTimeOffset.UtcNow;
        Luu(don, ds); return true;
    }

    public void ChuanBiApDung(int don, string id, int mayMoi, List<DongLenhInDto> dong, List<HaoHutChuyenMayRequest> haoHut, int nguoi)
    {
        using var k = Khoa(don);
        var ds = Doc(don); var x = ds.LastOrDefault();
        if (x == null || x.MaYeuCau != id || x.TrangThai == "DANG_DUNG" || x.LucDaDung == null
            || mayMoi <= 0 || mayMoi == x.MaMayCu || dong == null || dong.Count == 0)
            throw new InvalidOperationException("Chưa xác nhận dừng đúng lệnh hoặc máy mới không hợp lệ.");
        x.MaMayMoi = mayMoi; x.PhanConLai = dong; x.HaoHut = haoHut;
        x.MaNguoiChuyen = nguoi; x.LucChuyen = DateTimeOffset.UtcNow; x.TrangThai = "CHO_AP_DUNG";
        Luu(don, ds);
    }
    public bool ChanLenhCu(int don, int may, string token) => LichSu(don).Any(x => x.MaMayCu == may && x.MaLenhCu == token);
    public bool ChanNhanLenh(int don, int may)
    {
        var x = LayHienTai(don);
        return x != null && (x.TrangThai != "CHO_AP_DUNG" || x.MaMayMoi != may);
    }
    public PhienChuyenMay LayLenhDung(int may, string connector) => DanhSach()
        .Where(x => x.MaMayCu == may && x.ConnectorCu == connector && x.TrangThai != "CHO_AP_DUNG").OrderByDescending(x => x.LucYeuCau).FirstOrDefault();
    public int[] DonChoMay(int may) => DanhSach().Where(x => x.MaMayMoi == may && x.TrangThai == "CHO_AP_DUNG").Select(x => x.MaDonHang).ToArray();
    public int[] DonBiChanChoMay(int may) => DanhSach().Where(x => x.TrangThai != "CHO_AP_DUNG" || x.MaMayMoi != may).Select(x => x.MaDonHang).ToArray();
    private IEnumerable<PhienChuyenMay> DanhSach() => Directory.EnumerateFiles(_dir, "don-*.json")
        .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f)[4..])).Select(LayHienTai).Where(x => x != null).ToList();
}

public sealed class PhienChuyenMay
{
    public int MaDonHang { get; set; }
    public int MaChiNhanh { get; set; }
    public string MaYeuCau { get; set; }
    public string TrangThai { get; set; } = "DANG_DUNG";
    public int MaMayCu { get; set; }
    public int? MaMayMoi { get; set; }
    public string MaLenhCu { get; set; }
    public string ConnectorCu { get; set; }
    public int MaNguoiYeuCau { get; set; }
    public int? MaNguoiChuyen { get; set; }
    public string LyDo { get; set; }
    public DateTimeOffset LucYeuCau { get; set; }
    public DateTimeOffset? LucDaDung { get; set; }
    public DateTimeOffset? LucChuyen { get; set; }
    public List<DongLenhInDto> PhanConLai { get; set; } = new();
    public List<HaoHutChuyenMayRequest> HaoHut { get; set; } = new();
}
