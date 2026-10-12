USE PhotocopyChain;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- Kieu bang truyen danh sach file khi dat don (moi file 1 dong)
CREATE TYPE Kieu_DongDonHang AS TABLE (
    MaFile     INT NOT NULL,
    MaDichVu   INT NOT NULL,
    SoTrang    INT NOT NULL,
    SoBan      INT NOT NULL
);
GO

-- Kieu bang truyen gia cong di kem: 1 file chon duoc nhieu gia cong nen moi gia cong 1 dong
CREATE TYPE Kieu_DongGiaCong AS TABLE (
    MaFile     INT NOT NULL,
    MaDichVu   INT NOT NULL
);
GO

/* ===================== TAI KHOAN ===================== */

-- Quan ly chi nhanh chi tao duoc STAFF/SHIPPER cua chi nhanh minh, Super Admin tao duoc ca BRANCH_ADMIN
CREATE PROCEDURE sp_TaoNhanVien
    @HoTen NVARCHAR(100), @Email VARCHAR(100), @SoDienThoai VARCHAR(15), @MatKhau VARCHAR(255),
    @VaiTro VARCHAR(20), @MaChiNhanh INT, @MaNguoiTao INT
AS
BEGIN
    DECLARE @VaiTroNguoiTao VARCHAR(20), @ChiNhanhNguoiTao INT;
    SELECT @VaiTroNguoiTao = VaiTro, @ChiNhanhNguoiTao = MaChiNhanh FROM NguoiDung WHERE MaNguoiDung = @MaNguoiTao;

    IF @VaiTro NOT IN ('BRANCH_ADMIN','STAFF','SHIPPER')
        THROW 50001, N'Vai trò nhân viên không hợp lệ.', 1;
    IF @VaiTroNguoiTao = 'BRANCH_ADMIN' AND (@VaiTro = 'BRANCH_ADMIN' OR @MaChiNhanh <> @ChiNhanhNguoiTao)
        THROW 50002, N'Quản lý chi nhánh chỉ được tạo STAFF hoặc SHIPPER cho chi nhánh mình.', 1;
    IF ISNULL(@VaiTroNguoiTao, '') NOT IN ('SUPER_ADMIN','BRANCH_ADMIN')
        THROW 50003, N'Bạn không có quyền tạo tài khoản nhân viên.', 1;

    INSERT INTO NguoiDung (HoTen, Email, SoDienThoai, MatKhau, VaiTro, MaChiNhanh)
    VALUES (@HoTen, @Email, @SoDienThoai, @MatKhau, @VaiTro, @MaChiNhanh);
END;
GO

-- Nhan vien quen mat khau: cap tren cap lai mat khau tam
CREATE PROCEDURE sp_CapLaiMatKhau
    @MaNhanVien INT, @MatKhauMoi VARCHAR(255), @MaNguoiCap INT
AS
BEGIN
    DECLARE @VaiTroCap VARCHAR(20), @CNCap INT, @VaiTroNV VARCHAR(20), @CNNV INT;
    SELECT @VaiTroCap = VaiTro, @CNCap = MaChiNhanh FROM NguoiDung WHERE MaNguoiDung = @MaNguoiCap;
    SELECT @VaiTroNV = VaiTro, @CNNV = MaChiNhanh FROM NguoiDung WHERE MaNguoiDung = @MaNhanVien;

    IF @VaiTroNV IS NULL OR @VaiTroNV IN ('CUSTOMER','SUPER_ADMIN') OR @MaNhanVien = @MaNguoiCap
        THROW 50004, N'Không thể cấp lại mật khẩu cho tài khoản này.', 1;
    IF @VaiTroCap = 'BRANCH_ADMIN' AND (@VaiTroNV = 'BRANCH_ADMIN' OR @CNNV <> @CNCap)
        THROW 50005, N'Quản lý chỉ cấp lại mật khẩu cho nhân viên chi nhánh mình.', 1;
    IF ISNULL(@VaiTroCap, '') NOT IN ('SUPER_ADMIN','BRANCH_ADMIN')
        THROW 50006, N'Bạn không có quyền cấp lại mật khẩu.', 1;

    UPDATE NguoiDung SET MatKhau = @MatKhauMoi WHERE MaNguoiDung = @MaNhanVien;
