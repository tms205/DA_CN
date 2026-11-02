using System.Net;
using System.Net.Http.Json;

namespace PhotocopyAdmin;

public sealed class NgoaiLeBaoTri(string message) : Exception(message);

public static class DocDuLieuApi
{
    // Chỉ phân loại 503 có thông tin bảo trì của API; mất mạng/500/401 vẫn là lỗi thật.
    public static async Task<T> DocJsonAsync<T>(this HttpClient http, string path, CancellationToken token = default)
    {
        using var response = await http.GetAsync(path, token);
        await KiemTraBaoTriAsync(response, token);
        response.EnsureSuccessStatusCode();
        return response.StatusCode == HttpStatusCode.NoContent ? default : await response.Content.ReadFromJsonAsync<T>(cancellationToken: token);
    }
    public static async Task KiemTraBaoTriAsync(HttpResponseMessage response, CancellationToken token = default)
    {
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            PhanHoiBaoTri data = null;
            try { data = await response.Content.ReadFromJsonAsync<PhanHoiBaoTri>(cancellationToken: token); }
            catch (System.Text.Json.JsonException) { }
            catch (NotSupportedException) { }
            if (data?.DangBaoTri != null || data?.LoiNhan != null)
                throw new NgoaiLeBaoTri(data.Message ?? "Chức năng đang bảo trì.\n" + data.LoiNhan);
        }
    }
    private sealed class PhanHoiBaoTri { public bool? DangBaoTri { get; set; } public string LoiNhan { get; set; } public string Message { get; set; } }

    public static string ChucNangTrang(string route) => route.Split('?', '#')[0].Trim('/').ToLowerInvariant() switch
    {
        "" or "lap-don-tai-quay" or "may-in" => "DON_HANG",
        "kho-file" or "mau-don" => "FILE_IN", "ban-giao" => "THANH_TOAN",
        "giao-hang" => "GIAO_HANG", "nhan-dang-chu" => "OCR", "bao-cao" => "BAO_CAO",
        "kho-vat-tu" or "dich-vu" or "nhan-vien" or "danh-gia" or "khach-hang" or "phe-duyet" or "bang-gia" or "khuyen-mai" or "quan-tri" => "QUAN_LY",
        _ => ""
    };
}
