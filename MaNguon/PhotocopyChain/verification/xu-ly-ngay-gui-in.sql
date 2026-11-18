-- Chạy bằng sqlcmd -d tempdb: chỉ bảng/thủ tục tạm, không gửi lệnh connector.
:ON ERROR EXIT
SET ANSI_NULLS ON;SET QUOTED_IDENTIFIER ON;SET XACT_ABORT ON;
CREATE TABLE #DonHang(MaDonHang INT PRIMARY KEY,MaNhanVien INT NULL,MaChiNhanh INT,MaMayIn INT NULL,KenhDatDon VARCHAR(20),TrangThai VARCHAR(20),BangChungHuyJson NVARCHAR(MAX),MaKhachHang INT);
CREATE TABLE #ThanhToan(MaDonHang INT PRIMARY KEY,TrangThai VARCHAR(30),NgayThanhToan DATETIME NULL);
CREATE TABLE #ChiTietDonHang(MaChiTiet INT,MaDonHang INT,MaDichVu INT,SoTrang INT,SoBan INT);
CREATE TABLE #DichVu(MaDichVu INT,KhoGiay VARCHAR(5),MauSac VARCHAR(20),MatIn VARCHAR(10),MaVatTu INT);
CREATE TABLE #MayIn(MaMayIn INT,MaChiNhanh INT,TrangThai NVARCHAR(20),KhoGiay VARCHAR(5),InMau BIT,In2Mat BIT);
CREATE TABLE #TonKho(MaChiNhanh INT,MaVatTu INT,SoLuong INT);
CREATE TABLE #ChiTietGiaCong(MaChiTiet INT,MaDichVu INT);
CREATE TABLE #ThongBao(MaNguoiDung INT,MaDonHang INT,TieuDe NVARCHAR(200),NoiDung NVARCHAR(MAX));
INSERT #MayIn VALUES(100,1,N'HOAT_DONG','A4',0,1);
INSERT #DichVu VALUES(1,'A4','DEN_TRANG','1_MAT',1);
INSERT #TonKho VALUES(1,1,100);
INSERT #DonHang VALUES(1,20,1,NULL,'APP','DA_XAC_NHAN',N'{"BoQuaChoHaiPhut":true}',10),
 (2,20,1,NULL,'APP','DA_XAC_NHAN',NULL,10),
 (3,20,1,NULL,'APP','DA_XAC_NHAN',N'{"BoQuaChoHaiPhut":false}',10),
 (4,NULL,1,NULL,'APP','CHO_XAC_NHAN',N'{"BoQuaChoHaiPhut":true}',10),
 (5,20,1,NULL,'APP','DA_XAC_NHAN',N'{"BoQuaChoHaiPhut":true}',10),
 (6,20,1,NULL,'APP','DA_XAC_NHAN',NULL,10),
 (7,20,1,NULL,'APP','CHO_XAC_NHAN',N'{"BoQuaChoHaiPhut":true}',10);
INSERT #ThanhToan SELECT MaDonHang,CASE WHEN MaDonHang=5 THEN 'CHUA_THANH_TOAN' ELSE 'DA_THANH_TOAN' END,CASE WHEN MaDonHang=6 THEN DATEADD(SECOND,-121,GETDATE()) ELSE GETDATE() END FROM #DonHang;
INSERT #ChiTietDonHang SELECT MaDonHang,MaDonHang,1,2,1 FROM #DonHang;
GO

-- Gan may in: may phai cung chi nhanh, dang hoat dong, du kho giay / in mau / in 2 mat
CREATE PROCEDURE #sp_PhanCongMayIn
    @MaDonHang INT, @MaMayIn INT
