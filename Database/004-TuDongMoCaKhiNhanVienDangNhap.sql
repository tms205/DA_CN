/* =============================================================
   Sua QUOTED_IDENTIFIER cho cac thu tuc cap nhat CaLamViec.
   Nhan vien dang nhap lai se dung tiep ca dang mo, khong tao trung.
   ============================================================= */
USE PhotocopyChain;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE sp_MoCa
    @MaNhanVien INT, @TienDauCa DECIMAL(12,0), @MaCa INT OUTPUT,
    @MaChiNhanhThayThe INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MaChiNhanh INT, @VaiTro VARCHAR(20);

    SELECT @MaChiNhanh = MaChiNhanh, @VaiTro = VaiTro
    FROM NguoiDung
    WHERE MaNguoiDung = @MaNhanVien AND TrangThai = 'HOAT_DONG';

    IF @VaiTro = 'SUPER_ADMIN'
        SET @MaChiNhanh = @MaChiNhanhThayThe;

    IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN') OR @MaChiNhanh IS NULL
        THROW 50050, N'Bạn không có quyền mở ca tại chi nhánh này.', 1;

    IF NOT EXISTS (SELECT 1 FROM ChiNhanh
                   WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = 'HOAT_DONG')
        THROW 50051, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;

    IF @TienDauCa < 0
        THROW 50052, N'Tiền đầu ca không hợp lệ.', 1;

    SET XACT_ABORT ON;
    BEGIN TRAN;
        -- Dang nhap lai thi dung tiep ca dang mo, khong tao them ca moi.
        SELECT @MaCa = MaCa
        FROM CaLamViec WITH (UPDLOCK, HOLDLOCK)
        WHERE MaNhanVien = @MaNhanVien AND TrangThai = 'DANG_MO';

        IF @MaCa IS NOT NULL
        BEGIN
            COMMIT TRAN;
            RETURN;
        END

        -- Ca cho xac nhan phai duoc quan ly chot truoc khi mo ca moi.
        IF EXISTS (SELECT 1 FROM CaLamViec WITH (UPDLOCK, HOLDLOCK)
                   WHERE MaNhanVien = @MaNhanVien AND TrangThai = 'CHO_XAC_NHAN')
        BEGIN
            ROLLBACK TRAN;
            THROW 50059, N'Bạn còn một ca chờ quản lý xác nhận.', 1;
        END

        -- Trong mot chi nhanh chi co mot ca STAFF chua ket thuc.
        IF @VaiTro = 'STAFF' AND EXISTS (
            SELECT 1
            FROM CaLamViec ca WITH (UPDLOCK, HOLDLOCK)
            JOIN NguoiDung nv ON nv.MaNguoiDung = ca.MaNhanVien
            WHERE ca.MaChiNhanh = @MaChiNhanh AND nv.VaiTro = 'STAFF'
              AND ca.TrangThai IN ('DANG_MO','CHO_XAC_NHAN'))
        BEGIN
            ROLLBACK TRAN;
            THROW 50059, N'Chi nhánh đang có ca nhân viên chưa kết thúc.', 1;
        END

        INSERT INTO CaLamViec (MaChiNhanh, MaNhanVien, TienDauCa)
        VALUES (@MaChiNhanh, @MaNhanVien, @TienDauCa);

        SET @MaCa = SCOPE_IDENTITY();
    COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE sp_DongCa
    @MaCa INT, @TongDoanhThuCuoiCa DECIMAL(12,0)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @VaiTro VARCHAR(20);

    SELECT @VaiTro = nv.VaiTro
    FROM CaLamViec ca
    JOIN NguoiDung nv ON nv.MaNguoiDung = ca.MaNhanVien
    WHERE ca.MaCa = @MaCa AND ca.TrangThai = 'DANG_MO';

    IF @VaiTro IS NULL
        THROW 50053, N'Ca không tồn tại hoặc đã đóng.', 1;

    UPDATE CaLamViec
    SET TongDoanhThuCuoiCa = @TongDoanhThuCuoiCa,
        TienQuanLyDemCuoiCa = CASE WHEN @VaiTro IN ('BRANCH_ADMIN','SUPER_ADMIN')
                                  THEN @TongDoanhThuCuoiCa ELSE NULL END,
        GioDongCa = GETDATE(),
        TrangThai = CASE WHEN @VaiTro IN ('BRANCH_ADMIN','SUPER_ADMIN')
                         THEN 'DA_XAC_NHAN' ELSE 'CHO_XAC_NHAN' END
    WHERE MaCa = @MaCa AND TrangThai = 'DANG_MO';

    IF @@ROWCOUNT = 0
        THROW 50053, N'Ca không tồn tại hoặc đã đóng.', 1;
