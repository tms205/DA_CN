-- Migration 010 da duoc dong y: bo sung in anh the, khong xoa du lieu cu.
-- Mau don trong la danh muc PDF trong API, khong can bang rieng.
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
GO
IF COL_LENGTH('DichVu','KichThuocAnh') IS NULL ALTER TABLE DichVu ADD KichThuocAnh VARCHAR(10) NULL;
IF COL_LENGTH('FileIn','KichThuocAnh') IS NULL ALTER TABLE FileIn ADD KichThuocAnh VARCHAR(10) NULL;
IF COL_LENGTH('FileIn','SoLuongAnh') IS NULL ALTER TABLE FileIn ADD SoLuongAnh INT NULL;
IF COL_LENGTH('ChiTietDonHang','KichThuocAnh') IS NULL ALTER TABLE ChiTietDonHang ADD KichThuocAnh VARCHAR(10) NULL;
IF COL_LENGTH('ChiTietDonHang','SoLuongAnh') IS NULL ALTER TABLE ChiTietDonHang ADD SoLuongAnh INT NULL;
GO
IF OBJECT_ID('CK_DichVu_AnhThe') IS NOT NULL ALTER TABLE DichVu DROP CONSTRAINT CK_DichVu_AnhThe;
    ALTER TABLE DichVu ADD CONSTRAINT CK_DichVu_AnhThe CHECK
    (KichThuocAnh IS NULL OR (KichThuocAnh IN ('2X3','3X4','4X6','35X45','5X5')
     AND LoaiDichVu='IN_AN' AND KhoGiay IS NOT NULL AND KhoGiay='A4'
     AND LoaiGiay IS NOT NULL AND LoaiGiay='GIAY_ANH' AND MatIn IS NOT NULL AND MatIn='1_MAT'
     AND MauSac IS NOT NULL AND MauSac='MAU'));
IF OBJECT_ID('CK_FileIn_AnhThe') IS NULL
    ALTER TABLE FileIn ADD CONSTRAINT CK_FileIn_AnhThe CHECK
    ((KichThuocAnh IS NULL AND SoLuongAnh IS NULL) OR
     (KichThuocAnh IS NOT NULL AND KichThuocAnh IN ('2X3','3X4','4X6','35X45','5X5')
      AND SoLuongAnh IS NOT NULL AND SoLuongAnh BETWEEN 1 AND 100));
IF OBJECT_ID('CK_ChiTietDonHang_AnhThe') IS NOT NULL ALTER TABLE ChiTietDonHang DROP CONSTRAINT CK_ChiTietDonHang_AnhThe;
    ALTER TABLE ChiTietDonHang ADD CONSTRAINT CK_ChiTietDonHang_AnhThe CHECK
    ((KichThuocAnh IS NULL AND SoLuongAnh IS NULL) OR
     (KichThuocAnh IS NOT NULL AND KichThuocAnh IN ('2X3','3X4','4X6','35X45','5X5')
      AND SoLuongAnh IS NOT NULL AND SoLuongAnh BETWEEN 1 AND 100
      AND SoBan IS NOT NULL AND SoBan=1 AND HuongGiay IS NOT NULL AND HuongGiay='DUNG'));
GO
-- Gia de xuat, don vi dong/anh; khong ghi de gia da sua cua chi nhanh.
DECLARE @VatTu INT;
SELECT TOP 1 @VatTu=MaVatTu FROM VatTu WHERE TenVatTu=N'Giấy ảnh A4';
IF @VatTu IS NULL
BEGIN
    INSERT VatTu(TenVatTu,DonViTinh,DonGia) VALUES(N'Giấy ảnh A4',N'tờ',0);
    SET @VatTu=SCOPE_IDENTITY();
