using System.Collections.Concurrent;
using System.Text.Json;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyApi.MayInAo;

/// <summary>Heartbeat chỉ là trạng thái tức thời nên không cần thêm cột vào CSDL.</summary>
public sealed class TrangThaiMayInAoStore
{
    private readonly ConcurrentDictionary<int, TrangThaiKetNoiMayInDto> _ds = new();
    private readonly TimeProvider _gio;
    private readonly object _khoa = new();
    private readonly Dictionary<int, DieuKhienMayInDto> _dieuKhien = new();
    private readonly string _tepDieuKhien;

    public TrangThaiMayInAoStore(TimeProvider gio = null, string thuMucDieuKhien = null)
    {
        _gio = gio ?? TimeProvider.System;
        if (thuMucDieuKhien == null) return;
        Directory.CreateDirectory(thuMucDieuKhien);
        _tepDieuKhien = Path.Combine(thuMucDieuKhien, "dieu-khien.json");
        if (File.Exists(_tepDieuKhien))
            _dieuKhien = JsonSerializer.Deserialize<Dictionary<int, DieuKhienMayInDto>>(File.ReadAllText(_tepDieuKhien))
                ?? throw new InvalidDataException("File điều khiển máy in bị hỏng.");
    }

    public DieuKhienMayInDto LayDieuKhien(int maMayIn)
    {
        lock (_khoa)
        {
            var x = _dieuKhien.GetValueOrDefault(maMayIn) ?? new();
            return new() { TamDung = x.TamDung, LoiMoPhong = x.LoiMoPhong, MaLenhXacNhan = x.MaLenhXacNhan };
        }
    }

    public string DieuKhien(int maMayIn, DieuKhienMayInRequest req, string maLenh = null)
    {
        lock (_khoa)
        {
            var x = LayDieuKhien(maMayIn);
            var tt = LayTatCa().FirstOrDefault(t => t.MaMayIn == maMayIn);
            switch (req.ThaoTac)
            {
                case "TAM_DUNG": x.TamDung = true; break;
                case "TIEP_TUC": x.TamDung = false; break;
                case "HET_GIAY": case "KET_GIAY": case "OFFLINE": case "KHOI_PHUC":
                    if (tt?.CheDo == "THAT") return "Máy thật không cho phép mô phỏng lỗi.";
                    x.LoiMoPhong = req.ThaoTac == "KHOI_PHUC" ? null : req.ThaoTac;
                    break;
                case "XAC_NHAN_BAN_GIAY":
                    if (tt?.CheDo != "THAT" || tt.TrangThai != "CHO_XAC_NHAN"
                        || req.MaDonHang != tt.MaDonHangDangIn || string.IsNullOrWhiteSpace(maLenh))
                        return "Chỉ xác nhận đúng đơn của máy thật đang chờ kiểm tra bản giấy.";
                    x.MaLenhXacNhan = maLenh;
                    break;
                default: return "Thao tác máy in không hợp lệ.";
            }
            var moi = new Dictionary<int, DieuKhienMayInDto>(_dieuKhien) { [maMayIn] = x };
            if (_tepDieuKhien != null)
            {
                using (var file = new FileStream(_tepDieuKhien + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(file, moi);
                    file.Flush(true);
                }
                File.Move(_tepDieuKhien + ".tmp", _tepDieuKhien, true);
            }
            _dieuKhien[maMayIn] = x;
            return null;
        }
    }

    public void CapNhat(int maMayIn, string trangThai, int? maDonHang = null, string loi = null, BaoCaoMayInRequest baoCao = null)
    {
        // API đổi bước đơn hàng không được ghi đè loại kết nối vừa nhận từ connector.
        if (baoCao == null && _ds.TryGetValue(maMayIn, out var cu))
            baoCao = new() { CheDo = cu.CheDo, TenHangDoi = cu.TenHangDoi };
        _ds[maMayIn] = new TrangThaiKetNoiMayInDto
        {
            MaMayIn = maMayIn,
            LanCuoiPhanHoi = _gio.GetLocalNow().DateTime,
            TrangThai = trangThai,
            MaDonHangDangIn = maDonHang,
            Loi = loi,
            CheDo = baoCao?.CheDo == "THAT" ? "THAT" : "AO",
            TenHangDoi = baoCao?.TenHangDoi,
            TongTrang = Math.Max(0, baoCao?.TongTrang ?? 0),
            TrangDaIn = baoCao?.TrangDaIn == null ? null : Math.Clamp(baoCao.TrangDaIn.Value, 0, Math.Max(0, baoCao.TongTrang)),
            SoGiayConLai = baoCao?.SoGiayConLai,
            TenFileDangIn = baoCao?.TenFileDangIn,
            QuyCach = baoCao?.QuyCach,
            TamDung = baoCao?.TamDung ?? false, ThongDiep = baoCao?.ThongDiep
        };
    }

    public List<TrangThaiKetNoiMayInDto> LayTatCa()
    {
        DateTime mocMatKetNoi = _gio.GetLocalNow().DateTime.AddSeconds(-30);
        return _ds.Values
            .Select(x => new TrangThaiKetNoiMayInDto
            {
                MaMayIn = x.MaMayIn,
                LanCuoiPhanHoi = x.LanCuoiPhanHoi,
                TrangThai = x.LanCuoiPhanHoi < mocMatKetNoi ? "MAT_KET_NOI" : x.TrangThai,
                MaDonHangDangIn = x.MaDonHangDangIn,
                Loi = x.Loi,
                CheDo = x.CheDo, TenHangDoi = x.TenHangDoi, TongTrang = x.TongTrang,
                TrangDaIn = x.TrangDaIn, SoGiayConLai = x.SoGiayConLai,
                TenFileDangIn = x.TenFileDangIn, QuyCach = x.QuyCach, TamDung = x.TamDung, ThongDiep = x.ThongDiep
            })
            .OrderBy(x => x.MaMayIn)
            .ToList();
    }
}