END;
GO

/* ===================== DAT DON ===================== */

-- Tinh tien don hang: dung chung cho bao gia va tao don
CREATE PROCEDURE sp_TinhTien
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

-- Bao gia truoc khi dat (khong luu gi vao CSDL)
CREATE PROCEDURE sp_BaoGia
    @MaChiNhanh INT, @HinhThucNhan VARCHAR(20), @KhoangCach DECIMAL(6,2) = NULL,
    @PhiShipApi DECIMAL(12,0) = NULL, @InGap BIT = 0,
    @MaCode VARCHAR(30) = NULL, @DsFile Kieu_DongDonHang READONLY, @DsGiaCong Kieu_DongGiaCong READONLY
AS
BEGIN
    DECLARE @TienIn DECIMAL(12,0), @TienGiaCong DECIMAL(12,0), @PhuPhiGap DECIMAL(12,0), @PhiShip DECIMAL(12,0),
            @GiamGia DECIMAL(12,0), @MaKM INT, @LyDoKM NVARCHAR(200), @TongSoTrang INT;

    EXEC sp_TinhTien @MaChiNhanh, @HinhThucNhan, @KhoangCach, @PhiShipApi, @InGap, @MaCode, @DsFile, @DsGiaCong,
         @TienIn OUTPUT, @TienGiaCong OUTPUT, @PhuPhiGap OUTPUT, @PhiShip OUTPUT,
         @GiamGia OUTPUT, @MaKM OUTPUT, @LyDoKM OUTPUT, @TongSoTrang OUTPUT;

    SELECT @TienIn AS TienIn, @TienGiaCong AS TienGiaCong, @PhuPhiGap AS PhuPhiGap, @PhiShip AS PhiShip,
           @GiamGia AS GiamGia, @TienIn + @TienGiaCong + @PhuPhiGap + @PhiShip - @GiamGia AS TongTien,
           @TongSoTrang AS TongSoTrang,
           CASE WHEN @MaCode IS NULL THEN NULL WHEN @MaKM IS NULL THEN 0 ELSE 1 END AS KhuyenMaiHopLe,
           @LyDoKM AS LyDoKhuyenMai,
           dbo.fn_UocTinhGioXong(@MaChiNhanh, @TongSoTrang, @InGap) AS NgayHenTra;
END;
GO

-- Tao don hang + chi tiet + phieu thanh toan
CREATE PROCEDURE sp_TaoDonHang
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

-- Ngan hang bao da nhan tien chuyen khoan VietQR
CREATE PROCEDURE sp_XacNhanChuyenKhoan
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

-- Chay dinh ky: huy don chuyen khoan qua 15 phut chua thanh toan
CREATE PROCEDURE sp_HuyDonQuaHan
AS
BEGIN
    UPDATE dh SET TrangThai = 'DA_HUY', GhiChu = N'Quá 15 phút chưa thanh toán'
    FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
    WHERE dh.TrangThai = 'CHO_XAC_NHAN' AND tt.PhuongThuc = 'VIETQR'
      AND tt.TrangThai = 'CHUA_THANH_TOAN' AND DATEADD(MINUTE, 15, dh.NgayDat) < GETDATE();
END;
GO

/* ===================== XU LY DON TAI CHI NHANH ===================== */

CREATE PROCEDURE sp_TiepNhanDon
    @MaDonHang INT, @MaNhanVien INT
AS
BEGIN
    DECLARE @MaChiNhanhDon INT, @MaChiNhanhNhanVien INT, @VaiTro VARCHAR(20);

    SELECT @MaChiNhanhDon = MaChiNhanh
    FROM DonHang
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

    UPDATE DonHang
    SET TrangThai = 'DA_XAC_NHAN', MaNhanVien = @MaNhanVien
    WHERE MaDonHang = @MaDonHang;

    INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    SELECT MaKhachHang, MaDonHang, N'Đơn hàng đã được tiếp nhận',
           N'Chi nhánh đã nhận đơn và xếp vào hàng đợi in.'
    FROM DonHang WHERE MaDonHang = @MaDonHang;
