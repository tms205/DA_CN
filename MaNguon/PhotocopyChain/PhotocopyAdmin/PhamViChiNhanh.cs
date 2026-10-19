using Microsoft.JSInterop;
using PhotocopyShared.Constants;

namespace PhotocopyAdmin;

// Giữ chi nhánh CEO đang xem. null nghĩa là xem tổng quan toàn chuỗi.
public sealed class PhamViChiNhanh
{
    private const string TenKhoa = "photocopychain.pham-vi-chi-nhanh";

    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private bool _daKhoiTao;

    public PhamViChiNhanh(IJSRuntime js, HttpClient http)
    {
        _js = js;
        _http = http;
    }

    public int? MaChiNhanh { get; private set; }

    public async Task KhoiTaoAsync()
    {
        if (_daKhoiTao) return;
        _daKhoiTao = true;

        string giaTri = await _js.InvokeAsync<string>("localStorage.getItem", TenKhoa);
        MaChiNhanh = int.TryParse(giaTri, out int maChiNhanh) && maChiNhanh > 0
            ? maChiNhanh
            : null;
        GanHeader();
    }

    public async Task ChonAsync(int? maChiNhanh)
    {
        MaChiNhanh = maChiNhanh > 0 ? maChiNhanh : null;
        _daKhoiTao = true;
        GanHeader();

        // Đổi phạm vi chỉ đổi dữ liệu đang xem, không tự mở hoặc đóng ca.
        if (MaChiNhanh == null)
            await _js.InvokeVoidAsync("localStorage.removeItem", TenKhoa);
        else
            await _js.InvokeVoidAsync("localStorage.setItem", TenKhoa, MaChiNhanh.Value.ToString());
    }

    private void GanHeader()
    {
        _http.DefaultRequestHeaders.Remove(TenHeader.MaChiNhanh);
        if (MaChiNhanh != null)
            _http.DefaultRequestHeaders.TryAddWithoutValidation(TenHeader.MaChiNhanh, MaChiNhanh.Value.ToString());
    }
}
