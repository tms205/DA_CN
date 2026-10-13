using System.Text.Json;

namespace PhotocopyAdmin;

// Chỉ đọc thông báo dành cho người dùng; không đưa toàn bộ phản hồi lỗi lên giao diện.
public static class DocLoiApi
{
    public static async Task<string> TuPhanHoiAsync(HttpResponseMessage res, string macDinh)
    {
        try
        {
            using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                return message.GetString() ?? macDinh;
            if (json.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                foreach (var field in errors.EnumerateObject())
                    if (field.Value.ValueKind == JsonValueKind.Array)
                        foreach (var error in field.Value.EnumerateArray())
                            if (error.ValueKind == JsonValueKind.String) return error.GetString() ?? macDinh;
        }
        catch (JsonException) { }
        return macDinh;
    }
}