AS
BEGIN

    IF EXISTS(SELECT 1 FROM #DonHang dh JOIN #ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
      WHERE dh.MaDonHang=@MaDonHang AND dh.KenhDatDon='APP' AND
      (dh.MaNhanVien IS NULL OR tt.TrangThai<>'DA_THANH_TOAN' OR tt.NgayThanhToan IS NULL OR (GETDATE()<DATEADD(SECOND,120,tt.NgayThanhToan) AND ISNULL(JSON_VALUE(dh.BangChungHuyJson,'$.BoQuaChoHaiPhut'),'false')<>'true')))
      THROW 50223,N'Đơn app phải đã thanh toán, được nhân viên tiếp nhận và hết thời gian chờ hoặc khách đã xác nhận xử lý ngay.',1;
    DECLARE @CanA3 INT, @CanMau INT, @Can2Mat INT;
    SELECT @CanA3   = MAX(CASE WHEN dv.KhoGiay = 'A3' THEN 1 ELSE 0 END),
           @CanMau  = MAX(CASE WHEN dv.MauSac = 'MAU' THEN 1 ELSE 0 END),
           @Can2Mat = MAX(CASE WHEN dv.MatIn = '2_MAT' THEN 1 ELSE 0 END)
    FROM #ChiTietDonHang ct JOIN #DichVu dv ON dv.MaDichVu = ct.MaDichVu
    WHERE ct.MaDonHang = @MaDonHang;

    IF NOT EXISTS (SELECT 1 FROM #MayIn m JOIN #DonHang dh ON dh.MaChiNhanh = m.MaChiNhanh
                   WHERE m.MaMayIn = @MaMayIn AND dh.MaDonHang = @MaDonHang AND m.TrangThai = N'HOAT_DONG')
        THROW 50022, N'Máy in không thuộc chi nhánh hoặc đang không hoạt động.', 1;
    IF EXISTS (SELECT 1 FROM #MayIn WHERE MaMayIn = @MaMayIn
               AND ((@CanA3 = 1 AND KhoGiay <> 'A3') OR (@CanMau = 1 AND InMau = 0) OR (@Can2Mat = 1 AND In2Mat = 0)))
        THROW 50023, N'Máy in không đáp ứng được khổ giấy / in màu / in 2 mặt của đơn.', 1;

    UPDATE #DonHang SET MaMayIn = @MaMayIn WHERE MaDonHang = @MaDonHang;
END;


GO

-- DA_XAC_NHAN -> DANG_IN (tru kho vat tu) -> DA_IN.
-- File goc duoc giu lai den khi don HOAN_THANH / DA_HUY; job nen cua API se xoa file vat ly.
CREATE PROCEDURE #sp_CapNhatTrangThaiIn
    @MaDonHang INT, @TrangThaiMoi VARCHAR(20)
