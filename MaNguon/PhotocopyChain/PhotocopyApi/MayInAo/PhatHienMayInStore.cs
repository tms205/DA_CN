using System.Text.Json;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyApi.MayInAo;

/// <summary>Danh mục tức thời + liên kết bền vững; không thêm bảng/cột CSDL.</summary>
public sealed class PhatHienMayInStore
{
    // Dùng cùng khóa cho đổi liên kết và cấp quyền lệnh: không có khe hở đổi queue giữa hai bước.
    public object Khoa { get; } = new();
    private readonly Dictionary<string, ConnectorMayInDto> _connectors = new(StringComparer.Ordinal);
    private Dictionary<int, LienKetMayInDto> _lienKet = new();
    private readonly string _tep;
    private readonly TimeProvider _gio;

    public PhatHienMayInStore(string thuMuc, TimeProvider gio = null)
    {
        _gio = gio ?? TimeProvider.System;
        Directory.CreateDirectory(thuMuc);
        _tep = Path.Combine(thuMuc, "lien-ket-may-in.json");
        if (File.Exists(_tep))
            _lienKet = JsonSerializer.Deserialize<Dictionary<int, LienKetMayInDto>>(File.ReadAllText(_tep))
                ?? throw new InvalidDataException("File liên kết máy in không hợp lệ; không tự đặt lại cấu hình.");
    }

    public void CapNhat(string id, BaoCaoPhatHienRequest req)
    {
        lock (Khoa)
            _connectors[id] = new() {
                ConnectorId = id, TenMayTinh = req.TenMayTinh, Loi = req.Loi,
                LanCuoiPhatHien = _gio.GetUtcNow(), BoInPdfSanSang = req.BoInPdfSanSang,
                MaMayCauHinh = req.MaMayCauHinh.Distinct().ToList(), HangDoi = req.HangDoi,
                KetNoiCauHinh = req.KetNoiCauHinh
            };
    }

    public List<ConnectorMayInDto> LayTatCa(int? chiNhanh)
    {
        lock (Khoa)
        {
            // Snapshot sâu để caller không vô ý sửa store; không trả khóa API hoặc mã lệnh in.
            var ds = JsonSerializer.Deserialize<List<ConnectorMayInDto>>(JsonSerializer.Serialize(_connectors.Values))!;
            foreach (var x in ds)
            {
                x.LienKet = _lienKet.Values.Where(m => m.ConnectorId == x.ConnectorId).ToList();
                x.MaChiNhanh = x.LienKet.FirstOrDefault()?.MaChiNhanh;
                x.TrucTuyen = _gio.GetUtcNow() - x.LanCuoiPhatHien < TimeSpan.FromSeconds(90);
            }
            return JsonSerializer.Deserialize<List<ConnectorMayInDto>>(JsonSerializer.Serialize(
                ds.Where(x => chiNhanh == null || x.MaChiNhanh == chiNhanh)))!;
        }
    }

    public LienKetMayInDto LayLienKet(int ma)
    {
        lock (Khoa)
            return _lienKet.TryGetValue(ma, out var x)
                ? JsonSerializer.Deserialize<LienKetMayInDto>(JsonSerializer.Serialize(x)) : null;
    }

    public string LienKet(int ma, int chiNhanh, LienKetMayInRequest req, Func<bool> dangIn)
    {
        lock (Khoa)
        {
            var c = LayTatCa(null).FirstOrDefault(x => x.ConnectorId == req.ConnectorId);
            if (c == null || !c.TrucTuyen) return "Connector chưa kết nối hoặc dữ liệu đã quá 90 giây.";
            if (!c.MaMayCauHinh.Contains(ma)) return "Connector chưa được cấu hình phục vụ mã máy này.";
            if (c.MaChiNhanh != null && c.MaChiNhanh != chiNhanh) return "Connector đã thuộc chi nhánh khác.";
            if (dangIn()) return "Máy còn lệnh đang giữ; hoàn tất và kiểm tra lệnh trước khi đổi kết nối.";
            var cu = LayLienKet(ma);
            if (cu != null && cu.ConnectorId != req.ConnectorId)
                return "Không tự chuyển máy sang connector khác. Cần kiểm tra và chuyển cấu hình tại cửa hàng trước.";
            string ten = req.TenHangDoi?.Trim() ?? "";
            if (req.CheDo is not ("AO" or "THAT")) return "Chế độ không hợp lệ.";
            if (req.CheDo == "THAT")
            {
                var queue = c.HangDoi.FirstOrDefault(x => x.Ten == ten);
                if (!string.IsNullOrEmpty(c.Loi) || queue == null) return "Không tìm thấy queue trong lần quét mới nhất.";
                if (queue.LaMayAo) return "Queue ảo chỉ dùng kiểm thử, không nhận đơn sản xuất.";
                if ((queue.TrangThaiDriver & (1 | 2 | 8 | 16 | 128 | 4096)) != 0 || queue.SoJob > 0)
                    return "Queue đang bận hoặc driver báo lỗi/offline; xử lý trước khi kích hoạt.";
                if (!c.BoInPdfSanSang) return "Connector chưa có SumatraPDF.exe hợp lệ.";
                if (!req.DaKiemTraThietBi) return "Cần xác nhận đã kiểm tra driver và năng lực thiết bị.";
                if (_lienKet.Values.Any(x => x.MaMayIn != ma && x.ConnectorId == req.ConnectorId
                    && x.CheDo == "THAT" && string.Equals(x.TenHangDoi, ten, StringComparison.OrdinalIgnoreCase)))
                    return "Queue đã liên kết với một máy khác; không nhận đồng thời hai luồng in.";
                if (c.KetNoiCauHinh.Select(x => LayLienKet(x.MaMayIn) ?? x)
                    .Any(x => x.MaMayIn != ma && x.CheDo == "THAT"
                        && string.Equals(x.TenHangDoi, ten, StringComparison.OrdinalIgnoreCase)))
                    return "Queue đang được cấu hình cho máy khác trong connector.";
            }
            var moi = new Dictionary<int, LienKetMayInDto>(_lienKet) { [ma] = new() {
                MaMayIn = ma, MaChiNhanh = chiNhanh, ConnectorId = req.ConnectorId,
                CheDo = req.CheDo, TenHangDoi = req.CheDo == "THAT" ? ten : "", PhienBan = Guid.NewGuid().ToString("N")
            }};
            using (var file = new FileStream(_tep + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, moi); file.Flush(true); }
            File.Move(_tep + ".tmp", _tep, true);
            _lienKet = moi;
            return null;
        }
    }

    public bool DuocNhanLenh(int ma, string connector, string phienBan)
    {
        lock (Khoa)
            return !_lienKet.TryGetValue(ma, out var x) ||
                (x.ConnectorId == connector && x.PhienBan == phienBan);
    }
}
