using System.Collections.Concurrent;
using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyApi.MayInAo;

/// <summary>Heartbeat chỉ là trạng thái tức thời nên không cần thêm cột vào CSDL.</summary>
public sealed class TrangThaiMayInAoStore
{
    private readonly ConcurrentDictionary<int, TrangThaiKetNoiMayInDto> _ds = new();
    private readonly TimeProvider _gio;

    public TrangThaiMayInAoStore(TimeProvider gio = null) => _gio = gio ?? TimeProvider.System;

    public void CapNhat(int maMayIn, string trangThai, int? maDonHang = null, string loi = null)
    {
        _ds[maMayIn] = new TrangThaiKetNoiMayInDto
        {
            MaMayIn = maMayIn,
            LanCuoiPhanHoi = _gio.GetLocalNow().DateTime,
            TrangThai = trangThai,
            MaDonHangDangIn = maDonHang,
            Loi = loi
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
                Loi = x.Loi
            })
            .OrderBy(x => x.MaMayIn)
            .ToList();
    }
}
