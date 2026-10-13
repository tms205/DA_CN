namespace PhotocopyApi.DuLieu;

// Chỉ SELECT, không thay schema/dữ liệu. Các mốc ngày và phạm vi luôn là tham số.
internal static class BaoCaoTongQuanSql
{
    private const string DonHoanTat = @"
        FROM DonHang dh
        WHERE dh.TrangThai='HOAN_THANH'
          AND dh.NgayHoanThanh>=@TuNgay AND dh.NgayHoanThanh<@NgaySau
          AND ISNULL(dh.LoaiPhatSinh,'')<>'IN_LAI_LOI_QUAY'
          AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh)";

    public const string PhanTich = @"
        SELECT ISNULL(SUM(dh.TienHang),0) AS TienHang,
               ISNULL(SUM(dh.PhiShip),0) AS PhiShip,
               ISNULL(SUM(dh.GiamGia),0) AS GiamGia,
               ISNULL(SUM(dh.TongTien),0) AS DoanhThu " + DonHoanTat + @";

        WITH don AS (SELECT dh.MaDonHang " + DonHoanTat + @"),
        giaCong AS (
            SELECT gc.MaChiTiet, SUM(gc.GiaGiaCong) AS DonGia
            FROM ChiTietGiaCong gc GROUP BY gc.MaChiTiet
        ), dong AS (
            SELECT ct.MaDonHang,
                   CASE WHEN ct.KichThuocAnh IS NOT NULL THEN N'Ảnh thẻ'
                        WHEN dv.MauSac='MAU' THEN N'In màu'
                        WHEN dv.MauSac='DEN_TRANG' THEN N'In đen trắng'
                        ELSE dv.TenDichVu END AS Ten,
                   ISNULL(ct.ThanhTien,0)-ISNULL(gc.DonGia,0)*ct.SoBan AS SoTien
            FROM don JOIN ChiTietDonHang ct ON ct.MaDonHang=don.MaDonHang
            JOIN DichVu dv ON dv.MaDichVu=ct.MaDichVu
            LEFT JOIN giaCong gc ON gc.MaChiTiet=ct.MaChiTiet
            UNION ALL
            SELECT ct.MaDonHang, dv.TenDichVu, gc.GiaGiaCong*ct.SoBan
            FROM don JOIN ChiTietDonHang ct ON ct.MaDonHang=don.MaDonHang
            JOIN ChiTietGiaCong gc ON gc.MaChiTiet=ct.MaChiTiet
            JOIN DichVu dv ON dv.MaDichVu=gc.MaDichVu
        )
        SELECT Ten, SUM(SoTien) AS SoTien, COUNT(DISTINCT MaDonHang) AS SoDon
        FROM dong GROUP BY Ten ORDER BY SoTien DESC;

        WITH don AS (SELECT dh.MaDonHang, dh.TongTien " + DonHoanTat + @")
        SELECT CASE tt.PhuongThuc WHEN 'VIETQR' THEN N'VietQR'
                    WHEN 'COD' THEN N'COD (quầy / giao hàng)'
                    ELSE N'Chưa xác định' END AS Ten,
               SUM(don.TongTien) AS SoTien, COUNT(*) AS SoDon
        FROM don LEFT JOIN ThanhToan tt ON tt.MaDonHang=don.MaDonHang
        GROUP BY tt.PhuongThuc ORDER BY SoTien DESC;

        SELECT ((DATEDIFF(DAY,'19000101',CAST(dh.NgayDat AS DATE))%7)+7)%7 AS Thu,
               DATEPART(HOUR,dh.NgayDat) AS Gio, COUNT(*) AS SoDon
        FROM DonHang dh
        WHERE dh.NgayDat>=@TuNgay AND dh.NgayDat<@NgaySau
          AND ISNULL(dh.LoaiPhatSinh,'')<>'IN_LAI_LOI_QUAY'
          AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh)
        GROUP BY ((DATEDIFF(DAY,'19000101',CAST(dh.NgayDat AS DATE))%7)+7)%7,
                 DATEPART(HOUR,dh.NgayDat);

        SELECT COUNT(*) AS SoDon,
               COUNT(CASE WHEN dh.NgayHoanThanh>=dh.NgayDat THEN 1 END) AS SoDonCoMoc,
               AVG(CASE WHEN dh.NgayHoanThanh>=dh.NgayDat
                   THEN CAST(DATEDIFF_BIG(SECOND,dh.NgayDat,dh.NgayHoanThanh) AS DECIMAL(18,2))/60 END) AS PhutXuLyTrungBinh,
               COUNT(CASE WHEN dh.NgayHenTra>=dh.NgayDat AND dh.NgayHoanThanh>=dh.NgayDat THEN 1 END) AS SoDonCoHen,
               COUNT(CASE WHEN dh.NgayHenTra>=dh.NgayDat AND dh.NgayHoanThanh>=dh.NgayDat
                   AND dh.NgayHoanThanh<=dh.NgayHenTra THEN 1 END) AS SoDonDungHen " + DonHoanTat + @";

        SELECT COUNT(*) AS SoLuot, AVG(CAST(dg.SoSao AS DECIMAL(8,2))) AS DiemTrungBinh
        FROM DanhGia dg JOIN DonHang dh ON dh.MaDonHang=dg.MaDonHang
        WHERE dg.TrangThai=N'HIEN' AND dg.NgayDanhGia>=@TuNgay AND dg.NgayDanhGia<@NgaySau
          AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh);";

    public const string HienTai = @"
        SELECT COUNT(CASE WHEN ca.TrangThai='DANG_MO' THEN 1 END) AS SoCaDangMo,
               COUNT(CASE WHEN ca.TrangThai='CHO_XAC_NHAN' THEN 1 END) AS SoCaChoXacNhan
        FROM CaLamViec ca WHERE (@MaChiNhanh IS NULL OR ca.MaChiNhanh=@MaChiNhanh);

        WITH ca AS (
            SELECT COALESCE(TienQuanLyDemCuoiCa,TongDoanhThuCuoiCa)
                       -(TienDauCa+DoanhThuTrongCa-TienChi+TienBu) AS ChenhLech
            FROM CaLamViec
            WHERE TrangThai<>'DANG_MO' AND GioDongCa>=@TuNgay AND GioDongCa<=@DocLuc
              AND COALESCE(TienQuanLyDemCuoiCa,TongDoanhThuCuoiCa) IS NOT NULL
              AND (@MaChiNhanh IS NULL OR MaChiNhanh=@MaChiNhanh)
        )
        SELECT COUNT(*) AS SoCaKiemDem,
               COUNT(CASE WHEN ChenhLech<>0 THEN 1 END) AS SoCaLech,
               ISNULL(SUM(ChenhLech),0) AS TienLech FROM ca;

        SELECT COUNT(*) FROM PhieuNhap WHERE TrangThai='CHO_DUYET'
          AND (@MaChiNhanh IS NULL OR MaChiNhanh=@MaChiNhanh);
        SELECT COUNT(*) FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
        WHERE dh.TrangThai='DA_HUY' AND tt.TrangThai='DA_THANH_TOAN'
          AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh);";
}
