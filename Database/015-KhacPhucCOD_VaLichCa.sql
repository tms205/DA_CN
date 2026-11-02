/* 015 — chống thu COD trùng, thông báo Unicode, lịch ca 07:30–21:30.
   Chỉ CREATE OR ALTER 4 thủ tục. Không thêm/xóa bảng, cột hay cập nhật dữ liệu cũ khi chạy migration.
   Sau khi API mới chạy, ca quá giờ sẽ chuyển CHO_TONG_KET; không tự nhập tiền/duyệt két.
   Đọc docs/BAO_CAO_KHAC_PHUC_2026-10-29.md trước khi áp dụng. UTF-8, sqlcmd -f 65001.
*/
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GiaoTaiQuay
    @MaDonHang INT, @MaNhanVien INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @MaChiNhanhDon INT, @MaChiNhanhNhanVien INT, @VaiTro VARCHAR(20),
            @PhuongThuc VARCHAR(10), @TrangThaiThanhToan VARCHAR(20), @TrangThaiDon VARCHAR(20),
            @SoTien DECIMAL(12,0), @MaCa INT, @BayGio DATETIME = GETDATE(), @GioMoCa DATETIME;
    BEGIN TRY
        BEGIN TRAN;
        -- Mọi kiểm tra cùng nằm trong transaction; request thứ hai phải đọc lại sau commit.
        SELECT @MaChiNhanhDon = MaChiNhanh, @TrangThaiDon = TrangThai
        FROM dbo.DonHang WITH (UPDLOCK, HOLDLOCK)
        WHERE MaDonHang = @MaDonHang AND HinhThucNhan = 'TAI_QUAY';
        IF @MaChiNhanhDon IS NULL OR @TrangThaiDon <> 'DA_IN'
            THROW 50026, N'Chỉ bàn giao một lần cho đơn đã in xong và nhận tại quầy.', 1;

        SELECT @MaChiNhanhNhanVien = MaChiNhanh, @VaiTro = VaiTro
        FROM dbo.NguoiDung WHERE MaNguoiDung = @MaNhanVien AND TrangThai = 'HOAT_DONG';
        IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN')
           OR (@VaiTro <> 'SUPER_ADMIN' AND ISNULL(@MaChiNhanhNhanVien, -1) <> @MaChiNhanhDon)
            THROW 50021, N'Bạn không có quyền bàn giao đơn của chi nhánh này.', 1;

        SELECT @PhuongThuc = PhuongThuc, @TrangThaiThanhToan = TrangThai, @SoTien = SoTien
        FROM dbo.ThanhToan WITH (UPDLOCK, HOLDLOCK) WHERE MaDonHang = @MaDonHang;
        IF @PhuongThuc IS NULL OR @PhuongThuc NOT IN ('COD','VIETQR')
            THROW 50027, N'Không tìm thấy phương thức thanh toán hợp lệ của đơn.', 1;
        IF @PhuongThuc = 'VIETQR' AND ISNULL(@TrangThaiThanhToan, '') <> 'DA_THANH_TOAN'
            THROW 50027, N'Đơn hàng chưa thanh toán VietQR.', 1;

        IF @PhuongThuc = 'COD'
        BEGIN
            IF @TrangThaiThanhToan IS NULL OR @TrangThaiThanhToan NOT IN ('CHUA_THANH_TOAN','DA_THANH_TOAN') OR @SoTien IS NULL OR @SoTien < 0
                THROW 50029, N'Không thể ghi nhận thanh toán COD ở trạng thái hiện tại.', 1;
            SELECT @MaCa = MaCa, @GioMoCa = GioMoCa
            FROM dbo.CaLamViec WITH (UPDLOCK, HOLDLOCK)
            WHERE MaNhanVien = @MaNhanVien AND MaChiNhanh = @MaChiNhanhDon AND TrangThai = 'DANG_MO';
            SET @BayGio=GETDATE();
            DECLARE @GioKetThuc DATETIME = CASE
                WHEN CAST(@GioMoCa AS TIME) >= '07:30' AND CAST(@GioMoCa AS TIME) < '12:30' THEN DATEADD(MINUTE,750,CAST(CAST(@GioMoCa AS DATE) AS DATETIME))
                WHEN CAST(@GioMoCa AS TIME) >= '12:30' AND CAST(@GioMoCa AS TIME) < '17:30' THEN DATEADD(MINUTE,1050,CAST(CAST(@GioMoCa AS DATE) AS DATETIME))
                WHEN CAST(@GioMoCa AS TIME) >= '17:30' AND CAST(@GioMoCa AS TIME) < '21:30' THEN DATEADD(MINUTE,1290,CAST(CAST(@GioMoCa AS DATE) AS DATETIME))
                ELSE @GioMoCa END;
            IF @MaCa IS NULL OR @GioMoCa IS NULL OR @BayGio >= @GioKetThuc
                THROW 50028, N'Ca đã hết giờ hoặc chưa mở. Vui lòng tổng kết ca cũ và mở ca mới.', 1;

            -- Chỉ chính transaction đổi từ chưa trả sang đã trả mới được cộng tiền.
            IF @TrangThaiThanhToan = 'CHUA_THANH_TOAN'
            BEGIN
                UPDATE dbo.ThanhToan
                SET TrangThai = 'DA_THANH_TOAN', MaCa = @MaCa, NgayThanhToan = @BayGio
                WHERE MaDonHang = @MaDonHang AND TrangThai = 'CHUA_THANH_TOAN';
                IF @@ROWCOUNT <> 1 THROW 50029, N'Không thể ghi nhận thanh toán COD.', 1;
                UPDATE dbo.CaLamViec SET DoanhThuTrongCa = ISNULL(DoanhThuTrongCa,0) + @SoTien WHERE MaCa = @MaCa AND TrangThai = 'DANG_MO';
                IF @@ROWCOUNT <> 1 THROW 50028, N'Ca không còn mở để ghi nhận tiền.', 1;
            END
        END
        UPDATE dbo.DonHang SET TrangThai = 'HOAN_THANH', NgayHoanThanh = @BayGio
        WHERE MaDonHang = @MaDonHang AND TrangThai = 'DA_IN';
        IF @@ROWCOUNT <> 1 THROW 50026, N'Đơn đã được xử lý ở yêu cầu khác.', 1;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MoCa
    @MaNhanVien INT, @TienDauCa DECIMAL(12,0) = 200000, @MaCa INT OUTPUT,
    @MaChiNhanhThayThe INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @MaCa = NULL;
    DECLARE @MaChiNhanh INT, @VaiTro VARCHAR(20), @BayGio DATETIME = GETDATE();
    SELECT @MaChiNhanh = MaChiNhanh, @VaiTro = VaiTro
    FROM dbo.NguoiDung WHERE MaNguoiDung = @MaNhanVien AND TrangThai = 'HOAT_DONG';
    IF @VaiTro = 'SUPER_ADMIN' SET @MaChiNhanh = @MaChiNhanhThayThe;
    IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN') OR @MaChiNhanh IS NULL
        THROW 50050, N'Bạn không có quyền mở ca tại chi nhánh này.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ChiNhanh WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = 'HOAT_DONG')
        THROW 50051, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
    IF @TienDauCa IS NULL OR @TienDauCa < 0 THROW 50052, N'Tiền đầu ca không hợp lệ.', 1;
    IF CAST(@BayGio AS TIME) < '07:30' OR CAST(@BayGio AS TIME) >= '21:30'
        THROW 50059, N'Chỉ mở ca trong giờ làm việc 07:30–21:30.', 1;
    BEGIN TRY
        BEGIN TRAN;
        DECLARE @CaCu INT, @GioMo DATETIME, @ChiNhanhCu INT;
        SELECT @CaCu = MaCa, @GioMo = GioMoCa, @ChiNhanhCu = MaChiNhanh
        FROM dbo.CaLamViec WITH (UPDLOCK,HOLDLOCK)
        WHERE MaNhanVien = @MaNhanVien AND TrangThai = 'DANG_MO';
        SET @BayGio=GETDATE();
        IF CAST(@BayGio AS TIME) < '07:30' OR CAST(@BayGio AS TIME) >= '21:30'
            THROW 50059, N'Chỉ mở ca trong giờ làm việc 07:30–21:30.', 1;
        DECLARE @KetThuc DATETIME = CASE
            WHEN CAST(@GioMo AS TIME) >= '07:30' AND CAST(@GioMo AS TIME) < '12:30' THEN DATEADD(MINUTE,750,CAST(CAST(@GioMo AS DATE) AS DATETIME))
            WHEN CAST(@GioMo AS TIME) >= '12:30' AND CAST(@GioMo AS TIME) < '17:30' THEN DATEADD(MINUTE,1050,CAST(CAST(@GioMo AS DATE) AS DATETIME))
            WHEN CAST(@GioMo AS TIME) >= '17:30' AND CAST(@GioMo AS TIME) < '21:30' THEN DATEADD(MINUTE,1290,CAST(CAST(@GioMo AS DATE) AS DATETIME)) ELSE @GioMo END;
        IF EXISTS (SELECT 1 FROM dbo.CaLamViec WITH (UPDLOCK,HOLDLOCK) WHERE MaNhanVien=@MaNhanVien AND TrangThai='CHO_TONG_KET')
           OR (@CaCu IS NOT NULL AND @BayGio >= @KetThuc)
            THROW 50059, N'Bạn còn ca hết giờ chưa tổng kết. Hãy nhập tiền thực đếm trước khi mở ca mới.', 1;
        IF @CaCu IS NOT NULL
        BEGIN
            IF @ChiNhanhCu <> @MaChiNhanh THROW 50059, N'Bạn đang có ca mở tại một chi nhánh khác.', 1;
            SET @MaCa = @CaCu;
        END
        ELSE
        BEGIN
            INSERT INTO dbo.CaLamViec(MaChiNhanh,MaNhanVien,TienDauCa,GioMoCa)
            VALUES(@MaChiNhanh,@MaNhanVien,@TienDauCa,@BayGio);
            SET @MaCa = SCOPE_IDENTITY();
        END
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END;
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
            WHEN CAST(@GioMo AS TIME) >= '17:30' AND CAST(@GioMo AS TIME) < '21:30' THEN DATEADD(MINUTE,1290,CAST(CAST(@GioMo AS DATE) AS DATETIME)) ELSE @GioMo END;
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
CREATE OR ALTER PROCEDURE dbo.sp_XacNhanCa
    @MaCa INT, @MaQuanLy INT, @TienQuanLyDemCuoiCa DECIMAL(12,0) = NULL,
    @TienChi DECIMAL(12,0) = 0, @TienBu DECIMAL(12,0) = 0, @GhiChu NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRAN;
        DECLARE @NhanVien INT, @ChiNhanh INT, @PhaiCo DECIMAL(12,0), @Thieu DECIMAL(12,0);
        SELECT @NhanVien=MaNhanVien,@ChiNhanh=MaChiNhanh,@PhaiCo=ISNULL(TienDauCa,0)+ISNULL(DoanhThuTrongCa,0)
        FROM dbo.CaLamViec WITH(UPDLOCK,HOLDLOCK) WHERE MaCa=@MaCa AND TrangThai='CHO_XAC_NHAN';
        IF @NhanVien IS NULL THROW 50054, N'Ca chưa tổng kết hoặc đã được xác nhận.',1;
        IF @NhanVien=@MaQuanLy OR dbo.fn_LaQuanLy(@MaQuanLy,@ChiNhanh)=0
            THROW 50055,N'Phải do quản lý khác, không phải người trực ca, xác nhận.',1;
        IF @TienQuanLyDemCuoiCa IS NULL OR @TienQuanLyDemCuoiCa<0
            THROW 50059,N'Quản lý phải nhập số tiền thực tế kiểm đếm.',1;
        SET @Thieu=CASE WHEN @PhaiCo>@TienQuanLyDemCuoiCa THEN @PhaiCo-@TienQuanLyDemCuoiCa ELSE 0 END;
        IF @TienChi IS NULL OR @TienBu IS NULL OR @TienChi<0 OR @TienBu<0 OR @TienChi>@Thieu
            THROW 50056,N'Tiền chi hoặc tiền bù không hợp lệ.',1;
        SET @GhiChu=NULLIF(LTRIM(RTRIM(@GhiChu)),N'');
        IF (@TienChi>0 OR @TienQuanLyDemCuoiCa<>@PhaiCo) AND @GhiChu IS NULL
            THROW 50057,N'Két có chênh lệch hoặc khoản chi thì phải ghi rõ lý do.',1;
        IF @TienBu<>@Thieu-@TienChi THROW 50058,N'Nhân viên phải bù đủ phần còn thiếu sau khoản chi.',1;
        DECLARE @GhiChuCu NVARCHAR(255);
        SELECT @GhiChuCu=GhiChu FROM dbo.CaLamViec WHERE MaCa=@MaCa;
        DECLARE @Gop NVARCHAR(600)=CONCAT(@GhiChuCu,
            CASE WHEN @GhiChu IS NULL THEN N'' ELSE CONCAT(CASE WHEN ISNULL(@GhiChuCu,N'')=N'' THEN N'' ELSE NCHAR(13)+NCHAR(10) END,N'Quản lý: ',@GhiChu) END);
        IF LEN(@Gop)>255 THROW 50057,N'Tổng ghi chú nhân viên và quản lý vượt 255 ký tự. Vui lòng rút gọn ghi chú quản lý.',1;
        UPDATE dbo.CaLamViec SET TienQuanLyDemCuoiCa=@TienQuanLyDemCuoiCa,TienChi=@TienChi,TienBu=@TienBu,
            GhiChu=NULLIF(@Gop,N''),MaNguoiXacNhan=@MaQuanLy,TrangThai='DA_XAC_NHAN'
        WHERE MaCa=@MaCa AND TrangThai='CHO_XAC_NHAN';
        IF @@ROWCOUNT<>1 THROW 50054,N'Ca vừa được xác nhận ở yêu cầu khác.',1;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH
END;
GO
COMMIT;
PRINT N'Đã cập nhật chống thu COD trùng và lịch ca chờ tổng kết.';
GO
