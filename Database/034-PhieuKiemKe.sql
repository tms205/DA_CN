-- 034 (28/11/2026): PHIẾU KIỂM KÊ KHO.
-- Tồn kho trước đây chỉ tăng khi nhận hàng và giảm khi in, nên hao hụt ngoài in ấn (kẹt giấy, ẩm hỏng,
-- lấy dùng riêng) làm tồn trên máy lệch với kệ thật. Phiếu kiểm kê ghi lại lần đếm thực tế:
--   quản lý đếm → hệ thống ghi tồn theo sổ lúc đếm + số đếm + chênh lệch (bắt lý do nếu lệch)
--   → CEO duyệt → CỘNG CHÊNH LỆCH vào tồn hiện tại (không ghi đè, vì từ lúc đếm tới lúc duyệt vẫn có đơn in).
-- Mỗi phiếu một vật tư, giống phiếu nhập. Mỗi (chi nhánh, vật tư) chỉ có 1 phiếu chờ duyệt cùng lúc,
-- để 2 phiếu cùng chờ không bị cộng chênh lệch 2 lần.
-- Chạy bằng sqlcmd có -I (QUOTED_IDENTIFIER ON) vì có chỉ mục lọc.
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
IF OBJECT_ID('dbo.PhieuKiemKe','U') IS NULL
BEGIN
    CREATE TABLE dbo.PhieuKiemKe (
        MaPhieuKiemKe   INT IDENTITY(1,1) PRIMARY KEY,
        MaChiNhanh      INT NOT NULL REFERENCES dbo.ChiNhanh(MaChiNhanh),
        MaVatTu         INT NOT NULL REFERENCES dbo.VatTu(MaVatTu),
        TonTheoSo       INT NOT NULL,                       -- tồn trên máy lúc lập phiếu
        SoLuongThucDem  INT NOT NULL CONSTRAINT CK_PhieuKiemKe_ThucDem CHECK (SoLuongThucDem >= 0),
        ChenhLech       INT NOT NULL,                       -- = thực đếm - tồn theo sổ (âm là hao hụt)
        LyDo            NVARCHAR(255) NULL,                 -- bắt buộc khi chênh lệch khác 0 (API kiểm)
        MaNguoiLap      INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        MaNguoiDuyet    INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        TrangThai       VARCHAR(20) NOT NULL CONSTRAINT DF_PhieuKiemKe_TrangThai DEFAULT 'CHO_DUYET'
                        CONSTRAINT CK_PhieuKiemKe_TrangThai CHECK (TrangThai IN ('CHO_DUYET','DA_DUYET','TU_CHOI')),
        NgayLap         DATETIME NOT NULL CONSTRAINT DF_PhieuKiemKe_NgayLap DEFAULT GETDATE(),
        NgayDuyet       DATETIME NULL,
        CONSTRAINT CK_PhieuKiemKe_ChenhLech CHECK (ChenhLech = SoLuongThucDem - TonTheoSo)
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PhieuKiemKe_MotPhieuChoDuyet')
    CREATE UNIQUE INDEX UX_PhieuKiemKe_MotPhieuChoDuyet ON dbo.PhieuKiemKe(MaChiNhanh, MaVatTu)
    WHERE TrangThai = 'CHO_DUYET';
COMMIT;
GO
