-- Chỉ bảng/thủ tục tạm, không cập nhật dữ liệu nghiệp vụ.
:ON ERROR EXIT
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET XACT_ABORT ON;
CREATE TABLE #DonHang(MaDonHang INT PRIMARY KEY,MaKhachHang INT,MaChiNhanh INT,TrangThai VARCHAR(20) COLLATE DATABASE_DEFAULT,KenhDatDon VARCHAR(20) COLLATE DATABASE_DEFAULT,BangChungHuyJson NVARCHAR(MAX) COLLATE DATABASE_DEFAULT,MaNhanVien INT);
CREATE TABLE #ThanhToan(MaDonHang INT PRIMARY KEY,TrangThai VARCHAR(30) COLLATE DATABASE_DEFAULT,NgayThanhToan DATETIME,PhuongThuc VARCHAR(20) COLLATE DATABASE_DEFAULT);
CREATE TABLE #NguoiDung(MaNguoiDung INT PRIMARY KEY,MaChiNhanh INT,VaiTro VARCHAR(20) COLLATE DATABASE_DEFAULT);
CREATE TABLE #CaLamViec(MaNhanVien INT,MaChiNhanh INT,TrangThai VARCHAR(20) COLLATE DATABASE_DEFAULT);
CREATE TABLE #ThongBao(MaNguoiDung INT,MaDonHang INT,TieuDe NVARCHAR(200) COLLATE DATABASE_DEFAULT,NoiDung NVARCHAR(MAX) COLLATE DATABASE_DEFAULT);
INSERT #DonHang VALUES(1,10,1,'CHO_XAC_NHAN','APP',NULL,NULL),(2,10,1,'CHO_XAC_NHAN','APP',NULL,NULL),(3,10,1,'CHO_XAC_NHAN','APP',NULL,NULL),(4,10,1,'DA_HUY','APP',NULL,NULL);
INSERT #ThanhToan VALUES(1,'DA_THANH_TOAN',GETDATE(),'VIETQR'),(2,'DA_THANH_TOAN',GETDATE(),'VIETQR'),(3,'CHUA_THANH_TOAN',NULL,'VIETQR'),(4,'DA_THANH_TOAN',GETDATE(),'VIETQR');
INSERT #NguoiDung VALUES(20,1,'STAFF'); INSERT #CaLamViec VALUES(20,1,'DANG_MO');
GO


/* CEO va nhan vien chi nhanh duoc tiep nhan don khi da mo ca. */
CREATE PROCEDURE #sp_TiepNhanDon
    @MaDonHang INT, @MaNhanVien INT
AS
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRAN;
    DECLARE @MaChiNhanhDon INT, @MaChiNhanhNhanVien INT, @VaiTro VARCHAR(20);

    SELECT @MaChiNhanhDon = MaChiNhanh
    FROM #DonHang WITH(UPDLOCK,HOLDLOCK)
    WHERE MaDonHang = @MaDonHang;

    SELECT @MaChiNhanhNhanVien = MaChiNhanh, @VaiTro = VaiTro
    FROM #NguoiDung
    WHERE MaNguoiDung = @MaNhanVien;

    IF @VaiTro IS NULL OR @VaiTro NOT IN ('STAFF','BRANCH_ADMIN','SUPER_ADMIN')
       OR (@VaiTro <> 'SUPER_ADMIN' AND @MaChiNhanhNhanVien <> @MaChiNhanhDon)
        THROW 50021, N'Bạn không có quyền tiếp nhận đơn của chi nhánh này.', 1;

    IF NOT EXISTS (SELECT 1 FROM #CaLamViec
                   WHERE MaNhanVien = @MaNhanVien AND MaChiNhanh = @MaChiNhanhDon
                     AND TrangThai = 'DANG_MO')
        THROW 50029, N'Bạn chưa mở ca tại chi nhánh này.', 1;

    IF NOT EXISTS (SELECT 1 FROM #DonHang dh JOIN #ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
                   WHERE dh.MaDonHang = @MaDonHang AND dh.TrangThai = 'CHO_XAC_NHAN'
                     AND (tt.PhuongThuc = 'COD' OR tt.TrangThai = 'DA_THANH_TOAN'))
        THROW 50020, N'Đơn chưa sẵn sàng để tiếp nhận.', 1;


    IF EXISTS(SELECT 1 FROM #DonHang dh JOIN #ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
      WHERE dh.MaDonHang=@MaDonHang AND dh.KenhDatDon='APP'
        AND (tt.TrangThai<>'DA_THANH_TOAN' OR tt.NgayThanhToan IS NULL OR (GETDATE()<DATEADD(SECOND,120,tt.NgayThanhToan) AND ISNULL(JSON_VALUE(dh.BangChungHuyJson,'$.BoQuaChoHaiPhut'),'false')<>'true')))
      THROW 50221,N'Đơn app đang trong 2 phút xác nhận sau thanh toán; chưa được tiếp nhận.',1;

    UPDATE #DonHang
    SET TrangThai = 'DA_XAC_NHAN', MaNhanVien = @MaNhanVien
    WHERE MaDonHang = @MaDonHang;

    INSERT INTO #ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    SELECT MaKhachHang, MaDonHang, N'Đơn hàng đã được tiếp nhận',
           N'Chi nhánh đã nhận đơn và xếp vào hàng đợi in.'
    FROM #DonHang WHERE MaDonHang = @MaDonHang;
    COMMIT;
