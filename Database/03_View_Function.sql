/* =============================================================
   FILE 03: HAM VA VIEW
   ============================================================= */

USE PhotocopyChain;
GO

-- Kiem tra nguoi dung co phai Quan ly cua chi nhanh (hoac Super Admin) khong
CREATE FUNCTION fn_LaQuanLy (@MaNguoiDung INT, @MaChiNhanh INT)
RETURNS BIT
AS
BEGIN
    IF EXISTS (SELECT 1 FROM NguoiDung
               WHERE MaNguoiDung = @MaNguoiDung
                 AND (VaiTro = 'SUPER_ADMIN' OR (VaiTro = 'BRANCH_ADMIN' AND MaChiNhanh = @MaChiNhanh)))
        RETURN 1;
    RETURN 0;
END;
GO

CREATE FUNCTION fn_LaSuperAdmin (@MaNguoiDung INT)
RETURNS BIT
AS
BEGIN
    IF EXISTS (SELECT 1 FROM NguoiDung WHERE MaNguoiDung = @MaNguoiDung AND VaiTro = 'SUPER_ADMIN')
        RETURN 1;
    RETURN 0;
END;
GO

-- Ca dang mo cua nhan vien (NULL neu chua mo ca)
CREATE FUNCTION fn_CaDangMo (@MaNhanVien INT)
RETURNS INT
AS
BEGIN
    RETURN (SELECT TOP 1 MaCa FROM CaLamViec WHERE MaNhanVien = @MaNhanVien AND TrangThai = 'DANG_MO');
END;
GO

-- Uoc tinh gio in xong: (so trang dang cho + so trang cua don) / toc do may nhanh nhat
CREATE FUNCTION fn_UocTinhGioXong (@MaChiNhanh INT, @SoTrang INT, @InGap BIT)
RETURNS DATETIME
AS
BEGIN
    DECLARE @TocDo INT, @TrangCho INT;

    SELECT @TocDo = MAX(TocDo) FROM MayIn WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = N'HOAT_DONG';
    IF @TocDo IS NULL RETURN NULL;

    SELECT @TrangCho = ISNULL(SUM(ct.SoTrang * ct.SoBan), 0)
    FROM ChiTietDonHang ct JOIN DonHang dh ON dh.MaDonHang = ct.MaDonHang
    WHERE dh.MaChiNhanh = @MaChiNhanh AND dh.TrangThai IN ('CHO_XAC_NHAN','DA_XAC_NHAN','DANG_IN');

    -- don in gap duoc uu tien nen khong phai cho
    IF @InGap = 1 SET @TrangCho = 0;

    RETURN DATEADD(MINUTE, CEILING((@TrangCho + @SoTrang) * 1.0 / @TocDo), GETDATE());
END;
GO

-- Danh sach don hang day du thong tin
CREATE VIEW vw_DonHang AS
SELECT dh.MaDonHang, dh.NgayDat, kh.HoTen AS KhachHang, kh.SoDienThoai, cn.TenChiNhanh,
       nv.HoTen AS ThoIn, dh.HinhThucNhan, dh.TrangThai,
       dh.TienHang, dh.PhiShip, dh.GiamGia, dh.TongTien,
       tt.PhuongThuc, tt.TrangThai AS TrangThaiThanhToan, dh.NgayHoanThanh
FROM DonHang dh
JOIN NguoiDung kh ON kh.MaNguoiDung = dh.MaKhachHang
JOIN ChiNhanh cn ON cn.MaChiNhanh = dh.MaChiNhanh
LEFT JOIN NguoiDung nv ON nv.MaNguoiDung = dh.MaNhanVien
LEFT JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang;
GO

-- Doanh thu theo ngay cua tung chi nhanh
CREATE VIEW vw_DoanhThuNgay AS
SELECT dh.MaChiNhanh, cn.TenChiNhanh, CAST(dh.NgayHoanThanh AS DATE) AS Ngay,
       COUNT(*) AS SoDon,
       SUM(CASE WHEN tt.PhuongThuc = 'COD' THEN tt.SoTien ELSE 0 END) AS TienMat,
       SUM(CASE WHEN tt.PhuongThuc = 'VIETQR' THEN tt.SoTien ELSE 0 END) AS ChuyenKhoan,
       SUM(tt.SoTien) AS TongDoanhThu
