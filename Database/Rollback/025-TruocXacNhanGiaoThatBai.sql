/* Rollback 025 — quay về cách cũ: shipper báo thất bại là ghi nhận ngay.
   Lượt đang chờ cửa hàng xác nhận được chốt thành thất bại (giống code trước 025),
   rồi bỏ khóa ngoại và 3 cột đã thêm. Chạy cùng bản API trước 025.
*/
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE dh SET TrangThai='GIAO_THAT_BAI'
FROM dbo.DonHang dh JOIN dbo.GiaoHang gh ON gh.MaDonHang=dh.MaDonHang
WHERE gh.TrangThai='CHO_XAC_NHAN' AND dh.TrangThai='DANG_GIAO';
UPDATE dbo.GiaoHang SET TrangThai='THAT_BAI' WHERE TrangThai='CHO_XAC_NHAN';
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GiaoHang_NguoiXuLyThatBai' AND parent_object_id=OBJECT_ID(N'dbo.GiaoHang'))
    ALTER TABLE dbo.GiaoHang DROP CONSTRAINT FK_GiaoHang_NguoiXuLyThatBai;
IF COL_LENGTH('dbo.GiaoHang','NgayXuLyThatBai') IS NOT NULL ALTER TABLE dbo.GiaoHang DROP COLUMN NgayXuLyThatBai;
IF COL_LENGTH('dbo.GiaoHang','MaNguoiXuLyThatBai') IS NOT NULL ALTER TABLE dbo.GiaoHang DROP COLUMN MaNguoiXuLyThatBai;
IF COL_LENGTH('dbo.GiaoHang','LyDoTuChoi') IS NOT NULL ALTER TABLE dbo.GiaoHang DROP COLUMN LyDoTuChoi;
COMMIT;
GO
