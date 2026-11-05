/* 025 — cửa hàng xác nhận / từ chối báo giao thất bại (sơ đồ use case 4.10).
   Shipper báo thất bại (GPS + ảnh) → lượt 'CHO_XAC_NHAN', đơn vẫn 'DANG_GIAO'.
   Cửa hàng xem bằng chứng, so vị trí với địa chỉ giao:
     - xác nhận → lượt 'THAT_BAI', đơn 'GIAO_THAT_BAI';
     - từ chối  → lượt về 'DANG_GIAO', lưu lý do từ chối, shipper giao tiếp.
   Chỉ thêm 3 cột nullable vào GiaoHang; không thêm bảng, không sửa lượt giao cũ.
   Chạy UTF-8: sqlcmd -f 65001 -b -i <file>. Có thể chạy lại an toàn.
*/
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET XACT_ABORT ON;
BEGIN TRAN;
IF OBJECT_ID(N'dbo.GiaoHang', N'U') IS NULL THROW 50000, N'Không tìm thấy bảng GiaoHang.', 1;
IF COL_LENGTH('dbo.GiaoHang','LyDoTuChoi') IS NULL ALTER TABLE dbo.GiaoHang ADD LyDoTuChoi NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.GiaoHang','MaNguoiXuLyThatBai') IS NULL ALTER TABLE dbo.GiaoHang ADD MaNguoiXuLyThatBai INT NULL;
IF COL_LENGTH('dbo.GiaoHang','NgayXuLyThatBai') IS NULL ALTER TABLE dbo.GiaoHang ADD NgayXuLyThatBai DATETIME NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GiaoHang_NguoiXuLyThatBai' AND parent_object_id=OBJECT_ID(N'dbo.GiaoHang'))
    ALTER TABLE dbo.GiaoHang WITH CHECK ADD CONSTRAINT FK_GiaoHang_NguoiXuLyThatBai
        FOREIGN KEY (MaNguoiXuLyThatBai) REFERENCES dbo.NguoiDung(MaNguoiDung);
COMMIT;
GO
