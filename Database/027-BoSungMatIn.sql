/* Bổ sung mặt in còn thiếu cho bảng giá tài liệu. Chạy bằng sqlcmd -I -b -f 65001. */
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;

BEGIN TRAN;

-- Dòng có tên "1 mặt" bị gán nhầm thành 2_MAT. Chỉ sửa nếu chưa được dùng trong đơn.
DECLARE @A4 TABLE (MaDichVu INT PRIMARY KEY);
INSERT INTO @A4 (MaDichVu)
SELECT MaDichVu
FROM dbo.DichVu WITH (UPDLOCK, HOLDLOCK)
WHERE TenDichVu = N'In A4 70gsm 1 mặt màu' AND LoaiDichVu = 'IN_AN'
  AND KhoGiay = 'A4' AND LoaiGiay = '70gsm' AND MauSac = 'MAU'
  AND MatIn = '2_MAT' AND KichThuocAnh IS NULL;

IF EXISTS (SELECT 1 FROM dbo.ChiTietDonHang ct WITH (UPDLOCK, HOLDLOCK) JOIN @A4 a ON a.MaDichVu = ct.MaDichVu)
   OR EXISTS (SELECT 1 FROM dbo.ChiTietGiaCong ct WITH (UPDLOCK, HOLDLOCK) JOIN @A4 a ON a.MaDichVu = ct.MaDichVu)
    THROW 50270, N'Dòng A4 in màu đã có đơn. Cần giữ quy cách cũ và tạo dòng một mặt riêng.', 1;

UPDATE dv SET MatIn = '1_MAT'
FROM dbo.DichVu dv JOIN @A4 a ON a.MaDichVu = dv.MaDichVu;

-- Chỉ mở giá một mặt cho các chi nhánh đang bán A3 60gsm đen trắng hai mặt.
DECLARE @A3HaiMat TABLE (MaVatTu INT NOT NULL, MaChiNhanh INT NOT NULL, PRIMARY KEY (MaVatTu, MaChiNhanh));
INSERT INTO @A3HaiMat (MaVatTu, MaChiNhanh)
SELECT DISTINCT dv.MaVatTu, bg.MaChiNhanh
FROM dbo.DichVu dv WITH (UPDLOCK, HOLDLOCK)
JOIN dbo.BangGiaChiNhanh bg WITH (UPDLOCK, HOLDLOCK) ON bg.MaDichVu = dv.MaDichVu
WHERE dv.LoaiDichVu = 'IN_AN' AND dv.KhoGiay = 'A3' AND dv.LoaiGiay = '60GSM'
  AND dv.MatIn = '2_MAT' AND dv.MauSac = 'DEN_TRANG' AND dv.KichThuocAnh IS NULL
  AND dv.MaVatTu IS NOT NULL AND bg.TrangThai = N'HIEN';

INSERT INTO dbo.DichVu (TenDichVu, LoaiDichVu, KhoGiay, LoaiGiay, MatIn, MauSac, DonGia, MaVatTu, TrangThai)
SELECT DISTINCT N'In A3 60gsm 1 mặt đen trắng', 'IN_AN', 'A3', '60GSM', '1_MAT', 'DEN_TRANG', 500, a.MaVatTu, N'HIEN'
FROM @A3HaiMat a
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.DichVu dv WITH (UPDLOCK, HOLDLOCK)
    WHERE dv.LoaiDichVu = 'IN_AN' AND dv.KhoGiay = 'A3' AND dv.LoaiGiay = '60GSM'
      AND dv.MatIn = '1_MAT' AND dv.MauSac = 'DEN_TRANG' AND dv.KichThuocAnh IS NULL
      AND dv.MaVatTu = a.MaVatTu
);

INSERT INTO dbo.BangGiaChiNhanh (MaChiNhanh, MaDichVu, DonGia, TrangThai)
SELECT a.MaChiNhanh, dv.MaDichVu, dv.DonGia, N'HIEN'
FROM @A3HaiMat a
CROSS APPLY (
    SELECT TOP (1) MaDichVu, DonGia
    FROM dbo.DichVu
    WHERE LoaiDichVu = 'IN_AN' AND KhoGiay = 'A3' AND LoaiGiay = '60GSM'
      AND MatIn = '1_MAT' AND MauSac = 'DEN_TRANG' AND KichThuocAnh IS NULL
      AND MaVatTu = a.MaVatTu
    ORDER BY MaDichVu
) dv
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.BangGiaChiNhanh bg WITH (UPDLOCK, HOLDLOCK)
    WHERE bg.MaChiNhanh = a.MaChiNhanh AND bg.MaDichVu = dv.MaDichVu
);

COMMIT;
GO