FROM DonHang dh
JOIN ChiNhanh cn ON cn.MaChiNhanh = dh.MaChiNhanh
JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
WHERE dh.TrangThai = 'HOAN_THANH'
GROUP BY dh.MaChiNhanh, cn.TenChiNhanh, CAST(dh.NgayHoanThanh AS DATE);
GO

-- Vat tu sap het
CREATE VIEW vw_CanhBaoTonKho AS
SELECT tk.MaChiNhanh, cn.TenChiNhanh, vt.TenVatTu, tk.SoLuong, tk.MucToiThieu
FROM TonKho tk
JOIN VatTu vt ON vt.MaVatTu = tk.MaVatTu
JOIN ChiNhanh cn ON cn.MaChiNhanh = tk.MaChiNhanh
WHERE tk.SoLuong <= tk.MucToiThieu;
GO

-- Diem danh gia trung binh cua chi nhanh
CREATE VIEW vw_DanhGiaChiNhanh AS
SELECT cn.MaChiNhanh, cn.TenChiNhanh,
       ISNULL(AVG(CAST(dg.SoSao AS FLOAT)), 0) AS DiemTrungBinh,
       COUNT(dg.MaDanhGia) AS SoLuotDanhGia
FROM ChiNhanh cn
LEFT JOIN DonHang dh ON dh.MaChiNhanh = cn.MaChiNhanh
LEFT JOIN DanhGia dg ON dg.MaDonHang = dh.MaDonHang AND dg.TrangThai = N'HIEN'
GROUP BY cn.MaChiNhanh, cn.TenChiNhanh;
GO

-- Hieu suat giao hang cua shipper
CREATE VIEW vw_HieuSuatShipper AS
SELECT gh.MaShipper, nd.HoTen, COUNT(*) AS SoLanGiao,
       SUM(CASE WHEN gh.TrangThai = 'THANH_CONG' THEN 1 ELSE 0 END) AS ThanhCong,
       SUM(CASE WHEN gh.TrangThai = 'THAT_BAI' THEN 1 ELSE 0 END) AS ThatBai
FROM GiaoHang gh
JOIN NguoiDung nd ON nd.MaNguoiDung = gh.MaShipper
GROUP BY gh.MaShipper, nd.HoTen;
GO

-- Doi soat may in: so trang tren may so voi so trang he thong ghi nhan (phat hien in ngoai he thong).
-- Don huy khi chua in thi da bo MaMayIn (sp_HuyDonHang), nen don huy con MaMayIn la don da in that.
CREATE VIEW vw_DoiSoatMayIn AS
SELECT m.MaMayIn, m.TenMay, m.MaChiNhanh, m.SoTrangDaIn AS SoTrangTrenMay,
       ISNULL(SUM(ct.SoTrang * ct.SoBan), 0) AS SoTrangHeThong,
       m.SoTrangDaIn - ISNULL(SUM(ct.SoTrang * ct.SoBan), 0) AS ChenhLech
FROM MayIn m
LEFT JOIN DonHang dh ON dh.MaMayIn = m.MaMayIn AND dh.TrangThai NOT IN ('CHO_XAC_NHAN','DA_XAC_NHAN')
LEFT JOIN ChiTietDonHang ct ON ct.MaDonHang = dh.MaDonHang
GROUP BY m.MaMayIn, m.TenMay, m.MaChiNhanh, m.SoTrangDaIn;
GO

-- Ca lam viec va tien chenh lech (am la thieu, duong la thua)
CREATE VIEW vw_CaLamViec AS
SELECT ca.MaCa, cn.TenChiNhanh, nv.HoTen AS NhanVien, ca.GioMoCa, ca.GioDongCa,
       ca.TienDauCa, ca.DoanhThuTrongCa, ca.TongDoanhThuCuoiCa, ca.TienQuanLyDemCuoiCa,
       COALESCE(ca.TienQuanLyDemCuoiCa, ca.TongDoanhThuCuoiCa) - (ca.TienDauCa + ca.DoanhThuTrongCa) AS ChenhLech,
       ca.TienChi, ca.TienBu, ca.TrangThai
FROM CaLamViec ca
JOIN ChiNhanh cn ON cn.MaChiNhanh = ca.MaChiNhanh
JOIN NguoiDung nv ON nv.MaNguoiDung = ca.MaNhanVien;
GO

PRINT N'Da tao ham va view';
GO
