/* Danh mục in tài liệu A3/A4 và giá lẻ theo trang. Không sửa đơn đã lập.
   Mốc giá: Long Thịnh (longthinh.com.vn/bang-gia-in-photocopy), Huy Khánh
   (inanhuykhanh.com/photocopy) và SOC Printing (socprinting.vn/in-nhanh/).
   Mức hai mặt tính theo trang tài liệu; áp dụng chiết giảm nhẹ do dùng ít tờ hơn.
   Chạy bằng sqlcmd -I -b -f 65001. */
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @BangGia TABLE (
    KhoGiay VARCHAR(5), LoaiGiay VARCHAR(20), TenDichVu NVARCHAR(100),
    DenMotMat DECIMAL(12,0), DenHaiMat DECIMAL(12,0), MauMotMat DECIMAL(12,0), MauHaiMat DECIMAL(12,0)
);
INSERT INTO @BangGia VALUES
    ('A4','70GSM',N'In A4 70gsm',300,250,1500,1400),
    ('A4','80GSM',N'In A4 80gsm',400,350,1700,1600),
    ('A3','60GSM',N'In A3 60gsm',500,400,2700,2500),
    ('A3','70GSM',N'In A3 70gsm',600,500,3000,2800);

DECLARE @QuyCach TABLE (
    KhoGiay VARCHAR(5), LoaiGiay VARCHAR(20), MaVatTu INT, TenDichVu NVARCHAR(100),
    MatIn VARCHAR(10), MauSac VARCHAR(10), DonGia DECIMAL(12,0),
    PRIMARY KEY (KhoGiay, LoaiGiay, MaVatTu, MatIn, MauSac)
);

-- Ưu tiên nhóm vật tư đang bán mặc định; nhóm vật tư cũ vẫn giữ nguyên mã.
INSERT INTO @QuyCach
SELECT gia.KhoGiay, giay.LoaiGiay, giay.MaVatTu, gia.TenDichVu, q.MatIn, q.MauSac, q.DonGia
FROM @BangGia gia
CROSS APPLY (
    SELECT TOP (1) MIN(dv.LoaiGiay) AS LoaiGiay, dv.MaVatTu
    FROM dbo.DichVu dv WITH (UPDLOCK,HOLDLOCK)
    WHERE dv.LoaiDichVu='IN_AN' AND dv.KichThuocAnh IS NULL
      AND dv.KhoGiay=gia.KhoGiay AND dv.LoaiGiay=gia.LoaiGiay AND dv.MaVatTu IS NOT NULL
      AND (dv.TrangThai=N'HIEN' OR EXISTS (
          SELECT 1 FROM dbo.BangGiaChiNhanh b WHERE b.MaDichVu=dv.MaDichVu AND b.TrangThai=N'HIEN'))
    GROUP BY dv.MaVatTu
    ORDER BY SUM(CASE WHEN dv.TrangThai=N'HIEN' THEN 1 ELSE 0 END) DESC, COUNT(*) DESC, dv.MaVatTu
) giay
CROSS APPLY (VALUES
    ('1_MAT','DEN_TRANG',gia.DenMotMat), ('2_MAT','DEN_TRANG',gia.DenHaiMat),
    ('1_MAT','MAU',gia.MauMotMat), ('2_MAT','MAU',gia.MauHaiMat)
) q(MatIn,MauSac,DonGia);

DECLARE @ChiNhanhBan TABLE (MaChiNhanh INT, KhoGiay VARCHAR(5), LoaiGiay VARCHAR(20), PRIMARY KEY (MaChiNhanh,KhoGiay,LoaiGiay));
INSERT INTO @ChiNhanhBan
SELECT DISTINCT b.MaChiNhanh, q.KhoGiay, q.LoaiGiay
FROM dbo.BangGiaChiNhanh b WITH (UPDLOCK,HOLDLOCK)
JOIN dbo.DichVu dv ON dv.MaDichVu=b.MaDichVu
JOIN @QuyCach q ON q.KhoGiay=dv.KhoGiay AND q.LoaiGiay=dv.LoaiGiay
WHERE dv.LoaiDichVu='IN_AN' AND dv.KichThuocAnh IS NULL AND b.TrangThai=N'HIEN';

