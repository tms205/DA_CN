-- 020: Không thêm bảng. Hai cột DonHang + sửa 5 thủ tục; không cập nhật đơn cũ.
-- Đơn cũ giữ LEGACY. Chỉ đơn APP tạo sau triển khai áp dụng 120 giây.
-- Chạy sau khi được chủ dự án đồng ý. Lưu ảnh ngoài wwwroot; sao lưu cùng database.
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
IF COL_LENGTH('dbo.DonHang','KenhDatDon') IS NULL
 ALTER TABLE dbo.DonHang ADD KenhDatDon VARCHAR(10) NOT NULL CONSTRAINT DF_DonHang_KenhDatDon DEFAULT 'LEGACY';
IF COL_LENGTH('dbo.DonHang','YeuCauHoanTienJson') IS NULL
 ALTER TABLE dbo.DonHang ADD YeuCauHoanTienJson NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_DonHang_KenhDatDon')
 ALTER TABLE dbo.DonHang ADD CONSTRAINT CK_DonHang_KenhDatDon CHECK(KenhDatDon IN ('APP','TAI_QUAY','LEGACY'));
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_DonHang_YeuCauHoanTienJson')
 ALTER TABLE dbo.DonHang ADD CONSTRAINT CK_DonHang_YeuCauHoanTienJson CHECK(YeuCauHoanTienJson IS NULL OR ISJSON(YeuCauHoanTienJson)=1);
GO
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

    DECLARE @TienHang DECIMAL(12,0) = @TienIn + @TienGiaCong + @PhuPhiGap;

    BEGIN TRAN;
        INSERT INTO DonHang (MaKhachHang, MaChiNhanh, MaKhuyenMai, HinhThucNhan, DiaChiGiao, SdtNhan, InGap,
                             TienHang, PhiShip, GiamGia, TongTien, NgayHenTra, KenhDatDon)
        VALUES (@MaKhachHang, @MaChiNhanh, @MaKM, @HinhThucNhan, @DiaChiGiao, @SdtNhan, @InGap,
                @TienHang, @PhiShip, @GiamGia, @TienHang + @PhiShip - @GiamGia,
                dbo.fn_UocTinhGioXong(@MaChiNhanh, @TongSoTrang, @InGap), @KenhDatDon);
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

/* CEO va nhan vien chi nhanh duoc tiep nhan don khi da mo ca. */
CREATE OR ALTER PROCEDURE sp_TiepNhanDon
    @MaDonHang INT, @MaNhanVien INT
AS
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRAN;
    DECLARE @MaChiNhanhDon INT, @MaChiNhanhNhanVien INT, @VaiTro VARCHAR(20);

    SELECT @MaChiNhanhDon = MaChiNhanh
    FROM DonHang WITH(UPDLOCK,HOLDLOCK)
    WHERE MaDonHang = @MaDonHang;

    SELECT @MaChiNhanhNhanVien = MaChiNhanh, @VaiTro = VaiTro
    FROM NguoiDung
    WHERE MaNguoiDung = @MaNhanVien;

    IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN')
       OR (@VaiTro <> 'SUPER_ADMIN' AND @MaChiNhanhNhanVien <> @MaChiNhanhDon)
        THROW 50021, N'Bạn không có quyền tiếp nhận đơn của chi nhánh này.', 1;

    IF NOT EXISTS (SELECT 1 FROM CaLamViec
                   WHERE MaNhanVien = @MaNhanVien AND MaChiNhanh = @MaChiNhanhDon
                     AND TrangThai = 'DANG_MO')
        THROW 50029, N'Bạn chưa mở ca tại chi nhánh này.', 1;

    IF NOT EXISTS (SELECT 1 FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
                   WHERE dh.MaDonHang = @MaDonHang AND dh.TrangThai = 'CHO_XAC_NHAN'
                     AND (tt.PhuongThuc = 'COD' OR tt.TrangThai = 'DA_THANH_TOAN'))
        THROW 50020, N'Đơn chưa sẵn sàng để tiếp nhận.', 1;


    IF EXISTS(SELECT 1 FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
      WHERE dh.MaDonHang=@MaDonHang AND dh.KenhDatDon='APP'
        AND (tt.TrangThai<>'DA_THANH_TOAN' OR tt.NgayThanhToan IS NULL OR GETDATE()<DATEADD(SECOND,120,tt.NgayThanhToan)))
      THROW 50221,N'Đơn app đang trong 2 phút xác nhận sau thanh toán; chưa được tiếp nhận.',1;

    UPDATE DonHang
    SET TrangThai = 'DA_XAC_NHAN', MaNhanVien = @MaNhanVien
    WHERE MaDonHang = @MaDonHang;

    INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    SELECT MaKhachHang, MaDonHang, N'Đơn hàng đã được tiếp nhận',
           N'Chi nhánh đã nhận đơn và xếp vào hàng đợi in.'
    FROM DonHang WHERE MaDonHang = @MaDonHang;
    COMMIT;
