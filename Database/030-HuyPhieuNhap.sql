-- Hủy phiếu chưa nhập kho; không thay đổi tồn kho hoặc phiếu cũ.
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
IF COL_LENGTH('dbo.PhieuNhap','LyDoHuy') IS NULL
    ALTER TABLE dbo.PhieuNhap ADD LyDoHuy NVARCHAR(500) NULL;
GO
CREATE OR ALTER PROCEDURE dbo.sp_HuyPhieuNhap
    @MaPhieuNhap INT, @MaNguoiHuy INT, @LyDoHuy NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @LyDoHuy=NULLIF(LTRIM(RTRIM(@LyDoHuy)),N'');
    IF @LyDoHuy IS NULL THROW 50301,N'Vui lòng nhập lý do hủy phiếu nhập.',1;
    BEGIN TRY
        BEGIN TRAN;
        DECLARE @ChiNhanh INT,@TrangThai VARCHAR(20),@VaiTro VARCHAR(20),@ChiNhanhNguoiHuy INT;
        SELECT @ChiNhanh=MaChiNhanh,@TrangThai=TrangThai FROM dbo.PhieuNhap WITH(UPDLOCK,HOLDLOCK)
        WHERE MaPhieuNhap=@MaPhieuNhap;
        IF @ChiNhanh IS NULL THROW 50302,N'Không tìm thấy phiếu nhập.',1;
        SELECT @VaiTro=VaiTro,@ChiNhanhNguoiHuy=MaChiNhanh FROM dbo.NguoiDung
        WHERE MaNguoiDung=@MaNguoiHuy AND TrangThai='HOAT_DONG';
        IF @VaiTro IS NULL OR @VaiTro NOT IN('SUPER_ADMIN','BRANCH_ADMIN')
           OR (@VaiTro='BRANCH_ADMIN' AND ISNULL(@ChiNhanhNguoiHuy,-1)<>@ChiNhanh)
            THROW 50303,N'Bạn không có quyền hủy phiếu nhập của chi nhánh này.',1;
        IF @TrangThai NOT IN('CHO_DUYET','DA_DUYET')
            THROW 50304,N'Chỉ hủy phiếu chưa nhận hàng. Phiếu đã nhập kho, đã hủy hoặc từ chối không được hủy.',1;
        UPDATE dbo.PhieuNhap SET TrangThai='DA_HUY',LyDoHuy=@LyDoHuy
        WHERE MaPhieuNhap=@MaPhieuNhap AND TrangThai IN('CHO_DUYET','DA_DUYET');
        IF @@ROWCOUNT<>1 THROW 50304,N'Phiếu vừa thay đổi trạng thái; hãy tải lại.',1;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH
END;
GO
COMMIT;
GO
