-- 035 (28/11/2026): THEO DÕI HỘP MỰC CỦA MÁY IN.
-- Mực không trừ theo từng trang (mỗi trang tốn mực khác nhau tùy độ phủ). Thay vào đó:
--   * Trừ 1 hộp mực trong kho ĐÚNG LÚC nhân viên thay hộp (lúc hộp mực thật sự rời kho).
--   * Bộ đếm trang của máy (MayIn.SoTrangDaIn, đã có) so với "năng suất hộp mực" hãng công bố
--     để cảnh báo sắp hết mực, và đánh dấu nghi vấn khi hộp cũ hết quá sớm.
-- Thêm 3 cột cho MayIn và 1 bảng lịch sử thay mực. Không đổi dữ liệu cũ ngoài việc đặt mốc đếm ban đầu.
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
IF COL_LENGTH('dbo.MayIn','MaVatTuMuc') IS NULL
    ALTER TABLE dbo.MayIn ADD MaVatTuMuc INT NULL CONSTRAINT FK_MayIn_VatTuMuc REFERENCES dbo.VatTu(MaVatTu);
IF COL_LENGTH('dbo.MayIn','NangSuatHopMuc') IS NULL
    ALTER TABLE dbo.MayIn ADD NangSuatHopMuc INT NOT NULL CONSTRAINT DF_MayIn_NangSuatHopMuc DEFAULT 2000
        CONSTRAINT CK_MayIn_NangSuatHopMuc CHECK (NangSuatHopMuc BETWEEN 100 AND 1000000);
IF COL_LENGTH('dbo.MayIn','SoTrangLucThayMuc') IS NULL
    ALTER TABLE dbo.MayIn ADD SoTrangLucThayMuc INT NOT NULL CONSTRAINT DF_MayIn_SoTrangLucThayMuc DEFAULT 0;
GO
-- Mốc ban đầu: coi như hộp mực hiện tại mới lắp lúc chạy script, nên đếm từ số trang hiện có.
-- Gán sẵn loại mực theo năng lực máy: máy màu dùng "Mực màu", còn lại "Mực đen" (quản lý sửa lại được trên Web).
UPDATE m SET SoTrangLucThayMuc = ISNULL(m.SoTrangDaIn, 0),
             MaVatTuMuc = COALESCE(m.MaVatTuMuc,
                 (SELECT TOP 1 MaVatTu FROM dbo.VatTu WHERE TenVatTu = CASE WHEN m.InMau = 1 THEN N'Mực màu' ELSE N'Mực đen' END ORDER BY MaVatTu))
FROM dbo.MayIn m
WHERE m.SoTrangLucThayMuc = 0;
GO
IF OBJECT_ID('dbo.LichSuThayMuc','U') IS NULL
    CREATE TABLE dbo.LichSuThayMuc (
        MaThayMuc       INT IDENTITY(1,1) PRIMARY KEY,
        MaMayIn         INT NOT NULL REFERENCES dbo.MayIn(MaMayIn),
        MaVatTu         INT NOT NULL REFERENCES dbo.VatTu(MaVatTu),
        SoTrangHopCu    INT NOT NULL,      -- số trang hộp cũ đã in được trước khi thay
        NangSuatHopMuc  INT NOT NULL,      -- năng suất áp dụng lúc thay (để xem lại sau)
        NghiVan         BIT NOT NULL,      -- 1 khi hộp cũ hết dưới 50% năng suất
        MaNguoiThay     INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiGian        DATETIME NOT NULL CONSTRAINT DF_LichSuThayMuc_ThoiGian DEFAULT GETDATE()
    );
COMMIT;
GO
