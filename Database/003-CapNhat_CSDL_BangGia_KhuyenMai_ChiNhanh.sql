/* =============================================================
   Bang gia va khuyen mai rieng theo tung chi nhanh.
   Script giu nguyen du lieu cu va co the chay lai an toan.
   ============================================================= */
USE PhotocopyChain;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

SET XACT_ABORT ON;
BEGIN TRAN;

IF OBJECT_ID('BangGiaChiNhanh', 'U') IS NULL
BEGIN
    CREATE TABLE BangGiaChiNhanh (
        MaChiNhanh INT NOT NULL REFERENCES ChiNhanh(MaChiNhanh),
        MaDichVu INT NOT NULL REFERENCES DichVu(MaDichVu),
        DonGia DECIMAL(12,0) NOT NULL CHECK (DonGia >= 0),
        TrangThai NVARCHAR(20) NOT NULL DEFAULT N'HIEN'
            CHECK (TrangThai IN (N'HIEN',N'AN')),
        CONSTRAINT PK_BangGiaChiNhanh PRIMARY KEY (MaChiNhanh, MaDichVu)
    );
END

-- Moi chi nhanh bat dau bang gia hien tai, sau do CEO co the sua rieng.
INSERT INTO BangGiaChiNhanh (MaChiNhanh, MaDichVu, DonGia, TrangThai)
SELECT cn.MaChiNhanh, dv.MaDichVu, dv.DonGia, dv.TrangThai
FROM ChiNhanh cn CROSS JOIN DichVu dv
WHERE NOT EXISTS (
    SELECT 1 FROM BangGiaChiNhanh bg
    WHERE bg.MaChiNhanh = cn.MaChiNhanh AND bg.MaDichVu = dv.MaDichVu
);

IF COL_LENGTH('KhuyenMai', 'MaChiNhanh') IS NULL
BEGIN
    -- Giu ban sao de gan lai cac don cu sau khi tach khuyen mai theo chi nhanh.
    SELECT * INTO #KhuyenMaiCu FROM KhuyenMai;

    ALTER TABLE KhuyenMai ADD MaChiNhanh INT NULL;
END
GO

IF OBJECT_ID('tempdb..#KhuyenMaiCu') IS NOT NULL
BEGIN
    -- Bo UNIQUE cu chi gom MaCode.
    DECLARE @TenRangBuoc SYSNAME;
    SELECT TOP 1 @TenRangBuoc = kc.name
    FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id
                              AND ic.index_id = kc.unique_index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE kc.parent_object_id = OBJECT_ID('KhuyenMai') AND kc.type = 'UQ'
    GROUP BY kc.name
    HAVING COUNT(*) = 1 AND MAX(c.name) = 'MaCode';

    IF @TenRangBuoc IS NOT NULL
    BEGIN
        DECLARE @LenhXoaRangBuoc NVARCHAR(500);
        SET @LenhXoaRangBuoc = N'ALTER TABLE KhuyenMai DROP CONSTRAINT ' + QUOTENAME(@TenRangBuoc);
        EXEC sp_executesql @LenhXoaRangBuoc;
    END

    -- Ban goc thuoc chi nhanh dau tien tung dung ma; neu chua dung thi lay chi nhanh dau tien.
    UPDATE km
    SET MaChiNhanh = COALESCE(
        (SELECT MIN(dh.MaChiNhanh) FROM DonHang dh WHERE dh.MaKhuyenMai = km.MaKhuyenMai),
        (SELECT MIN(MaChiNhanh) FROM ChiNhanh)
    )
    FROM KhuyenMai km;

    IF EXISTS (SELECT 1 FROM KhuyenMai WHERE MaChiNhanh IS NULL)
        THROW 50100, N'Không thể chuyển khuyến mãi vì chưa có chi nhánh.', 1;

    -- Tao ban khuyen mai cho cac chi nhanh con lai.
    INSERT INTO KhuyenMai (MaChiNhanh, MaCode, TenKhuyenMai, PhanTramGiam, GiamToiDa,
                           DonToiThieu, NgayBatDau, NgayKetThuc, SoLuong, DaDung, TrangThai)
    SELECT cn.MaChiNhanh, cu.MaCode, cu.TenKhuyenMai, cu.PhanTramGiam, cu.GiamToiDa,
           cu.DonToiThieu, cu.NgayBatDau, cu.NgayKetThuc, cu.SoLuong, 0, cu.TrangThai
    FROM #KhuyenMaiCu cu CROSS JOIN ChiNhanh cn
    WHERE NOT EXISTS (
        SELECT 1 FROM KhuyenMai km
        WHERE km.MaChiNhanh = cn.MaChiNhanh AND km.MaCode = cu.MaCode
    );

    -- Don cu tro ve dung ban khuyen mai cua chi nhanh dat don.
    UPDATE dh
    SET MaKhuyenMai = kmMoi.MaKhuyenMai
    FROM DonHang dh
    JOIN #KhuyenMaiCu cu ON cu.MaKhuyenMai = dh.MaKhuyenMai
    JOIN KhuyenMai kmMoi ON kmMoi.MaChiNhanh = dh.MaChiNhanh AND kmMoi.MaCode = cu.MaCode;

    ALTER TABLE KhuyenMai ALTER COLUMN MaChiNhanh INT NOT NULL;
    ALTER TABLE KhuyenMai ADD CONSTRAINT FK_KhuyenMai_ChiNhanh
        FOREIGN KEY (MaChiNhanh) REFERENCES ChiNhanh(MaChiNhanh);
    ALTER TABLE KhuyenMai ADD CONSTRAINT UQ_KhuyenMai_ChiNhanh_MaCode
        UNIQUE (MaChiNhanh, MaCode);