END;
DECLARE @Gia TABLE(Ma VARCHAR(10), Ten NVARCHAR(100), Gia DECIMAL(12,0));
INSERT @Gia VALUES
('2X3',N'In ảnh thẻ 2x3 cm',3000),
('3X4',N'In ảnh thẻ 3x4 cm',5000),
('4X6',N'In ảnh thẻ 4x6 cm',10000),
('35X45',N'In ảnh thẻ 3,5x4,5 cm',7000),
('5X5',N'In ảnh thẻ 5x5 cm',10000);
INSERT DichVu(TenDichVu,LoaiDichVu,KhoGiay,LoaiGiay,MatIn,MauSac,DonGia,MaVatTu,TrangThai,KichThuocAnh)
SELECT g.Ten,'IN_AN','A4','GIAY_ANH','1_MAT','MAU',g.Gia,@VatTu,N'HIEN',g.Ma
FROM @Gia g WHERE NOT EXISTS(SELECT 1 FROM DichVu dv WHERE dv.KichThuocAnh=g.Ma);
INSERT BangGiaChiNhanh(MaChiNhanh,MaDichVu,DonGia,TrangThai)
SELECT cn.MaChiNhanh,dv.MaDichVu,dv.DonGia,N'HIEN'
FROM ChiNhanh cn CROSS JOIN DichVu dv
WHERE dv.KichThuocAnh IS NOT NULL
  AND NOT EXISTS(SELECT 1 FROM BangGiaChiNhanh bg WHERE bg.MaChiNhanh=cn.MaChiNhanh AND bg.MaDichVu=dv.MaDichVu);
-- Ton ban dau = 0: quan ly nhap va nhan giay anh truoc khi in, khong tao ton gia.
INSERT TonKho(MaChiNhanh,MaVatTu,SoLuong,MucToiThieu)
SELECT MaChiNhanh,@VatTu,0,10 FROM ChiNhanh cn
WHERE NOT EXISTS(SELECT 1 FROM TonKho tk WHERE tk.MaChiNhanh=cn.MaChiNhanh AND tk.MaVatTu=@VatTu);
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
CREATE OR ALTER PROCEDURE sp_TaoDonHang
    @MaKhachHang INT, @MaChiNhanh INT, @HinhThucNhan VARCHAR(20), @PhuongThuc VARCHAR(10),
    @DiaChiGiao NVARCHAR(255) = NULL, @SdtNhan VARCHAR(15) = NULL, @KhoangCach DECIMAL(6,2) = NULL,
    @PhiShipApi DECIMAL(12,0) = NULL, @InGap BIT = 0, @MaCode VARCHAR(30) = NULL, @DsFile Kieu_DongDonHang READONLY,
    @DsGiaCong Kieu_DongGiaCong READONLY, @MaDonHang INT OUTPUT, @HuongGiayJson NVARCHAR(MAX) = NULL
AS
BEGIN
    SET XACT_ABORT ON;

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

    DECLARE @TienHang DECIMAL(12,0) = @TienIn + @TienGiaCong + @PhuPhiGap;

    BEGIN TRAN;
        INSERT INTO DonHang (MaKhachHang, MaChiNhanh, MaKhuyenMai, HinhThucNhan, DiaChiGiao, SdtNhan, InGap,
                             TienHang, PhiShip, GiamGia, TongTien, NgayHenTra)
        VALUES (@MaKhachHang, @MaChiNhanh, @MaKM, @HinhThucNhan, @DiaChiGiao, @SdtNhan, @InGap,
                @TienHang, @PhiShip, @GiamGia, @TienHang + @PhiShip - @GiamGia,
                dbo.fn_UocTinhGioXong(@MaChiNhanh, @TongSoTrang, @InGap));
        SET @MaDonHang = SCOPE_IDENTITY();

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

        INSERT INTO ThanhToan (MaDonHang, PhuongThuc, SoTien)
        VALUES (@MaDonHang, @PhuongThuc, @TienHang + @PhiShip - @GiamGia);

        IF @MaKM IS NOT NULL
            UPDATE KhuyenMai SET DaDung = DaDung + 1 WHERE MaKhuyenMai = @MaKM;
    COMMIT;
END;
GO
COMMIT;
GO
PRINT N'Đã bổ sung dịch vụ ảnh thẻ; dữ liệu cũ được giữ nguyên.';