AS
BEGIN

    IF EXISTS(SELECT 1 FROM #DonHang dh JOIN #ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
      WHERE dh.MaDonHang=@MaDonHang AND dh.KenhDatDon='APP' AND
      (dh.MaNhanVien IS NULL OR tt.TrangThai<>'DA_THANH_TOAN' OR tt.NgayThanhToan IS NULL OR (GETDATE()<DATEADD(SECOND,120,tt.NgayThanhToan) AND ISNULL(JSON_VALUE(dh.BangChungHuyJson,'$.BoQuaChoHaiPhut'),'false')<>'true')))
      THROW 50223,N'Đơn app phải đã thanh toán, được nhân viên tiếp nhận và hết thời gian chờ hoặc khách đã xác nhận xử lý ngay.',1;
    SET XACT_ABORT ON;
    DECLARE @TrangThai VARCHAR(20), @MaChiNhanh INT;
    SELECT @TrangThai = TrangThai, @MaChiNhanh = MaChiNhanh FROM #DonHang WHERE MaDonHang = @MaDonHang;

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
            FROM #ChiTietDonHang ct JOIN #DichVu dv ON dv.MaDichVu = ct.MaDichVu
            WHERE ct.MaDonHang = @MaDonHang AND dv.MaVatTu IS NOT NULL
            UNION ALL
            SELECT gc.MaVatTu, ct.SoBan
            FROM #ChiTietDonHang ct
            JOIN #ChiTietGiaCong ctgc ON ctgc.MaChiTiet = ct.MaChiTiet
            JOIN #DichVu gc ON gc.MaDichVu = ctgc.MaDichVu
            WHERE ct.MaDonHang = @MaDonHang AND gc.MaVatTu IS NOT NULL
        ) x GROUP BY MaVatTu;

        IF EXISTS (SELECT 1 FROM @Can c LEFT JOIN #TonKho tk ON tk.MaVatTu = c.MaVatTu AND tk.MaChiNhanh = @MaChiNhanh
                   WHERE ISNULL(tk.SoLuong, 0) < c.SoLuong)
            THROW 50025, N'Không đủ vật tư trong kho để in đơn này.', 1;

        UPDATE tk SET SoLuong = tk.SoLuong - c.SoLuong
        FROM #TonKho tk JOIN @Can c ON c.MaVatTu = tk.MaVatTu
        WHERE tk.MaChiNhanh = @MaChiNhanh;
    END

    UPDATE #DonHang SET TrangThai = @TrangThaiMoi WHERE MaDonHang = @MaDonHang;

    INSERT INTO #ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    SELECT MaKhachHang, MaDonHang,
           CASE WHEN @TrangThaiMoi = 'DANG_IN' THEN N'Đơn hàng đang được in' ELSE N'Đơn hàng đã in xong' END,
           CASE WHEN @TrangThaiMoi = 'DANG_IN' THEN N'Đơn của bạn đang được in.' ELSE N'Đơn của bạn đã in xong.' END
    FROM #DonHang WHERE MaDonHang = @MaDonHang;
    COMMIT;
END;


GO
DECLARE @i INT=1,@Ok BIT,@Loi INT,@truoc INT,@sau INT;
WHILE @i<=7
BEGIN
 SET @Ok=CASE WHEN @i IN(1,6) THEN 1 ELSE 0 END;
 SET @Loi=0;SELECT @truoc=SoLuong FROM #TonKho;
 BEGIN TRY
  BEGIN TRAN;
  EXEC #sp_PhanCongMayIn @i,100;
  EXEC #sp_CapNhatTrangThaiIn @i,'DANG_IN';
  COMMIT;
 END TRY
 BEGIN CATCH
  SET @Loi=ERROR_NUMBER();IF @@TRANCOUNT>0 ROLLBACK;
 END CATCH;
 IF (@Ok=1 AND @Loi<>0) OR (@Ok=0 AND @Loi=0) THROW 51000,N'Sai kết quả phân công/gửi in',1;
 SELECT @sau=SoLuong FROM #TonKho;
 IF @sau<>@truoc-CASE WHEN @Ok=1 THEN 2 ELSE 0 END THROW 51000,N'Sai tồn kho sau gửi in',1;
 IF @Ok=1
 BEGIN
  SET @Loi=0;
  BEGIN TRY EXEC #sp_CapNhatTrangThaiIn @i,'DANG_IN'; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER();IF @@TRANCOUNT>0 ROLLBACK;END CATCH;
  IF @Loi<>50024 THROW 51000,N'Không chặn gửi in lặp',1;
  IF (SELECT SoLuong FROM #TonKho)<>@sau THROW 51000,N'Trừ kho lặp',1;
  EXEC #sp_CapNhatTrangThaiIn @i,'DA_IN';
  IF (SELECT TrangThai FROM #DonHang WHERE MaDonHang=@i)<>'DA_IN' THROW 51000,N'Không báo được in xong',1;
 END;
 SET @i+=1;
END;
PRINT N'PASS: xử lý ngay trước 2 phút, đơn thường vẫn chờ, chưa trả/chưa tiếp nhận bị chặn, hết chờ in được, trừ kho một lần, báo in xong.';
