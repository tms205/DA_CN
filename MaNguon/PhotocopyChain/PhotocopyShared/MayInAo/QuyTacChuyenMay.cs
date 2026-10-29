using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyShared.MayInAo;

public static class QuyTacChuyenMay
{
    public static string? KiemTra(IEnumerable<DongLenhInDto> nguon, IEnumerable<DongChuyenMayRequest>? yeuCau)
    {
        var goc = nguon.ToList();
        var ds = yeuCau?.ToList();
        if (ds == null || ds.Count is < 1 or > 200 || ds.Any(x => x == null))
            return "Chọn ít nhất một phạm vi cần in, tối đa 200 phạm vi.";
        foreach (var x in ds)
        {
            var cacDong = goc.Where(g => g.MaChiTiet == x.MaChiTiet).ToList();
            if (cacDong.Count == 0 || x.TuTrang < 1 || x.DenTrang < x.TuTrang || x.SoBan is < 1 or > 10000)
                return "Phạm vi trang hoặc số bản không hợp lệ.";
            var dong = cacDong[0];
            int dau = cacDong.Min(g => g.TuTrang ?? 1), cuoi = cacDong.Max(g => g.DenTrang ?? g.SoTrang);
            if (x.TuTrang < dau || x.DenTrang > cuoi) return "Phạm vi vượt phần tài liệu đang xử lý.";
            if (dong.MatIn == "2_MAT" && ((x.TuTrang - dau) % 2 != 0 || (x.DenTrang != cuoi && (x.DenTrang - dau + 1) % 2 != 0)))
                return "In hai mặt phải chuyển theo trọn cặp mặt của tờ giấy, hoặc tới trang cuối.";
        }
        // Quét theo điểm biên, không duyệt từng trang của PDF lớn. Không cho các dòng trùng
        // vượt số bản còn lại ở bất kỳ trang nào, kể cả request bị sửa ngoài giao diện.
        foreach (var nhom in ds.GroupBy(x => x.MaChiTiet))
        {
            var g = goc.Where(x => x.MaChiTiet == nhom.Key).ToList();
            var moc = g.SelectMany(x => new long[] { x.TuTrang ?? 1, (long)(x.DenTrang ?? x.SoTrang) + 1 })
                .Concat(nhom.SelectMany(x => new long[] { x.TuTrang, (long)x.DenTrang + 1 })).Distinct().Order().ToArray();
            foreach (long p in moc)
            {
                long can = nhom.Where(x => x.TuTrang <= p && x.DenTrang >= p).Sum(x => (long)x.SoBan);
                long co = g.Where(x => (x.TuTrang ?? 1) <= p && (x.DenTrang ?? x.SoTrang) >= p).Sum(x => (long)x.SoBan);
                if (can > co) return "Phần chuyển bị trùng hoặc vượt số bản của lệnh cũ.";
            }
        }
        return null;
    }

    public static List<DongLenhInDto> TaoPhanConLai(IEnumerable<DongLenhInDto> nguon, IEnumerable<DongChuyenMayRequest> yeuCau)
    {
        var goc = nguon.ToList();
        var ds = yeuCau.ToList();
        string? loi = KiemTra(goc, ds);
        if (loi != null) throw new InvalidOperationException(loi);
        return ds.Select(x => {
            var g = goc.First(g => g.MaChiTiet == x.MaChiTiet);
            return new DongLenhInDto { MaChiTiet = g.MaChiTiet, MaFile = g.MaFile, TenFile = g.TenFile,
                KhoGiay = g.KhoGiay, MauSac = g.MauSac, MatIn = g.MatIn, HuongGiay = g.HuongGiay,
                TuTrang = x.TuTrang, DenTrang = x.DenTrang, SoTrang = checked(x.DenTrang - x.TuTrang + 1), SoBan = x.SoBan };
        }).ToList();
    }
}
