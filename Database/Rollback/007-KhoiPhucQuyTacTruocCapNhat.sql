/* Chỉ khôi phục định nghĩa trước 007, không xóa dữ liệu.
   Không tự chạy file này: ca nhân viên mở đồng thời có thể không phù hợp quy tắc cũ.
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
        THROW 50050, N'Báº¡n khÃ´ng cÃ³ quyá»n má»Ÿ ca táº¡i chi nhÃ¡nh nÃ y.', 1;

    IF NOT EXISTS (SELECT 1 FROM ChiNhanh
                   WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = 'HOAT_DONG')
        THROW 50051, N'Chi nhÃ¡nh khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ ngá»«ng hoáº¡t Ä‘á»™ng.', 1;

    IF @TienDauCa < 0
        THROW 50052, N'Tiá»n Ä‘áº§u ca khÃ´ng há»£p lá»‡.', 1;

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
            THROW 50059, N'Báº¡n cÃ²n má»™t ca chá» quáº£n lÃ½ xÃ¡c nháº­n.', 1;
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
            THROW 50059, N'Chi nhÃ¡nh Ä‘ang cÃ³ ca nhÃ¢n viÃªn chÆ°a káº¿t thÃºc.', 1;
        END

        INSERT INTO CaLamViec (MaChiNhanh, MaNhanVien, TienDauCa)
        VALUES (@MaChiNhanh, @MaNhanVien, @TienDauCa);

        SET @MaCa = SCOPE_IDENTITY();
    COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE sp_TaoPhieuNhap
    @MaChiNhanh INT, @MaVatTu INT, @NhaCungCap NVARCHAR(150), @SoLuong INT, @DonGia DECIMAL(12,0), @MaNguoiLap INT
AS
BEGIN
    IF dbo.fn_LaQuanLy(@MaNguoiLap, @MaChiNhanh) = 0
        THROW 50060, N'Chỉ quản lý chi nhánh mới được lập phiếu nhập.', 1;

    INSERT INTO PhieuNhap (MaChiNhanh, MaVatTu, NhaCungCap, SoLuong, DonGia, MaNguoiLap)
    VALUES (@MaChiNhanh, @MaVatTu, @NhaCungCap, @SoLuong, @DonGia, @MaNguoiLap);
END;
GO
CREATE OR ALTER TRIGGER trg_PhieuNhap_KiemTraNguoiLapDuyet
ON PhieuNhap
AFTER INSERT, UPDATE
AS
BEGIN
    IF EXISTS (SELECT 1 FROM inserted
               WHERE MaNguoiLap IS NULL OR dbo.fn_LaQuanLy(MaNguoiLap, MaChiNhanh) = 0)
    BEGIN
        RAISERROR (N'Người lập phiếu nhập phải là Quản lý chi nhánh đó hoặc Super Admin.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM inserted
               WHERE MaNguoiDuyet IS NOT NULL
                 AND (dbo.fn_LaSuperAdmin(MaNguoiDuyet) = 0 OR MaNguoiDuyet = MaNguoiLap))
    BEGIN
        RAISERROR (N'Người duyệt phiếu nhập phải là Super Admin và không phải người lập phiếu.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO
COMMIT TRAN;
GO
