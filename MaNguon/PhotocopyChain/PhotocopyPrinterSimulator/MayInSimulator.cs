using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotocopyShared.Dtos.MayInAo;
using PhotocopyShared.MayInAo;

namespace PhotocopyPrinterSimulator;

/// <summary>
/// Mỗi máy chạy một vòng lặp độc lập: nhận việc, tải file, chờ theo tốc độ thật rồi báo hoàn thành.
/// Chế độ thật gửi qua driver Windows (USB/LAN); hoàn tất cần kiểm tra bản giấy.
/// </summary>
public sealed class MayInSimulator : BackgroundService
{
    private readonly HttpClient _http;
    private readonly CauHinhSimulator _config;
    private readonly ILogger<MayInSimulator> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _thuMucTrangThai;
    private readonly MayInWindows _windows;

    public MayInSimulator(
        HttpClient http,
        IOptions<CauHinhSimulator> config,
        ILogger<MayInSimulator> logger,
        MayInWindows? windows = null)
    {
        _http = http;
        _config = config.Value;
        _logger = logger;
        _windows = windows ?? new MayInWindows();
        _thuMucTrangThai = _config.ThuMucTrangThai ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhotocopyChain",
            "PrinterSimulator");
        Directory.CreateDirectory(_thuMucTrangThai);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _config.KiemTraDanhSachMay();

