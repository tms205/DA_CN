/* =============================================================
   FILE 02: DU LIEU MAU
   ============================================================= */

USE PhotocopyChain;
GO

INSERT INTO ChiNhanh (TenChiNhanh, DiaChi, SoDienThoai, ViDo, KinhDo, BanKinhGiao) VALUES
(N'Cơ sở 1 - Lê Trọng Tấn', N'140 Lê Trọng Tấn, Phường Tây Thạnh, Quận Tân Phú, Thành phố Hồ Chí Minh', '02838381111', 10.791234, 106.628123, 10),
(N'Cơ sở 2 - Quận 5',       N'25 Nguyễn Trãi, Phường 2, Quận 5, Thành phố Hồ Chí Minh',                  '02838382222', 10.754321, 106.663456, 10);
GO

-- Mat khau tat ca tai khoan: Demo@123 (ma hoa bcrypt)
-- 1: Super Admin, 2-3: Quan ly CN, 4-5: Tho in, 6-7: Shipper, 8-10: Khach hang, 11: Tho in 3 CN1
INSERT INTO NguoiDung (HoTen, Email, SoDienThoai, MatKhau, VaiTro, MaChiNhanh, DiaChi) VALUES
(N'Nguyễn Văn Chủ',       'ceo@photocopy.vn',    '0900000001', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'SUPER_ADMIN',  NULL, NULL),
(N'Trần Thị Quản',        'qlcn1@photocopy.vn',  '0900000002', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'BRANCH_ADMIN', 1, NULL),
(N'Lê Văn Quản',          'qlcn2@photocopy.vn',  '0900000003', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'BRANCH_ADMIN', 2, NULL),
(N'Phạm Văn Thợ',         'thoin1@photocopy.vn', '0900000004', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'STAFF',        1, NULL),
(N'Hoàng Thị Thợ',        'thoin2@photocopy.vn', '0900000005', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'STAFF',        2, NULL),
(N'Đỗ Văn Giao',          'ship1@photocopy.vn',  '0900000006', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'SHIPPER',      1, NULL),
(N'Bùi Văn Giao',         'ship2@photocopy.vn',  '0900000007', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'SHIPPER',      2, NULL),
(N'Nguyễn Thị Sinh Viên', 'sv1@huit.edu.vn',     '0910000001', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'CUSTOMER',     NULL, N'12 Tây Thạnh, Tân Phú, TP.HCM'),
(N'Trần Văn Học',         'sv2@huit.edu.vn',     '0910000002', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'CUSTOMER',     NULL, N'45 An Dương Vương, Quận 5, TP.HCM'),
(N'Lê Thị Sinh',          'sv3@huit.edu.vn',     '0910000003', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'CUSTOMER',     NULL, N'8 Kinh Dương Vương, Bình Tân, TP.HCM'),
(N'Nguyễn Văn Thợ 3',     'thoin3@photocopy.vn', '0900000008', '$2a$11$qVzmC2NjP8xRplNSmmrQ7eCYwcI3sRD68kUlImKk2M.GC2cq1vwr.', 'STAFF',        1, NULL);
GO

INSERT INTO MayIn (MaChiNhanh, TenMay, KhoGiay, InMau, In2Mat, TocDo) VALUES
(1, N'Ricoh MP2001',          'A3', 0, 1, 20),
(1, N'Canon iR-ADV C3525',    'A3', 1, 1, 25),
(2, N'Xerox WorkCentre 3345', 'A4', 0, 1, 40),
(1, N'HP LaserJet Pro M404dn','A4', 0, 1, 38);
GO

INSERT INTO VatTu (TenVatTu, DonViTinh, DonGia) VALUES
(N'Giấy A4',    N'Tờ',  120),
(N'Giấy A3',    N'Tờ',  240),
(N'Mực đen',    N'Hộp', 850000),
(N'Mực màu',    N'Hộp', 1200000),
(N'Bìa kiếng',  N'Cái', 2000),
(N'Lò xo',      N'Cái', 3500);
GO

-- Ma 1-12: in an (gia / trang), ma 13-16: gia cong (gia / cuon)
-- Bìa kiếng và lò xo giữ nhóm cũ; sp_TinhTien cho phép kết hợp riêng cặp này.
INSERT INTO DichVu (TenDichVu, LoaiDichVu, KhoGiay, LoaiGiay, MatIn, MauSac, NhomGiaCong, DonGia, MaVatTu) VALUES
(N'In A4 70gsm 1 mặt đen trắng', 'IN_AN', 'A4', '70gsm', '1_MAT', 'DEN_TRANG', NULL,  300, 1),
(N'In A4 70gsm 2 mặt đen trắng', 'IN_AN', 'A4', '70gsm', '2_MAT', 'DEN_TRANG', NULL,  250, 1),
(N'In A4 70gsm 1 mặt màu',       'IN_AN', 'A4', '70gsm', '1_MAT', 'MAU',       NULL, 3000, 1),
(N'In A4 80gsm 1 mặt đen trắng', 'IN_AN', 'A4', '80gsm', '1_MAT', 'DEN_TRANG', NULL,  400, 1),
(N'In A3 70gsm 1 mặt đen trắng', 'IN_AN', 'A3', '70gsm', '1_MAT', 'DEN_TRANG', NULL,  600, 2),
(N'In A3 70gsm 1 mặt màu',       'IN_AN', 'A3', '70gsm', '1_MAT', 'MAU',       NULL, 6000, 2),
(N'In A4 70gsm 2 mặt màu',       'IN_AN', 'A4', '70gsm', '2_MAT', 'MAU',       NULL, 2500, 1),
(N'In A4 80gsm 2 mặt đen trắng', 'IN_AN', 'A4', '80gsm', '2_MAT', 'DEN_TRANG', NULL,  350, 1),
(N'In A4 80gsm 1 mặt màu',       'IN_AN', 'A4', '80gsm', '1_MAT', 'MAU',       NULL, 3500, 1),
(N'In A4 80gsm 2 mặt màu',       'IN_AN', 'A4', '80gsm', '2_MAT', 'MAU',       NULL, 3000, 1),
(N'In A3 70gsm 2 mặt đen trắng', 'IN_AN', 'A3', '70gsm', '2_MAT', 'DEN_TRANG', NULL,  500, 2),
(N'In A3 70gsm 2 mặt màu',       'IN_AN', 'A3', '70gsm', '2_MAT', 'MAU',       NULL, 5000, 2),
(N'Đóng bìa kiếng',              'GIA_CONG', NULL, NULL, NULL, NULL, 'DONG_GAY',  5000, 5),
(N'Đóng lò xo',                  'GIA_CONG', NULL, NULL, NULL, NULL, 'DONG_GAY', 10000, 6),
(N'Ép plastic',                  'GIA_CONG', NULL, NULL, NULL, NULL, NULL,      15000, NULL),
(N'Bấm kim góc',                 'GIA_CONG', NULL, NULL, NULL, NULL, NULL,       1000, NULL);
GO

