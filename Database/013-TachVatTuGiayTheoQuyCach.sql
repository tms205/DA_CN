-- 013: Tách vật tư giấy theo khổ/định lượng, không thêm bảng hoặc cột.
-- Tồn giấy chung giữ nguyên ở mục chưa phân loại. Vật tư mới bắt đầu 0 tờ,
-- giá vốn 0 (chưa xác định), chỉ tăng sau khi nhận hàng/kiểm kê được xác nhận.
-- Dịch vụ đã có đơn được giữ lại; phiên bản mới sao chép đúng giá chi nhánh.
USE PhotocopyChain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @QuyCach TABLE (
        KhoGiay VARCHAR(5), LoaiGiay VARCHAR(20), TenVatTu NVARCHAR(100), MaVatTu INT,
        PRIMARY KEY (KhoGiay, LoaiGiay)
    );
    INSERT @QuyCach (KhoGiay, LoaiGiay, TenVatTu)
    SELECT DISTINCT dv.KhoGiay, dv.LoaiGiay, N'Giấy ' + dv.KhoGiay + N' ' + dv.LoaiGiay
    FROM dbo.DichVu dv WITH (UPDLOCK, HOLDLOCK)
    WHERE dv.LoaiDichVu='IN_AN' AND dv.KichThuocAnh IS NULL
      AND NULLIF(LTRIM(RTRIM(dv.KhoGiay)), '') IS NOT NULL
      AND dv.LoaiGiay LIKE '%gsm';

    -- Dừng nếu danh mục đích bị trùng hoặc không dùng đơn vị tờ.
    IF EXISTS (SELECT vt.TenVatTu FROM dbo.VatTu vt JOIN @QuyCach q ON q.TenVatTu=vt.TenVatTu
               GROUP BY vt.TenVatTu HAVING COUNT(*)>1)
        THROW 50130, N'Tên vật tư giấy bị trùng. Cần kiểm tra danh mục trước khi tách.', 1;
    IF EXISTS (SELECT 1 FROM dbo.VatTu vt JOIN @QuyCach q ON q.TenVatTu=vt.TenVatTu
               WHERE ISNULL(LOWER(LTRIM(RTRIM(vt.DonViTinh))), '')<>N'tờ')
        THROW 50131, N'Vật tư giấy đích phải dùng đơn vị Tờ. Không tự quy đổi tồn kho.', 1;

    INSERT dbo.VatTu (TenVatTu, DonViTinh, DonGia)
    SELECT q.TenVatTu, N'Tờ', 0 FROM @QuyCach q
    WHERE NOT EXISTS (SELECT 1 FROM dbo.VatTu vt WITH (UPDLOCK, HOLDLOCK) WHERE vt.TenVatTu=q.TenVatTu);
    UPDATE q SET MaVatTu=vt.MaVatTu FROM @QuyCach q JOIN dbo.VatTu vt ON vt.TenVatTu=q.TenVatTu;

    INSERT dbo.TonKho (MaChiNhanh, MaVatTu, SoLuong, MucToiThieu)
    SELECT cn.MaChiNhanh, q.MaVatTu, 0, 0 FROM dbo.ChiNhanh cn CROSS JOIN @QuyCach q
    WHERE NOT EXISTS (SELECT 1 FROM dbo.TonKho tk WITH (UPDLOCK, HOLDLOCK)
                      WHERE tk.MaChiNhanh=cn.MaChiNhanh AND tk.MaVatTu=q.MaVatTu);

    DECLARE @MaDichVu INT, @MaVatTu INT, @MaMoi INT;
    DECLARE dichVuCanTach CURSOR LOCAL FAST_FORWARD FOR
        SELECT dv.MaDichVu, q.MaVatTu FROM dbo.DichVu dv JOIN @QuyCach q
            ON q.KhoGiay=dv.KhoGiay AND q.LoaiGiay=dv.LoaiGiay
        WHERE dv.LoaiDichVu='IN_AN' AND dv.KichThuocAnh IS NULL
          AND ISNULL(dv.MaVatTu,0)<>q.MaVatTu
          AND (ISNULL(dv.TrangThai,N'HIEN')<>N'AN' OR EXISTS (
              SELECT 1 FROM dbo.BangGiaChiNhanh bg WHERE bg.MaDichVu=dv.MaDichVu AND bg.TrangThai=N'HIEN'));
    OPEN dichVuCanTach;
    FETCH NEXT FROM dichVuCanTach INTO @MaDichVu, @MaVatTu;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.ChiTietDonHang WITH (UPDLOCK,HOLDLOCK) WHERE MaDichVu=@MaDichVu)
        BEGIN
            INSERT dbo.DichVu (TenDichVu,LoaiDichVu,KhoGiay,LoaiGiay,MatIn,MauSac,NhomGiaCong,
                               KichThuocAnh,DonGia,MaVatTu,TrangThai)
            SELECT TenDichVu,LoaiDichVu,KhoGiay,LoaiGiay,MatIn,MauSac,NhomGiaCong,
                   KichThuocAnh,DonGia,@MaVatTu,TrangThai FROM dbo.DichVu WHERE MaDichVu=@MaDichVu;
            SET @MaMoi=CAST(SCOPE_IDENTITY() AS INT);
            INSERT dbo.BangGiaChiNhanh (MaChiNhanh,MaDichVu,DonGia,TrangThai)
            SELECT MaChiNhanh,@MaMoi,DonGia,TrangThai FROM dbo.BangGiaChiNhanh WHERE MaDichVu=@MaDichVu;
            UPDATE dbo.BangGiaChiNhanh SET TrangThai=N'AN' WHERE MaDichVu=@MaDichVu;
            UPDATE dbo.DichVu SET TrangThai=N'AN' WHERE MaDichVu=@MaDichVu;
        END
        ELSE
            UPDATE dbo.DichVu SET MaVatTu=@MaVatTu WHERE MaDichVu=@MaDichVu;
        FETCH NEXT FROM dichVuCanTach INTO @MaDichVu, @MaVatTu;
    END;
    CLOSE dichVuCanTach;
    DEALLOCATE dichVuCanTach;

    -- Chỉ đổi nhãn kho cũ, không phân bổ hoặc thay số lượng/giá vốn/phiếu nhập cũ.
    UPDATE vt SET TenVatTu=vt.TenVatTu+N' (chưa phân loại)'
    FROM dbo.VatTu vt
    WHERE vt.TenVatTu IN (N'Giấy A4',N'Giấy A3')
      AND EXISTS (SELECT 1 FROM @QuyCach q WHERE q.KhoGiay=REPLACE(vt.TenVatTu,N'Giấy ',N''));

    COMMIT TRANSACTION;
    PRINT N'Đã tách vật tư giấy. Cần kiểm kê tồn chưa phân loại trước khi nhập sang các loại giấy mới.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
