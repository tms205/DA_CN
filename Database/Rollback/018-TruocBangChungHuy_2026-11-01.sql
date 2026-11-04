USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;


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


-- Chay dinh ky: huy don chuyen khoan qua 15 phut chua thanh toan
CREATE OR ALTER PROCEDURE sp_HuyDonQuaHan
AS
BEGIN
    UPDATE dh SET TrangThai = 'DA_HUY', GhiChu = N'Quá 15 phút chưa thanh toán'
    FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
    WHERE dh.TrangThai = 'CHO_XAC_NHAN' AND tt.PhuongThuc = 'VIETQR'
      AND tt.TrangThai = 'CHUA_THANH_TOAN' AND DATEADD(MINUTE, 15, dh.NgayDat) < GETDATE();
END;

GO

