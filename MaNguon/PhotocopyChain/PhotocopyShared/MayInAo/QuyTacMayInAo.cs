using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyShared.MayInAo;

/// <summary>Các phép tính thuần dùng chung giữa API, simulator và kiểm thử.</summary>
public static class QuyTacMayInAo
{
    public static bool FilePhucHoiHopLe(int soTrangCu, int soTrangMoi, string trangThaiMoi, bool conFile)
    {
        return soTrangCu > 0 && soTrangCu == soTrangMoi && trangThaiMoi == "SAN_SANG" && conFile;
    }

    public static bool TepSanSang(string trangThai, string duongDan, bool tepVatLyTonTai)
    {
        return string.Equals(trangThai, "SAN_SANG", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(duongDan)
            && tepVatLyTonTai;
    }

    public static bool DapUngQuyCach(
        string khoGiayToiDa,
        bool mayInMau,
        bool mayInHaiMat,
        string khoGiayCan,
        bool canInMau,
        bool canInHaiMat)
    {
        bool duKhoGiay = string.Equals(khoGiayCan, "A4", StringComparison.OrdinalIgnoreCase)
            || string.Equals(khoGiayToiDa, "A3", StringComparison.OrdinalIgnoreCase);

        return duKhoGiay
            && (!canInMau || mayInMau)
            && (!canInHaiMat || mayInHaiMat);
    }

    public static double TinhSoGiay(CauHinhTocDoMayIn may, IEnumerable<DongLenhInDto> dsDong)
    {
        double tongSoGiay = may.SoGiayRaTrangDau;

        foreach (DongLenhInDto dong in dsDong)
        {
            double tocDo = ChonTocDo(may, dong);
            if (tocDo <= 0)
                throw new InvalidOperationException("Tốc độ máy in phải lớn hơn 0.");

            int soMatIn = Math.Max(0, dong.SoTrang) * Math.Max(0, dong.SoBan);
            tongSoGiay += soMatIn * 60d / tocDo;
        }

        return Math.Max(1, tongSoGiay);
    }

    private static double ChonTocDo(CauHinhTocDoMayIn may, DongLenhInDto dong)
    {
        if (string.Equals(dong.KhoGiay, "A3", StringComparison.OrdinalIgnoreCase))
            return may.TocDoA3;
        if (string.Equals(dong.MatIn, "2_MAT", StringComparison.OrdinalIgnoreCase))
            return may.TocDoHaiMat;
        return may.TocDoA4;
    }
}
