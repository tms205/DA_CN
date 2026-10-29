namespace PhotocopyApi.Ocr;

public static class OcrRules
{
    public static bool Valid(string model, string filename, long length) =>
        model is "2b" or "4b" && !string.IsNullOrWhiteSpace(filename) && filename.Length <= 255 &&
        Path.GetExtension(filename).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" &&
        length > 0 && length <= 10 * 1024 * 1024;
}
