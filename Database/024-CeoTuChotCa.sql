-- Ca của CEO chốt ngay sau khi nhập thực đếm; thừa/thiếu bắt buộc lý do.
-- Chỉ thay sp_DongCa. Không thêm bảng/cột, không sửa dữ liệu hoặc ca cũ khi áp dụng.
-- Giữ lịch ca thông đêm, tiền két chỉ gồm đầu ca + tiền mặt, không cộng VietQR.
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
        DECLARE @TrangThai VARCHAR(20), @PhaiCo DECIMAL(12,0), @GioMo DATETIME, @BayGio DATETIME=GETDATE(), @TuChot BIT=0;
        -- Quyền được lấy từ chủ ca trong DB, không nhận cờ CEO từ client.
        SELECT @TrangThai=ca.TrangThai, @PhaiCo=ISNULL(ca.TienDauCa,0)+ISNULL(ca.DoanhThuTrongCa,0),
               @GioMo=ca.GioMoCa,
               @TuChot=CASE WHEN nv.VaiTro='SUPER_ADMIN' AND nv.TrangThai=N'HOAT_DONG' THEN 1 ELSE 0 END
        FROM dbo.CaLamViec ca WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.NguoiDung nv ON nv.MaNguoiDung=ca.MaNhanVien
        WHERE ca.MaCa=@MaCa;
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
            GhiChu=@GhiChu,
            TrangThai=CASE WHEN @TuChot=1 THEN 'DA_XAC_NHAN' ELSE 'CHO_XAC_NHAN' END,
            -- CEO tự đóng, không giả lập một lượt kiểm đếm/duyệt độc lập.
            TienQuanLyDemCuoiCa=CASE WHEN @TuChot=1 THEN NULL ELSE TienQuanLyDemCuoiCa END,
            MaNguoiXacNhan=CASE WHEN @TuChot=1 THEN NULL ELSE MaNguoiXacNhan END
        WHERE MaCa=@MaCa AND TrangThai IN ('DANG_MO','CHO_TONG_KET');
        IF @@ROWCOUNT<>1 THROW 50053, N'Ca vừa được tổng kết ở yêu cầu khác.', 1;
        COMMIT;
        SELECT @TuChot AS DaTuChot;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END;

GO
