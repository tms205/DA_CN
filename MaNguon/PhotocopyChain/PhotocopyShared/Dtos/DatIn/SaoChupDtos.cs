namespace PhotocopyShared.Dtos.DatIn;

public sealed class QuyCachQuet
{
    // Tọa độ cắt theo phần trăm ảnh sau khi áp dụng hướng EXIF; cắt trước, xoay sau.
    public double CatTrai { get; set; }
    public double CatTren { get; set; }
    public double CatRong { get; set; } = 100;
    public double CatCao { get; set; } = 100;
    public int GocXoay { get; set; }
}

public sealed class AnhQuetDto
{
    public string CheDo { get; set; } = "AO";
    public string AnhBase64 { get; set; } = "";
    public int RongPixel { get; set; }
    public int CaoPixel { get; set; }
}

public sealed class BanSaoChupDto
{
    public string PdfBase64 { get; set; } = "";
    public string AnhXemTruocBase64 { get; set; } = "";
    public int SoTrang { get; set; } = 1;
}