END;
GO

-- Gan may in: may phai cung chi nhanh, dang hoat dong, du kho giay / in mau / in 2 mat
CREATE PROCEDURE sp_PhanCongMayIn
    @MaDonHang INT, @MaMayIn INT
AS
BEGIN
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
CREATE PROCEDURE sp_CapNhatTrangThaiIn
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

-- Khach nhan tai quay: don VietQR phai tra truoc; don COD thu tien luc ban giao.
CREATE PROCEDURE sp_GiaoTaiQuay
    @MaDonHang INT, @MaNhanVien INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MaChiNhanhDon INT, @MaChiNhanhNhanVien INT, @VaiTro VARCHAR(20),
            @PhuongThuc VARCHAR(10), @TrangThaiThanhToan VARCHAR(20),
            @SoTien DECIMAL(12,0), @MaCa INT;

    SELECT @MaChiNhanhDon = MaChiNhanh
    FROM DonHang
    WHERE MaDonHang = @MaDonHang AND TrangThai = 'DA_IN' AND HinhThucNhan = 'TAI_QUAY';

    IF @MaChiNhanhDon IS NULL
        THROW 50026, N'Chỉ giao được đơn đã in xong và nhận tại quầy.', 1;

    SELECT @MaChiNhanhNhanVien = MaChiNhanh, @VaiTro = VaiTro
    FROM NguoiDung
    WHERE MaNguoiDung = @MaNhanVien AND TrangThai = 'HOAT_DONG';

    IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN')
       OR (@VaiTro <> 'SUPER_ADMIN' AND @MaChiNhanhNhanVien <> @MaChiNhanhDon)
        THROW 50021, N'Bạn không có quyền bàn giao đơn của chi nhánh này.', 1;

    SELECT @PhuongThuc = PhuongThuc, @TrangThaiThanhToan = TrangThai, @SoTien = SoTien
    FROM ThanhToan
    WHERE MaDonHang = @MaDonHang;

    IF @PhuongThuc = 'VIETQR' AND @TrangThaiThanhToan <> 'DA_THANH_TOAN'
        THROW 50027, N'Đơn hàng chưa thanh toán VietQR.', 1;

    SET XACT_ABORT ON;
    BEGIN TRAN;
        IF @PhuongThuc = 'COD'
        BEGIN
            SELECT @MaCa = MaCa
            FROM CaLamViec
            WHERE MaNhanVien = @MaNhanVien AND MaChiNhanh = @MaChiNhanhDon
              AND TrangThai = 'DANG_MO';

            IF @MaCa IS NULL
                THROW 50028, N'Bạn chưa mở ca tại chi nhánh này.', 1;

            UPDATE ThanhToan
            SET TrangThai = 'DA_THANH_TOAN', MaCa = @MaCa, NgayThanhToan = GETDATE()
            WHERE MaDonHang = @MaDonHang AND TrangThai = 'CHUA_THANH_TOAN';

            IF @@ROWCOUNT = 0 AND @TrangThaiThanhToan <> 'DA_THANH_TOAN'
                THROW 50029, N'Không thể ghi nhận thanh toán COD.', 1;

            UPDATE CaLamViec
            SET DoanhThuTrongCa = DoanhThuTrongCa + @SoTien
            WHERE MaCa = @MaCa;
        END

        UPDATE DonHang
        SET TrangThai = 'HOAN_THANH', NgayHoanThanh = GETDATE()
        WHERE MaDonHang = @MaDonHang;
    COMMIT;
END;
GO

/* ===================== GIAO HANG ===================== */

-- Shipper cua chi nhanh nhan don di giao (toi da 3 lan giao)
CREATE PROCEDURE sp_NhanDonGiao
    @MaDonHang INT, @MaShipper INT
