-- Chỉ gỡ khi chưa có đơn mới dùng metadata; không xóa lịch sử chống trùng đang tồn tại.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRAN;
IF COL_LENGTH('dbo.DonHang','MaYeuCauTao') IS NOT NULL
BEGIN
    EXEC(N'IF EXISTS(SELECT 1 FROM dbo.DonHang WHERE MaYeuCauTao IS NOT NULL)
        THROW 50230,N''Đã có đơn sử dụng mã yêu cầu. Giữ metadata để tránh tạo trùng; không tự rollback cột.'',1;');
    IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.DonHang') AND name='UX_DonHang_MaYeuCauTao')
        DROP INDEX UX_DonHang_MaYeuCauTao ON dbo.DonHang;
    IF OBJECT_ID('dbo.CK_DonHang_YeuCauTao','C') IS NOT NULL ALTER TABLE dbo.DonHang DROP CONSTRAINT CK_DonHang_YeuCauTao;
    ALTER TABLE dbo.DonHang DROP COLUMN MaYeuCauTao,DauYeuCauTao;
END;
COMMIT;
