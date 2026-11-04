-- 022: Luu lai SET options cua cac thu tuc hien hanh; giu nguyen than thu tuc.
-- Khong them/xoa bang, cot, chi muc hay du lieu nghiep vu.
-- Chay lai an toan: chi ALTER module co ANSI_NULLS / QUOTED_IDENTIFIER chua ON.
:ON ERROR EXIT
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO
BEGIN TRY
    BEGIN TRAN;
    DECLARE @Targets TABLE (Ten sysname PRIMARY KEY);
    INSERT @Targets VALUES
        ('sp_MoCa'),('sp_DongCa'),('sp_GiaoTaiQuay'),('sp_HuyDonHang'),
        ('sp_TaoDonHang'),('sp_TiepNhanDon'),('sp_PhanCongMayIn'),
        ('sp_CapNhatTrangThaiIn'),('sp_HuyDonQuaHan');
    IF EXISTS (SELECT 1 FROM @Targets t LEFT JOIN sys.sql_modules m
        ON m.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Ten),N'P')
        WHERE m.definition IS NULL)
        THROW 50280, N'Khong doc duoc du thu tuc; dung de bao toan logic hien tai.', 1;
    IF EXISTS (SELECT 1 FROM @Targets t JOIN sys.crypt_properties c
        ON c.major_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Ten),N'P'))
        THROW 50281, N'Thu tuc co chu ky so; can quy trinh ky lai rieng.', 1;

    DECLARE @Name sysname, @Definition nvarchar(max), @CreateAt int, @ProcAt int;
    DECLARE FixModules CURSOR LOCAL FAST_FORWARD FOR
        SELECT t.Ten,m.definition FROM @Targets t JOIN sys.sql_modules m
        ON m.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Ten),N'P')
        WHERE m.uses_quoted_identifier=0 OR m.uses_ansi_nulls=0;
    OPEN FixModules;
    FETCH NEXT FROM FixModules INTO @Name,@Definition;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @CreateAt=CHARINDEX(N'CREATE',UPPER(@Definition));
        SET @ProcAt=CHARINDEX(N'PROCEDURE',UPPER(@Definition));
        IF @CreateAt=0 OR @ProcAt<=@CreateAt+6
            THROW 50282, N'Dinh dang module khong phu hop; khong tu viet lai logic.', 1;
        IF LEN(REPLACE(REPLACE(REPLACE(REPLACE(
            SUBSTRING(@Definition,@CreateAt+6,@ProcAt-@CreateAt-6),
            N' ',N''),NCHAR(9),N''),NCHAR(10),N''),NCHAR(13),N''))<>0
            THROW 50283, N'Header thu tuc khong duoc ho tro; dung an toan.', 1;
        SET @Definition=STUFF(@Definition,@CreateAt,6,N'ALTER');
        EXEC sys.sp_executesql @Definition;
        PRINT N'Da sua SET options: dbo.'+@Name;
        FETCH NEXT FROM FixModules INTO @Name,@Definition;
    END;
    CLOSE FixModules;
    DEALLOCATE FixModules;
    IF EXISTS (SELECT 1 FROM @Targets t JOIN sys.sql_modules m
        ON m.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Ten),N'P')
        WHERE m.uses_quoted_identifier<>1 OR m.uses_ansi_nulls<>1)
        THROW 50284, N'Kiem tra SET options khong dat; huy toan bo thay doi.', 1;
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
GO
