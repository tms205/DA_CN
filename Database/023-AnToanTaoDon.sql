-- Thêm metadata chống tạo đơn trùng; không thêm bảng hoặc sửa đơn cũ.
-- Chỉ áp dụng sau khi được chấp thuận. Kiểm thử bằng bảng tạm trước khi chạy trên database đang dùng.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRAN;
IF COL_LENGTH('dbo.DonHang','MaYeuCauTao') IS NULL
    ALTER TABLE dbo.DonHang ADD MaYeuCauTao UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.DonHang','DauYeuCauTao') IS NULL
    ALTER TABLE dbo.DonHang ADD DauYeuCauTao CHAR(64) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.DonHang') AND name='CK_DonHang_YeuCauTao')
    EXEC(N'ALTER TABLE dbo.DonHang ADD CONSTRAINT CK_DonHang_YeuCauTao CHECK
        ((MaYeuCauTao IS NULL AND DauYeuCauTao IS NULL) OR (MaYeuCauTao IS NOT NULL AND DauYeuCauTao IS NOT NULL));');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.DonHang') AND name='UX_DonHang_MaYeuCauTao')
    EXEC(N'CREATE UNIQUE INDEX UX_DonHang_MaYeuCauTao ON dbo.DonHang(MaYeuCauTao) WHERE MaYeuCauTao IS NOT NULL;');
COMMIT;
