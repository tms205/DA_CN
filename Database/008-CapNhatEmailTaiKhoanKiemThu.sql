USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

-- Chỉ đổi email của 3 tài khoản kiểm thử, không đổi mật khẩu, quyền hoặc chi nhánh.
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TaiKhoan TABLE (
        MaNguoiDung INT PRIMARY KEY,
        EmailCu VARCHAR(100),
        EmailMoi VARCHAR(100),
        VaiTro VARCHAR(20)
    );

    INSERT INTO @TaiKhoan VALUES
        (1, 'ceo@photocopy.vn', 'manh27062005@gmail.com', 'SUPER_ADMIN'),
        (2, 'qlcn1@photocopy.vn', 'gg.tms205@gmail.com', 'BRANCH_ADMIN'),
        (4, 'thoin1@photocopy.vn', 'gg.tms2765@gmail.com', 'STAFF');

    -- Cho phép chạy lại, nhưng dừng nếu tài khoản đã bị đổi ngoài dự kiến.
    IF (SELECT COUNT(*)
        FROM NguoiDung nd WITH (UPDLOCK, HOLDLOCK)
        JOIN @TaiKhoan tk ON nd.MaNguoiDung = tk.MaNguoiDung
        WHERE nd.VaiTro = tk.VaiTro
          AND nd.Email IN (tk.EmailCu, tk.EmailMoi)) <> 3
        THROW 50001, N'Không tìm thấy đúng 3 tài khoản. Chưa cập nhật email.', 1;

    IF EXISTS (
        SELECT 1
        FROM NguoiDung nd WITH (UPDLOCK, HOLDLOCK)
        JOIN @TaiKhoan tk ON nd.Email = tk.EmailMoi
        WHERE nd.MaNguoiDung <> tk.MaNguoiDung
    )
        THROW 50002, N'Email mới đã thuộc tài khoản khác. Chưa cập nhật email.', 1;

    -- Bản đối chiếu chỉ tồn tại trong phiên SQL, không ghi mật khẩu ra file/log.
    SELECT nd.* INTO #TaiKhoanDoiChieu
    FROM NguoiDung nd
    JOIN @TaiKhoan tk ON nd.MaNguoiDung = tk.MaNguoiDung;

    UPDATE dc SET Email = tk.EmailMoi
    FROM #TaiKhoanDoiChieu dc
    JOIN @TaiKhoan tk ON dc.MaNguoiDung = tk.MaNguoiDung;

    UPDATE nd SET Email = tk.EmailMoi
    FROM NguoiDung nd
    JOIN @TaiKhoan tk ON nd.MaNguoiDung = tk.MaNguoiDung;

    IF @@ROWCOUNT <> 3
        THROW 50003, N'Số tài khoản cập nhật không đúng. Hoàn tác.', 1;

    IF EXISTS (
        SELECT * FROM #TaiKhoanDoiChieu
        EXCEPT
        SELECT nd.* FROM NguoiDung nd
        JOIN @TaiKhoan tk ON nd.MaNguoiDung = tk.MaNguoiDung
    )
        THROW 50004, N'Thông tin ngoài email đã thay đổi. Hoàn tác.', 1;

    DROP TABLE #TaiKhoanDoiChieu;
    COMMIT TRANSACTION;

    SELECT MaNguoiDung, HoTen, Email, VaiTro, TrangThai
    FROM NguoiDung WHERE MaNguoiDung IN (1, 2, 4)
    ORDER BY MaNguoiDung;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
