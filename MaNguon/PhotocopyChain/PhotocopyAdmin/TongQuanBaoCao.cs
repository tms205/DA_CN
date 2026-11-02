using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyAdmin;

// Chỉ tổng hợp dữ liệu API báo cáo đã lọc quyền và đơn hoàn tất.
public static class TongQuanBaoCao
{
    public static (DateTime TuNgay, DateTime DenNgay) KhoangThoiGian(DateTime homNay, string moc)
    {
        homNay = homNay.Date;
        var dauThang = new DateTime(homNay.Year, homNay.Month, 1);
        var dauTuan = DauKy(homNay, "tuan");
        return moc switch
        {
            "ngay" => (homNay, homNay), "thang" => (dauThang, homNay),
            "thang-truoc" => (dauThang.AddMonths(-1), dauThang.AddDays(-1)),
            "tuan" => (dauTuan, homNay), "tuan-truoc" => (dauTuan.AddDays(-7), dauTuan.AddDays(-1)),
            _ => throw new ArgumentException("Chọn một mốc thời gian hợp lệ.")
        };
    }

    public static string KiemTraNgayNhap(string tu, string den, DateTime homNay, out DateTime tuNgay, out DateTime denNgay)
    {
        tuNgay = denNgay = default;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (!DateTime.TryParseExact(tu?.Trim(), "dd/MM/yyyy", culture, System.Globalization.DateTimeStyles.None, out tuNgay))
            return "Từ ngày phải là ngày có thật, đúng định dạng dd/MM/yyyy.";
        if (!DateTime.TryParseExact(den?.Trim(), "dd/MM/yyyy", culture, System.Globalization.DateTimeStyles.None, out denNgay))
            return "Đến ngày phải là ngày có thật, đúng định dạng dd/MM/yyyy.";
        if (tuNgay > denNgay) return "Từ ngày phải nhỏ hơn hoặc bằng Đến ngày.";
        if (denNgay > homNay.Date) return "Không chọn ngày trong tương lai.";
        try { KyTruoc(tuNgay, denNgay); }
        catch (ArgumentException ex) { return ex.Message; }
        return null;
    }

    public static DateTime DauKy(DateTime ngay, string nhomTheo)
    {
        ngay = ngay.Date;
        return nhomTheo switch
        {
            "ngay" => ngay,
            "tuan" => ngay.AddDays(-((int)ngay.DayOfWeek + 6) % 7),
            "thang" => new DateTime(ngay.Year, ngay.Month, 1),
            _ => throw new ArgumentException("Chỉ hỗ trợ nhóm ngày, tuần hoặc tháng.")
        };
    }

    public static (DateTime TuNgay, DateTime DenNgay) KyTruoc(DateTime tuNgay, DateTime denNgay)
    {
        tuNgay = tuNgay.Date;
        denNgay = denNgay.Date;
        int soNgay = (denNgay - tuNgay).Days + 1;
        if (soNgay < 1 || soNgay > 366 || tuNgay < new DateTime(1754, 1, 1) || denNgay >= DateTime.MaxValue.Date)
            throw new ArgumentException("Chọn khoảng từ 1 đến 366 ngày, bắt đầu từ năm 1754.");
        if (tuNgay.AddDays(-soNgay) < new DateTime(1753, 1, 1))
            throw new ArgumentException("Kỳ đối chiếu phải từ 01/01/1753 trở đi. Vui lòng chọn khoảng ngày muộn hơn.");
        return (tuNgay.AddDays(-soNgay), tuNgay.AddDays(-1));
    }

    public static List<BaoCaoDoanhThuDto> NhomKy(IEnumerable<BaoCaoDoanhThuDto> ds, DateTime tuNgay, DateTime denNgay, string nhomTheo)
    {
        KyTruoc(tuNgay, denNgay);
        var theoKy = ds.Where(x => x.Ngay.Date >= tuNgay.Date && x.Ngay.Date <= denNgay.Date)
            .GroupBy(x => DauKy(x.Ngay, nhomTheo))
            .ToDictionary(x => x.Key, x => new BaoCaoDoanhThuDto {
                Ngay = x.Key, SoDon = x.Sum(d => d.SoDon), DoanhThu = x.Sum(d => d.DoanhThu)
            });
        var ketQua = new List<BaoCaoDoanhThuDto>();
        DateTime kyCuoi = DauKy(denNgay, nhomTheo);
        for (DateTime ngay = DauKy(tuNgay, nhomTheo); ngay <= denNgay.Date;)
        {
            ketQua.Add(theoKy.TryGetValue(ngay, out var ky) ? ky : new BaoCaoDoanhThuDto { Ngay = ngay });
            if (ngay == kyCuoi) break;
            ngay = nhomTheo == "thang" ? ngay.AddMonths(1) : ngay.AddDays(nhomTheo == "tuan" ? 7 : 1);
        }
        return ketQua;
    }

    public static decimal? TyLeThayDoi(decimal hienTai, decimal truoc)
    {
        if (truoc == 0) return hienTai == 0 ? 0 : null;
        return (hienTai - truoc) / truoc * 100;
    }

    public static decimal BuocTruc(decimal lonNhat)
    {
        decimal can = Math.Max(1, lonNhat / 4), bac = 1;
        while (bac * 10 <= can) bac *= 10;
        foreach (decimal heSo in new[] { 1m, 2m, 2.5m, 5m, 10m })
            if (heSo * bac >= can) return heSo * bac;
        return bac * 10;
    }

    public static string Tien(decimal so) => so.ToString("#,##0", KieuSo) + "₫";
    public static string So(decimal so) => so.ToString("0.#", KieuSo);
    private static readonly System.Globalization.NumberFormatInfo KieuSo = new() { NumberGroupSeparator = ".", NumberDecimalSeparator = "," };
}