END;

GO
CREATE OR ALTER PROCEDURE dbo.sp_HuyDonHang
 @MaDonHang INT,@MaNguoiHuy INT,@LyDo NVARCHAR(255),@BangChungJson NVARCHAR(MAX)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRAN;
 DECLARE @TrangThai VARCHAR(20),@MaChiNhanh INT,@MaKhachHang INT,@NgayHenTra DATETIME,@LaKhach BIT=0;
 SELECT @TrangThai=TrangThai,@MaChiNhanh=MaChiNhanh,@MaKhachHang=MaKhachHang,@NgayHenTra=NgayHenTra
 FROM dbo.DonHang WITH(UPDLOCK,HOLDLOCK) WHERE MaDonHang=@MaDonHang;
 IF @TrangThai IS NULL OR @TrangThai NOT IN('CHO_XAC_NHAN','DA_XAC_NHAN') THROW 50040,N'Đơn đã bắt đầu in hoặc không còn được hủy.',1;
 IF NOT(@MaNguoiHuy=@MaKhachHang OR dbo.fn_LaQuanLy(@MaNguoiHuy,@MaChiNhanh)=1) THROW 50041,N'Bạn không có quyền hủy đơn này.',1;
 IF EXISTS(SELECT 1 FROM dbo.NguoiDung WHERE MaNguoiDung=@MaNguoiHuy AND VaiTro='CUSTOMER') SET @LaKhach=1;
 IF EXISTS(SELECT 1 FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang WHERE dh.MaDonHang=@MaDonHang AND dh.KenhDatDon='APP' AND tt.TrangThai='DA_THANH_TOAN')
 BEGIN
   IF @TrangThai<>'CHO_XAC_NHAN' OR NOT EXISTS(SELECT 1 FROM ThanhToan WHERE MaDonHang=@MaDonHang AND NgayThanhToan IS NOT NULL AND GETDATE()<DATEADD(SECOND,120,NgayThanhToan))
     THROW 50222,N'Đơn đã tiếp nhận hoặc hết hạn 2 phút sau thanh toán; không thể hủy/hoàn thông thường.',1;
 END
 ELSE IF @LaKhach=1 AND (@TrangThai<>'CHO_XAC_NHAN' OR (@NgayHenTra IS NOT NULL AND @NgayHenTra<=DATEADD(MINUTE,15,GETDATE())))
   THROW 50046,N'Khách chỉ hủy đơn chưa thanh toán/đơn cũ khi chưa tiếp nhận và còn hơn 15 phút trước giờ hẹn.',1;
 IF NULLIF(LTRIM(RTRIM(@LyDo)),N'') IS NULL THROW 50045,N'Vui lòng nhập lý do hủy.',1;
 IF ISJSON(@BangChungJson)<>1 OR @BangChungJson IS NULL THROW 50047,N'Cần bằng chứng hủy đơn hợp lệ.',1;
 IF ISNULL(JSON_VALUE(@BangChungJson,'$.Loai'),'')<>'THU_CONG' OR ISNULL(TRY_CONVERT(INT,JSON_VALUE(@BangChungJson,'$.MaDonHang')),-1)<>@MaDonHang
    OR ISNULL(TRY_CONVERT(INT,JSON_VALUE(@BangChungJson,'$.MaNguoiGui')),-1)<>@MaNguoiHuy
    OR (SELECT COUNT(*) FROM OPENJSON(@BangChungJson,'$.Anh')) NOT BETWEEN 1 AND 3
    THROW 50047,N'Bằng chứng phải đúng đơn/người gửi và có 1–3 ảnh.',1;
 UPDATE dbo.DonHang SET TrangThai='DA_HUY',GhiChu=@LyDo,MaMayIn=NULL,BangChungHuyJson=@BangChungJson WHERE MaDonHang=@MaDonHang;
 INSERT dbo.ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
 SELECT MaNguoiDung,@MaDonHang,N'Đơn hàng đã bị hủy',@LyDo FROM dbo.NguoiDung
 WHERE MaNguoiDung=@MaKhachHang OR (TrangThai=N'HOAT_DONG' AND (VaiTro='SUPER_ADMIN' OR (VaiTro='BRANCH_ADMIN' AND MaChiNhanh=@MaChiNhanh)));
 COMMIT;
END;

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
            SELECT dv.MaVatTu,
                   (CASE WHEN dv.MatIn='2_MAT' THEN ct.SoTrang / 2 + ct.SoTrang % 2
                         ELSE ct.SoTrang END) * ct.SoBan AS SoLuong
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
