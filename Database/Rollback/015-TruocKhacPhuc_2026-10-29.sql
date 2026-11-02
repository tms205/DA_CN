/* Bản định nghĩa đọc trực tiếp từ SQL Server trước sửa, 29/10/2026.
Chỉ dùng rollback khi được duyệt; giữ nguyên cả thông báo lỗi mã hóa cũ để phục hồi chính xác. */
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
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
        THROW 50053, N'Ca khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ Ä‘Ã³ng.', 1;

    UPDATE CaLamViec
    SET TongDoanhThuCuoiCa = @TongDoanhThuCuoiCa,
        TienQuanLyDemCuoiCa = CASE WHEN @VaiTro IN ('BRANCH_ADMIN','SUPER_ADMIN')
                                  THEN @TongDoanhThuCuoiCa ELSE NULL END,
        GioDongCa = GETDATE(),
        TrangThai = CASE WHEN @VaiTro IN ('BRANCH_ADMIN','SUPER_ADMIN')
                         THEN 'DA_XAC_NHAN' ELSE 'CHO_XAC_NHAN' END
    WHERE MaCa = @MaCa AND TrangThai = 'DANG_MO';

    IF @@ROWCOUNT = 0
        THROW 50053, N'Ca khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ Ä‘Ã³ng.', 1;
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
        THROW 50026, N'Chá»‰ giao Ä‘Æ°á»£c Ä‘Æ¡n Ä‘Ã£ in xong vÃ  nháº­n táº¡i quáº§y.', 1;

    SELECT @MaChiNhanhNhanVien = MaChiNhanh, @VaiTro = VaiTro
    FROM NguoiDung
    WHERE MaNguoiDung = @MaNhanVien AND TrangThai = 'HOAT_DONG';

    IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN')
       OR (@VaiTro <> 'SUPER_ADMIN' AND @MaChiNhanhNhanVien <> @MaChiNhanhDon)
        THROW 50021, N'Báº¡n khÃ´ng cÃ³ quyá»n bÃ n giao Ä‘Æ¡n cá»§a chi nhÃ¡nh nÃ y.', 1;

    SELECT @PhuongThuc = PhuongThuc, @TrangThaiThanhToan = TrangThai, @SoTien = SoTien
    FROM ThanhToan
    WHERE MaDonHang = @MaDonHang;

    IF @PhuongThuc = 'VIETQR' AND @TrangThaiThanhToan <> 'DA_THANH_TOAN'
        THROW 50027, N'ÄÆ¡n hÃ ng chÆ°a thanh toÃ¡n VietQR.', 1;

    SET XACT_ABORT ON;
    BEGIN TRAN;
        IF @PhuongThuc = 'COD'
        BEGIN
            SELECT @MaCa = MaCa
            FROM CaLamViec
            WHERE MaNhanVien = @MaNhanVien AND MaChiNhanh = @MaChiNhanhDon
              AND TrangThai = 'DANG_MO';

            IF @MaCa IS NULL
                THROW 50028, N'Báº¡n chÆ°a má»Ÿ ca táº¡i chi nhÃ¡nh nÃ y.', 1;

            UPDATE ThanhToan
            SET TrangThai = 'DA_THANH_TOAN', MaCa = @MaCa, NgayThanhToan = GETDATE()
            WHERE MaDonHang = @MaDonHang AND TrangThai = 'CHUA_THANH_TOAN';

            IF @@ROWCOUNT = 0 AND @TrangThaiThanhToan <> 'DA_THANH_TOAN'
                THROW 50029, N'KhÃ´ng thá»ƒ ghi nháº­n thanh toÃ¡n COD.', 1;

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


/* Quan ly xac nhan dong ca (khong duoc tu xac nhan ca cua minh).
   Ket thieu: tru khoan da chi cho tiem, phan con lai nhan vien phai bu du ngay.
   Ket thua: phan thua nop ve tiem. */
GO
CREATE OR ALTER PROCEDURE sp_XacNhanCa
    @MaCa INT, @MaQuanLy INT, @TienQuanLyDemCuoiCa DECIMAL(12,0) = NULL,
    @TienChi DECIMAL(12,0) = 0, @TienBu DECIMAL(12,0) = 0, @GhiChu NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MaNhanVien INT, @MaChiNhanh INT, @Thieu DECIMAL(12,0), @TienNhanVienDem DECIMAL(12,0);
    SELECT @MaNhanVien = MaNhanVien, @MaChiNhanh = MaChiNhanh,
           @TienNhanVienDem = TongDoanhThuCuoiCa,
           @Thieu = (TienDauCa + DoanhThuTrongCa) - @TienQuanLyDemCuoiCa
    FROM CaLamViec WHERE MaCa = @MaCa AND TrangThai = 'CHO_XAC_NHAN';

    IF @MaNhanVien IS NULL
        THROW 50054, N'Ca không ở trạng thái chờ xác nhận.', 1;
    IF @MaNhanVien = @MaQuanLy OR dbo.fn_LaQuanLy(@MaQuanLy, @MaChiNhanh) = 0
        THROW 50055, N'Phải do quản lý khác (không phải người trực ca) xác nhận.', 1;
    -- Cho phep cac kich ban demo cu khong truyen tham so moi; API that luon bat buoc quan ly kiem dem.
    IF @TienQuanLyDemCuoiCa IS NULL SET @TienQuanLyDemCuoiCa = @TienNhanVienDem;
    IF @TienQuanLyDemCuoiCa IS NULL OR @TienQuanLyDemCuoiCa < 0
        THROW 50059, N'Số tiền quản lý kiểm đếm không hợp lệ.', 1;
    SELECT @Thieu = (TienDauCa + DoanhThuTrongCa) - @TienQuanLyDemCuoiCa
    FROM CaLamViec WHERE MaCa = @MaCa;
    IF @Thieu < 0 SET @Thieu = 0;
    IF @TienChi < 0 OR @TienChi > @Thieu
        THROW 50056, N'Số tiền chi cho tiệm không hợp lệ.', 1;
    IF @TienChi > 0 AND ISNULL(@GhiChu, N'') = N''
        THROW 50057, N'Có khoản chi thì phải ghi chú chi vào việc gì.', 1;
    IF @TienBu <> @Thieu - @TienChi
        THROW 50058, N'Nhân viên phải bù đủ số tiền còn thiếu.', 1;

    UPDATE CaLamViec
    SET TienQuanLyDemCuoiCa = @TienQuanLyDemCuoiCa,
        TienChi = @TienChi, TienBu = @TienBu, GhiChu = @GhiChu,
        MaNguoiXacNhan = @MaQuanLy, TrangThai = 'DA_XAC_NHAN'
    WHERE MaCa = @MaCa;
END;
GO
COMMIT;
GO
