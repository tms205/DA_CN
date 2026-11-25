-- 033: Chặn mã khuyến mãi bị dùng quá số lượt khi nhiều đơn dùng cùng lúc. Không thêm bảng/cột, không đổi dữ liệu cũ.
-- Lỗi cũ (chạy thử 25/11/2026): mã còn 1 lượt, 2 khách đặt cùng lúc → 8/10 lần cả 2 đơn đều được giảm, DaDung = 2 > SoLuong = 1.
-- Nguyên nhân: sp_TinhTien (file 012) chỉ ĐỌC điều kiện DaDung < SoLuong, không khóa; sp_TaoDonHang (file 020) cộng
--             DaDung + 1 không kèm điều kiện → 2 đơn cùng đọc "còn 1 lượt" rồi cùng cộng.
-- Sửa: chỉ thay sp_TaoDonHang, gộp kiểm còn lượt + cộng lượt vào một câu UPDATE có điều kiện, hết lượt thì THROW 50320.
-- API/Web/App không phải sửa: DonHangController bắt SqlException và trả message, Web/App hiện câu báo lỗi đó.
-- Quay lại bản trước: Rollback/033-TruocChanMaKhuyenMaiVuotLuot.sql
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
GO

-- =====================================================================================
-- sp_TaoDonHang: TẠO ĐƠN HÀNG (bản đang chạy, thay bản ở file 020). Dùng chung cho đơn App và đơn lập tại quầy.
-- Gọi từ: TaoDonHangService.Tao (C#), nằm trong transaction do C# mở.
-- Các bước:
--   1. Kiểm file thuộc đúng khách và đã sẵn sàng; kiểm hướng giấy (gửi dạng JSON).
--   2. Gọi sp_TinhTien để tính tiền (file 012).
--   3. Ghi 4 bảng: DonHang (1 dòng), ChiTietDonHang (mỗi file 1 dòng), ChiTietGiaCong (mỗi gia công 1 dòng),
--      ThanhToan (1 dòng, trạng thái mặc định CHUA_THANH_TOAN).
--   4. Mã khuyến mãi dùng được thì tăng DaDung thêm 1, nhưng chỉ khi còn lượt (DaDung < SoLuong);
--      hết lượt thì báo lỗi 50320 và hủy cả đơn.
-- Ra: @MaDonHang (OUTPUT) = mã đơn mới.
-- =====================================================================================
CREATE OR ALTER PROCEDURE sp_TaoDonHang
    @MaKhachHang INT, @MaChiNhanh INT, @HinhThucNhan VARCHAR(20), @PhuongThuc VARCHAR(10),
    @DiaChiGiao NVARCHAR(255) = NULL, @SdtNhan VARCHAR(15) = NULL, @KhoangCach DECIMAL(6,2) = NULL,
    @PhiShipApi DECIMAL(12,0) = NULL, @InGap BIT = 0, @MaCode VARCHAR(30) = NULL, @DsFile Kieu_DongDonHang READONLY,
    @DsGiaCong Kieu_DongGiaCong READONLY, @MaDonHang INT OUTPUT, @HuongGiayJson NVARCHAR(MAX) = NULL, @KenhDatDon VARCHAR(10) = 'LEGACY'
AS
BEGIN
    SET XACT_ABORT ON;
    IF @KenhDatDon NOT IN ('APP','TAI_QUAY','LEGACY') THROW 50220,N'Kênh đặt đơn không hợp lệ.',1;

    IF EXISTS (SELECT 1 FROM @DsFile d LEFT JOIN FileIn f ON f.MaFile = d.MaFile
               WHERE f.MaFile IS NULL OR f.MaKhachHang <> @MaKhachHang OR f.TrangThai <> N'SAN_SANG')
        THROW 50015, N'File in không tồn tại hoặc đã bị xóa.', 1;


    -- Tham so tuy chon: client cu khong gui thi giu huong DUNG.
    DECLARE @Huong TABLE (MaFile INT, HuongGiay NVARCHAR(MAX));
    IF @HuongGiayJson IS NOT NULL
    BEGIN
        IF ISJSON(@HuongGiayJson) <> 1 OR LEFT(LTRIM(@HuongGiayJson), 1) <> '['
            THROW 50112, N'Danh sách hướng giấy không hợp lệ.', 1;

        INSERT INTO @Huong (MaFile, HuongGiay)
        SELECT MaFile, HuongGiay FROM OPENJSON(@HuongGiayJson)
        WITH (MaFile INT '$.maFile', HuongGiay NVARCHAR(MAX) '$.huongGiay');

        IF EXISTS (SELECT 1 FROM @Huong
                   WHERE MaFile IS NULL OR HuongGiay IS NULL
                      OR NOT ((HuongGiay COLLATE Latin1_General_100_BIN2 = N'DUNG' AND DATALENGTH(HuongGiay) = 8)
                           OR (HuongGiay COLLATE Latin1_General_100_BIN2 = N'NGANG' AND DATALENGTH(HuongGiay) = 10))
                      OR MaFile NOT IN (SELECT MaFile FROM @DsFile))
           OR EXISTS (SELECT 1 FROM @Huong GROUP BY MaFile HAVING COUNT(*) > 1)
           OR EXISTS (SELECT 1 FROM @DsFile d WHERE NOT EXISTS (SELECT 1 FROM @Huong h WHERE h.MaFile = d.MaFile))
            THROW 50112, N'Mỗi file phải có đúng một hướng giấy DUNG hoặc NGANG.', 1;
    END

    IF EXISTS (SELECT 1 FROM @Huong h JOIN FileIn f ON f.MaFile = h.MaFile
               WHERE f.KichThuocAnh IS NOT NULL AND h.HuongGiay <> N'DUNG')
        THROW 50132, N'PDF ảnh thẻ đã ghép đúng kích thước, không đổi sang giấy ngang.', 1;

    DECLARE @TienIn DECIMAL(12,0), @TienGiaCong DECIMAL(12,0), @PhuPhiGap DECIMAL(12,0), @PhiShip DECIMAL(12,0),
            @GiamGia DECIMAL(12,0), @MaKM INT, @LyDoKM NVARCHAR(200), @TongSoTrang INT;

    EXEC sp_TinhTien @MaChiNhanh, @HinhThucNhan, @KhoangCach, @PhiShipApi, @InGap, @MaCode, @DsFile, @DsGiaCong,
         @TienIn OUTPUT, @TienGiaCong OUTPUT, @PhuPhiGap OUTPUT, @PhiShip OUTPUT,
         @GiamGia OUTPUT, @MaKM OUTPUT, @LyDoKM OUTPUT, @TongSoTrang OUTPUT;

    -- Tiền hàng = tiền in + gia công + phụ phí gấp (chưa gồm phí ship, chưa trừ giảm giá)
    DECLARE @TienHang DECIMAL(12,0) = @TienIn + @TienGiaCong + @PhuPhiGap;

    BEGIN TRAN;
        -- (3a) Dòng đơn hàng. TongTien = tiền hàng + phí ship - giảm giá.
        -- NgayHenTra = giờ nhận dự kiến, do hàm fn_UocTinhGioXong ước theo tổng số trang và in gấp.
        INSERT INTO DonHang (MaKhachHang, MaChiNhanh, MaKhuyenMai, HinhThucNhan, DiaChiGiao, SdtNhan, InGap,
                             TienHang, PhiShip, GiamGia, TongTien, NgayHenTra, KenhDatDon)
        VALUES (@MaKhachHang, @MaChiNhanh, @MaKM, @HinhThucNhan, @DiaChiGiao, @SdtNhan, @InGap,
                @TienHang, @PhiShip, @GiamGia, @TienHang + @PhiShip - @GiamGia,
                dbo.fn_UocTinhGioXong(@MaChiNhanh, @TongSoTrang, @InGap), @KenhDatDon);
        SET @MaDonHang = SCOPE_IDENTITY();

        -- (3b) Mỗi file một dòng chi tiết, ghi lại đơn giá TẠI THỜI ĐIỂM ĐẶT (sau này đổi giá cũng không ảnh hưởng đơn cũ)
        INSERT INTO ChiTietDonHang (MaDonHang, MaFile, MaDichVu, SoTrang, SoBan, DonGia, ThanhTien, KichThuocAnh, SoLuongAnh)
        SELECT @MaDonHang, d.MaFile, d.MaDichVu, d.SoTrang, d.SoBan, bg.DonGia,
               CASE WHEN f.KichThuocAnh IS NOT NULL THEN f.SoLuongAnh * bg.DonGia
                    ELSE CAST(d.SoTrang AS BIGINT) * d.SoBan * bg.DonGia END,
               f.KichThuocAnh, f.SoLuongAnh
        FROM @DsFile d
        JOIN FileIn f ON f.MaFile = d.MaFile
        JOIN BangGiaChiNhanh bg ON bg.MaChiNhanh = @MaChiNhanh AND bg.MaDichVu = d.MaDichVu;


        UPDATE ct SET HuongGiay = h.HuongGiay
        FROM ChiTietDonHang ct JOIN @Huong h ON h.MaFile = ct.MaFile
        WHERE ct.MaDonHang = @MaDonHang;

        -- Gia cong di kem: doi MaFile sang MaChiTiet vua sinh ra o tren
        INSERT INTO ChiTietGiaCong (MaChiTiet, MaDichVu, GiaGiaCong)
        SELECT ct.MaChiTiet, bg.MaDichVu, bg.DonGia
        FROM @DsGiaCong g
        JOIN ChiTietDonHang ct ON ct.MaDonHang = @MaDonHang AND ct.MaFile = g.MaFile
        JOIN BangGiaChiNhanh bg ON bg.MaChiNhanh = @MaChiNhanh AND bg.MaDichVu = g.MaDichVu;

        -- Cong tien gia cong vao thanh tien cua tung dong
        UPDATE ct SET ThanhTien = ct.ThanhTien + ct.SoBan * x.TienGiaCong
        FROM ChiTietDonHang ct
        JOIN (SELECT MaChiTiet, SUM(GiaGiaCong) AS TienGiaCong FROM ChiTietGiaCong GROUP BY MaChiTiet) x
          ON x.MaChiTiet = ct.MaChiTiet
        WHERE ct.MaDonHang = @MaDonHang;

        -- (3d) Dòng thanh toán: COD (tiền mặt khi nhận) hoặc VIETQR. Số tiền = tổng tiền đơn.
        INSERT INTO ThanhToan (MaDonHang, PhuongThuc, SoTien)
        VALUES (@MaDonHang, @PhuongThuc, @TienHang + @PhiShip - @GiamGia);

        -- (4) Dùng mã khuyến mãi thì tăng số lượt đã dùng.
        --     Kiểm "còn lượt" và cộng lượt nằm trong CÙNG MỘT câu UPDATE. SQL Server khóa dòng khuyến mãi khi cập nhật,
        --     nên 2 đơn tới cùng lúc phải chạy lần lượt: đơn trước cộng được, đơn sau thấy DaDung đã bằng SoLuong
        --     thì không cập nhật được dòng nào (@@ROWCOUNT = 0) → báo hết lượt, cả đơn vừa ghi bị hủy theo transaction.
        IF @MaKM IS NOT NULL
        BEGIN
            UPDATE KhuyenMai SET DaDung = DaDung + 1
            WHERE MaKhuyenMai = @MaKM AND DaDung < SoLuong;
            IF @@ROWCOUNT = 0
                THROW 50320, N'Mã khuyến mãi đã hết lượt sử dụng. Vui lòng bỏ mã hoặc dùng mã khác rồi đặt lại.', 1;
        END
    COMMIT;
END;
GO
COMMIT;
GO
