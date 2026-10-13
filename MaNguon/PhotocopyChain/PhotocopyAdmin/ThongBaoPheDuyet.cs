using System.Net.Http.Json;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyAdmin;

// Dùng chung badge sidebar và kết quả tải ở dashboard/phê duyệt, không tự duyệt dữ liệu.
public sealed class ThongBaoPheDuyet(HttpClient http)
{
    public int? SoCho { get; private set; }
    public event Action ThayDoi;

    public void CapNhat(int? soCho) { SoCho = soCho; ThayDoi?.Invoke(); }

    public async Task TaiAsync()
    {
        try
        {
            var ds = await http.GetFromJsonAsync<TongQuanHienTaiDto>("api/quan-tri/bao-cao/hien-tai");
            CapNhat(ds == null ? null : ds.SoPhieuChoDuyet + ds.SoHoanTienChoDuyet);
        }
        catch { CapNhat(null); }
    }
}