AS
BEGIN
    DECLARE @SoLan INT = (SELECT COUNT(*) FROM GiaoHang WHERE MaDonHang = @MaDonHang);

    IF NOT EXISTS (SELECT 1 FROM DonHang WHERE MaDonHang = @MaDonHang AND HinhThucNhan = 'GIAO_HANG'
                   AND TrangThai IN ('DA_IN','GIAO_THAT_BAI'))
        THROW 50030, N'Đơn chưa sẵn sàng để giao.', 1;
    IF NOT EXISTS (SELECT 1 FROM NguoiDung sp JOIN DonHang dh ON dh.MaChiNhanh = sp.MaChiNhanh
                   WHERE sp.MaNguoiDung = @MaShipper AND sp.VaiTro = 'SHIPPER' AND dh.MaDonHang = @MaDonHang)
        THROW 50031, N'Chỉ shipper của chi nhánh giữ đơn mới được nhận giao.', 1;
    IF @SoLan >= 3
        THROW 50032, N'Đơn đã giao thất bại 3 lần, hãy chuyển sang nhận tại quầy hoặc hủy đơn.', 1;

    INSERT INTO GiaoHang (MaDonHang, MaShipper, LanGiao) VALUES (@MaDonHang, @MaShipper, @SoLan + 1);
    UPDATE DonHang SET TrangThai = 'DANG_GIAO' WHERE MaDonHang = @MaDonHang;

    INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    SELECT MaKhachHang, MaDonHang, N'Đơn hàng đang được giao', N'Shipper đang giao tài liệu đến bạn.'
    FROM DonHang WHERE MaDonHang = @MaDonHang;
END;
GO

-- Shipper bao ket qua giao. Don tien mat thi shipper thu tien (COD), sau do nop ve chi nhanh.
CREATE PROCEDURE sp_CapNhatGiaoHang
    @MaGiaoHang INT, @ThanhCong BIT, @LyDo NVARCHAR(255) = NULL
AS
BEGIN
    SET XACT_ABORT ON;
    DECLARE @MaDonHang INT, @ChuaTra DECIMAL(12,0);
    SELECT @MaDonHang = MaDonHang FROM GiaoHang WHERE MaGiaoHang = @MaGiaoHang AND TrangThai = 'DANG_GIAO';
    IF @MaDonHang IS NULL
        THROW 50033, N'Không tìm thấy lượt giao đang thực hiện.', 1;
    SELECT @ChuaTra = CASE WHEN TrangThai = 'CHUA_THANH_TOAN' THEN SoTien ELSE 0 END
    FROM ThanhToan WHERE MaDonHang = @MaDonHang;

    BEGIN TRAN;
    IF @ThanhCong = 1
    BEGIN
        UPDATE GiaoHang SET TrangThai = 'THANH_CONG', TienThu = @ChuaTra, NgayGiao = GETDATE() WHERE MaGiaoHang = @MaGiaoHang;
        -- da tra truoc thi hoan thanh luon, con COD thi cho shipper nop tien
        UPDATE DonHang SET TrangThai = CASE WHEN @ChuaTra = 0 THEN 'HOAN_THANH' ELSE 'DA_GIAO' END
        WHERE MaDonHang = @MaDonHang;
    END
    ELSE
    BEGIN
        UPDATE GiaoHang SET TrangThai = 'THAT_BAI', LyDo = @LyDo, NgayGiao = GETDATE() WHERE MaGiaoHang = @MaGiaoHang;
        UPDATE DonHang SET TrangThai = 'GIAO_THAT_BAI' WHERE MaDonHang = @MaDonHang;
    END

    INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    SELECT MaKhachHang, MaDonHang,
           CASE WHEN @ThanhCong = 1 THEN N'Giao hàng thành công' ELSE N'Giao hàng thất bại' END,
           CASE WHEN @ThanhCong = 1 THEN N'Tài liệu đã được giao đến bạn.' ELSE ISNULL(@LyDo, N'Shipper không giao được hàng.') END
    FROM DonHang WHERE MaDonHang = @MaDonHang;
    COMMIT;
END;
GO

-- Quan ly nhan tien COD shipper nop ve, tien vao ca cua quan ly
CREATE PROCEDURE sp_NhanTienCOD
    @MaDonHang INT, @MaQuanLy INT
