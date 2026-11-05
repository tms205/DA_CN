/* 026 — tạo lại trigger bằng QUOTED_IDENTIFIER ON (bổ sung cho 022, 022 mới sửa thủ tục).
   Lỗi: trigger tạo bằng sqlcmd không có -I sẽ lưu QUOTED_IDENTIFIER OFF. Từ khi 023 thêm chỉ mục có lọc
   UX_DonHang_MaYeuCauTao, câu UPDATE DonHang bên trong trg_DonHang_HoanThanh chạy với QUOTED_IDENTIFIER OFF
   nên MỌI câu UPDATE DonHang đều lỗi "UPDATE failed because the following SET options have incorrect settings".
   Script đọc lại đúng nội dung trigger đang có trong CSDL, chỉ tạo lại với SET ON; không đổi logic, không sửa dữ liệu.
   Chạy UTF-8: sqlcmd -I -f 65001 -b -i <file>. Có thể chạy lại an toàn (không còn trigger nào OFF thì không làm gì).
*/
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @ten SYSNAME, @dinhNghia NVARCHAR(MAX), @viTri INT;
DECLARE dsTrigger CURSOR LOCAL FAST_FORWARD FOR
    SELECT o.name, m.definition FROM sys.sql_modules m JOIN sys.objects o ON o.object_id = m.object_id
    WHERE o.type = 'TR' AND SCHEMA_NAME(o.schema_id) = 'dbo'
      AND (m.uses_quoted_identifier = 0 OR m.uses_ansi_nulls = 0);
OPEN dsTrigger;
FETCH NEXT FROM dsTrigger INTO @ten, @dinhNghia;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @viTri = CHARINDEX(N'CREATE TRIGGER', @dinhNghia);
    IF @viTri = 0 THROW 50260, N'Không tìm thấy chữ CREATE TRIGGER trong định nghĩa trigger; dừng để sửa tay.', 1;
    SET @dinhNghia = STUFF(@dinhNghia, @viTri, LEN(N'CREATE TRIGGER'), N'CREATE OR ALTER TRIGGER');
    EXEC sys.sp_executesql @dinhNghia;
    PRINT N'Đã tạo lại ' + @ten + N' với QUOTED_IDENTIFIER ON.';
    FETCH NEXT FROM dsTrigger INTO @ten, @dinhNghia;
END
CLOSE dsTrigger;
DEALLOCATE dsTrigger;
IF EXISTS (SELECT 1 FROM sys.sql_modules m JOIN sys.objects o ON o.object_id = m.object_id
           WHERE o.type = 'TR' AND (m.uses_quoted_identifier = 0 OR m.uses_ansi_nulls = 0))
    THROW 50261, N'Vẫn còn trigger QUOTED_IDENTIFIER/ANSI_NULLS OFF.', 1;
COMMIT;
GO