INSERT INTO BangGiaChiNhanh (MaChiNhanh, MaDichVu, DonGia, TrangThai)
SELECT cn.MaChiNhanh, dv.MaDichVu, dv.DonGia, dv.TrangThai
FROM ChiNhanh cn CROSS JOIN DichVu dv;
GO

INSERT INTO TonKho (MaChiNhanh, MaVatTu, SoLuong, MucToiThieu) VALUES
(1, 1, 50000, 10000), (1, 2, 20000, 5000), (1, 3, 8, 2), (1, 4, 1, 2), (1, 5, 30, 10), (1, 6, 30, 10),
(2, 1, 40000, 10000), (2, 2, 15000, 5000), (2, 3, 6, 2), (2, 4, 4, 2), (2, 5, 25, 10), (2, 6, 25, 10);
GO

INSERT INTO KhuyenMai (MaChiNhanh, MaCode, TenKhuyenMai, PhanTramGiam, GiamToiDa,
                       DonToiThieu, NgayBatDau, NgayKetThuc, SoLuong)
SELECT MaChiNhanh, 'MUATHI2026', N'Khuyến mãi mùa thi 2026', 20, 20000,
       50000, '2026-08-01', '2026-12-31', 200
FROM ChiNhanh;
GO

PRINT N'Da nhap du lieu mau';
GO
-- In anh the: seed sau khi danh muc, chi nhanh va ton kho goc da duoc tao.
-- Gia de xuat, don vi dong/anh; khong ghi de gia da sua cua chi nhanh.
DECLARE @VatTu INT;
SELECT TOP 1 @VatTu=MaVatTu FROM VatTu WHERE TenVatTu=N'Giấy ảnh A4';
IF @VatTu IS NULL
BEGIN
    INSERT VatTu(TenVatTu,DonViTinh,DonGia) VALUES(N'Giấy ảnh A4',N'tờ',0);
    SET @VatTu=SCOPE_IDENTITY();
END;
DECLARE @Gia TABLE(Ma VARCHAR(10), Ten NVARCHAR(100), Gia DECIMAL(12,0));
INSERT @Gia VALUES
('2X3',N'In ảnh thẻ 2x3 cm',3000),
('3X4',N'In ảnh thẻ 3x4 cm',5000),
('4X6',N'In ảnh thẻ 4x6 cm',10000),
('35X45',N'In ảnh thẻ 3,5x4,5 cm',7000),
('5X5',N'In ảnh thẻ 5x5 cm',10000);
INSERT DichVu(TenDichVu,LoaiDichVu,KhoGiay,LoaiGiay,MatIn,MauSac,DonGia,MaVatTu,TrangThai,KichThuocAnh)
SELECT g.Ten,'IN_AN','A4','GIAY_ANH','1_MAT','MAU',g.Gia,@VatTu,N'HIEN',g.Ma
FROM @Gia g WHERE NOT EXISTS(SELECT 1 FROM DichVu dv WHERE dv.KichThuocAnh=g.Ma);
INSERT BangGiaChiNhanh(MaChiNhanh,MaDichVu,DonGia,TrangThai)
SELECT cn.MaChiNhanh,dv.MaDichVu,dv.DonGia,N'HIEN'
FROM ChiNhanh cn CROSS JOIN DichVu dv
WHERE dv.KichThuocAnh IS NOT NULL
  AND NOT EXISTS(SELECT 1 FROM BangGiaChiNhanh bg WHERE bg.MaChiNhanh=cn.MaChiNhanh AND bg.MaDichVu=dv.MaDichVu);
-- Ton ban dau = 0: quan ly nhap va nhan giay anh truoc khi in, khong tao ton gia.
INSERT TonKho(MaChiNhanh,MaVatTu,SoLuong,MucToiThieu)
SELECT MaChiNhanh,@VatTu,0,10 FROM ChiNhanh cn
WHERE NOT EXISTS(SELECT 1 FROM TonKho tk WHERE tk.MaChiNhanh=cn.MaChiNhanh AND tk.MaVatTu=@VatTu);
GO
