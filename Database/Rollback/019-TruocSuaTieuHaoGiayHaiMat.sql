-- Khoi phuc logic procedure truoc 019; khong phuc hoi so luong ton kho.
CREATE OR ALTER PROCEDURE dbo.sp_CapNhatTrangThaiIn
    @MaDonHang INT, @TrangThaiMoi VARCHAR(20)
AS
BEGIN
    SET XACT_ABORT ON;
    DECLARE @TrangThai VARCHAR(20), @MaChiNhanh INT;
    SELECT @TrangThai = TrangThai, @MaChiNhanh = MaChiNhanh FROM DonHang WHERE MaDonHang = @MaDonHang;

    IF NOT ((@TrangThai = 'DA_XAC_NHAN' AND @TrangThaiMoi = 'DANG_IN') OR (@TrangThai = 'DANG_IN' AND @TrangThaiMoi = 'DA_IN'))
        THROW 50024, N'Chuyển trạng thái in không hợp lệ.', 1;

    BEGIN TRAN;
    IF @TrangThaiMoi = 'DANG_IN'
    BEGIN
        -- So luong vat tu can: giay theo so to, vat tu gia cong theo so ban
        DECLARE @Can TABLE (MaVatTu INT, SoLuong INT);
        INSERT INTO @Can
        SELECT MaVatTu, SUM(SoLuong) FROM (
            SELECT dv.MaVatTu, ct.SoTrang * ct.SoBan AS SoLuong
            FROM ChiTietDonHang ct JOIN DichVu dv ON dv.MaDichVu = ct.MaDichVu
            WHERE ct.MaDonHang = @MaDonHang AND dv.MaVatTu IS NOT NULL
            UNION ALL
            SELECT gc.MaVatTu, ct.SoBan
            FROM ChiTietDonHang ct
            JOIN ChiTietGiaCong ctgc ON ctgc.MaChiTiet = ct.MaChiTiet
            JOIN DichVu gc ON gc.MaDichVu = ctgc.MaDichVu
            WHERE ct.MaDonHang = @MaDonHang AND gc.MaVatTu IS NOT NULL
        ) x GROUP BY MaVatTu;

        IF EXISTS (SELECT 1 FROM @Can c LEFT JOIN TonKho tk ON tk.MaVatTu = c.MaVatTu AND tk.MaChiNhanh = @MaChiNhanh
                   WHERE ISNULL(tk.SoLuong, 0) < c.SoLuong)
            THROW 50025, N'Không đủ vật tư trong kho để in đơn này.', 1;

        UPDATE tk SET SoLuong = tk.SoLuong - c.SoLuong
        FROM TonKho tk JOIN @Can c ON c.MaVatTu = tk.MaVatTu
        WHERE tk.MaChiNhanh = @MaChiNhanh;
    END

    UPDATE DonHang SET TrangThai = @TrangThaiMoi WHERE MaDonHang = @MaDonHang;

    INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    SELECT MaKhachHang, MaDonHang,
           CASE WHEN @TrangThaiMoi = 'DANG_IN' THEN N'Đơn hàng đang được in' ELSE N'Đơn hàng đã in xong' END,
           CASE WHEN @TrangThaiMoi = 'DANG_IN' THEN N'Đơn của bạn đang được in.' ELSE N'Đơn của bạn đã in xong.' END
    FROM DonHang WHERE MaDonHang = @MaDonHang;
    COMMIT;
END;
GO
