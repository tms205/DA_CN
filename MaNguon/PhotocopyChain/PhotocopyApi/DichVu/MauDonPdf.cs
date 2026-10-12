using PhotocopyShared.Dtos.DatIn;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace PhotocopyApi.DichVu;

public static class MauDonPdf
{
    public static IReadOnlyList<MauDonDto> DanhSach { get; } = Array.AsReadOnly(new[]
    {
        new MauDonDto("DON_XIN_VIEC", "Đơn xin việc", "Bản trống để điền thông tin và ký bằng tay."),
        new MauDonDto("DON_XIN_NGHI", "Đơn xin nghỉ phép", "Mẫu tham khảo, điền thời gian và lý do nghỉ bằng tay."),
        new MauDonDto("DON_DE_NGHI", "Đơn đề nghị", "Mẫu đơn chung, để trống nơi nhận và nội dung đề nghị.")
    });

    public static byte[] Tao(string maMau)
    {
        var mau = DanhSach.FirstOrDefault(x => x.MaMau == maMau)
            ?? throw new ArgumentException("Mẫu đơn không tồn tại.");
        using var pdf = new PdfDocumentBuilder();
        string font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
        if (!File.Exists(font)) throw new InvalidOperationException("Máy chủ cần font Arial để in mẫu đơn tiếng Việt.");
        var chu = pdf.AddTrueTypeFont(File.ReadAllBytes(font));
        var trang = pdf.AddPage(PageSize.A4);
        void Dong(string text, double y, double size = 12, bool giua = false)
        {
            var chuDo = trang.MeasureText(text, size, new PdfPoint(0, 0), chu);
            double rong = chuDo.Count == 0 ? 0 : chuDo.Max(x => x.BoundingBox.Right);
            double x = giua ? (trang.PageSize.Width - rong) / 2 : 55;
            trang.AddText(text, size, new PdfPoint(x, y), chu);
        }
        Dong("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", 785, 13, true);
        Dong("Độc lập - Tự do - Hạnh phúc", 764, 12, true);
        Dong(mau.TenMau.ToUpperInvariant(), 710, 18, true);
        Dong("Kính gửi: ................................................................................................", 665);
        Dong("Họ và tên: ...............................................................................................", 625);
        Dong("Ngày sinh: ..............................................................................................", 590);
        Dong("Địa chỉ: ...................................................................................................", 555);
        Dong("Số điện thoại: .........................................................................................", 520);
        string noiDung = maMau switch
        {
            "DON_XIN_VIEC" => "Vị trí ứng tuyển, trình độ và kinh nghiệm:",
            "DON_XIN_NGHI" => "Thời gian xin nghỉ, lý do và người tiếp nhận bàn giao:",
            _ => "Nội dung và lý do đề nghị:"
        };
        Dong(noiDung, 477);
        for (int i = 0; i < 8; i++)
            Dong("................................................................................................................", 440 - i * 28);
        Dong("............., ngày ....... tháng ....... năm ...........", 178);
        Dong("Người làm đơn", 141);
        Dong("(Ký và ghi rõ họ tên)", 119, 10);
        Dong("Mẫu tham khảo để viết tay; vui lòng dùng biểu mẫu riêng nếu nơi nhận yêu cầu.", 35, 9);
        return pdf.Build();
    }
}
