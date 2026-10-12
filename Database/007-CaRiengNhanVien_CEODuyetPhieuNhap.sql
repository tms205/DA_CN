/* 007: mỗi nhân viên một ca riêng; CEO lập phiếu được duyệt trực tiếp.
   Không thêm/xóa bảng, cột, dữ liệu hoặc cập nhật các ca/phiếu cũ.
   Chạy bằng sqlcmd hoặc SSMS ở SQLCMD Mode; lỗi thì dừng và rollback.
*/
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
GO
CREATE OR ALTER PROCEDURE sp_MoCa
    @MaNhanVien INT, @TienDauCa DECIMAL(12,0) = 200000, @MaCa INT OUTPUT,
    @MaChiNhanhThayThe INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @MaCa = NULL;

    DECLARE @MaChiNhanh INT, @VaiTro VARCHAR(20);
    SELECT @MaChiNhanh = MaChiNhanh, @VaiTro = VaiTro
    FROM NguoiDung
    WHERE MaNguoiDung = @MaNhanVien AND TrangThai = 'HOAT_DONG';

    IF @VaiTro = 'SUPER_ADMIN'
        SET @MaChiNhanh = @MaChiNhanhThayThe;

    IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN') OR @MaChiNhanh IS NULL
        THROW 50050, N'Bạn không có quyền mở ca tại chi nhánh này.', 1;
    IF NOT EXISTS (SELECT 1 FROM ChiNhanh WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = 'HOAT_DONG')
        THROW 50051, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
    IF @TienDauCa IS NULL OR @TienDauCa < 0
        THROW 50052, N'Tiền đầu ca không hợp lệ.', 1;

    BEGIN TRAN;
        -- Khóa theo nhân viên, không khóa ca của người khác cùng chi nhánh.
        SELECT @MaCa = MaCa
        FROM CaLamViec WITH (UPDLOCK, HOLDLOCK)
        WHERE MaNhanVien = @MaNhanVien AND TrangThai = 'DANG_MO';

        IF @MaCa IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM CaLamViec WHERE MaCa = @MaCa AND MaChiNhanh = @MaChiNhanh)
            BEGIN
                ROLLBACK TRAN;
                THROW 50059, N'Bạn đang có ca mở tại một chi nhánh khác.', 1;
            END
            COMMIT TRAN;
            RETURN; -- Đăng nhập lại dùng ca cũ, không đổi tiền đầu ca.
        END

        -- Ca đã đóng chờ xác nhận vẫn giữ nguyên để quản lý đối chiếu sau.
        INSERT INTO CaLamViec (MaChiNhanh, MaNhanVien, TienDauCa)
        VALUES (@MaChiNhanh, @MaNhanVien, @TienDauCa);
        SET @MaCa = SCOPE_IDENTITY();
    COMMIT TRAN;
END;
GO
CREATE OR ALTER TRIGGER trg_PhieuNhap_KiemTraNguoiLapDuyet
ON PhieuNhap
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted
               WHERE MaNguoiLap IS NULL OR dbo.fn_LaQuanLy(MaNguoiLap, MaChiNhanh) = 0)
        THROW 50060, N'Người lập phiếu nhập phải là Quản lý chi nhánh đó hoặc Super Admin.', 1;

    -- CEO được tự duyệt; quản lý chi nhánh vẫn không có quyền duyệt.
    IF EXISTS (SELECT 1 FROM inserted
               WHERE MaNguoiDuyet IS NOT NULL AND dbo.fn_LaSuperAdmin(MaNguoiDuyet) = 0)
        THROW 50061, N'Người duyệt phiếu nhập phải là Super Admin.', 1;
END;
GO
CREATE OR ALTER PROCEDURE sp_TaoPhieuNhap
    @MaChiNhanh INT, @MaVatTu INT, @NhaCungCap NVARCHAR(150), @SoLuong INT, @DonGia DECIMAL(12,0), @MaNguoiLap INT
AS
BEGIN
    SET NOCOUNT ON;
    IF dbo.fn_LaQuanLy(@MaNguoiLap, @MaChiNhanh) = 0
        THROW 50060, N'Chỉ quản lý chi nhánh hoặc Super Admin mới được lập phiếu nhập.', 1;
    IF @SoLuong IS NULL OR @SoLuong <= 0 OR @DonGia IS NULL OR @DonGia < 0
        THROW 50064, N'Số lượng và đơn giá nhập không hợp lệ.', 1;
    IF NOT EXISTS (SELECT 1 FROM ChiNhanh WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = 'HOAT_DONG')
        THROW 50065, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
    IF NOT EXISTS (SELECT 1 FROM VatTu WHERE MaVatTu = @MaVatTu)
        THROW 50066, N'Vật tư không tồn tại.', 1;

    DECLARE @LaCEO BIT = dbo.fn_LaSuperAdmin(@MaNguoiLap);
    INSERT INTO PhieuNhap (MaChiNhanh, MaVatTu, NhaCungCap, SoLuong, DonGia, MaNguoiLap, MaNguoiDuyet, TrangThai)
    VALUES (@MaChiNhanh, @MaVatTu, @NhaCungCap, @SoLuong, @DonGia, @MaNguoiLap,
            CASE WHEN @LaCEO = 1 THEN @MaNguoiLap ELSE NULL END,
            CASE WHEN @LaCEO = 1 THEN 'DA_DUYET' ELSE 'CHO_DUYET' END);
    -- Duyệt không tăng tồn kho: vẫn phải xác nhận nhận hàng bằng sp_NhapKho.
END;
GO
COMMIT TRAN;
PRINT N'Đã áp dụng 007: ca riêng mặc định 200.000đ và CEO duyệt phiếu trực tiếp.';
GO
