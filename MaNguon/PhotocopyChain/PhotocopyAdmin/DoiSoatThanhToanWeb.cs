using System.Net.Http.Json;
using PhotocopyShared.Dtos.DonHang;

namespace PhotocopyAdmin;

// Dùng chung cho lúc quay lại từ payOS và nút kiểm tra tại hàng đợi.
public static class DoiSoatThanhToanWeb
{
    public static int? LayMaDonQuayLai(int? maDonHang, string orderCode)
    {
        if (int.TryParse(orderCode, out int maPayOS) && maPayOS > 0)
            return maPayOS;
        return maDonHang > 0 ? maDonHang : null;
    }

    public static async Task<ThanhToanDto> KiemTraAsync(HttpClient http, int maDonHang)
    {
        // API kiểm tra quyền và đối soát trực tiếp với payOS. Không tin status=PAID trên URL.
        using var res = await http.GetAsync("api/thanh-toan/" + maDonHang);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<ThanhToanDto>()
            ?? throw new HttpRequestException("Không đọc được trạng thái thanh toán.");
    }
}