AS
BEGIN
    SET XACT_ABORT ON;
    DECLARE @MaCa INT = dbo.fn_CaDangMo(@MaQuanLy), @MaChiNhanh INT;
    SELECT @MaChiNhanh = MaChiNhanh FROM DonHang WHERE MaDonHang = @MaDonHang AND TrangThai = 'DA_GIAO';

    IF @MaChiNhanh IS NULL
        THROW 50034, N'Đơn không ở trạng thái chờ nộp tiền COD.', 1;
    IF dbo.fn_LaQuanLy(@MaQuanLy, @MaChiNhanh) = 0
        THROW 50035, N'Chỉ quản lý chi nhánh mới được nhận tiền COD.', 1;
    IF @MaCa IS NULL
        THROW 50036, N'Quản lý chưa mở ca.', 1;

    BEGIN TRAN;
        UPDATE ca SET DoanhThuTrongCa = DoanhThuTrongCa + tt.SoTien
        FROM CaLamViec ca JOIN ThanhToan tt ON tt.MaDonHang = @MaDonHang
        WHERE ca.MaCa = @MaCa;

        UPDATE ThanhToan SET TrangThai = 'DA_THANH_TOAN', MaCa = @MaCa, NgayThanhToan = GETDATE()
        WHERE MaDonHang = @MaDonHang;
        UPDATE GiaoHang SET DaNopTien = 1 WHERE MaDonHang = @MaDonHang AND TrangThai = 'THANH_CONG';
        UPDATE DonHang SET TrangThai = 'HOAN_THANH' WHERE MaDonHang = @MaDonHang;
    COMMIT;
END;
GO

-- Giao that bai -> khach tu toi quay nhan: bo phi ship
CREATE PROCEDURE sp_ChuyenNhanTaiQuay
    @MaDonHang INT, @MaQuanLy INT
AS
BEGIN
    DECLARE @MaChiNhanh INT;
    SELECT @MaChiNhanh = MaChiNhanh FROM DonHang WHERE MaDonHang = @MaDonHang AND TrangThai = 'GIAO_THAT_BAI';
    IF @MaChiNhanh IS NULL
        THROW 50037, N'Chỉ chuyển được đơn đang giao thất bại.', 1;
    IF dbo.fn_LaQuanLy(@MaQuanLy, @MaChiNhanh) = 0
        THROW 50038, N'Chỉ quản lý chi nhánh mới được đổi hình thức nhận.', 1;

    UPDATE DonHang
    SET HinhThucNhan = 'TAI_QUAY', TrangThai = 'DA_IN', TongTien = TongTien - PhiShip, PhiShip = 0,
        GhiChu = N'Giao thất bại, khách tới quầy nhận'
    WHERE MaDonHang = @MaDonHang;

    -- chua thanh toan thi giam so tien phai thu
    UPDATE ThanhToan SET SoTien = (SELECT TongTien FROM DonHang WHERE MaDonHang = @MaDonHang)
    WHERE MaDonHang = @MaDonHang AND TrangThai = 'CHUA_THANH_TOAN';
END;
GO

/* ===================== HUY DON, DANH GIA ===================== */

CREATE PROCEDURE sp_HuyDonHang
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
CREATE PROCEDURE sp_HoanTien
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

CREATE PROCEDURE sp_DanhGia
    @MaDonHang INT, @MaKhachHang INT, @SoSao INT, @NhanXet NVARCHAR(500) = NULL
AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM DonHang WHERE MaDonHang = @MaDonHang AND MaKhachHang = @MaKhachHang AND TrangThai = 'HOAN_THANH')
        THROW 50044, N'Chỉ đánh giá được đơn của mình đã hoàn thành.', 1;
    IF EXISTS (SELECT 1 FROM DanhGia WHERE MaDonHang = @MaDonHang)
        THROW 50045, N'Đơn hàng này đã được đánh giá.', 1;

    INSERT INTO DanhGia (MaDonHang, SoSao, NhanXet) VALUES (@MaDonHang, @SoSao, @NhanXet);
END;
GO

/* ===================== CA LAM VIEC ===================== */

