/* 016 — bằng chứng theo từng lượt giao: GPS, thời điểm, địa chỉ và metadata ảnh.
   Thêm 7 cột nullable vào GiaoHang; không thêm bảng, không sửa/xóa lượt giao cũ.
   Ảnh nằm ngoài wwwroot, phải sao lưu cùng database. Chỉ áp dụng sau khi được đồng ý.
   Chạy UTF-8: sqlcmd -f 65001 -b -i <file>. Có thể chạy lại an toàn.
*/
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET XACT_ABORT ON;
BEGIN TRAN;
IF OBJECT_ID(N'dbo.GiaoHang', N'U') IS NULL THROW 50000, N'Không tìm thấy bảng GiaoHang.', 1;
IF COL_LENGTH('dbo.GiaoHang','ViDoBaoGiao') IS NULL ALTER TABLE dbo.GiaoHang ADD ViDoBaoGiao DECIMAL(10,7) NULL;
IF COL_LENGTH('dbo.GiaoHang','KinhDoBaoGiao') IS NULL ALTER TABLE dbo.GiaoHang ADD KinhDoBaoGiao DECIMAL(10,7) NULL;
IF COL_LENGTH('dbo.GiaoHang','DoChinhXacGps') IS NULL ALTER TABLE dbo.GiaoHang ADD DoChinhXacGps FLOAT NULL;
IF COL_LENGTH('dbo.GiaoHang','ThoiDiemGps') IS NULL ALTER TABLE dbo.GiaoHang ADD ThoiDiemGps DATETIMEOFFSET(0) NULL;
IF COL_LENGTH('dbo.GiaoHang','ThoiDiemBaoCao') IS NULL ALTER TABLE dbo.GiaoHang ADD ThoiDiemBaoCao DATETIMEOFFSET(0) NULL;
IF COL_LENGTH('dbo.GiaoHang','DiaChiBaoGiao') IS NULL ALTER TABLE dbo.GiaoHang ADD DiaChiBaoGiao NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.GiaoHang','AnhBangChungJson') IS NULL ALTER TABLE dbo.GiaoHang ADD AnhBangChungJson NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GiaoHang_GpsBangChung' AND parent_object_id=OBJECT_ID(N'dbo.GiaoHang'))
    ALTER TABLE dbo.GiaoHang WITH CHECK ADD CONSTRAINT CK_GiaoHang_GpsBangChung CHECK (
        (ViDoBaoGiao IS NULL AND KinhDoBaoGiao IS NULL AND DoChinhXacGps IS NULL AND ThoiDiemGps IS NULL)
        OR (ViDoBaoGiao IS NOT NULL AND KinhDoBaoGiao IS NOT NULL AND DoChinhXacGps IS NOT NULL AND ThoiDiemGps IS NOT NULL
            AND ViDoBaoGiao BETWEEN -90 AND 90 AND KinhDoBaoGiao BETWEEN -180 AND 180 AND DoChinhXacGps BETWEEN 0 AND 100000));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GiaoHang_AnhBangChungJson' AND parent_object_id=OBJECT_ID(N'dbo.GiaoHang'))
    ALTER TABLE dbo.GiaoHang WITH CHECK ADD CONSTRAINT CK_GiaoHang_AnhBangChungJson CHECK (
        AnhBangChungJson IS NULL OR (ISJSON(AnhBangChungJson)=1 AND LEFT(LTRIM(AnhBangChungJson),1)='['));
COMMIT;
GO
