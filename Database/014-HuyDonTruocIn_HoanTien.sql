-- 014: Chỉ hủy trước DANG_IN; tiền đến sau hủy được ghi nhận để hoàn, không khôi phục đơn.
-- Chỉ thay nội dung 3 thủ tục; không thêm/xóa bảng, cột hoặc cập nhật dữ liệu cũ.
-- Thực thi toàn bộ script trên cùng kết nối; dừng và rollback nếu có lỗi.
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO
CREATE OR ALTER PROCEDURE sp_HuyDonHang
    @MaDonHang INT, @MaNguoiHuy INT, @LyDo NVARCHAR(255)
AS
BEGIN
    DECLARE @TrangThai VARCHAR(20), @MaChiNhanh INT, @MaKhachHang INT;
    SELECT @TrangThai = TrangThai, @MaChiNhanh = MaChiNhanh, @MaKhachHang = MaKhachHang
    FROM DonHang WHERE MaDonHang = @MaDonHang;

    IF @TrangThai IS NULL OR @TrangThai NOT IN ('CHO_XAC_NHAN','DA_XAC_NHAN')
        THROW 50040, N'Chỉ được hủy đơn trước khi bắt đầu in.', 1;
    IF NULLIF(LTRIM(RTRIM(@LyDo)), N'') IS NULL
        THROW 50045, N'Vui lòng nhập lý do hủy đơn.', 1;
    -- Khách đúng chủ hoặc quản lý đúng phạm vi; tất cả đều phải ở trước mốc in.
    IF NOT (@MaNguoiHuy = @MaKhachHang OR dbo.fn_LaQuanLy(@MaNguoiHuy, @MaChiNhanh) = 1)
        THROW 50041, N'Bạn không có quyền hủy đơn này.', 1;

    -- Chỉ hai trạng thái trước in mới tới được đây. Kho chưa trừ; bỏ máy đã phân công.
    UPDATE DonHang
    SET TrangThai = 'DA_HUY', GhiChu = @LyDo,
        MaMayIn = NULL
    WHERE MaDonHang = @MaDonHang AND TrangThai IN ('CHO_XAC_NHAN','DA_XAC_NHAN');
    IF @@ROWCOUNT = 0
        THROW 50040, N'Đơn đã bắt đầu in hoặc không còn được phép hủy.', 1;

    INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    VALUES (@MaKhachHang, @MaDonHang, N'Đơn hàng đã bị hủy', @LyDo);
END;
GO
CREATE OR ALTER PROCEDURE sp_HoanTien
    @MaDonHang INT, @MaNguoiDuyet INT
AS
BEGIN
    IF dbo.fn_LaSuperAdmin(@MaNguoiDuyet) = 0
        THROW 50042, N'Chỉ Super Admin được duyệt hoàn tiền.', 1;

    UPDATE tt SET TrangThai = 'DA_HOAN_TIEN'
    FROM ThanhToan tt JOIN DonHang dh ON dh.MaDonHang = tt.MaDonHang
    WHERE tt.MaDonHang = @MaDonHang AND dh.TrangThai = 'DA_HUY' AND dh.MaMayIn IS NULL
      AND tt.TrangThai = 'DA_THANH_TOAN';

    IF @@ROWCOUNT = 0
        THROW 50043, N'Chỉ hoàn tiền đơn đã hủy trước khi in, đã thanh toán và chưa hoàn tiền.', 1;
END;
GO
CREATE OR ALTER PROCEDURE sp_XacNhanChuyenKhoan
    @MaDonHang INT, @MaGiaoDich VARCHAR(50)
AS
BEGIN
    UPDATE ThanhToan
    SET TrangThai = 'DA_THANH_TOAN', MaGiaoDich = @MaGiaoDich, NgayThanhToan = GETDATE()
    WHERE MaDonHang = @MaDonHang AND PhuongThuc = 'VIETQR' AND TrangThai = 'CHUA_THANH_TOAN';

    IF @@ROWCOUNT > 0
        INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
        SELECT MaKhachHang, MaDonHang,
               CASE WHEN TrangThai = 'DA_HUY' THEN N'Đã nhận tiền cho đơn đã hủy' ELSE N'Thanh toán thành công' END,
               CASE WHEN TrangThai = 'DA_HUY' THEN N'Đơn đã hủy vẫn được giữ nguyên; khoản tiền đã nhận đang chờ xử lý hoàn tiền.'
                    ELSE N'Đơn hàng của bạn đã được thanh toán.' END
        FROM DonHang WHERE MaDonHang = @MaDonHang;
END;
GO
COMMIT TRANSACTION;
GO
