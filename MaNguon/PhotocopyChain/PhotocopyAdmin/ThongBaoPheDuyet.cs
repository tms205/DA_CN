using System.Net.Http.Json;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyAdmin;

// Dùng chung badge sidebar và kết quả tải ở dashboard/phê duyệt, không tự duyệt dữ liệu.
public sealed class ThongBaoPheDuyet(HttpClient http)
{
    public int? SoCho { get; private set; }
    public event Action ThayDoi;
    private int _lanTai;

    public void CapNhat(int? soCho) { ++_lanTai; SoCho = soCho; ThayDoi?.Invoke(); }

    public async Task TaiAsync()
    {
        int lanTai = ++_lanTai;
        try
        {
            var ds = await http.GetFromJsonAsync<TongQuanHienTaiDto>("api/quan-tri/bao-cao/hien-tai");
            if (lanTai == _lanTai)
                CapNhat(ds == null ? null : ds.SoPhieuChoDuyet + ds.SoHoanTienChoDuyet);
        }
        catch { if (lanTai == _lanTai) CapNhat(null); }
    }
}