INSERT INTO dbo.DichVu (TenDichVu,LoaiDichVu,KhoGiay,LoaiGiay,MatIn,MauSac,DonGia,MaVatTu,TrangThai)
SELECT q.TenDichVu,'IN_AN',q.KhoGiay,q.LoaiGiay,q.MatIn,q.MauSac,q.DonGia,q.MaVatTu,N'HIEN'
FROM @QuyCach q
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.DichVu dv WITH (UPDLOCK,HOLDLOCK)
    WHERE dv.LoaiDichVu='IN_AN' AND dv.KichThuocAnh IS NULL AND dv.KhoGiay=q.KhoGiay AND dv.LoaiGiay=q.LoaiGiay
      AND dv.MaVatTu=q.MaVatTu AND dv.MatIn=q.MatIn AND dv.MauSac=q.MauSac
);

DECLARE @DichVu TABLE (MaDichVu INT PRIMARY KEY, KhoGiay VARCHAR(5), LoaiGiay VARCHAR(20), TenDichVu NVARCHAR(100), DonGia DECIMAL(12,0));
INSERT INTO @DichVu
SELECT MIN(dv.MaDichVu), q.KhoGiay,q.LoaiGiay,q.TenDichVu,q.DonGia
FROM dbo.DichVu dv
JOIN @QuyCach q ON dv.KhoGiay=q.KhoGiay AND dv.LoaiGiay=q.LoaiGiay
    AND dv.MaVatTu=q.MaVatTu AND dv.MatIn=q.MatIn AND dv.MauSac=q.MauSac
WHERE dv.LoaiDichVu='IN_AN' AND dv.KichThuocAnh IS NULL
GROUP BY q.KhoGiay,q.LoaiGiay,q.TenDichVu,q.MatIn,q.MauSac,q.DonGia;

UPDATE dv SET TenDichVu=d.TenDichVu, DonGia=d.DonGia, TrangThai=N'HIEN'
FROM dbo.DichVu dv JOIN @DichVu d ON d.MaDichVu=dv.MaDichVu
WHERE dv.TenDichVu<>d.TenDichVu OR dv.DonGia<>d.DonGia OR ISNULL(dv.TrangThai,N'')<>N'HIEN';

UPDATE b SET DonGia=d.DonGia, TrangThai=N'HIEN'
FROM dbo.BangGiaChiNhanh b
JOIN @DichVu d ON d.MaDichVu=b.MaDichVu
JOIN @ChiNhanhBan cn ON cn.MaChiNhanh=b.MaChiNhanh AND cn.KhoGiay=d.KhoGiay AND cn.LoaiGiay=d.LoaiGiay
WHERE b.DonGia<>d.DonGia OR b.TrangThai<>N'HIEN';

INSERT INTO dbo.BangGiaChiNhanh (MaChiNhanh,MaDichVu,DonGia,TrangThai)
SELECT cn.MaChiNhanh,d.MaDichVu,d.DonGia,N'HIEN'
FROM @DichVu d
JOIN @ChiNhanhBan cn ON cn.KhoGiay=d.KhoGiay AND cn.LoaiGiay=d.LoaiGiay
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.BangGiaChiNhanh b WITH (UPDLOCK,HOLDLOCK)
    WHERE b.MaChiNhanh=cn.MaChiNhanh AND b.MaDichVu=d.MaDichVu
);

-- Ẩn bảng giá còn sót của nhóm vật tư đã ngừng bán mặc định và có nhóm thay thế.
UPDATE b SET TrangThai=N'AN'
FROM dbo.BangGiaChiNhanh b
JOIN dbo.DichVu dv ON dv.MaDichVu=b.MaDichVu
JOIN @ChiNhanhBan cn ON cn.MaChiNhanh=b.MaChiNhanh AND cn.KhoGiay=dv.KhoGiay AND cn.LoaiGiay=dv.LoaiGiay
WHERE dv.LoaiDichVu='IN_AN' AND dv.KichThuocAnh IS NULL AND dv.TrangThai=N'AN' AND b.TrangThai=N'HIEN'
  AND NOT EXISTS (SELECT 1 FROM @DichVu d WHERE d.MaDichVu=dv.MaDichVu);

COMMIT;
GO