END

COMMIT;
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

    SELECT @TienIn = SUM(d.SoTrang * d.SoBan * bg.DonGia),
           @TongSoTrang = SUM(d.SoTrang * d.SoBan)
    FROM @DsFile d
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


CREATE OR ALTER PROCEDURE sp_TaoDonHang
    @MaKhachHang INT, @MaChiNhanh INT, @HinhThucNhan VARCHAR(20), @PhuongThuc VARCHAR(10),
    @DiaChiGiao NVARCHAR(255) = NULL, @SdtNhan VARCHAR(15) = NULL, @KhoangCach DECIMAL(6,2) = NULL,
    @PhiShipApi DECIMAL(12,0) = NULL, @InGap BIT = 0, @MaCode VARCHAR(30) = NULL, @DsFile Kieu_DongDonHang READONLY,
    @DsGiaCong Kieu_DongGiaCong READONLY, @MaDonHang INT OUTPUT
AS
BEGIN
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM @DsFile d LEFT JOIN FileIn f ON f.MaFile = d.MaFile
               WHERE f.MaFile IS NULL OR f.MaKhachHang <> @MaKhachHang OR f.TrangThai <> N'SAN_SANG')
        THROW 50015, N'File in không tồn tại hoặc đã bị xóa.', 1;

    DECLARE @TienIn DECIMAL(12,0), @TienGiaCong DECIMAL(12,0), @PhuPhiGap DECIMAL(12,0), @PhiShip DECIMAL(12,0),
            @GiamGia DECIMAL(12,0), @MaKM INT, @LyDoKM NVARCHAR(200), @TongSoTrang INT;

    EXEC sp_TinhTien @MaChiNhanh, @HinhThucNhan, @KhoangCach, @PhiShipApi, @InGap, @MaCode, @DsFile, @DsGiaCong,
         @TienIn OUTPUT, @TienGiaCong OUTPUT, @PhuPhiGap OUTPUT, @PhiShip OUTPUT,
         @GiamGia OUTPUT, @MaKM OUTPUT, @LyDoKM OUTPUT, @TongSoTrang OUTPUT;

    DECLARE @TienHang DECIMAL(12,0) = @TienIn + @TienGiaCong + @PhuPhiGap;

    BEGIN TRAN;
        INSERT INTO DonHang (MaKhachHang, MaChiNhanh, MaKhuyenMai, HinhThucNhan, DiaChiGiao, SdtNhan, InGap,
                             TienHang, PhiShip, GiamGia, TongTien, NgayHenTra)
        VALUES (@MaKhachHang, @MaChiNhanh, @MaKM, @HinhThucNhan, @DiaChiGiao, @SdtNhan, @InGap,
                @TienHang, @PhiShip, @GiamGia, @TienHang + @PhiShip - @GiamGia,
                dbo.fn_UocTinhGioXong(@MaChiNhanh, @TongSoTrang, @InGap));
        SET @MaDonHang = SCOPE_IDENTITY();

        INSERT INTO ChiTietDonHang (MaDonHang, MaFile, MaDichVu, SoTrang, SoBan, DonGia, ThanhTien)
        SELECT @MaDonHang, d.MaFile, d.MaDichVu, d.SoTrang, d.SoBan, bg.DonGia,
               d.SoTrang * d.SoBan * bg.DonGia
        FROM @DsFile d
        JOIN BangGiaChiNhanh bg ON bg.MaChiNhanh = @MaChiNhanh AND bg.MaDichVu = d.MaDichVu;

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

        INSERT INTO ThanhToan (MaDonHang, PhuongThuc, SoTien)
        VALUES (@MaDonHang, @PhuongThuc, @TienHang + @PhiShip - @GiamGia);

        IF @MaKM IS NOT NULL
            UPDATE KhuyenMai SET DaDung = DaDung + 1 WHERE MaKhuyenMai = @MaKM;
    COMMIT;
END;
GO


PRINT N'Đã chuyển bảng giá và khuyến mãi sang phạm vi từng chi nhánh.';
GO
