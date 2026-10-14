using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PhotocopyApi.Models;
using PhotocopyShared.Dtos.GiaoHang;

namespace PhotocopyApi.GiaoHang;

// Một nơi dùng chung cho báo giá và đặt đơn. Chỉ gửi tọa độ/địa chỉ, không gửi tên/SĐT khách.
public sealed class GeoapifyService
{
    private static readonly HttpClient HttpDungChung = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _luotGoi = new(1, 1);
    private DateTime _lanGoiCuoi;
    private DateTime _ngayDem;
    private int _creditsDaDung;

    public GeoapifyService(IConfiguration config, IMemoryCache cache, HttpClient http = null)
    {
        _config = config;
        _cache = cache;
        _http = http ?? HttpDungChung;
    }

    public bool DaCauHinh => !string.IsNullOrWhiteSpace(_config["GiaoHang:GeoapifyApiKey"]);

    // Vé điểm nhận do server phát, không cho đổi chữ địa chỉ và giữ tọa độ cũ để tính thiếu phí.
    public string GhiNhanViTri(int maKhach, DiaChiGiaoDto diem)
    {
        string ma = Guid.NewGuid().ToString("N");
        _cache.Set("vi-tri-giao:" + ma, new ViTriDaLuu(maKhach, diem.DiaChi.Trim(), diem.ViDo, diem.KinhDo), TimeSpan.FromMinutes(30));
        return ma;
    }

    public void KiemTraViTri(string ma, int maKhach, string diaChi, decimal? viDo, decimal? kinhDo)
    {
        if (string.IsNullOrWhiteSpace(ma) || !_cache.TryGetValue("vi-tri-giao:" + ma, out ViTriDaLuu diem) ||
            diem.MaKhach != maKhach || diem.DiaChi != diaChi?.Trim() || diem.ViDo != viDo || diem.KinhDo != kinhDo)
            throw new InvalidOperationException("Điểm nhận chưa được xác nhận hoặc đã hết hạn. Hãy tìm và chọn lại địa chỉ trên app.");
    }
    private sealed record ViTriDaLuu(int MaKhach, string DiaChi, decimal ViDo, decimal KinhDo);

    public async Task<List<DiaChiGiaoDto>> TimDiaChi(string diaChi, decimal? viDo, decimal? kinhDo, CancellationToken dung = default)
    {
        if ((viDo == null) != (kinhDo == null) || viDo is < -90 or > 90 || kinhDo is < -180 or > 180)
            throw new InvalidOperationException("Tọa độ giao hàng không hợp lệ.");
        string query;
        if (viDo != null)
            query = $"geocode/reverse?lat={So(viDo.Value)}&lon={So(kinhDo.Value)}&lang=vi&limit=1";
        else
        {
            if (string.IsNullOrWhiteSpace(diaChi) || diaChi.Trim().Length < 10 || diaChi.Length > 255)
                throw new InvalidOperationException("Nhập đủ địa chỉ (ít nhất 10, tối đa 255 ký tự) để tìm vị trí.");
            query = "geocode/search?text=" + Uri.EscapeDataString(diaChi.Trim()) + "&filter=countrycode:vn&lang=vi&limit=5";
        }
        using JsonDocument json = await GoiBanDo(query, dung);
        var ds = new List<DiaChiGiaoDto>();
        foreach (var feature in json.RootElement.GetProperty("features").EnumerateArray())
        {
            if (feature.ValueKind != JsonValueKind.Object || !feature.TryGetProperty("properties", out var p) ||
                p.ValueKind != JsonValueKind.Object) continue;
            if (!p.TryGetProperty("country_code", out var country) || country.ValueKind != JsonValueKind.String || country.GetString() != "vn") continue;
            if (!p.TryGetProperty("lat", out var lat) || !p.TryGetProperty("lon", out var lon) ||
                !p.TryGetProperty("formatted", out var text)) continue;
            if (lat.ValueKind != JsonValueKind.Number || !lat.TryGetDecimal(out decimal vi) || vi is < -90 or > 90 ||
                lon.ValueKind != JsonValueKind.Number || !lon.TryGetDecimal(out decimal kinh) || kinh is < -180 or > 180 ||
                text.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(text.GetString()) || text.GetString().Length > 255) continue;
            ds.Add(new DiaChiGiaoDto { DiaChi = text.GetString(), ViDo = vi, KinhDo = kinh });
        }
        if (ds.Count == 0) throw new InvalidOperationException("Chưa tìm thấy địa chỉ tại Việt Nam. Hãy nhập rõ số nhà, đường, phường/xã, tỉnh/thành.");
        return ds;
    }

