USE PhotocopyChain;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
IF COL_LENGTH('FileIn', 'SoLuongAnh') IS NULL
    THROW 51010, N'Chưa có metadata ảnh thẻ.', 1;
GO

-- Toàn bộ dữ liệu thử được rollback; không tạo thanh toán thật.
BEGIN TRAN;
DECLARE @Khach INT = (SELECT TOP 1 MaNguoiDung FROM NguoiDung WHERE VaiTro = 'CUSTOMER'),
        @CN INT = (SELECT TOP 1 MaChiNhanh FROM ChiNhanh), @File INT, @DV INT, @Don INT;
SELECT TOP 1 @DV = MaDichVu FROM DichVu WHERE KichThuocAnh = '3X4';
INSERT FileIn(MaKhachHang,TenFile,DinhDang,SoTrang,KichThuocAnh,SoLuongAnh)
VALUES(@Khach,N'KiemThu_AnhThe.pdf','pdf',1,'3X4',8);
SET @File=SCOPE_IDENTITY();
DECLARE @F Kieu_DongDonHang, @G Kieu_DongGiaCong;
INSERT @F VALUES(@File,@DV,1,1);
DECLARE @Gia TABLE(TienIn DECIMAL(12,0), TienGiaCong DECIMAL(12,0), PhuPhiGap DECIMAL(12,0),
    PhiShip DECIMAL(12,0), GiamGia DECIMAL(12,0), TongTien DECIMAL(12,0), TongSoTrang INT,
    KhuyenMaiHopLe BIT, LyDoKhuyenMai NVARCHAR(200), NgayHenTra DATETIME);
INSERT @Gia EXEC sp_BaoGia @CN,'TAI_QUAY',NULL,NULL,0,NULL,@F,@G;
IF (SELECT TienIn FROM @Gia) <> (SELECT DonGia*8 FROM BangGiaChiNhanh WHERE MaChiNhanh=@CN AND MaDichVu=@DV)
    THROW 51011, N'Giá ảnh không tính theo số ảnh.',1;
IF (SELECT TongSoTrang FROM @Gia) <> 1 THROW 51012,N'Nhầm số ảnh thành số tờ PDF.',1;
EXEC sp_TaoDonHang @Khach,@CN,'TAI_QUAY','COD',NULL,NULL,NULL,NULL,0,NULL,@F,@G,@Don OUTPUT;
IF NOT EXISTS(SELECT 1 FROM ChiTietDonHang WHERE MaDonHang=@Don AND SoLuongAnh=8 AND KichThuocAnh='3X4' AND SoTrang=1 AND SoBan=1)
    THROW 51013,N'Chi tiết đơn chưa lưu snapshot số ảnh.',1;
ROLLBACK;
PRINT N'PASS: báo giá theo 8 ảnh, máy in 1 tờ, snapshot đơn; dữ liệu thử đã rollback.';
GO
