/* 017 — phản hồi đối chiếu của khách theo từng lượt giao thất bại.
   Chỉ thêm một cột JSON nullable và CHECK, không thêm bảng hoặc sửa dữ liệu cũ.
   Chỉ áp dụng sau khi chủ dự án đồng ý. Ảnh lưu riêng ngoài wwwroot như migration 016.
*/
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET XACT_ABORT ON;
BEGIN TRAN;
IF OBJECT_ID(N'dbo.GiaoHang',N'U') IS NULL THROW 50000,N'Không tìm thấy bảng GiaoHang.',1;
IF COL_LENGTH('dbo.GiaoHang','DoiChieuKhachHangJson') IS NULL
    ALTER TABLE dbo.GiaoHang ADD DoiChieuKhachHangJson NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GiaoHang_DoiChieuKhachHangJson' AND parent_object_id=OBJECT_ID(N'dbo.GiaoHang'))
    ALTER TABLE dbo.GiaoHang WITH CHECK ADD CONSTRAINT CK_GiaoHang_DoiChieuKhachHangJson
        CHECK (DoiChieuKhachHangJson IS NULL OR (ISJSON(DoiChieuKhachHangJson)=1 AND LEFT(LTRIM(DoiChieuKhachHangJson),1)='{'));
COMMIT;
GO