    public async Task<KetQuaPhiGiaoHang> TinhPhi(ChiNhanh chiNhanh, string diaChi, decimal? viDo, decimal? kinhDo, CancellationToken dung = default)
    {
        if (!DaCauHinh) throw new InvalidOperationException("Máy chủ chưa cấu hình Geoapify. Điền GiaoHang__GeoapifyApiKey trong .env rồi chạy lại API; hiện bạn vẫn có thể nhận tại quầy.");
        if (chiNhanh.ViDo == null || chiNhanh.KinhDo == null || chiNhanh.BanKinhGiao <= 0)
            throw new InvalidOperationException("Chi nhánh chưa có tọa độ hoặc bán kính giao hợp lệ.");
        if ((viDo == null) != (kinhDo == null) || viDo is < -90 or > 90 || kinhDo is < -180 or > 180)
            throw new InvalidOperationException("Tọa độ giao hàng không hợp lệ.");
        if (viDo == null)
        {
            var diaChiTim = await TimDiaChi(diaChi, null, null, dung);
            if (diaChiTim.Count != 1) throw new InvalidOperationException("Địa chỉ có nhiều kết quả. Hãy tìm và chọn đúng địa chỉ trên app trước khi báo giá.");
            viDo = diaChiTim[0].ViDo;
            kinhDo = diaChiTim[0].KinhDo;
        }
        // Không làm tròn điểm nhận vào cache: hai địa chỉ gần nhau có thể khác đường đi.
        string query = $"routing?waypoints={So(chiNhanh.ViDo.Value)},{So(chiNhanh.KinhDo.Value)}|{So(viDo.Value)},{So(kinhDo.Value)}&mode=scooter&units=metric";
        using JsonDocument json = await GoiBanDo(query, dung);
        var features = json.RootElement.GetProperty("features");
        if (features.GetArrayLength() == 0) throw new InvalidOperationException("Không tìm được tuyến đường. Hãy kiểm tra lại điểm giao.");
        if (features[0].ValueKind != JsonValueKind.Object || !features[0].TryGetProperty("properties", out var p) ||
            p.ValueKind != JsonValueKind.Object ||
            !p.TryGetProperty("distance", out var distance) || distance.ValueKind != JsonValueKind.Number || !distance.TryGetDecimal(out decimal met) || met < 0 ||
            !p.TryGetProperty("time", out var time) || time.ValueKind != JsonValueKind.Number || !time.TryGetDouble(out double giay) || !double.IsFinite(giay) || giay < 0 || giay > int.MaxValue)
            throw new InvalidOperationException("Bản đồ trả về quãng đường/thời gian không hợp lệ. Vui lòng thử lại.");
        decimal km = met / 1000;
        if (km > chiNhanh.BanKinhGiao) throw new InvalidOperationException($"Địa chỉ nằm ngoài bán kính giao {chiNhanh.BanKinhGiao:0.##} km của chi nhánh.");
        return new KetQuaPhiGiaoHang(Math.Ceiling(km * 100) / 100, QuyTacGiaoHang.TinhPhi(km, _config), (int)Math.Ceiling(giay));
    }

    // Snapshot phí 15 phút, gắn đúng khách/chi nhánh/địa chỉ/tọa độ. Không nhận phí client gửi.
    public string GiuBaoGia(int maKhach, int maChiNhanh, string diaChi, decimal? viDo, decimal? kinhDo, KetQuaPhiGiaoHang ketQua)
    {
        string ma = Guid.NewGuid().ToString("N");
        _cache.Set("phi-giao:" + ma, new BaoGiaDaLuu(maKhach, maChiNhanh, diaChi.Trim(), viDo, kinhDo, ketQua), TimeSpan.FromMinutes(15));
        return ma;
    }