CREATE PROCEDURE sp_MoCa
    @MaNhanVien INT, @TienDauCa DECIMAL(12,0) = 200000, @MaCa INT OUTPUT,
    @MaChiNhanhThayThe INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @MaCa = NULL;

    DECLARE @MaChiNhanh INT, @VaiTro VARCHAR(20);
    SELECT @MaChiNhanh = MaChiNhanh, @VaiTro = VaiTro
    FROM NguoiDung
    WHERE MaNguoiDung = @MaNhanVien AND TrangThai = 'HOAT_DONG';

    IF @VaiTro = 'SUPER_ADMIN'
        SET @MaChiNhanh = @MaChiNhanhThayThe;

    IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN') OR @MaChiNhanh IS NULL
        THROW 50050, N'Bạn không có quyền mở ca tại chi nhánh này.', 1;
    IF NOT EXISTS (SELECT 1 FROM ChiNhanh WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = 'HOAT_DONG')
        THROW 50051, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
    IF @TienDauCa IS NULL OR @TienDauCa < 0
        THROW 50052, N'Tiền đầu ca không hợp lệ.', 1;

    BEGIN TRAN;
        -- Khóa theo nhân viên, không khóa ca của người khác cùng chi nhánh.
        SELECT @MaCa = MaCa
        FROM CaLamViec WITH (UPDLOCK, HOLDLOCK)
        WHERE MaNhanVien = @MaNhanVien AND TrangThai = 'DANG_MO';

        IF @MaCa IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM CaLamViec WHERE MaCa = @MaCa AND MaChiNhanh = @MaChiNhanh)
            BEGIN
                ROLLBACK TRAN;
                THROW 50059, N'Bạn đang có ca mở tại một chi nhánh khác.', 1;
            END
            COMMIT TRAN;
            RETURN; -- Đăng nhập lại dùng ca cũ, không đổi tiền đầu ca.
        END

        -- Ca đã đóng chờ xác nhận vẫn giữ nguyên để quản lý đối chiếu sau.
        INSERT INTO CaLamViec (MaChiNhanh, MaNhanVien, TienDauCa)
        VALUES (@MaChiNhanh, @MaNhanVien, @TienDauCa);
        SET @MaCa = SCOPE_IDENTITY();
    COMMIT TRAN;
END;
GO

-- STAFF dong ca thi cho quan ly xac nhan. BRANCH_ADMIN dong ca cua minh thi hoan tat ngay.
CREATE PROCEDURE sp_DongCa
    @MaCa INT, @TongDoanhThuCuoiCa DECIMAL(12,0)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @VaiTro VARCHAR(20);

    SELECT @VaiTro = nv.VaiTro
    FROM CaLamViec ca
    JOIN NguoiDung nv ON nv.MaNguoiDung = ca.MaNhanVien
    WHERE ca.MaCa = @MaCa AND ca.TrangThai = 'DANG_MO';

    IF @VaiTro IS NULL
        THROW 50053, N'Ca không tồn tại hoặc đã đóng.', 1;

    UPDATE CaLamViec
    SET TongDoanhThuCuoiCa = @TongDoanhThuCuoiCa,
        TienQuanLyDemCuoiCa = CASE WHEN @VaiTro IN ('BRANCH_ADMIN','SUPER_ADMIN')
                                  THEN @TongDoanhThuCuoiCa ELSE NULL END,
        GioDongCa = GETDATE(),
        TrangThai = CASE WHEN @VaiTro IN ('BRANCH_ADMIN','SUPER_ADMIN')
                         THEN 'DA_XAC_NHAN' ELSE 'CHO_XAC_NHAN' END
    WHERE MaCa = @MaCa AND TrangThai = 'DANG_MO';

    IF @@ROWCOUNT = 0
        THROW 50053, N'Ca không tồn tại hoặc đã đóng.', 1;
END;
GO

/* Quan ly xac nhan dong ca (khong duoc tu xac nhan ca cua minh).
   Ket thieu: tru khoan da chi cho tiem, phan con lai nhan vien phai bu du ngay.
   Ket thua: phan thua nop ve tiem. */
