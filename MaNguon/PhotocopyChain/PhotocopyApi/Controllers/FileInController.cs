using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DatIn;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace PhotocopyApi.Controllers;

// khách tải file lên -> hệ thống đổi sang PDF -> đếm số trang.
// Mọi file đều được lưu thành PDF (file gốc bị xóa) để số trang tính tiền = số trang sẽ in.
// File lưu ở thư mục NGOÀI mã nguồn (LuuTruFile:ThuMucGoc), không ai tải trực tiếp được qua đường link.
[ApiController]
[Route("api/file-in")]
[Authorize(Roles = "CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
public class FileInController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly IConfiguration _config;
    private readonly ILogger<FileInController> _logger;

    // Các loại file nhận in (ngoài PDF)
    private static readonly string[] DuoiWord = { "doc", "docx", "rtf", "odt", "txt" };
    private static readonly string[] DuoiExcel = { "xls", "xlsx", "ods", "csv" };
    private static readonly string[] DuoiPowerPoint = { "ppt", "pptx", "odp" };
    private static readonly string[] DuoiAnh = { "png", "jpg", "jpeg" };

    // Office đổi 2 file cùng lúc hay bị treo → mỗi lúc chỉ cho đổi 1 file
    private static readonly object _khoaOffice = new object();

    public FileInController(IConfiguration config, ILogger<FileInController> logger)
    {
        _config = config;
        _logger = logger;
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    [HttpPost]
    // Request du 1 MB cho thong tin form, kich thuoc file toi da la 60 MB.
    [RequestSizeLimit(61 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60 * 1024 * 1024)]
    public async Task<IActionResult> TaiLen(IFormFile file, [FromQuery] int? maKhachHang)
    {
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int maKhachHangLuu = maNguoiDung;

        // Nhân viên tải file hộ khách tại quầy. Khách dùng App luôn chỉ được tải vào tài khoản của mình.
        if (vaiTro != VaiTro.Customer)
        {
            if (maKhachHang == null)
                return BadRequest(new { message = "Vui lòng chọn khách hàng trước khi tải file tại quầy." });

            using var connKiemTra = new SqlConnection(_chuoiKetNoi);
            bool laKhach = await connKiemTra.ExecuteScalarAsync<bool>(
                "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM NguoiDung WHERE MaNguoiDung = @MaKhachHang AND VaiTro = 'CUSTOMER' AND TrangThai = N'HOAT_DONG') THEN 1 ELSE 0 END AS BIT)",
                new { MaKhachHang = maKhachHang.Value });
            if (!laKhach)
                return BadRequest(new { message = "Khách hàng không tồn tại hoặc tài khoản đã bị khóa." });

            maKhachHangLuu = maKhachHang.Value;
        }

        // kiểm tra file rỗng, dung lượng, đuôi file
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File rỗng." });

        int toiDaMb = _config.GetValue<int>("LuuTruFile:DungLuongToiDaMb");
        if (file.Length > toiDaMb * 1024L * 1024L)
            return BadRequest(new { message = $"File vượt quá {toiDaMb} MB." });

        string duoi = Path.GetExtension(file.FileName).TrimStart('.').ToLower();   // vd "docx"
        bool hopLe = duoi == "pdf" || DuoiWord.Contains(duoi) || DuoiExcel.Contains(duoi)
                     || DuoiPowerPoint.Contains(duoi) || DuoiAnh.Contains(duoi);
        if (!hopLe)
            return BadRequest(new { message = "Chỉ nhận file PDF, Word, Excel, PowerPoint, văn bản (.txt, .rtf, .odt) hoặc ảnh (.png, .jpg)." });

        // lưu file vào D:\PhotocopyData\năm\tháng theo đúng lúc khách tải lên.
        // Giữ tên gốc cho dễ tìm; nếu trùng thì thêm (2), (3)... thay vì dùng chuỗi Guid khó đọc.
        DateTime thoiDiemTaiLen = DateTime.Now;
        string thuMuc = Path.Combine(_config["LuuTruFile:ThuMucGoc"],
            thoiDiemTaiLen.ToString("yyyy"), thoiDiemTaiLen.ToString("MM"));
        Directory.CreateDirectory(thuMuc);

        string tenKhongDuoi = Path.GetFileNameWithoutExtension(file.FileName).Trim();
        foreach (char kyTuCam in Path.GetInvalidFileNameChars())
            tenKhongDuoi = tenKhongDuoi.Replace(kyTuCam, '_');
        if (string.IsNullOrWhiteSpace(tenKhongDuoi)) tenKhongDuoi = "Tai_lieu";

        int soTrung = 1;
        string tenLuu;
        string duongDanGoc;
        string duongDanPdf;
        while (true)
        {
            tenLuu = soTrung == 1 ? tenKhongDuoi : tenKhongDuoi + " (" + soTrung + ")";
            duongDanGoc = Path.Combine(thuMuc, tenLuu + "." + duoi);
            duongDanPdf = Path.Combine(thuMuc, tenLuu + ".pdf");

            if (!System.IO.File.Exists(duongDanGoc) && !System.IO.File.Exists(duongDanPdf)) break;
            soTrung++;
        }

        using (var luong = System.IO.File.Create(duongDanGoc))
        {
            await file.CopyToAsync(luong);
        }

        // đổi sang PDF rồi đếm số trang
        int soTrang;
        try
        {
            if (duoi == "pdf")
                duongDanPdf = duongDanGoc;// PDF sẵn rồi, không cần đổi
            else if (DuoiAnh.Contains(duoi))
                AnhSangPdf(duongDanGoc, duongDanPdf);
            else
                OfficeSangPdf(duongDanGoc, duongDanPdf, duoi);

            soTrang = DemSoTrang(duongDanPdf);
        }
        catch (Exception ex)
        {
            // Lỗi thì dọn file trên ổ đĩa, trả câu báo lỗi cho App
            XoaFile(duongDanGoc);
            XoaFile(duongDanPdf);
            return BadRequest(new { message = ex.Message });
        }

        if (duongDanPdf != duongDanGoc)
            XoaFile(duongDanGoc);   // chỉ giữ bản PDF để in

        //lưu thông tin file vào bảng FileIn
        string sql = @"INSERT INTO FileIn (MaKhachHang, TenFile, DinhDang, DungLuong, DuongDan, SoTrang)
                       VALUES (@MaKhachHang, @TenFile, @DinhDang, @DungLuong, @DuongDan, @SoTrang);
                       SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = new SqlConnection(_chuoiKetNoi);
        int maFile = await conn.ExecuteScalarAsync<int>(sql, new
        {
            MaKhachHang = maKhachHangLuu,
            TenFile = Path.GetFileName(file.FileName),
            DinhDang = duoi,
            DungLuong = (int)file.Length,
            DuongDan = duongDanPdf,
            SoTrang = soTrang
        });

        return Ok(await DocFile(conn, maFile, maKhachHangLuu));
    }

    // xem lại thông tin 1 file (chỉ xem được file của chính mình)
    [HttpGet("{maFileIn}")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> ChiTiet(int maFileIn)
    {
        int maKhachHang = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);
        FileInDto fileIn = await DocFile(conn, maFileIn, maKhachHang);
        if (fileIn == null)
            return NotFound(new { message = "Không tìm thấy file." });

        return Ok(fileIn);
    }

    // Xóa file vừa tải nhưng chưa dùng để đặt đơn. Dùng khi người dùng chọn nhầm file
    // hoặc đổi khách trên màn Lập đơn tại quầy, tránh để PDF mồ côi trên ổ đĩa.
    [HttpDelete("{maFileIn}")]
    public async Task<IActionResult> XoaFileChuaDat(int maFileIn, [FromQuery] int? maKhachHang)
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int maKhachHangCanXoa = vaiTro == VaiTro.Customer ? maNguoiDung : maKhachHang ?? 0;
        if (maKhachHangCanXoa == 0)
            return BadRequest(new { message = "Thiếu khách hàng của file cần xóa." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        FileChuaDat file = await conn.QueryFirstOrDefaultAsync<FileChuaDat>(
            @"SELECT MaFile, DuongDan FROM FileIn f
              WHERE f.MaFile = @MaFile AND f.MaKhachHang = @MaKhachHang
                AND f.TrangThai = N'SAN_SANG' AND f.DuongDan IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM ChiTietDonHang ct WHERE ct.MaFile = f.MaFile)",
            new { MaFile = maFileIn, MaKhachHang = maKhachHangCanXoa });
        if (file == null)
            return BadRequest(new { message = "File đã nằm trong đơn hàng hoặc không tồn tại." });

        XoaFile(file.DuongDan);
        if (System.IO.File.Exists(file.DuongDan))
            return BadRequest(new { message = "File đang được chương trình khác sử dụng, chưa thể xóa." });

        await conn.ExecuteAsync(
            "UPDATE FileIn SET TrangThai = N'DA_XOA', DuongDan = NULL WHERE MaFile = @MaFile",
            new { MaFile = maFileIn });
        return Ok(new { message = "Đã xóa file chưa đặt đơn." });
    }

    //CÁC HÀM PHỤ

    private class FileChuaDat
    {
        public int MaFile { get; set; }
        public string DuongDan { get; set; }
    }

    private async Task<FileInDto> DocFile(SqlConnection conn, int maFile, int maKhachHang)
    {
        string sql = @"SELECT MaFile AS MaFileIn, TenFile AS TenFileGoc, DinhDang AS DinhDangGoc, DungLuong, SoTrang,
                              TrangThai AS TrangThaiFile, NgayTaiLen
                       FROM FileIn
                       WHERE MaFile = @MaFile AND MaKhachHang = @MaKhachHang";
        return await conn.QueryFirstOrDefaultAsync<FileInDto>(sql, new { MaFile = maFile, MaKhachHang = maKhachHang });
    }

    // Đếm số trang của file PDF bằng thư viện PdfPig
    private int DemSoTrang(string duongDanPdf)
    {
        int soTrang = 0;
        try
        {
            using (var pdf = PdfDocument.Open(duongDanPdf))
            {
                soTrang = pdf.NumberOfPages;
            }
        }
        catch
        {
            soTrang = 0;
        }

        if (soTrang <= 0)
            throw new Exception("Không đọc được file PDF. Vui lòng kiểm tra lại file.");
        return soTrang;
    }

    // Ảnh -> PDF: 1 ảnh = 1 trang A4, lề 1 cm, căn giữa, giữ đúng tỉ lệ ảnh. Ảnh ngang thì xoay trang ngang.
    private void AnhSangPdf(string duongDanAnh, string duongDanPdf)
    {
        int rongAnh, caoAnh;
        bool laPng;
        try
        {
            using (var anh = Image.FromFile(duongDanAnh))
            {
                rongAnh = anh.Width;
                caoAnh = anh.Height;
                laPng = anh.RawFormat.Equals(ImageFormat.Png);
            }
        }
        catch
        {
            throw new Exception("Không đọc được file ảnh (file hỏng hoặc không phải ảnh PNG / JPG).");
        }

        // Khổ A4 tính theo đơn vị point: 595 x 842
        bool ngang = rongAnh > caoAnh;
        double rongTrang = ngang ? 842 : 595;
        double caoTrang = ngang ? 595 : 842;
        double le = 28;   // khoảng 1 cm

        // Thu / phóng ảnh cho vừa khung trang (trừ lề), rồi căn giữa
        double tiLe = Math.Min((rongTrang - 2 * le) / rongAnh, (caoTrang - 2 * le) / caoAnh);
        double rong = rongAnh * tiLe;
        double cao = caoAnh * tiLe;
        double x = (rongTrang - rong) / 2;
        double y = (caoTrang - cao) / 2;

        try
        {
            byte[] duLieuAnh = System.IO.File.ReadAllBytes(duongDanAnh);
            using (var pdf = new PdfDocumentBuilder())
            {
                var trang = pdf.AddPage(PageSize.A4, !ngang);
                var khung = new PdfRectangle(x, y, x + rong, y + cao);
                if (laPng)
                    trang.AddPng(duLieuAnh, khung);
                else
                    trang.AddJpeg(duLieuAnh, khung);
                System.IO.File.WriteAllBytes(duongDanPdf, pdf.Build());
            }
        }
        catch
        {
            throw new Exception("Không đọc được file ảnh (định dạng ảnh chưa hỗ trợ). Vui lòng lưu lại ảnh dạng JPG rồi tải lên.");
        }
    }

    // Word / Excel / PowerPoint → PDF.
    // Máy có Microsoft Office thì dùng Office (ngắt trang giống hệt lúc khách soạn nên đếm trang đúng),
    // không có thì dùng LibreOffice (đường dẫn ở LuuTruFile:DuongDanLibreOffice).
    private void OfficeSangPdf(string duongDanGoc, string duongDanPdf, string duoi)
    {
        string loai = "Word";
        if (DuoiExcel.Contains(duoi)) loai = "Excel";
        if (DuoiPowerPoint.Contains(duoi)) loai = "PowerPoint";

        bool coOffice = Type.GetTypeFromProgID(loai + ".Application") != null;
        string libreOffice = _config["LuuTruFile:DuongDanLibreOffice"];

        if (coOffice)
            DoiBangMicrosoftOffice(duongDanGoc, duongDanPdf, loai);
        else if (System.IO.File.Exists(libreOffice))
            DoiBangLibreOffice(duongDanGoc, Path.GetDirectoryName(duongDanPdf), libreOffice);
        else
            throw new Exception($"Máy chủ chưa cài Microsoft Office hoặc LibreOffice nên chưa đổi được file {loai} sang PDF. Vui lòng xuất file sang PDF rồi tải lên.");

        if (!System.IO.File.Exists(duongDanPdf))
            throw new Exception($"Không đổi được file {loai} sang PDF. Vui lòng xuất file sang PDF rồi tải lên.");
    }

    private void DoiBangMicrosoftOffice(string duongDanGoc, string duongDanPdf, string loai)
    {
        lock (_khoaOffice)
        {
            // Office vừa khởi động đôi khi còn bận nên từ chối lệnh → lỗi thì cho thử lại thêm 1 lần
            for (int lanThu = 1; lanThu <= 2; lanThu++)
            {
                // Chạy Office trên 1 luồng riêng để đặt giờ chờ tối đa 90 giây
                // (file lạ có thể làm Office hiện hộp thoại rồi đứng chờ mãi)
                Exception loi = null;
                var luong = new Thread(() =>
                {
                    try
                    {
                        XuatPdfBangOffice(duongDanGoc, duongDanPdf, loai);
                    }
                    catch (Exception ex)
                    {
                        loi = ex;
                    }
                });
                luong.SetApartmentState(ApartmentState.STA);   // Office (COM) chạy ổn định nhất trên luồng STA
                luong.Start();

                bool xongDungGio = luong.Join(TimeSpan.FromSeconds(90));
                if (!xongDungGio)
                {
                    TatOfficeChayNgam(loai);
                    throw new Exception($"Đổi file {loai} sang PDF quá lâu (file quá lớn hoặc đang đặt mật khẩu). Vui lòng xuất file sang PDF rồi tải lên.");
                }
                if (loi == null)
                    return;   // đổi xong

                _logger.LogWarning(loi, "Lần {LanThu}: {Loai} không đổi được {File} sang PDF", lanThu, loai, duongDanGoc);
                TatOfficeChayNgam(loai);   // Office lỗi giữa chừng có thể còn chạy ngầm → tắt đi
            }
            throw new Exception($"Không đổi được file {loai} sang PDF (file hỏng hoặc đang đặt mật khẩu). Vui lòng kiểm tra lại file.");
        }
    }

    // Mở file bằng Office đang chạy ẩn rồi xuất ra PDF. Các con số (17, 32...) là hằng số trong tài liệu Office.
    private static void XuatPdfBangOffice(string duongDanGoc, string duongDanPdf, string loai)
    {
        // Mật khẩu giả: file có đặt mật khẩu sẽ báo lỗi ngay thay vì hiện hộp thoại chờ nhập mật khẩu
        string matKhauGia = "khongcomk1";

        dynamic app = Activator.CreateInstance(Type.GetTypeFromProgID(loai + ".Application"));
        try
        {
            app.AutomationSecurity = 3;   // không cho chạy macro trong file của khách

            if (loai == "Word")
            {
                app.Visible = false;
                app.DisplayAlerts = 0;                                                  // không hiện hộp thoại
                dynamic doc = app.Documents.Open(duongDanGoc, false, true, false, matKhauGia);   // mở chỉ đọc
                doc.ExportAsFixedFormat(duongDanPdf, 17);                               // 17 = PDF
                doc.Close(0);                                                           // 0 = không lưu thay đổi
            }
            else if (loai == "Excel")
            {
                app.Visible = false;
                app.DisplayAlerts = false;
                dynamic book = app.Workbooks.Open(duongDanGoc, 0, true, Type.Missing, matKhauGia);
                book.ExportAsFixedFormat(0, duongDanPdf);                               // 0 = PDF
                book.Close(false);
            }
            else
            {
                app.DisplayAlerts = 1;                                                  // PowerPoint: 1 = không hiện hộp thoại
                dynamic slide = app.Presentations.Open(duongDanGoc, -1, 0, 0);          // chỉ đọc, không mở cửa sổ
                slide.SaveAs(duongDanPdf, 32);                                          // 32 = PDF
                slide.Close();
            }
        }
        finally
        {
            // Tắt Office. 
            try
            {
                if (loai == "Word") app.Quit(0);
                else if (loai == "Excel") app.Quit();
                else if (app.Presentations.Count == 0) app.Quit();
            }
            catch { }
            Marshal.FinalReleaseComObject(app);
        }
    }

    // Office bị treo quá giờ → tắt các tiến trình Office chạy ngầm (không có cửa sổ) do API mở.
    // Word / Excel người dùng đang mở trên máy có cửa sổ nên không bị tắt nhầm.
    private void TatOfficeChayNgam(string loai)
    {
        string tenTienTrinh = "WINWORD";
        if (loai == "Excel") tenTienTrinh = "EXCEL";
        if (loai == "PowerPoint") tenTienTrinh = "POWERPNT";

        foreach (var tienTrinh in Process.GetProcessesByName(tenTienTrinh))
        {
            if (tienTrinh.MainWindowHandle == IntPtr.Zero)
                tienTrinh.Kill();
        }
    }

    // Đổi bằng LibreOffice qua dòng lệnh: soffice --headless --convert-to pdf --outdir <thư mục> <file>
    private void DoiBangLibreOffice(string duongDanGoc, string thuMucRa, string libreOffice)
    {
        var thongTin = new ProcessStartInfo
        {
            FileName = libreOffice,
            Arguments = $"--headless --convert-to pdf --outdir \"{thuMucRa}\" \"{duongDanGoc}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var tienTrinh = Process.Start(thongTin))
        {
            bool xongDungGio = tienTrinh.WaitForExit(90 * 1000);
            if (!xongDungGio)
            {
                tienTrinh.Kill(true);
                throw new Exception("Đổi file sang PDF quá lâu. Vui lòng xuất file sang PDF rồi tải lên.");
            }
        }
    }

    // Xóa file trên ổ đĩa, lỗi thì bỏ qua
    private void XoaFile(string duongDan)
    {
        try
        {
            if (System.IO.File.Exists(duongDan))
                System.IO.File.Delete(duongDan);
        }
        catch { }
    }
}
