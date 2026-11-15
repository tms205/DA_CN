-- Chạy với database tempdb; không sửa dữ liệu nghiệp vụ.
:ON ERROR EXIT
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
CREATE TABLE #PhieuNhap(MaPhieuNhap INT PRIMARY KEY,MaChiNhanh INT,TrangThai VARCHAR(20),LyDoHuy NVARCHAR(500));
CREATE TABLE #NguoiDung(MaNguoiDung INT PRIMARY KEY,MaChiNhanh INT,VaiTro VARCHAR(20),TrangThai VARCHAR(20));
INSERT #PhieuNhap VALUES(1,1,'DA_DUYET',NULL),(2,1,'CHO_DUYET',NULL),(3,1,'DA_NHAP',NULL),(4,1,'DA_DUYET',NULL),(5,1,'DA_DUYET',NULL);
INSERT #NguoiDung VALUES(10,1,'BRANCH_ADMIN','HOAT_DONG'),(11,2,'BRANCH_ADMIN','HOAT_DONG'),(12,NULL,'SUPER_ADMIN','HOAT_DONG'),(13,1,'STAFF','HOAT_DONG');
GO

CREATE PROCEDURE #HuyPhieuNhap
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
        SELECT @ChiNhanh=MaChiNhanh,@TrangThai=TrangThai FROM #PhieuNhap WITH(UPDLOCK,HOLDLOCK)
        WHERE MaPhieuNhap=@MaPhieuNhap;
        IF @ChiNhanh IS NULL THROW 50302,N'Không tìm thấy phiếu nhập.',1;
        SELECT @VaiTro=VaiTro,@ChiNhanhNguoiHuy=MaChiNhanh FROM #NguoiDung
        WHERE MaNguoiDung=@MaNguoiHuy AND TrangThai='HOAT_DONG';
        IF @VaiTro IS NULL OR @VaiTro NOT IN('SUPER_ADMIN','BRANCH_ADMIN')
           OR (@VaiTro='BRANCH_ADMIN' AND ISNULL(@ChiNhanhNguoiHuy,-1)<>@ChiNhanh)
            THROW 50303,N'Bạn không có quyền hủy phiếu nhập của chi nhánh này.',1;
        IF @TrangThai NOT IN('CHO_DUYET','DA_DUYET')
            THROW 50304,N'Chỉ hủy phiếu chưa nhận hàng. Phiếu đã nhập kho, đã hủy hoặc từ chối không được hủy.',1;
        UPDATE #PhieuNhap SET TrangThai='DA_HUY',LyDoHuy=@LyDoHuy
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
EXEC #HuyPhieuNhap 1,10,N'Nhà cung cấp không còn hàng';
EXEC #HuyPhieuNhap 2,12,N'Không còn nhu cầu nhập';
IF (SELECT COUNT(*) FROM #PhieuNhap WHERE TrangThai='DA_HUY' AND LyDoHuy IS NOT NULL)<>2 THROW 51000,N'Không lưu trạng thái/lý do',1;
DECLARE @Loi INT=0;
BEGIN TRY EXEC #HuyPhieuNhap 3,10,N'Đã nhận rồi'; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); END CATCH;
IF @Loi<>50304 THROW 51000,N'Không chặn phiếu đã nhập',1;
SET @Loi=0;
BEGIN TRY EXEC #HuyPhieuNhap 4,11,N'Khác chi nhánh'; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); END CATCH;
IF @Loi<>50303 THROW 51000,N'Không chặn khác chi nhánh',1;
SET @Loi=0;
BEGIN TRY EXEC #HuyPhieuNhap 4,13,N'Nhân viên'; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); END CATCH;
IF @Loi<>50303 THROW 51000,N'Không chặn nhân viên',1;
SET @Loi=0;
BEGIN TRY EXEC #HuyPhieuNhap 1,10,N'Hủy lặp'; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); END CATCH;
IF @Loi<>50304 THROW 51000,N'Không chặn hủy lặp',1;
SET @Loi=0;
BEGIN TRY EXEC #HuyPhieuNhap 5,10,N'   '; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); END CATCH;
IF @Loi<>50301 THROW 51000,N'Không chặn lý do trống',1;
IF (SELECT TrangThai FROM #PhieuNhap WHERE MaPhieuNhap=3)<>'DA_NHAP' THROW 51000,N'Đã làm đổi phiếu nhập kho',1;
PRINT N'PASS: hủy chờ duyệt/đã duyệt, lưu lý do, chặn đã nhập/sai quyền/khác chi nhánh/hủy lặp/lý do trống.';
