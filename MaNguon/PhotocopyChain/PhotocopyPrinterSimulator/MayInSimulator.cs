using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotocopyShared.Dtos.MayInAo;
using PhotocopyShared.MayInAo;

namespace PhotocopyPrinterSimulator;

/// <summary>
/// Mỗi máy chạy một vòng lặp độc lập: nhận việc, tải file, chờ theo tốc độ thật rồi báo hoàn thành.
/// Connector máy thật sau này giữ nguyên API và thay phần chờ bằng Windows Print Queue hoặc IPP.
/// </summary>
public sealed class MayInSimulator : BackgroundService
{
    private readonly HttpClient _http;
    private readonly CauHinhSimulator _config;
    private readonly ILogger<MayInSimulator> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _thuMucTrangThai;

    public MayInSimulator(
        HttpClient http,
        IOptions<CauHinhSimulator> config,
        ILogger<MayInSimulator> logger)
    {
        _http = http;
        _config = config.Value;
        _logger = logger;
        _thuMucTrangThai = _config.ThuMucTrangThai ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhotocopyChain",
            "PrinterSimulator");
        Directory.CreateDirectory(_thuMucTrangThai);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_config.MayIn.Count == 0)
            throw new InvalidOperationException("Simulator chưa có cấu hình máy in nào.");

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
        _logger.LogInformation("Máy in ảo đã kết nối tới {DiaChiApi}.", _http.BaseAddress);
        await Task.WhenAll(_config.MayIn.Select(may => ChayMotMay(may, stoppingToken)));
    }

    private async Task ChayMotMay(CauHinhTocDoMayIn may, CancellationToken dung)
    {
        _logger.LogInformation("Máy {MaMayIn} - {TenMay} đã sẵn sàng.", may.MaMayIn, may.TenMay);
        int maDonDangXuLy = 0;
        string maLenhDangXuLy = "";

        while (!dung.IsCancellationRequested)
        {
            try
            {
                TienTrinhInDaLuu? dangIn = await DocTienTrinh(may.MaMayIn, dung);
                if (dangIn != null)
                {
                    maDonDangXuLy = dangIn.MaDonHang;
                    maLenhDangXuLy = dangIn.MaLenh;
                    if (string.IsNullOrWhiteSpace(dangIn.MaLenh))
                        throw new InvalidDataException("Tiến trình cũ chưa có mã lệnh; cần kiểm tra thủ công, không tự báo in xong.");
                    await GuiHeartbeat(may.MaMayIn, "DANG_IN", dung, dangIn);
                    if (!dangIn.DaBatDau)
                    {
                        var lenhDaLuu = dangIn.Lenh ?? throw new InvalidDataException("Thiếu dữ liệu lệnh đã nhận.");
                        await KiemTraFileIn(lenhDaLuu, dung);
                        double soGiay = QuyTacMayInAo.TinhSoGiay(may, lenhDaLuu.DsDongIn);
                        dangIn.DuKienXong = DateTime.Now.AddSeconds(soGiay);
                        dangIn.DaBatDau = true;
                        await LuuTienTrinh(may.MaMayIn, dangIn, dung);
                        _logger.LogInformation("Máy {TenMay} bắt đầu đơn {MaDonHang}; dự kiến {SoGiay:0.0} giây.",
                            may.TenMay, lenhDaLuu.MaDonHangHienThi, soGiay);
                    }
                    TimeSpan conLai = dangIn.DuKienXong - DateTime.Now;
                    if (conLai > TimeSpan.Zero)
                        await Task.Delay(TimeSpan.FromSeconds(Math.Min(conLai.TotalSeconds, 5)), dung);
                    else
                        await HoanTat(may, dangIn, dung);
                    continue;
                }

                await GuiHeartbeat(may.MaMayIn, "SAN_SANG", dung);
                maDonDangXuLy = 0;
                maLenhDangXuLy = "";
                using HttpResponseMessage phanHoi = await _http.GetAsync(
                    $"api/may-in-ao/{may.MaMayIn}/lenh-tiep-theo", dung);

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
                    Lenh = lenh
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

    private async Task GuiHeartbeat(int maMayIn, string trangThai, CancellationToken dung, TienTrinhInDaLuu? tienTrinh = null)
    {
        using HttpResponseMessage phanHoi = await _http.PostAsJsonAsync(
            $"api/may-in-ao/{maMayIn}/heartbeat",
            new BaoCaoMayInRequest { MaMayIn = maMayIn, TrangThai = trangThai,
                MaDonHang = tienTrinh?.MaDonHang, MaLenh = tienTrinh?.MaLenh ?? "" },
            _json,
            dung);
        phanHoi.EnsureSuccessStatusCode();
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
