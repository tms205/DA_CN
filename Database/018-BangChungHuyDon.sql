-- 018: bằng chứng hủy; khách chỉ hủy trước tiếp nhận và cách giờ hẹn >15 phút.
-- Thêm một cột JSON nullable, thay hai thủ tục. Không sửa dữ liệu cũ.
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRAN;
IF COL_LENGTH('dbo.DonHang','BangChungHuyJson') IS NULL
    ALTER TABLE dbo.DonHang ADD BangChungHuyJson NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_DonHang_BangChungHuyJson' AND parent_object_id=OBJECT_ID('dbo.DonHang'))
    ALTER TABLE dbo.DonHang WITH CHECK ADD CONSTRAINT CK_DonHang_BangChungHuyJson CHECK(BangChungHuyJson IS NULL OR ISJSON(BangChungHuyJson)=1);
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
 IF @LaKhach=1 AND (@TrangThai<>'CHO_XAC_NHAN' OR (@NgayHenTra IS NOT NULL AND @NgayHenTra<=DATEADD(MINUTE,15,GETDATE()))) THROW 50046,N'Khách chỉ hủy khi chưa tiếp nhận và còn hơn 15 phút trước giờ hẹn.',1;
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
CREATE OR ALTER PROCEDURE dbo.sp_HuyDonQuaHan
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRAN;
 DECLARE @Huy TABLE(MaDonHang INT,MaKhachHang INT,MaChiNhanh INT);
 UPDATE dh SET TrangThai='DA_HUY',GhiChu=N'Quá 15 phút chưa thanh toán',
   BangChungHuyJson=(SELECT 'TU_DONG' AS Loai,dh.MaDonHang,0 AS MaNguoiGui,SYSDATETIMEOFFSET() AS GuiLuc,
     N'Quá 15 phút chưa thanh toán' AS LyDo,dh.NgayDat,DATEADD(MINUTE,15,dh.NgayDat) AS HetHanThanhToan,
     tt.PhuongThuc,tt.TrangThai AS TrangThaiThanhToan FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
 OUTPUT inserted.MaDonHang,inserted.MaKhachHang,inserted.MaChiNhanh INTO @Huy
 FROM dbo.DonHang dh JOIN dbo.ThanhToan tt WITH(UPDLOCK,HOLDLOCK) ON tt.MaDonHang=dh.MaDonHang
 WHERE dh.TrangThai='CHO_XAC_NHAN' AND tt.PhuongThuc='VIETQR' AND tt.TrangThai='CHUA_THANH_TOAN'
   AND DATEADD(MINUTE,15,dh.NgayDat)<GETDATE();
 INSERT dbo.ThongBao(MaNguoiDung,MaDonHang,TieuDe,NoiDung)
 SELECT nd.MaNguoiDung,h.MaDonHang,N'Đơn tự hủy do hết hạn thanh toán',N'Đã ghi nhật ký thời gian và trạng thái thanh toán làm bằng chứng hệ thống.'
 FROM @Huy h JOIN dbo.NguoiDung nd ON nd.MaNguoiDung=h.MaKhachHang OR (nd.TrangThai=N'HOAT_DONG' AND (nd.VaiTro='SUPER_ADMIN' OR(nd.VaiTro='BRANCH_ADMIN' AND nd.MaChiNhanh=h.MaChiNhanh)));
 COMMIT;
END;
GO
COMMIT;
GO
