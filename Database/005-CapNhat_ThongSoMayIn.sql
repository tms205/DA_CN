USE PhotocopyChain;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

-- Chỉ cập nhật dữ liệu mẫu theo thông số công bố của hãng.
-- Script không thêm bảng, cột, khóa hay chỉ mục mới.
UPDATE MayIn
SET In2Mat = 1,
    TocDo = 40
WHERE TenMay = N'Xerox WorkCentre 3345';

UPDATE MayIn
SET TenMay = N'HP LaserJet Pro M404dn',
    In2Mat = 1,
    TocDo = 38
WHERE TenMay IN (N'HP LaserJet M404', N'HP LaserJet Pro M404dn');
GO

SELECT MaMayIn, MaChiNhanh, TenMay, KhoGiay, InMau, In2Mat, TocDo, TrangThai
FROM MayIn
ORDER BY MaChiNhanh, MaMayIn;
GO
