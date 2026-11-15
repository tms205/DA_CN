SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
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
 ELSE IF @LaKhach=1 AND (@TrangThai<>'CHO_XAC_NHAN' OR (NOT EXISTS(SELECT 1 FROM dbo.ThanhToan WHERE MaDonHang=@MaDonHang AND TrangThai='CHUA_THANH_TOAN') AND @NgayHenTra IS NOT NULL AND @NgayHenTra<=DATEADD(MINUTE,15,GETDATE())))
   THROW 50046,N'Khách chỉ hủy đơn chưa thanh toán/đơn cũ khi chưa tiếp nhận và còn hơn 15 phút trước giờ hẹn.',1;
 IF @TrangThai='CHO_XAC_NHAN'
 BEGIN
   -- Không yêu cầu ảnh; ghi nhật ký máy chủ để giữ dấu vết hủy và thời điểm nhận tiền.
   SET @BangChungJson=(SELECT 'HUY_TRUOC_TIEP_NHAN' AS Loai,@MaDonHang AS MaDonHang,@MaNguoiHuy AS MaNguoiGui,
     SYSDATETIMEOFFSET() AS GuiLuc,@LyDo AS LyDo,tt.TrangThai AS TrangThaiThanhToan,tt.NgayThanhToan,JSON_QUERY('[]') AS Anh
     FROM dbo.ThanhToan tt WHERE tt.MaDonHang=@MaDonHang FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 END
 IF NULLIF(LTRIM(RTRIM(@LyDo)),N'') IS NULL THROW 50045,N'Vui lòng nhập lý do hủy.',1;
 IF ISJSON(@BangChungJson)<>1 OR @BangChungJson IS NULL THROW 50047,N'Cần bằng chứng hủy đơn hợp lệ.',1;
 IF @TrangThai<>'CHO_XAC_NHAN' AND (ISNULL(JSON_VALUE(@BangChungJson,'$.Loai'),'')<>'THU_CONG' OR ISNULL(TRY_CONVERT(INT,JSON_VALUE(@BangChungJson,'$.MaDonHang')),-1)<>@MaDonHang
    OR ISNULL(TRY_CONVERT(INT,JSON_VALUE(@BangChungJson,'$.MaNguoiGui')),-1)<>@MaNguoiHuy
    OR (SELECT COUNT(*) FROM OPENJSON(@BangChungJson,'$.Anh')) NOT BETWEEN 1 AND 3)
    THROW 50047,N'Bằng chứng phải đúng đơn/người gửi và có 1–3 ảnh.',1;
 UPDATE dbo.DonHang SET TrangThai='DA_HUY',GhiChu=@LyDo,MaMayIn=NULL,BangChungHuyJson=@BangChungJson WHERE MaDonHang=@MaDonHang;
 INSERT dbo.ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
 SELECT MaNguoiDung,@MaDonHang,N'Đơn hàng đã bị hủy',@LyDo FROM dbo.NguoiDung
 WHERE MaNguoiDung=@MaKhachHang OR (TrangThai=N'HOAT_DONG' AND (VaiTro='SUPER_ADMIN' OR (VaiTro='BRANCH_ADMIN' AND MaChiNhanh=@MaChiNhanh)));
 COMMIT;
END;



GO
DROP PROCEDURE IF EXISTS dbo.sp_XacNhanXuLyNgay;
GO