CREATE PROCEDURE sp_XacNhanCa
    @MaCa INT, @MaQuanLy INT, @TienQuanLyDemCuoiCa DECIMAL(12,0) = NULL,
    @TienChi DECIMAL(12,0) = 0, @TienBu DECIMAL(12,0) = 0, @GhiChu NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MaNhanVien INT, @MaChiNhanh INT, @Thieu DECIMAL(12,0), @TienNhanVienDem DECIMAL(12,0);
    SELECT @MaNhanVien = MaNhanVien, @MaChiNhanh = MaChiNhanh,
           @TienNhanVienDem = TongDoanhThuCuoiCa,
           @Thieu = (TienDauCa + DoanhThuTrongCa) - @TienQuanLyDemCuoiCa
    FROM CaLamViec WHERE MaCa = @MaCa AND TrangThai = 'CHO_XAC_NHAN';

    IF @MaNhanVien IS NULL
        THROW 50054, N'Ca không ở trạng thái chờ xác nhận.', 1;
    IF @MaNhanVien = @MaQuanLy OR dbo.fn_LaQuanLy(@MaQuanLy, @MaChiNhanh) = 0
        THROW 50055, N'Phải do quản lý khác (không phải người trực ca) xác nhận.', 1;
    -- Cho phep cac kich ban demo cu khong truyen tham so moi; API that luon bat buoc quan ly kiem dem.
    IF @TienQuanLyDemCuoiCa IS NULL SET @TienQuanLyDemCuoiCa = @TienNhanVienDem;
    IF @TienQuanLyDemCuoiCa IS NULL OR @TienQuanLyDemCuoiCa < 0
        THROW 50059, N'Số tiền quản lý kiểm đếm không hợp lệ.', 1;
    SELECT @Thieu = (TienDauCa + DoanhThuTrongCa) - @TienQuanLyDemCuoiCa
    FROM CaLamViec WHERE MaCa = @MaCa;
    IF @Thieu < 0 SET @Thieu = 0;
    IF @TienChi < 0 OR @TienChi > @Thieu
        THROW 50056, N'Số tiền chi cho tiệm không hợp lệ.', 1;
    IF @TienChi > 0 AND ISNULL(@GhiChu, N'') = N''
        THROW 50057, N'Có khoản chi thì phải ghi chú chi vào việc gì.', 1;
    IF @TienBu <> @Thieu - @TienChi
        THROW 50058, N'Nhân viên phải bù đủ số tiền còn thiếu.', 1;

    UPDATE CaLamViec
    SET TienQuanLyDemCuoiCa = @TienQuanLyDemCuoiCa,
        TienChi = @TienChi, TienBu = @TienBu, GhiChu = @GhiChu,
        MaNguoiXacNhan = @MaQuanLy, TrangThai = 'DA_XAC_NHAN'
    WHERE MaCa = @MaCa;
END;
GO

/* ===================== NHAP KHO ===================== */

CREATE PROCEDURE sp_TaoPhieuNhap
    @MaChiNhanh INT, @MaVatTu INT, @NhaCungCap NVARCHAR(150), @SoLuong INT, @DonGia DECIMAL(12,0), @MaNguoiLap INT
AS
BEGIN
    SET NOCOUNT ON;
    IF dbo.fn_LaQuanLy(@MaNguoiLap, @MaChiNhanh) = 0
        THROW 50060, N'Chỉ quản lý chi nhánh hoặc Super Admin mới được lập phiếu nhập.', 1;
    IF @SoLuong IS NULL OR @SoLuong <= 0 OR @DonGia IS NULL OR @DonGia < 0
        THROW 50064, N'Số lượng và đơn giá nhập không hợp lệ.', 1;
    IF NOT EXISTS (SELECT 1 FROM ChiNhanh WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = 'HOAT_DONG')
        THROW 50065, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
    IF NOT EXISTS (SELECT 1 FROM VatTu WHERE MaVatTu = @MaVatTu)
        THROW 50066, N'Vật tư không tồn tại.', 1;

    DECLARE @LaCEO BIT = dbo.fn_LaSuperAdmin(@MaNguoiLap);
    INSERT INTO PhieuNhap (MaChiNhanh, MaVatTu, NhaCungCap, SoLuong, DonGia, MaNguoiLap, MaNguoiDuyet, TrangThai)
    VALUES (@MaChiNhanh, @MaVatTu, @NhaCungCap, @SoLuong, @DonGia, @MaNguoiLap,
            CASE WHEN @LaCEO = 1 THEN @MaNguoiLap ELSE NULL END,
            CASE WHEN @LaCEO = 1 THEN 'DA_DUYET' ELSE 'CHO_DUYET' END);
    -- Duyệt không tăng tồn kho: vẫn phải xác nhận nhận hàng bằng sp_NhapKho.