        // Giữ khóa suốt phiên chạy: không cho hai simulator dùng chung danh tính và tiến trình.
        using var khoa = new FileStream(Path.Combine(_thuMucTrangThai, "connector.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string tepId = Path.Combine(_thuMucTrangThai, "connector-id.txt");
        string connectorId;
        if (File.Exists(tepId)) connectorId = File.ReadAllText(tepId).Trim();
        else
        {
            connectorId = Guid.NewGuid().ToString("N");
            File.WriteAllText(tepId, connectorId);
        }
        if (!Guid.TryParse(connectorId, out _)) throw new InvalidDataException("Mã connector đã lưu không hợp lệ.");
        _http.DefaultRequestHeaders.Add("X-Printer-Connector-Id", connectorId);
        _logger.LogInformation("Printer connector đã kết nối tới {DiaChiApi}.", _http.BaseAddress);
        var tacVu = _config.MayIn.Select(may => ChayMotMay(may, stoppingToken)).ToList();
        if (_config.TuPhatHienWindows) tacVu.Add(Task.Run(() => DongBoThietBi(stoppingToken), stoppingToken));
        await Task.WhenAll(tacVu);
    }

    private async Task DongBoThietBi(CancellationToken dung)
    {
        while (!dung.IsCancellationRequested)
        {
            var req = new BaoCaoPhatHienRequest { TenMayTinh = Environment.MachineName,
                MaMayCauHinh = _config.MayIn.Select(x => x.MaMayIn).ToList(),
                KetNoiCauHinh = _config.MayIn.Select(x => new LienKetMayInDto {
                    MaMayIn = x.MaMayIn, CheDo = x.CheDo, TenHangDoi = x.TenHangDoiWindows }).ToList(),
                BoInPdfSanSang = Path.IsPathFullyQualified(_config.SumatraPdfPath)
                    && File.Exists(_config.SumatraPdfPath)
                    && Path.GetFileName(_config.SumatraPdfPath).Equals("SumatraPDF.exe", StringComparison.OrdinalIgnoreCase) };
            try { req.HangDoi = new PhatHienMayInWindows().Quet(); }
            catch (Exception ex) { req.Loi = "Không đọc được queue Windows. Kiểm tra dịch vụ Print Spooler, driver và quyền tài khoản connector."; _logger.LogWarning(ex, "Chưa quét được máy in; máy ảo vẫn chạy độc lập."); }
            try
            {
                using var res = await _http.PostAsJsonAsync("api/may-in-ao/phat-hien", req, dung);
                res.EnsureSuccessStatusCode();
            }
            catch (OperationCanceledException) when (dung.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "Chưa đồng bộ được danh sách máy in; sẽ thử lại."); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), dung); }
            catch (OperationCanceledException) when (dung.IsCancellationRequested) { break; }
        }
    }

    public static CauHinhTocDoMayIn ApDungLienKet(CauHinhTocDoMayIn goc, LienKetMayInDto? lk)
    {
        if (lk == null) return goc;
        if (lk.MaMayIn != goc.MaMayIn || lk.CheDo is not ("AO" or "THAT")
            || (lk.CheDo == "THAT" && string.IsNullOrWhiteSpace(lk.TenHangDoi)))
            throw new InvalidDataException("Liên kết máy do API trả về không hợp lệ.");
        return new() { MaMayIn = goc.MaMayIn, TenMay = goc.TenMay, TocDoA4 = goc.TocDoA4,
            TocDoA3 = goc.TocDoA3, TocDoHaiMat = goc.TocDoHaiMat, SoGiayRaTrangDau = goc.SoGiayRaTrangDau,
            CheDo = lk.CheDo, TenHangDoiWindows = lk.TenHangDoi };
    }

    private async Task ChayMotMay(CauHinhTocDoMayIn cauHinhGoc, CancellationToken dung)
    {
        var may = cauHinhGoc;
        _logger.LogInformation("Máy {MaMayIn} - {TenMay} đã sẵn sàng.", may.MaMayIn, may.TenMay);
        int maDonDangXuLy = 0;
        string maLenhDangXuLy = "";

        while (!dung.IsCancellationRequested)
        {
            try
            {
                TienTrinhInDaLuu? dangIn = await DocTienTrinh(may.MaMayIn, dung);
                var dieuKhien = await _http.GetFromJsonAsync<DieuKhienMayInDto>($"api/may-in-ao/{may.MaMayIn}/dieu-khien", dung)
                    ?? throw new InvalidDataException("Thiếu trạng thái điều khiển máy in.");
                may = ApDungLienKet(cauHinhGoc, dieuKhien.LienKet);
                bool mayThat = may.CheDo == "THAT";
                if (may.CheDo is not ("AO" or "THAT")) throw new InvalidOperationException("Chế độ máy phải là AO hoặc THAT.");
                string? loiAo = mayThat ? null : dieuKhien.LoiMoPhong;
                if (dangIn != null)
                {
                    maDonDangXuLy = dangIn.MaDonHang;
                    maLenhDangXuLy = dangIn.MaLenh;
                    if (string.IsNullOrWhiteSpace(dangIn.MaLenh))
                        throw new InvalidDataException("Tiến trình cũ chưa có mã lệnh; cần kiểm tra thủ công, không tự báo in xong.");
                    if (dangIn.CheDo != may.CheDo || (mayThat && dangIn.TenHangDoi != may.TenHangDoiWindows))
                        throw new InvalidOperationException("Có lệnh đang giữ: không đổi chế độ/queue. Khôi phục cấu hình cũ để kiểm tra lệnh.");
                    if (mayThat)
                    {
                        await XuLyMayThat(may, dangIn, dieuKhien, dung);
                        await Task.Delay(TimeSpan.FromSeconds(1), dung);
                        continue;
                    }
                    if (dieuKhien.TamDung || loiAo != null)
                    {
                        await GuiHeartbeat(may.MaMayIn, loiAo ?? "TAM_DUNG", dung, dangIn, may, dieuKhien);
                        await Task.Delay(TimeSpan.FromSeconds(1), dung);
                        continue;
                    }
                    if (!dangIn.DaBatDau)
                    {
                        var lenhDaLuu = dangIn.Lenh ?? throw new InvalidDataException("Thiếu dữ liệu lệnh đã nhận.");
                        await KiemTraFileIn(lenhDaLuu, dung);
                        double soGiay = QuyTacMayInAo.TinhSoGiay(may, lenhDaLuu.DsDongIn);
                        dangIn.DuKienXong = DateTime.Now.AddSeconds(soGiay);
                        dangIn.DaBatDau = true;
                        dangIn.TongSoGiay = soGiay;
                        await LuuTienTrinh(may.MaMayIn, dangIn, dung);
                        _logger.LogInformation("Máy {TenMay} bắt đầu đơn {MaDonHang}; dự kiến {SoGiay:0.0} giây.",
                            may.TenMay, lenhDaLuu.MaDonHangHienThi, soGiay);
                    }
                    // Checkpoint cũ chưa có tổng thời gian: không đoán downtime là thời gian đã in.
                    if (dangIn.TongSoGiay <= 0) dangIn.TongSoGiay = QuyTacMayInAo.TinhSoGiay(may, dangIn.Lenh!.DsDongIn);
                    await GuiHeartbeat(may.MaMayIn, "DANG_IN", dung, dangIn, may, dieuKhien);
                    if (dangIn.SoGiayDaIn >= dangIn.TongSoGiay)
                        await HoanTat(may, dangIn, dung);
                    else
                    {
                        var dongHo = Stopwatch.StartNew();
                        await Task.Delay(TimeSpan.FromSeconds(Math.Min(1, dangIn.TongSoGiay - dangIn.SoGiayDaIn)), dung);
                        dangIn.TangTienDoAo(Math.Min(1, dongHo.Elapsed.TotalSeconds), false);
                        await LuuTienTrinh(may.MaMayIn, dangIn, dung);
                    }
                    continue;
                }

                maDonDangXuLy = 0;
                maLenhDangXuLy = "";
                if (mayThat) _windows.KiemTraCauHinh(_config.SumatraPdfPath, may.TenHangDoiWindows);
                await GuiHeartbeat(may.MaMayIn, loiAo ?? (dieuKhien.TamDung ? "TAM_DUNG" : "SAN_SANG"), dung, may: may, dieuKhien: dieuKhien);
                if (dieuKhien.TamDung || loiAo != null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), dung);
                    continue;
                }
                using var nhanLenh = new HttpRequestMessage(HttpMethod.Get, $"api/may-in-ao/{may.MaMayIn}/lenh-tiep-theo");
                if (dieuKhien.LienKet != null) nhanLenh.Headers.Add("X-Printer-Binding-Version", dieuKhien.LienKet.PhienBan);
                using HttpResponseMessage phanHoi = await _http.SendAsync(nhanLenh, dung);

                if (phanHoi.StatusCode == HttpStatusCode.NoContent)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _config.SoGiayKiemTra)), dung);
                    continue;
                }

                phanHoi.EnsureSuccessStatusCode();
                LenhInAoDto lenh = await phanHoi.Content.ReadFromJsonAsync<LenhInAoDto>(_json, dung)
                    ?? throw new InvalidOperationException("API trả về lệnh in rỗng.");

                maDonDangXuLy = lenh.MaDonHang;
                maLenhDangXuLy = lenh.MaLenh;
                var tienTrinh = new TienTrinhInDaLuu
                {
                    MaDonHang = lenh.MaDonHang,
                    MaLenh = lenh.MaLenh,
                    Lenh = lenh,
                    CheDo = may.CheDo,
                    TenHangDoi = may.TenHangDoiWindows
                };
                // Lưu quyền đã nhận TRƯỚC khi tải và bắt đầu in, để phục hồi đúng lệnh sau khi tắt.
                await LuuTienTrinh(may.MaMayIn, tienTrinh, dung);
            }
            catch (OperationCanceledException) when (dung.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Máy {MaMayIn} tạm thời không xử lý được lệnh in.", may.MaMayIn);
                await GuiLoiAnToan(may.MaMayIn, maDonDangXuLy, ex.Message, dung, maLenhDangXuLy);
                try { await GuiHeartbeat(may.MaMayIn, "LOI", dung, await DocTienTrinh(may.MaMayIn, dung), may, loi: ex.Message); }
                catch (Exception) when (!dung.IsCancellationRequested) { }
                maDonDangXuLy = 0;
                await Task.Delay(TimeSpan.FromSeconds(5), dung);
            }
        }
    }

    private async Task KiemTraFileIn(LenhInAoDto lenh, CancellationToken dung)
    {
        foreach (DongLenhInDto dong in lenh.DsDongIn)
        {
            if (!PhotocopyShared.QuyCachIn.QuyTacIn.HuongGiayHopLe(dong.HuongGiay))
                throw new InvalidOperationException("Lệnh in có hướng giấy không hợp lệ.");
            using var yeuCau = new HttpRequestMessage(HttpMethod.Get, dong.DuongDanTaiFile);
            yeuCau.Headers.Add("X-Printer-Job-Token", lenh.MaLenh);
            using HttpResponseMessage phanHoi = await _http.SendAsync(yeuCau, dung);
            phanHoi.EnsureSuccessStatusCode();
            long doDai = phanHoi.Content.Headers.ContentLength ?? 0;
            if (doDai == 0)
            {
                byte[] noiDung = await phanHoi.Content.ReadAsByteArrayAsync(dung);
                if (noiDung.Length == 0)
                    throw new InvalidOperationException($"File {dong.TenFile} không có dữ liệu.");
            }
            _logger.LogInformation("Máy {MaMayIn}: file {MaFile}, {HuongGiay}, {MatIn}, {SoTrang} trang × {SoBan} bản.",
                lenh.MaMayIn, dong.MaFile, dong.HuongGiay == "NGANG" ? "Giấy ngang" : "Giấy đứng",
                dong.MatIn, dong.SoTrang, dong.SoBan);
        }
    }

    private async Task HoanTat(CauHinhTocDoMayIn may, TienTrinhInDaLuu tienTrinh, CancellationToken dung)
    {
        int maDonHang = tienTrinh.MaDonHang;
        using HttpResponseMessage phanHoi = await _http.PostAsJsonAsync(
            $"api/may-in-ao/don/{maDonHang}/hoan-tat",
            new BaoCaoMayInRequest { MaMayIn = may.MaMayIn, MaLenh = tienTrinh.MaLenh, TrangThai = "SAN_SANG" },
            _json,
            dung);
        phanHoi.EnsureSuccessStatusCode();
        XoaTienTrinh(may.MaMayIn);
        _logger.LogInformation("Máy {TenMay} đã in xong đơn DH{MaDonHang:D5}.", may.TenMay, maDonHang);
    }

    private async Task GuiHeartbeat(int maMayIn, string trangThai, CancellationToken dung, TienTrinhInDaLuu? tienTrinh = null,
        CauHinhTocDoMayIn? may = null, DieuKhienMayInDto? dieuKhien = null, string? loi = null, int? trangThat = null)
    {
        int tong = tienTrinh?.Lenh?.DsDongIn.Sum(x => checked(x.SoTrang * x.SoBan)) ?? 0;
        int? trang = tienTrinh?.CheDo == "THAT" ? trangThat : tienTrinh == null ? 0 :
            (int)Math.Floor(tong * (tienTrinh.TongSoGiay <= 0 ? 0 : tienTrinh.SoGiayDaIn / tienTrinh.TongSoGiay));
        var dong = tienTrinh?.Lenh?.DsDongIn.FirstOrDefault();
        using HttpResponseMessage phanHoi = await _http.PostAsJsonAsync(
            $"api/may-in-ao/{maMayIn}/heartbeat",
            new BaoCaoMayInRequest { MaMayIn = maMayIn, TrangThai = trangThai,
                MaDonHang = tienTrinh?.MaDonHang, MaLenh = tienTrinh?.MaLenh ?? "",
                CheDo = may?.CheDo ?? "AO", TenHangDoi = may?.TenHangDoiWindows, Loi = loi,
                TongTrang = tong, TrangDaIn = trang, TamDung = dieuKhien?.TamDung ?? false,
                ThongDiep = tienTrinh?.CanhBao,
                SoGiayConLai = tienTrinh?.CheDo == "AO" && tienTrinh.DaBatDau
                    ? (int)Math.Ceiling(Math.Max(0, tienTrinh.TongSoGiay - tienTrinh.SoGiayDaIn)) : null,
                TenFileDangIn = dong?.TenFile,
                QuyCach = dong == null ? null : $"{dong.KhoGiay} · {dong.MatIn} · {dong.HuongGiay} · {dong.MauSac} · {dong.SoBan} bản / {tienTrinh!.Lenh!.DsDongIn.Count} file" },
            _json,
            dung);
        phanHoi.EnsureSuccessStatusCode();
    }

    private async Task XuLyMayThat(CauHinhTocDoMayIn may, TienTrinhInDaLuu p, DieuKhienMayInDto dk, CancellationToken dung)
    {
        var lenh = p.Lenh ?? throw new InvalidDataException("Thiếu lệnh in thật.");
        if (dk.TamDung && !p.DaBatDau)
        {
            await GuiHeartbeat(may.MaMayIn, "TAM_DUNG", dung, p, may, dk);
            return;
        }
        if (!p.DaBatDau)
        {
            _windows.KiemTraCauHinh(_config.SumatraPdfPath, may.TenHangDoiWindows);
            // Kiểm tra toàn bộ quy cách trước khi gửi file đầu, tránh gửi một phần vì dữ liệu sai.
            foreach (var dong in lenh.DsDongIn) _ = MayInWindows.TaoThamSo(may.TenHangDoiWindows, "test.pdf", dong);
            var files = await TaiPdfChoMayThat(lenh, dung);
            p.DaBatDau = true;
            p.CanhBao = "Đã bắt đầu gửi tới Windows. Nếu connector bị ngắt, kiểm tra queue và toàn bộ bản giấy; không tự gửi lại.";
            await LuuTienTrinh(may.MaMayIn, p, dung);
            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    // Pause khi đang gửi file trước phải chặn việc gửi thêm file kế tiếp.
                    while (true)
                    {
                        var control = await DocDieuKhien(may.MaMayIn, dung);
                        if (!control.TamDung) break;
                        await GuiHeartbeat(may.MaMayIn, "TAM_DUNG", dung, p, may, control);
                        await Task.Delay(1000, dung);
                    }
                    // Ghi trước lệnh ngoài tiến trình: crash ở đây phải chờ kiểm tra, không gửi lần hai.
                    p.TepDaGui.Add(files[i]);
                    await LuuTienTrinh(may.MaMayIn, p, dung);
                    await GuiVaTheoDoi(may, p, files[i], lenh.DsDongIn[i], dung);
                }
                p.CanhBao = "Đã gửi PDF tới Windows. Kiểm tra đủ giấy, số bản và quy cách trước khi xác nhận.";
                p.DaGuiDuFile = true;
            }
            catch (OperationCanceledException) when (dung.IsCancellationRequested) { throw; }
            catch (Exception ex) { p.CanhBao = ex.Message; }
            p.ChoXacNhan = true;
            await LuuTienTrinh(may.MaMayIn, p, dung);
        }
        // Gửi lỗi hoặc crash giữa chừng không được biến thành một lệnh đã in xong chỉ vì queue rỗng.
        // Checkpoint cũ chưa có cờ này cũng phải giữ lại để đối chiếu, không tự gửi lại.
        if (!p.DaGuiDuFile)
        {
            await GuiHeartbeat(may.MaMayIn, "LOI", dung, p, may, dk,
                loi: p.CanhBao ?? "Chưa xác minh đã gửi đủ mọi file. Kiểm tra queue và bản giấy; không tự gửi lại hoặc xác nhận hoàn tất.");
            return;
        }
        // Sau restart, bất kỳ lệnh đã bắt đầu gửi nào đều là tình huống có thể đã in một phần.
        if (!p.ChoXacNhan)
        {
            p.ChoXacNhan = true;
            await LuuTienTrinh(may.MaMayIn, p, dung);
        }
        var jobs = _windows.DocJob(may.TenHangDoiWindows)
            .Where(j => p.TepDaGui.Any(tep => MayInWindows.ThuocTep(j, tep))).ToList();
        foreach (var job in jobs)
        {
            bool dangDung = (job.Status & 1) != 0;
            if (dangDung != dk.TamDung) _windows.TamDungJob(may.TenHangDoiWindows, job, dk.TamDung);
        }
        bool loi = jobs.Any(j => (j.Status & (2 | 32 | 64 | 512 | 1024)) != 0);
        bool dangXuLy = jobs.Any(j => (j.Status & (128 | 4096)) == 0);
        string state = dk.TamDung ? "TAM_DUNG" : loi ? "LOI" : dangXuLy ? "DANG_IN" : "CHO_XAC_NHAN";
        await GuiHeartbeat(may.MaMayIn, state, dung, p, may, dk,
            loi ? "Driver báo lỗi/offline/hết giấy; xử lý ở máy và Windows Print Queue." : null,
            jobs.Count == 0 ? null : jobs.Sum(j => j.TrangDaIn));
        if (state == "CHO_XAC_NHAN" && dk.MaLenhXacNhan == p.MaLenh)
        {
            await HoanTat(may, p, dung);
            // Chỉ xóa PDF tạm của lệnh đã được xác nhận; không xóa giấy tờ của người dùng.
            string pdfRoot = Path.GetFullPath(Path.Combine(_thuMucTrangThai, "pdf")) + Path.DirectorySeparatorChar;
            foreach (string tep in p.TepDaGui)
                if (Path.GetFullPath(tep).StartsWith(pdfRoot, StringComparison.OrdinalIgnoreCase)
                    && Path.GetExtension(tep).Equals(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(tep)) File.Delete(tep);
        }
    }

    private async Task<DieuKhienMayInDto> DocDieuKhien(int maMayIn, CancellationToken dung) =>
        await _http.GetFromJsonAsync<DieuKhienMayInDto>($"api/may-in-ao/{maMayIn}/dieu-khien", dung)
        ?? throw new InvalidDataException("Thiếu trạng thái điều khiển máy in.");

    private async Task GuiVaTheoDoi(CauHinhTocDoMayIn may, TienTrinhInDaLuu p, string tep, DongLenhInDto dong, CancellationToken dung)
    {
        Task gui = _windows.GuiPdf(_config.SumatraPdfPath, may.TenHangDoiWindows, tep, dong, dung);
        // Bộ render có thể chậm; tiếp tục heartbeat và xử lý pause đúng job khi nó chạy.
        while (!gui.IsCompleted)
        {
            await Task.WhenAny(gui, Task.Delay(1000, dung));
            if (gui.IsCompleted || dung.IsCancellationRequested) break;
            try
            {
                var control = await DocDieuKhien(may.MaMayIn, dung);
                var jobs = _windows.DocJob(may.TenHangDoiWindows).Where(x => MayInWindows.ThuocTep(x, tep)).ToList();
                foreach (var job in jobs)
                    if (((job.Status & 1) != 0) != control.TamDung) _windows.TamDungJob(may.TenHangDoiWindows, job, control.TamDung);
                await GuiHeartbeat(may.MaMayIn, control.TamDung ? "TAM_DUNG" : "DANG_IN", dung, p, may, control,
                    trangThat: jobs.Count == 0 ? null : jobs.Sum(j => j.TrangDaIn));
            }
            catch (Exception ex) when (!dung.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Chưa theo dõi được job khi đang gửi PDF của máy {MaMayIn}; không gửi lại.", may.MaMayIn);
            }
        }
        // Luôn quan sát task gửi, không bỏ một process in chạy mồ côi rồi gửi thêm lần nữa.
        await gui;
    }

    private async Task<List<string>> TaiPdfChoMayThat(LenhInAoDto lenh, CancellationToken dung)
    {
        if (lenh.DsDongIn.Count == 0) throw new InvalidDataException("Lệnh không có tài liệu để in.");
        string thuMuc = Path.Combine(_thuMucTrangThai, "pdf");
        Directory.CreateDirectory(thuMuc);
        string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lenh.MaLenh)))[..12];
        var files = new List<string>();
        for (int i = 0; i < lenh.DsDongIn.Count; i++)
        {
            var dong = lenh.DsDongIn[i];
            string url = $"api/may-in-ao/don/{lenh.MaDonHang}/file/{dong.MaFile}";
            if (dong.DuongDanTaiFile != url) throw new InvalidDataException("Đường dẫn tải file không thuộc API của lệnh.");
            string tep = Path.Combine(thuMuc, $"DH{lenh.MaDonHang:D5}-{id}-{i}.pdf");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-Printer-Job-Token", lenh.MaLenh);
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, dung);
            res.EnsureSuccessStatusCode();
            await using (var nguon = await res.Content.ReadAsStreamAsync(dung))
            await using (var file = File.Create(tep + ".tmp"))
            {
                var buffer = new byte[65536];
                long size = 0;
                int read;
                while ((read = await nguon.ReadAsync(buffer, dung)) > 0)
                {
                    size += read;
                    if (size > 200L * 1024 * 1024) throw new InvalidDataException("PDF vượt giới hạn 200 MB của connector.");
                    await file.WriteAsync(buffer.AsMemory(0, read), dung);
                }
                if (size < 5) throw new InvalidDataException("PDF tải về rỗng.");
            }
            using (var file = File.OpenRead(tep + ".tmp"))
            {
                var header = new byte[5];
                if (file.Read(header) != 5 || Encoding.ASCII.GetString(header) != "%PDF-")
                    throw new InvalidDataException("Nội dung tải về không phải PDF.");
            }
            File.Move(tep + ".tmp", tep, true);
            files.Add(tep);
        }
        return files;
    }

    private async Task GuiLoiAnToan(int maMayIn, int maDonHang, string loi, CancellationToken dung, string maLenh = "")
    {
        if (maDonHang <= 0) return;
        try
        {
            using var phanHoi = await _http.PostAsJsonAsync(
                $"api/may-in-ao/don/{maDonHang}/loi",
                new BaoCaoMayInRequest { MaMayIn = maMayIn, MaLenh = maLenh, TrangThai = "LOI", Loi = loi },
                _json,
                dung);
        }
        catch
        {
            // API đang tắt thì chỉ ghi console; vòng lặp sau sẽ tự nối lại.
        }
    }

    private string DuongDanTrangThai(int maMayIn)
    {
        return Path.Combine(_thuMucTrangThai, $"may-{maMayIn}.json");
    }

    private async Task<TienTrinhInDaLuu?> DocTienTrinh(int maMayIn, CancellationToken dung)
    {
        string duongDan = DuongDanTrangThai(maMayIn);
        if (!File.Exists(duongDan)) return null;
        await using FileStream file = File.OpenRead(duongDan);
        return await JsonSerializer.DeserializeAsync<TienTrinhInDaLuu>(file, _json, dung);
    }

    private async Task LuuTienTrinh(int maMayIn, TienTrinhInDaLuu tienTrinh, CancellationToken dung)
    {
        string tep = DuongDanTrangThai(maMayIn);
        await using (var file = new FileStream(tep + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(file, tienTrinh, _json, dung);
            file.Flush(flushToDisk: true);
        }
        File.Move(tep + ".tmp", tep, overwrite: true);
    }

    private void XoaTienTrinh(int maMayIn)
    {
        string duongDan = DuongDanTrangThai(maMayIn);
        if (File.Exists(duongDan)) File.Delete(duongDan);
    }
}
