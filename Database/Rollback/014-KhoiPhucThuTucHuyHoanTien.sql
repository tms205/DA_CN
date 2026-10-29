-- Snapshot database trước migration 014; khôi phục cùng API/Web/App cũ.
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
/* ===================== HUY DON, DANH GIA ===================== */
CREATE OR ALTER PROCEDURE sp_HuyDonHang
    @MaDonHang INT, @MaNguoiHuy INT, @LyDo NVARCHAR(255)
AS
BEGIN
    DECLARE @TrangThai VARCHAR(20), @MaChiNhanh INT, @MaKhachHang INT;
    SELECT @TrangThai = TrangThai, @MaChiNhanh = MaChiNhanh, @MaKhachHang = MaKhachHang
    FROM DonHang WHERE MaDonHang = @MaDonHang;

    IF @TrangThai IS NULL OR @TrangThai IN ('HOAN_THANH','DA_HUY')
        THROW 50040, N'Không thể hủy đơn này.', 1;
    -- khach chi tu huy khi don chua duoc tiep nhan, con lai do quan ly huy
    IF NOT ((@MaNguoiHuy = @MaKhachHang AND @TrangThai = 'CHO_XAC_NHAN') OR dbo.fn_LaQuanLy(@MaNguoiHuy, @MaChiNhanh) = 1)
        THROW 50041, N'Bạn không có quyền hủy đơn này.', 1;

    -- don chua in thi bo may in (don da in roi thi giu lai de doi soat so trang may in)
    UPDATE DonHang
    SET TrangThai = 'DA_HUY', GhiChu = @LyDo,
        MaMayIn = CASE WHEN @TrangThai IN ('CHO_XAC_NHAN','DA_XAC_NHAN') THEN NULL ELSE MaMayIn END
    WHERE MaDonHang = @MaDonHang;

    INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    VALUES (@MaKhachHang, @MaDonHang, N'Đơn hàng đã bị hủy', @LyDo);
END;
GO
-- Don da thanh toan ma bi huy: Super Admin xac nhan da hoan tien cho khach
CREATE OR ALTER PROCEDURE sp_HoanTien
    @MaDonHang INT, @MaNguoiDuyet INT
AS
BEGIN
    IF dbo.fn_LaSuperAdmin(@MaNguoiDuyet) = 0
        THROW 50042, N'Chỉ Super Admin được duyệt hoàn tiền.', 1;

    UPDATE tt SET TrangThai = 'DA_HOAN_TIEN'
    FROM ThanhToan tt JOIN DonHang dh ON dh.MaDonHang = tt.MaDonHang
    WHERE tt.MaDonHang = @MaDonHang AND dh.TrangThai = 'DA_HUY' AND tt.TrangThai = 'DA_THANH_TOAN';

    IF @@ROWCOUNT = 0
        THROW 50043, N'Đơn không cần hoàn tiền.', 1;
END;
GO
-- Ngan hang bao da nhan tien chuyen khoan VietQR
CREATE OR ALTER PROCEDURE sp_XacNhanChuyenKhoan
    @MaDonHang INT, @MaGiaoDich VARCHAR(50)
AS
BEGIN
    UPDATE ThanhToan
    SET TrangThai = 'DA_THANH_TOAN', MaGiaoDich = @MaGiaoDich, NgayThanhToan = GETDATE()
    WHERE MaDonHang = @MaDonHang AND PhuongThuc = 'VIETQR' AND TrangThai = 'CHUA_THANH_TOAN';

    IF @@ROWCOUNT > 0
        INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
        SELECT MaKhachHang, MaDonHang, N'Thanh toán thành công', N'Đơn hàng của bạn đã được thanh toán.'
        FROM DonHang WHERE MaDonHang = @MaDonHang;
END;
GO