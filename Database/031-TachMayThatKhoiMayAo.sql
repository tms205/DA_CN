-- Dữ liệu cấu hình: giữ máy ảo 1/2/4, đăng ký riêng hai máy thật ở chi nhánh 1.
-- Không đổi bảng/cột, không sửa lịch sử đơn hoặc counter máy cũ.
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRAN;
IF EXISTS(SELECT 1 FROM DonHang WITH(UPDLOCK,HOLDLOCK) WHERE MaMayIn IN(1,2,4) AND TrangThai='DANG_IN')
    THROW 50310,N'Còn đơn đang in; chưa được tách cấu hình máy.',1;
IF NOT EXISTS(SELECT 1 FROM MayIn WITH(UPDLOCK,HOLDLOCK) WHERE MaChiNhanh=1 AND TenMay=N'HP LaserJet MFP M236sdw')
    INSERT MayIn(MaChiNhanh,TenMay,KhoGiay,InMau,In2Mat,TocDo,SoTrangDaIn,TrangThai)
    VALUES(1,N'HP LaserJet MFP M236sdw','A4',0,1,20,0,N'HOAT_DONG');
IF NOT EXISTS(SELECT 1 FROM MayIn WITH(UPDLOCK,HOLDLOCK) WHERE MaChiNhanh=1 AND TenMay=N'HP LaserJet 1020')
    INSERT MayIn(MaChiNhanh,TenMay,KhoGiay,InMau,In2Mat,TocDo,SoTrangDaIn,TrangThai)
    VALUES(1,N'HP LaserJet 1020','A4',0,0,14,0,N'HOAT_DONG');
-- Tốc độ trên là cấu hình ước tính; gửi máy thật vẫn kiểm năng lực từ driver Windows.
COMMIT;
SELECT MaMayIn,TenMay FROM MayIn WHERE MaChiNhanh=1 AND TenMay IN(N'HP LaserJet MFP M236sdw',N'HP LaserJet 1020');
GO