END;



GO
CREATE PROCEDURE #sp_XacNhanXuLyNgay
 @MaDonHang INT,@MaKhachHang INT
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRAN;
  DECLARE @Json NVARCHAR(MAX),@TrangThai VARCHAR(20),@Kenh VARCHAR(20),@Chu INT,@Tra VARCHAR(30),@NgayTra DATETIME;
  SELECT @Json=BangChungHuyJson,@TrangThai=TrangThai,@Kenh=KenhDatDon,@Chu=MaKhachHang
  FROM #DonHang WITH(UPDLOCK,HOLDLOCK) WHERE MaDonHang=@MaDonHang;
  IF @Chu IS NULL OR @Chu<>@MaKhachHang THROW 50224,N'Không tìm thấy đơn hàng của bạn.',1;
  IF @Kenh<>'APP' OR @TrangThai<>'CHO_XAC_NHAN' THROW 50224,N'Đơn không còn ở bước xác nhận xử lý.',1;
  SELECT @Tra=TrangThai,@NgayTra=NgayThanhToan FROM #ThanhToan WITH(UPDLOCK,HOLDLOCK) WHERE MaDonHang=@MaDonHang;
  IF @Tra IS NULL OR @Tra<>'DA_THANH_TOAN' OR @NgayTra IS NULL THROW 50224,N'Đơn chưa được xác nhận thanh toán.',1;
  IF ISNULL(JSON_VALUE(@Json,'$.BoQuaChoHaiPhut'),'false')<>'true'
  BEGIN
   IF GETDATE()>=DATEADD(SECOND,120,@NgayTra) THROW 50224,N'Đã hết thời gian chờ; nhân viên có thể tiếp nhận đơn.',1;
   SET @Json=JSON_MODIFY(ISNULL(@Json,N'{}'),'$.BoQuaChoHaiPhut',CAST(1 AS BIT));
   SET @Json=JSON_MODIFY(@Json,'$.NguoiXacNhanXuLyNgay',@MaKhachHang);
   SET @Json=JSON_MODIFY(@Json,'$.XacNhanXuLyNgayLuc',CONVERT(NVARCHAR(40),SYSDATETIMEOFFSET(),127));
   UPDATE #DonHang SET BangChungHuyJson=@Json WHERE MaDonHang=@MaDonHang;
   INSERT #ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
   VALUES(@MaKhachHang,@MaDonHang,N'Đã xác nhận xử lý ngay',N'Bạn đã bỏ qua thời gian chờ hủy/hoàn. Đơn chờ nhân viên tiếp nhận.');
  END;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END;


GO
DECLARE @NgayTra DATETIME=(SELECT NgayThanhToan FROM #ThanhToan WHERE MaDonHang=1);
EXEC #sp_XacNhanXuLyNgay 1,10;
EXEC #sp_XacNhanXuLyNgay 1,10;
IF (SELECT COUNT(*) FROM #ThongBao WHERE MaDonHang=1)<>1 THROW 51000,N'Lặp yêu cầu tạo trùng thông báo',1;
IF (SELECT NgayThanhToan FROM #ThanhToan WHERE MaDonHang=1)<>@NgayTra THROW 51000,N'Đã sửa ngày thanh toán',1;
IF (SELECT TrangThai FROM #DonHang WHERE MaDonHang=1)<>'CHO_XAC_NHAN' THROW 51000,N'Tự tiếp nhận sai luồng',1;
DECLARE @Loi INT=0;
BEGIN TRY EXEC #sp_XacNhanXuLyNgay 2,99; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); END CATCH;
IF @Loi<>50224 THROW 51000,N'Không chặn sai chủ đơn',1;
SET @Loi=0;
BEGIN TRY EXEC #sp_XacNhanXuLyNgay 3,10; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); END CATCH;
IF @Loi<>50224 THROW 51000,N'Không chặn chưa thanh toán',1;
SET @Loi=0;
BEGIN TRY EXEC #sp_XacNhanXuLyNgay 4,10; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); END CATCH;
IF @Loi<>50224 THROW 51000,N'Không chặn đơn đã hủy',1;
SET @Loi=0;
BEGIN TRY EXEC #sp_TiepNhanDon 2,20; END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); IF @@TRANCOUNT>0 ROLLBACK; END CATCH;
IF @Loi<>50221 THROW 51000,N'Không giữ chờ 2 phút cho đơn thường',1;
SET @Loi=0;
BEGIN TRY
 IF EXISTS(SELECT 1 FROM #DonHang WHERE MaDonHang=1 AND JSON_VALUE(BangChungHuyJson,'$.BoQuaChoHaiPhut')='true')
   THROW 50223,N'Không được hủy khi đã xác nhận xử lý ngay',1;
END TRY BEGIN CATCH SET @Loi=ERROR_NUMBER(); END CATCH;
IF @Loi<>50223 THROW 51000,N'Không chặn hủy sau xác nhận',1;
EXEC #sp_TiepNhanDon 1,20;
IF (SELECT TrangThai FROM #DonHang WHERE MaDonHang=1)<>'DA_XAC_NHAN' THROW 51000,N'Đơn xử lý ngay không được tiếp nhận',1;
PRINT N'PASS: xác nhận, lặp yêu cầu, quyền sở hữu, chưa trả, đã hủy, giữ chờ, tiếp nhận ngay, giữ ngày trả.';