    public KetQuaPhiGiaoHang DocBaoGia(string ma, int maKhach, int maChiNhanh, string diaChi, decimal? viDo, decimal? kinhDo)
    {
        if (string.IsNullOrWhiteSpace(ma) || !_cache.TryGetValue("phi-giao:" + ma, out BaoGiaDaLuu daLuu) ||
            daLuu.MaKhach != maKhach || daLuu.MaChiNhanh != maChiNhanh || daLuu.DiaChi != diaChi.Trim() || daLuu.ViDo != viDo || daLuu.KinhDo != kinhDo)
            throw new InvalidOperationException("Báo giá giao hàng đã hết hạn hoặc địa chỉ đã đổi. Vui lòng tính lại giá trước khi đặt đơn.");
        return daLuu.KetQua;
    }

    private sealed record BaoGiaDaLuu(int MaKhach, int MaChiNhanh, string DiaChi, decimal? ViDo, decimal? KinhDo, KetQuaPhiGiaoHang KetQua);
    private static string So(decimal so) => so.ToString(CultureInfo.InvariantCulture);

    private async Task<JsonDocument> GoiBanDo(string query, CancellationToken dung)
    {
        if (!DaCauHinh) throw new InvalidOperationException("Máy chủ chưa cấu hình Geoapify. Cần điền API Key trong .env và chạy lại API.");
        string khoa = "geoapify:" + query;
        if (_cache.TryGetValue(khoa, out string daNho)) return JsonDocument.Parse(daNho);
        await _luotGoi.WaitAsync(dung);
        try
        {
            if (_cache.TryGetValue(khoa, out daNho)) return JsonDocument.Parse(daNho);
            DateTime homNay = DateTime.UtcNow.AddHours(7).Date;
            if (_ngayDem != homNay) { _ngayDem = homNay; _creditsDaDung = 0; }
            int gioiHan = Math.Clamp(_config.GetValue<int?>("GiaoHang:GioiHanCreditsNgay") ?? 2800, 1, 3000);
            if (_creditsDaDung >= gioiHan) throw new InvalidOperationException("Đã đạt hạn mức bản đồ miễn phí trong ngày. Vui lòng chọn nhận tại quầy hoặc liên hệ cửa hàng.");
            TimeSpan doi = TimeSpan.FromMilliseconds(250) - (DateTime.UtcNow - _lanGoiCuoi);
            if (doi > TimeSpan.Zero) await Task.Delay(doi, dung);
            _creditsDaDung++;
            _lanGoiCuoi = DateTime.UtcNow;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(dung);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            // URI/key không ghi log và không trả nguyên lỗi HTTP cho client.
            using var res = await _http.GetAsync("https://api.geoapify.com/v1/" + query + "&apiKey=" + Uri.EscapeDataString(_config["GiaoHang:GeoapifyApiKey"]), timeout.Token);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException(res.StatusCode == HttpStatusCode.TooManyRequests
                    ? "Dịch vụ bản đồ đang giới hạn lượt gọi. Vui lòng thử lại sau."
                    : res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        ? "API Key Geoapify chưa hợp lệ. Vui lòng liên hệ cửa hàng."
                        : "Dịch vụ bản đồ chưa xử lý được địa chỉ. Vui lòng thử lại hoặc chọn nhận tại quầy.");
            string json = await res.Content.ReadAsStringAsync(timeout.Token);
            var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object || !parsed.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            { parsed.Dispose(); throw new InvalidOperationException("Dịch vụ bản đồ trả dữ liệu không đầy đủ."); }
            _cache.Set(khoa, json, TimeSpan.FromMinutes(10));
            return parsed;
        }
        catch (OperationCanceledException) when (!dung.IsCancellationRequested)
        { throw new InvalidOperationException("Dịch vụ bản đồ phản hồi chậm. Vui lòng thử lại."); }
        catch (HttpRequestException)
        { throw new InvalidOperationException("Chưa kết nối được dịch vụ bản đồ. Vui lòng thử lại."); }
        catch (JsonException)
        { throw new InvalidOperationException("Dịch vụ bản đồ trả dữ liệu không hợp lệ. Vui lòng thử lại."); }
        finally { _luotGoi.Release(); }
    }
}
