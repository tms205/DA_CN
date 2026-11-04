-- Khôi phục quy tắc đóng ca trước 024; không cập nhật dữ liệu ca đã đóng.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
CREATE OR ALTER PROCEDURE dbo.sp_DongCa
    @MaCa INT, @TongDoanhThuCuoiCa DECIMAL(12,0), @GhiChu NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @TongDoanhThuCuoiCa IS NULL OR @TongDoanhThuCuoiCa < 0 THROW 50053, N'Số tiền thực đếm không hợp lệ.', 1;
    BEGIN TRY
        BEGIN TRAN;
        DECLARE @TrangThai VARCHAR(20), @PhaiCo DECIMAL(12,0), @GioMo DATETIME, @BayGio DATETIME=GETDATE();
        SELECT @TrangThai=TrangThai, @PhaiCo=ISNULL(TienDauCa,0)+ISNULL(DoanhThuTrongCa,0), @GioMo=GioMoCa
        FROM dbo.CaLamViec WITH (UPDLOCK,HOLDLOCK) WHERE MaCa=@MaCa;
        IF @TrangThai IS NULL OR @TrangThai NOT IN ('DANG_MO','CHO_TONG_KET')
            THROW 50053, N'Ca không tồn tại hoặc đã gửi tổng kết.', 1;
        SET @GhiChu = NULLIF(LTRIM(RTRIM(@GhiChu)), N'');
        IF @TongDoanhThuCuoiCa <> @PhaiCo AND @GhiChu IS NULL
            THROW 50057, N'Số tiền lệch két. Vui lòng nhập lý do chênh lệch.', 1;
        DECLARE @KetThuc DATETIME = CASE
            WHEN CAST(@GioMo AS TIME) >= '07:30' AND CAST(@GioMo AS TIME) < '12:30' THEN DATEADD(MINUTE,750,CAST(CAST(@GioMo AS DATE) AS DATETIME))
            WHEN CAST(@GioMo AS TIME) >= '12:30' AND CAST(@GioMo AS TIME) < '17:30' THEN DATEADD(MINUTE,1050,CAST(CAST(@GioMo AS DATE) AS DATETIME))
            WHEN CAST(@GioMo AS TIME) >= '17:30' AND CAST(@GioMo AS TIME) < '21:30' THEN DATEADD(MINUTE,1290,CAST(CAST(@GioMo AS DATE) AS DATETIME)) WHEN CAST(@GioMo AS TIME)>='21:30' THEN DATEADD(MINUTE,2190,CAST(CAST(@GioMo AS DATE) AS DATETIME)) ELSE DATEADD(MINUTE,750,CAST(CAST(@GioMo AS DATE) AS DATETIME)) END;
        UPDATE dbo.CaLamViec
        SET TongDoanhThuCuoiCa=@TongDoanhThuCuoiCa,
            GioDongCa=ISNULL(GioDongCa, CASE WHEN @BayGio>=@KetThuc THEN @KetThuc ELSE @BayGio END),
            GhiChu=@GhiChu, TrangThai='CHO_XAC_NHAN'
        WHERE MaCa=@MaCa AND TrangThai IN ('DANG_MO','CHO_TONG_KET');
        IF @@ROWCOUNT<>1 THROW 50053, N'Ca vừa được tổng kết ở yêu cầu khác.', 1;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END;

GO
