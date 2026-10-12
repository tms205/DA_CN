/* =============================================================
   FILE 05: TRIGGER
   ============================================================= */

USE PhotocopyChain;
GO

-- Don hoan thanh thi ghi ngay hoan thanh
CREATE TRIGGER trg_DonHang_HoanThanh
ON DonHang
AFTER UPDATE
AS
BEGIN
    UPDATE DonHang SET NgayHoanThanh = GETDATE()
    WHERE MaDonHang IN (SELECT i.MaDonHang FROM inserted i JOIN deleted d ON d.MaDonHang = i.MaDonHang
                        WHERE i.TrangThai = 'HOAN_THANH' AND d.TrangThai <> 'HOAN_THANH');
END;
GO

-- Don bi huy thi tra lai luot dung ma khuyen mai
CREATE TRIGGER trg_DonHang_Huy
ON DonHang
AFTER UPDATE
AS
BEGIN
    UPDATE km SET DaDung = DaDung - 1
    FROM KhuyenMai km
    JOIN inserted i ON i.MaKhuyenMai = km.MaKhuyenMai
    JOIN deleted d ON d.MaDonHang = i.MaDonHang
    WHERE i.TrangThai = 'DA_HUY' AND d.TrangThai <> 'DA_HUY';
END;
GO

-- Khach danh gia 1-2 sao thi bao cho quan ly chi nhanh
CREATE TRIGGER trg_DanhGia_BaoQuanLy
ON DanhGia
AFTER INSERT
AS
BEGIN
    INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
    SELECT ql.MaNguoiDung, i.MaDonHang, N'Có đánh giá thấp',
           N'Đơn #' + CAST(i.MaDonHang AS NVARCHAR(10)) + N' bị đánh giá ' + CAST(i.SoSao AS NVARCHAR(1)) + N' sao.'
    FROM inserted i
    JOIN DonHang dh ON dh.MaDonHang = i.MaDonHang
    JOIN NguoiDung ql ON ql.MaChiNhanh = dh.MaChiNhanh AND ql.VaiTro = 'BRANCH_ADMIN'
    WHERE i.SoSao <= 2;
END;
GO

-- Nguoi xac nhan dong ca phai la Quan ly cua chi nhanh do hoac Super Admin,
-- va khong duoc la chinh nhan vien truc ca
CREATE TRIGGER trg_CaLamViec_KiemTraNguoiXacNhan
ON CaLamViec
AFTER INSERT, UPDATE
AS
BEGIN
    IF EXISTS (SELECT 1 FROM inserted
               WHERE MaNguoiXacNhan IS NOT NULL
                 AND (dbo.fn_LaQuanLy(MaNguoiXacNhan, MaChiNhanh) = 0 OR MaNguoiXacNhan = MaNhanVien))
    BEGIN
        RAISERROR (N'Người xác nhận ca phải là Quản lý chi nhánh đó hoặc Super Admin, và không phải người trực ca.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

-- Phieu nhap: nguoi lap la Quan ly cua chi nhanh do hoac Super Admin,
-- nguoi duyet phai la Super Admin; CEO duoc tu duyet phieu minh lap
CREATE TRIGGER trg_PhieuNhap_KiemTraNguoiLapDuyet
ON PhieuNhap
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted
               WHERE MaNguoiLap IS NULL OR dbo.fn_LaQuanLy(MaNguoiLap, MaChiNhanh) = 0)
        THROW 50060, N'Người lập phiếu nhập phải là Quản lý chi nhánh đó hoặc Super Admin.', 1;

    -- CEO được tự duyệt; quản lý chi nhánh vẫn không có quyền duyệt.
    IF EXISTS (SELECT 1 FROM inserted
               WHERE MaNguoiDuyet IS NOT NULL AND dbo.fn_LaSuperAdmin(MaNguoiDuyet) = 0)
        THROW 50061, N'Người duyệt phiếu nhập phải là Super Admin.', 1;
END;
GO

PRINT N'Da tao trigger';
GO
