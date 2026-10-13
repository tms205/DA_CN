-- Khôi phục thủ tục trước migration 012; cặp bìa kiếng/lò xo lại bị loại trừ.
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE sp_TinhTien
    @MaChiNhanh INT, @HinhThucNhan VARCHAR(20), @KhoangCach DECIMAL(6,2), @PhiShipApi DECIMAL(12,0), @InGap BIT,
    @MaCode VARCHAR(30), @DsFile Kieu_DongDonHang READONLY, @DsGiaCong Kieu_DongGiaCong READONLY,
    @TienIn DECIMAL(12,0) OUTPUT, @TienGiaCong DECIMAL(12,0) OUTPUT, @PhuPhiGap DECIMAL(12,0) OUTPUT,
    @PhiShip DECIMAL(12,0) OUTPUT, @GiamGia DECIMAL(12,0) OUTPUT, @MaKhuyenMai INT OUTPUT,
    @LyDoKhuyenMai NVARCHAR(200) OUTPUT, @TongSoTrang INT OUTPUT
AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM @DsFile)
        THROW 50010, N'Đơn hàng phải có ít nhất 1 file.', 1;
    IF EXISTS (SELECT 1 FROM @DsFile WHERE SoTrang <= 0 OR SoBan <= 0)
        THROW 50011, N'Số trang và số bản phải lớn hơn 0.', 1;
    IF EXISTS (SELECT 1 FROM @DsFile d
               LEFT JOIN DichVu dv ON dv.MaDichVu = d.MaDichVu
               LEFT JOIN BangGiaChiNhanh bg ON bg.MaChiNhanh = @MaChiNhanh AND bg.MaDichVu = d.MaDichVu
               WHERE dv.LoaiDichVu IS NULL OR dv.LoaiDichVu <> 'IN_AN'
                  OR bg.MaDichVu IS NULL OR bg.TrangThai <> N'HIEN')
        THROW 50012, N'Cấu hình in không có trong bảng giá.', 1;
    IF EXISTS (SELECT 1 FROM @DsGiaCong g
               LEFT JOIN DichVu dv ON dv.MaDichVu = g.MaDichVu
               LEFT JOIN BangGiaChiNhanh bg ON bg.MaChiNhanh = @MaChiNhanh AND bg.MaDichVu = g.MaDichVu
               WHERE dv.LoaiDichVu IS NULL OR dv.LoaiDichVu <> 'GIA_CONG'
                  OR bg.MaDichVu IS NULL OR bg.TrangThai <> N'HIEN')
        THROW 50013, N'Dịch vụ gia công không hợp lệ.', 1;
    -- Gia cong gan theo MaFile nen moi file chi duoc 1 dong cau hinh in
    IF EXISTS (SELECT 1 FROM @DsFile GROUP BY MaFile HAVING COUNT(*) > 1)
        THROW 50016, N'Mỗi file chỉ được chọn 1 cấu hình in.', 1;
    IF EXISTS (SELECT 1 FROM @DsGiaCong g WHERE g.MaFile NOT IN (SELECT MaFile FROM @DsFile))
        THROW 50017, N'Gia công phải đi kèm một file có trong đơn.', 1;
    IF EXISTS (SELECT 1 FROM @DsGiaCong GROUP BY MaFile, MaDichVu HAVING COUNT(*) > 1)
        THROW 50018, N'Mỗi gia công chỉ được chọn 1 lần cho cùng một file.', 1;
    -- Gia cong cung nhom thi loai tru nhau: 1 ban in chi dong lo xo HOAC bia kieng
    IF EXISTS (SELECT 1 FROM @DsGiaCong g JOIN DichVu dv ON dv.MaDichVu = g.MaDichVu
               WHERE dv.NhomGiaCong IS NOT NULL
               GROUP BY g.MaFile, dv.NhomGiaCong HAVING COUNT(*) > 1)
        THROW 50019, N'Mỗi bản in chỉ được chọn 1 kiểu đóng gáy (lò xo hoặc bìa kiếng).', 1;


    -- Lo xo va kim goc loai tru nhau; plastic van duoc chon cung lo xo.
    IF EXISTS (SELECT 1 FROM @DsGiaCong g JOIN DichVu dv ON dv.MaDichVu = g.MaDichVu
               GROUP BY g.MaFile
               HAVING SUM(CASE WHEN LTRIM(RTRIM(dv.TenDichVu)) = N'Đóng lò xo' THEN 1 ELSE 0 END) > 0
                  AND SUM(CASE WHEN LTRIM(RTRIM(dv.TenDichVu)) = N'Bấm kim góc' THEN 1 ELSE 0 END) > 0)
        THROW 50110, N'Không được chọn lò xo cùng bấm kim góc cho một bản in.', 1;

    -- Gioi han moi ban, khong nhan SoBan. Hai mat tinh so to lam tron len.
    IF EXISTS (SELECT 1 FROM @DsGiaCong g
               JOIN DichVu gc ON gc.MaDichVu = g.MaDichVu
               JOIN @DsFile d ON d.MaFile = g.MaFile
               JOIN DichVu dv ON dv.MaDichVu = d.MaDichVu
               WHERE LTRIM(RTRIM(gc.TenDichVu)) = N'Bấm kim góc'
                 AND CASE WHEN dv.MatIn = '2_MAT' THEN d.SoTrang / 2 + d.SoTrang % 2
                          ELSE d.SoTrang END > 50)
        THROW 50111, N'Bấm kim góc chỉ áp dụng tối đa 50 tờ giấy thực tế mỗi bản.', 1;

    -- Anh the tinh theo so anh, khong theo so trang PDF ghep.
    IF EXISTS (SELECT 1 FROM @DsFile d
               LEFT JOIN FileIn f ON f.MaFile = d.MaFile
               JOIN DichVu dv ON dv.MaDichVu = d.MaDichVu
               WHERE f.MaFile IS NULL
                  OR ISNULL(f.KichThuocAnh, '') <> ISNULL(dv.KichThuocAnh, '')
                  OR (f.KichThuocAnh IS NOT NULL AND
                      (f.SoLuongAnh IS NULL OR f.SoLuongAnh NOT BETWEEN 1 AND 100
                       OR d.SoBan <> 1 OR d.SoTrang <> f.SoTrang)))
        THROW 50130, N'Ảnh thẻ phải chọn đúng kích thước, in toàn bộ PDF và giữ một bản. Muốn đổi số ảnh, vui lòng tải lại.', 1;
    IF EXISTS (SELECT 1 FROM @DsGiaCong g JOIN FileIn f ON f.MaFile = g.MaFile
               WHERE f.KichThuocAnh IS NOT NULL)
        THROW 50131, N'Ảnh thẻ không dùng gia công đóng gáy hoặc ép plastic của tài liệu.', 1;

    SELECT @TienIn = SUM(CASE WHEN f.KichThuocAnh IS NOT NULL THEN f.SoLuongAnh * bg.DonGia
                             ELSE CAST(d.SoTrang AS BIGINT) * d.SoBan * bg.DonGia END),
           @TongSoTrang = SUM(d.SoTrang * d.SoBan)
    FROM @DsFile d
    JOIN FileIn f ON f.MaFile = d.MaFile
    JOIN BangGiaChiNhanh bg ON bg.MaChiNhanh = @MaChiNhanh AND bg.MaDichVu = d.MaDichVu;

    -- Tien gia cong: cong het cac gia cong da chon, moi cai nhan voi so ban in cua file do
    SELECT @TienGiaCong = ISNULL(SUM(d.SoBan * bg.DonGia), 0)
    FROM @DsGiaCong g
    JOIN @DsFile d ON d.MaFile = g.MaFile
    JOIN BangGiaChiNhanh bg ON bg.MaChiNhanh = @MaChiNhanh AND bg.MaDichVu = g.MaDichVu;

    SET @PhuPhiGap = CASE WHEN @InGap = 1 THEN 10000 ELSE 0 END;

    -- Tuan 3: Ahamove tra quang duong + phi ship. CSDL chi kiem tra ban kinh va dung dung phi API.
    SET @PhiShip = 0;
    IF @HinhThucNhan = 'GIAO_HANG'
    BEGIN
        DECLARE @BanKinh DECIMAL(5,2);
        SELECT @BanKinh = BanKinhGiao FROM ChiNhanh WHERE MaChiNhanh = @MaChiNhanh;
        IF @KhoangCach IS NULL OR @KhoangCach > @BanKinh
            THROW 50014, N'Địa chỉ giao hàng nằm ngoài bán kính giao hàng của chi nhánh.', 1;
        IF @PhiShipApi IS NULL OR @PhiShipApi < 0
            THROW 50037, N'Chưa lấy được phí giao hàng từ Ahamove.', 1;
        SET @PhiShip = ROUND(@PhiShipApi, 0);
    END

    -- Khuyen mai: ma sai thi khong giam, van cho dat don
    SET @GiamGia = 0; SET @MaKhuyenMai = NULL; SET @LyDoKhuyenMai = NULL;
    IF @MaCode IS NOT NULL
    BEGIN
        DECLARE @Ma INT, @PhanTram INT, @ToiDa DECIMAL(12,0), @DonToiThieu DECIMAL(12,0);
        SELECT @Ma = MaKhuyenMai, @PhanTram = PhanTramGiam, @ToiDa = GiamToiDa, @DonToiThieu = DonToiThieu
        FROM KhuyenMai
        WHERE MaChiNhanh = @MaChiNhanh AND MaCode = @MaCode
          AND TrangThai = N'HOAT_DONG' AND DaDung < SoLuong
          AND CAST(GETDATE() AS DATE) BETWEEN NgayBatDau AND NgayKetThuc;

        IF @Ma IS NULL
            SET @LyDoKhuyenMai = N'Mã khuyến mãi không hợp lệ hoặc đã hết hạn.';
        ELSE IF @TienIn + @TienGiaCong < @DonToiThieu
            SET @LyDoKhuyenMai = N'Đơn hàng chưa đạt giá trị tối thiểu để áp mã.';
        ELSE
        BEGIN
            SET @MaKhuyenMai = @Ma;
            SET @GiamGia = ROUND((@TienIn + @TienGiaCong) * @PhanTram / 100.0, 0);
            IF @ToiDa IS NOT NULL AND @GiamGia > @ToiDa SET @GiamGia = @ToiDa;
        END
    END
END;
GO
