using System.Text.Json;
namespace PhotocopyApi.Ocr;
public static class OcrCrop
{
    public static bool Valid(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 256) return false;
        try {
            var rect = JsonSerializer.Deserialize<double[]>(json);
            return rect?.Length == 4 && rect.All(double.IsFinite) && rect[0] >= 0 && rect[1] >= 0 &&
                rect[2] > 0 && rect[3] > 0 && rect[0] + rect[2] <= 1.000000001 && rect[1] + rect[3] <= 1.000000001;
        } catch (JsonException) { return false; }
    }
}