END;
GO

CREATE OR ALTER PROCEDURE sp_GiaoTaiQuay
    @MaDonHang INT, @MaNhanVien INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MaChiNhanhDon INT, @MaChiNhanhNhanVien INT, @VaiTro VARCHAR(20),
            @PhuongThuc VARCHAR(10), @TrangThaiThanhToan VARCHAR(20),
            @SoTien DECIMAL(12,0), @MaCa INT;

    SELECT @MaChiNhanhDon = MaChiNhanh
    FROM DonHang
    WHERE MaDonHang = @MaDonHang AND TrangThai = 'DA_IN' AND HinhThucNhan = 'TAI_QUAY';

    IF @MaChiNhanhDon IS NULL
        THROW 50026, N'Chỉ giao được đơn đã in xong và nhận tại quầy.', 1;

    SELECT @MaChiNhanhNhanVien = MaChiNhanh, @VaiTro = VaiTro
    FROM NguoiDung
    WHERE MaNguoiDung = @MaNhanVien AND TrangThai = 'HOAT_DONG';

    IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN')
       OR (@VaiTro <> 'SUPER_ADMIN' AND @MaChiNhanhNhanVien <> @MaChiNhanhDon)
        THROW 50021, N'Bạn không có quyền bàn giao đơn của chi nhánh này.', 1;

    SELECT @PhuongThuc = PhuongThuc, @TrangThaiThanhToan = TrangThai, @SoTien = SoTien
    FROM ThanhToan
    WHERE MaDonHang = @MaDonHang;

    IF @PhuongThuc = 'VIETQR' AND @TrangThaiThanhToan <> 'DA_THANH_TOAN'
        THROW 50027, N'Đơn hàng chưa thanh toán VietQR.', 1;

    SET XACT_ABORT ON;
    BEGIN TRAN;
        IF @PhuongThuc = 'COD'
        BEGIN
            SELECT @MaCa = MaCa
            FROM CaLamViec
            WHERE MaNhanVien = @MaNhanVien AND MaChiNhanh = @MaChiNhanhDon
              AND TrangThai = 'DANG_MO';

            IF @MaCa IS NULL
                THROW 50028, N'Bạn chưa mở ca tại chi nhánh này.', 1;

            UPDATE ThanhToan
            SET TrangThai = 'DA_THANH_TOAN', MaCa = @MaCa, NgayThanhToan = GETDATE()
            WHERE MaDonHang = @MaDonHang AND TrangThai = 'CHUA_THANH_TOAN';

            IF @@ROWCOUNT = 0 AND @TrangThaiThanhToan <> 'DA_THANH_TOAN'
                THROW 50029, N'Không thể ghi nhận thanh toán COD.', 1;

            UPDATE CaLamViec
            SET DoanhThuTrongCa = DoanhThuTrongCa + @SoTien
            WHERE MaCa = @MaCa;
        END

        UPDATE DonHang
        SET TrangThai = 'HOAN_THANH', NgayHoanThanh = GETDATE()
        WHERE MaDonHang = @MaDonHang;
    COMMIT;
END;
GO

PRINT N'Đã sửa QUOTED_IDENTIFIER và bật tự động mở ca khi nhân viên đăng nhập.';
GO
