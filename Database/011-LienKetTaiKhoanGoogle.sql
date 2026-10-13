-- 011: Liên kết Google bằng subject; không sửa dữ liệu/tài khoản đang có.
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.NguoiDung', N'U') IS NULL
    THROW 50140, N'Chưa có bảng NguoiDung. Hãy chạy bộ SQL gốc trước.', 1;

IF OBJECT_ID(N'dbo.LienKetTaiKhoanGoogle', N'U') IS NULL
BEGIN
    BEGIN TRANSACTION;
    CREATE TABLE dbo.LienKetTaiKhoanGoogle (
        GoogleSubject VARCHAR(255) COLLATE Latin1_General_100_BIN2 NOT NULL,
        MaNguoiDung INT NOT NULL,
        NgayLienKet DATETIME2 NOT NULL CONSTRAINT DF_LienKetGoogle_Ngay DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_LienKetTaiKhoanGoogle PRIMARY KEY (GoogleSubject),
        CONSTRAINT UQ_LienKetGoogle_NguoiDung UNIQUE (MaNguoiDung),
        CONSTRAINT FK_LienKetGoogle_NguoiDung FOREIGN KEY (MaNguoiDung) REFERENCES dbo.NguoiDung(MaNguoiDung)
    );
    COMMIT TRANSACTION;
END;
GO
