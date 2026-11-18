:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
GO

-- Gan may in: may phai cung chi nhanh, dang hoat dong, du kho giay / in mau / in 2 mat
CREATE OR ALTER PROCEDURE sp_PhanCongMayIn
    @MaDonHang INT, @MaMayIn INT
AS
BEGIN

    IF EXISTS(SELECT 1 FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
      WHERE dh.MaDonHang=@MaDonHang AND dh.KenhDatDon='APP' AND
      (dh.MaNhanVien IS NULL OR tt.TrangThai<>'DA_THANH_TOAN' OR tt.NgayThanhToan IS NULL OR GETDATE()<DATEADD(SECOND,120,tt.NgayThanhToan)))
      THROW 50223,N'Đơn app cần hết 2 phút và được nhân viên tiếp nhận trước khi gửi máy in.',1;
    DECLARE @CanA3 INT, @CanMau INT, @Can2Mat INT;
    SELECT @CanA3   = MAX(CASE WHEN dv.KhoGiay = 'A3' THEN 1 ELSE 0 END),
           @CanMau  = MAX(CASE WHEN dv.MauSac = 'MAU' THEN 1 ELSE 0 END),
           @Can2Mat = MAX(CASE WHEN dv.MatIn = '2_MAT' THEN 1 ELSE 0 END)
    FROM ChiTietDonHang ct JOIN DichVu dv ON dv.MaDichVu = ct.MaDichVu
    WHERE ct.MaDonHang = @MaDonHang;

    IF NOT EXISTS (SELECT 1 FROM MayIn m JOIN DonHang dh ON dh.MaChiNhanh = m.MaChiNhanh
                   WHERE m.MaMayIn = @MaMayIn AND dh.MaDonHang = @MaDonHang AND m.TrangThai = N'HOAT_DONG')
        THROW 50022, N'Máy in không thuộc chi nhánh hoặc đang không hoạt động.', 1;
    IF EXISTS (SELECT 1 FROM MayIn WHERE MaMayIn = @MaMayIn
               AND ((@CanA3 = 1 AND KhoGiay <> 'A3') OR (@CanMau = 1 AND InMau = 0) OR (@Can2Mat = 1 AND In2Mat = 0)))
        THROW 50023, N'Máy in không đáp ứng được khổ giấy / in màu / in 2 mặt của đơn.', 1;

    UPDATE DonHang SET MaMayIn = @MaMayIn WHERE MaDonHang = @MaDonHang;
END;


GO

-- DA_XAC_NHAN -> DANG_IN (tru kho vat tu) -> DA_IN.
-- File goc duoc giu lai den khi don HOAN_THANH / DA_HUY; job nen cua API se xoa file vat ly.
CREATE OR ALTER PROCEDURE sp_CapNhatTrangThaiIn
    @MaDonHang INT, @TrangThaiMoi VARCHAR(20)
AS
BEGIN

    IF EXISTS(SELECT 1 FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
      WHERE dh.MaDonHang=@MaDonHang AND dh.KenhDatDon='APP' AND
      (dh.MaNhanVien IS NULL OR tt.TrangThai<>'DA_THANH_TOAN' OR tt.NgayThanhToan IS NULL OR GETDATE()<DATEADD(SECOND,120,tt.NgayThanhToan)))
      THROW 50223,N'Đơn app cần hết 2 phút và được nhân viên tiếp nhận trước khi gửi máy in.',1;
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
COMMIT;
GO