END;
GO

CREATE PROCEDURE sp_DuyetPhieuNhap
    @MaPhieuNhap INT, @MaNguoiDuyet INT, @DongY BIT
AS
BEGIN
    IF dbo.fn_LaSuperAdmin(@MaNguoiDuyet) = 0
        THROW 50061, N'Chỉ Super Admin mới được duyệt phiếu nhập.', 1;

    UPDATE PhieuNhap SET TrangThai = CASE WHEN @DongY = 1 THEN 'DA_DUYET' ELSE 'TU_CHOI' END, MaNguoiDuyet = @MaNguoiDuyet
    WHERE MaPhieuNhap = @MaPhieuNhap AND TrangThai = 'CHO_DUYET';

    IF @@ROWCOUNT = 0
        THROW 50062, N'Phiếu nhập không ở trạng thái chờ duyệt.', 1;
END;
GO

-- Nhan hang: cong vao ton kho
CREATE PROCEDURE sp_NhapKho
    @MaPhieuNhap INT, @SoLuongThucNhan INT
AS
BEGIN
    SET XACT_ABORT ON;
    DECLARE @MaChiNhanh INT, @MaVatTu INT, @DonGia DECIMAL(12,0);
    SELECT @MaChiNhanh = MaChiNhanh, @MaVatTu = MaVatTu, @DonGia = DonGia
    FROM PhieuNhap WHERE MaPhieuNhap = @MaPhieuNhap AND TrangThai = 'DA_DUYET';

    IF @MaChiNhanh IS NULL
        THROW 50063, N'Phiếu nhập chưa được duyệt.', 1;

    BEGIN TRAN;
        UPDATE PhieuNhap SET SoLuong = @SoLuongThucNhan, TrangThai = 'DA_NHAP' WHERE MaPhieuNhap = @MaPhieuNhap;

        IF EXISTS (SELECT 1 FROM TonKho WHERE MaChiNhanh = @MaChiNhanh AND MaVatTu = @MaVatTu)
            UPDATE TonKho SET SoLuong = SoLuong + @SoLuongThucNhan WHERE MaChiNhanh = @MaChiNhanh AND MaVatTu = @MaVatTu;
        ELSE
            INSERT INTO TonKho (MaChiNhanh, MaVatTu, SoLuong) VALUES (@MaChiNhanh, @MaVatTu, @SoLuongThucNhan);

        UPDATE VatTu SET DonGia = @DonGia WHERE MaVatTu = @MaVatTu;
    COMMIT;
END;
GO

/* ===================== MAY IN ===================== */

-- Cap nhat trang thai may; may bao tri / hong thi cac don chua in tra ve hang doi
CREATE PROCEDURE sp_CapNhatMayIn
    @MaMayIn INT, @TrangThai NVARCHAR(20), @SoTrangDaIn INT = NULL, @MaQuanLy INT
AS
BEGIN
    IF dbo.fn_LaQuanLy(@MaQuanLy, (SELECT MaChiNhanh FROM MayIn WHERE MaMayIn = @MaMayIn)) = 0
        THROW 50070, N'Chỉ quản lý chi nhánh mới được cập nhật máy in.', 1;

    UPDATE MayIn SET TrangThai = @TrangThai, SoTrangDaIn = ISNULL(@SoTrangDaIn, SoTrangDaIn)
    WHERE MaMayIn = @MaMayIn;

    IF @TrangThai <> N'HOAT_DONG'
        UPDATE DonHang SET MaMayIn = NULL WHERE MaMayIn = @MaMayIn AND TrangThai IN ('CHO_XAC_NHAN','DA_XAC_NHAN');
END;
GO

PRINT N'Da tao stored procedure';
GO
