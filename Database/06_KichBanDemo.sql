/* =============================================================
   FILE 06: KICH BAN CHAY THU (chay sau file 00 -> 05)
   ============================================================= */

USE PhotocopyChain;
GO

DECLARE @Don1 INT, @Don2 INT, @Don3 INT, @Don4 INT, @Ca1 INT, @Ca2 INT, @Ca3 INT, @MaGiao INT;
DECLARE @DS1 Kieu_DongDonHang, @DS2 Kieu_DongDonHang, @DS3 Kieu_DongDonHang, @DS4 Kieu_DongDonHang;
-- @GC1: gia cong cua don 1 (chon nhieu); @GCRong: don khong chon gia cong nao
DECLARE @GC1 Kieu_DongGiaCong, @GCRong Kieu_DongGiaCong;

-- Khach hang tai file len
INSERT INTO FileIn (MaKhachHang, TenFile, DinhDang, DuongDan, SoTrang) VALUES
(8,  N'BaoCaoDoAn.pdf',     'pdf', '/files/1.pdf', 50),
(9,  N'LuanVan.pdf',        'pdf', '/files/2.pdf', 30),
(9,  N'HopDong.pdf',        'pdf', '/files/3.pdf', 40),
(10, N'DeCuong.pdf',        'pdf', '/files/4.pdf', 200);

PRINT N'--- 1. Tho in mo ca';
EXEC sp_MoCa @MaNhanVien = 4, @TienDauCa = 200000, @MaCa = @Ca1 OUTPUT;

PRINT N'--- 2. Bao gia va dat don nhan tai quay, thanh toan VietQR';
-- 50 trang A4 2 mat x 2 ban = 25.000; chon 2 gia cong: lo xo 2 x 10.000 + ep plastic 2 x 15.000 = 50.000 -> 75.000
INSERT INTO @DS1 VALUES (1, 2, 50, 2);
INSERT INTO @GC1 VALUES (1, 14), (1, 15);
EXEC sp_BaoGia @MaChiNhanh = 1, @HinhThucNhan = 'TAI_QUAY', @DsFile = @DS1, @DsGiaCong = @GC1;
EXEC sp_TaoDonHang @MaKhachHang = 8, @MaChiNhanh = 1, @HinhThucNhan = 'TAI_QUAY', @PhuongThuc = 'VIETQR',
     @DsFile = @DS1, @DsGiaCong = @GC1, @MaDonHang = @Don1 OUTPUT;
EXEC sp_XacNhanChuyenKhoan @MaDonHang = @Don1, @MaGiaoDich = 'GD-DEMO-TAI-QUAY-01';

-- Chan chon 2 kieu dong gay cung luc (lo xo 14 + bia kieng 13)
DECLARE @GCSai Kieu_DongGiaCong;
INSERT INTO @GCSai VALUES (1, 14), (1, 13);
BEGIN TRY
    EXEC sp_BaoGia @MaChiNhanh = 1, @HinhThucNhan = 'TAI_QUAY', @DsFile = @DS1, @DsGiaCong = @GCSai;
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH

PRINT N'--- 3. Tiep nhan, phan may, in, giao tai quay';
EXEC sp_TiepNhanDon @MaDonHang = @Don1, @MaNhanVien = 4;
BEGIN TRY
    EXEC sp_PhanCongMayIn @MaDonHang = @Don1, @MaMayIn = 4;   -- may 4 khong in 2 mat
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
EXEC sp_PhanCongMayIn @MaDonHang = @Don1, @MaMayIn = 1;
EXEC sp_CapNhatTrangThaiIn @MaDonHang = @Don1, @TrangThaiMoi = 'DANG_IN';
EXEC sp_CapNhatTrangThaiIn @MaDonHang = @Don1, @TrangThaiMoi = 'DA_IN';
EXEC sp_GiaoTaiQuay @MaDonHang = @Don1, @MaNhanVien = 4;
EXEC sp_DanhGia @MaDonHang = @Don1, @MaKhachHang = 8, @SoSao = 5, @NhanXet = N'In đẹp, nhanh';
SELECT * FROM vw_DonHang WHERE MaDonHang = @Don1;

PRINT N'--- 4. Don giao hang, chuyen khoan VietQR';
-- Trong chay that Backend lay @KhoangCach + @PhiShipApi tu Ahamove; kich ban SQL dung so mau de test thu tuc.
-- 30 trang A4 1 mat = 9.000 + phi Ahamove gia lap 15.000
INSERT INTO @DS2 VALUES (2, 1, 30, 1);
BEGIN TRY
    EXEC sp_TaoDonHang @MaKhachHang = 9, @MaChiNhanh = 1, @HinhThucNhan = 'GIAO_HANG', @PhuongThuc = 'VIETQR',
         @DiaChiGiao = N'Quận 7', @SdtNhan = '0910000002', @KhoangCach = 12.5, @PhiShipApi = 30000,
         @DsFile = @DS2, @DsGiaCong = @GCRong, @MaDonHang = @Don2 OUTPUT;
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
EXEC sp_TaoDonHang @MaKhachHang = 9, @MaChiNhanh = 1, @HinhThucNhan = 'GIAO_HANG', @PhuongThuc = 'VIETQR',
     @DiaChiGiao = N'45 An Dương Vương, Quận 5', @SdtNhan = '0910000002', @KhoangCach = 2.6, @PhiShipApi = 15000,
     @DsFile = @DS2, @DsGiaCong = @GCRong, @MaDonHang = @Don2 OUTPUT;
EXEC sp_XacNhanChuyenKhoan @MaDonHang = @Don2, @MaGiaoDich = 'FT2609001';
EXEC sp_TiepNhanDon @MaDonHang = @Don2, @MaNhanVien = 4;
EXEC sp_PhanCongMayIn @MaDonHang = @Don2, @MaMayIn = 1;
EXEC sp_CapNhatTrangThaiIn @MaDonHang = @Don2, @TrangThaiMoi = 'DANG_IN';
EXEC sp_CapNhatTrangThaiIn @MaDonHang = @Don2, @TrangThaiMoi = 'DA_IN';
BEGIN TRY
    EXEC sp_NhanDonGiao @MaDonHang = @Don2, @MaShipper = 7;   -- shipper chi nhanh khac
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
EXEC sp_NhanDonGiao @MaDonHang = @Don2, @MaShipper = 6;
SELECT @MaGiao = MaGiaoHang FROM GiaoHang WHERE MaDonHang = @Don2 AND TrangThai = 'DANG_GIAO';
EXEC sp_CapNhatGiaoHang @MaGiaoHang = @MaGiao, @ThanhCong = 1;
SELECT * FROM vw_DonHang WHERE MaDonHang = @Don2;

PRINT N'--- 5. Don COD: giao that bai 1 lan, lan 2 thanh cong, shipper nop tien ve';
-- 40 trang = 12.000 + phi Ahamove gia lap 15.000 = 27.000
INSERT INTO @DS3 VALUES (3, 1, 40, 1);
EXEC sp_TaoDonHang @MaKhachHang = 9, @MaChiNhanh = 1, @HinhThucNhan = 'GIAO_HANG', @PhuongThuc = 'COD',
     @DiaChiGiao = N'45 An Dương Vương, Quận 5', @SdtNhan = '0910000002', @KhoangCach = 2.6, @PhiShipApi = 15000,
     @DsFile = @DS3, @DsGiaCong = @GCRong, @MaDonHang = @Don3 OUTPUT;
EXEC sp_TiepNhanDon @MaDonHang = @Don3, @MaNhanVien = 4;
EXEC sp_PhanCongMayIn @MaDonHang = @Don3, @MaMayIn = 1;
EXEC sp_CapNhatTrangThaiIn @MaDonHang = @Don3, @TrangThaiMoi = 'DANG_IN';
EXEC sp_CapNhatTrangThaiIn @MaDonHang = @Don3, @TrangThaiMoi = 'DA_IN';
EXEC sp_NhanDonGiao @MaDonHang = @Don3, @MaShipper = 6;
SELECT @MaGiao = MaGiaoHang FROM GiaoHang WHERE MaDonHang = @Don3 AND TrangThai = 'DANG_GIAO';
EXEC sp_CapNhatGiaoHang @MaGiaoHang = @MaGiao, @ThanhCong = 0, @LyDo = N'Khách không nghe máy';
EXEC sp_NhanDonGiao @MaDonHang = @Don3, @MaShipper = 6;
SELECT @MaGiao = MaGiaoHang FROM GiaoHang WHERE MaDonHang = @Don3 AND TrangThai = 'DANG_GIAO';
EXEC sp_CapNhatGiaoHang @MaGiaoHang = @MaGiao, @ThanhCong = 1;
BEGIN TRY
    EXEC sp_NhanTienCOD @MaDonHang = @Don3, @MaQuanLy = 6;   -- shipper tu nhan tien cua minh
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
EXEC sp_MoCa @MaNhanVien = 2, @TienDauCa = 200000, @MaCa = @Ca2 OUTPUT;
EXEC sp_NhanTienCOD @MaDonHang = @Don3, @MaQuanLy = 2;
SELECT * FROM GiaoHang WHERE MaDonHang = @Don3;

PRINT N'--- 6. Dong ca va xac nhan';
-- Don 1 da tra VietQR nen 75.000 khong vao ket. Ca 1: 200.000, dem duoc 195.000 -> thieu 5.000
-- (da chi 2.000 mua bang keo, nhan vien bu 3.000)
EXEC sp_DongCa @MaCa = @Ca1, @TongDoanhThuCuoiCa = 195000;
BEGIN TRY
    EXEC sp_XacNhanCa @MaCa = @Ca1, @MaQuanLy = 2, @TienChi = 2000, @TienBu = 0, @GhiChu = N'Mua băng keo';
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
EXEC sp_XacNhanCa @MaCa = @Ca1, @MaQuanLy = 2, @TienChi = 2000, @TienBu = 3000, @GhiChu = N'Mua băng keo';
-- Ca 2 cua quan ly: phai do Super Admin xac nhan
EXEC sp_DongCa @MaCa = @Ca2, @TongDoanhThuCuoiCa = 227000;
EXEC sp_XacNhanCa @MaCa = @Ca2, @MaQuanLy = 1;
SELECT * FROM vw_CaLamViec;

PRINT N'--- 7. Nhap kho';
DECLARE @MaPhieu INT;
EXEC sp_TaoPhieuNhap @MaChiNhanh = 1, @MaVatTu = 3, @NhaCungCap = N'Công ty Giấy Việt Nhật', @SoLuong = 10, @DonGia = 800000, @MaNguoiLap = 2;
SET @MaPhieu = (SELECT MAX(MaPhieuNhap) FROM PhieuNhap);
BEGIN TRY
    EXEC sp_DuyetPhieuNhap @MaPhieuNhap = @MaPhieu, @MaNguoiDuyet = 2, @DongY = 1;   -- quan ly tu duyet
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
EXEC sp_DuyetPhieuNhap @MaPhieuNhap = @MaPhieu, @MaNguoiDuyet = 1, @DongY = 1;
EXEC sp_NhapKho @MaPhieuNhap = @MaPhieu, @SoLuongThucNhan = 7;
SELECT * FROM TonKho WHERE MaChiNhanh = 1 AND MaVatTu = 3;

PRINT N'--- 7b. Sua thang vao bang (khong qua thu tuc) cung bi trigger chan';
BEGIN TRY
    UPDATE CaLamViec SET MaNguoiXacNhan = 4 WHERE MaCa = @Ca2;              -- tho in xac nhan ca
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY
    UPDATE CaLamViec SET MaNguoiXacNhan = 3 WHERE MaCa = @Ca1;              -- quan ly chi nhanh 2 xac nhan ca chi nhanh 1
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY
    INSERT INTO PhieuNhap (MaChiNhanh, MaVatTu, NhaCungCap, SoLuong, DonGia, MaNguoiLap)
    VALUES (1, 1, N'NCC A', 100, 120, 5);                                   -- tho in chi nhanh 2 lap phieu
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY
    UPDATE PhieuNhap SET MaNguoiDuyet = 3 WHERE MaPhieuNhap = @MaPhieu;     -- quan ly duyet phieu
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
SELECT MaCa, MaNguoiXacNhan FROM CaLamViec;                                 -- van la 2 va 1, khong doi
SELECT MaPhieuNhap, MaNguoiLap, MaNguoiDuyet FROM PhieuNhap;                -- van la 2 va 1

PRINT N'--- 8. Don tien mat tai quay: sai chi nhanh bi chan, dung chi nhanh thu tien vao ca';
-- 200 trang A4 1 mat = 60.000, giam 20% = 12.000
INSERT INTO @DS4 VALUES (4, 1, 200, 1);
-- Don nay do nhan vien lap tai quay; khach tren App van chi dung VietQR.
EXEC sp_TaoDonHang @MaKhachHang = 10, @MaChiNhanh = 1, @HinhThucNhan = 'TAI_QUAY', @PhuongThuc = 'COD',
     @MaCode = 'MUATHI2026', @DsFile = @DS4, @DsGiaCong = @GCRong, @MaDonHang = @Don4 OUTPUT;
SELECT TienHang, GiamGia, TongTien FROM DonHang WHERE MaDonHang = @Don4;
-- Ca 1 da dong o buoc 6 nen tho in phai mo ca moi truoc khi nhan don tiep theo
EXEC sp_MoCa @MaNhanVien = 4, @TienDauCa = 200000, @MaCa = @Ca3 OUTPUT;
EXEC sp_TiepNhanDon @MaDonHang = @Don4, @MaNhanVien = 4;
EXEC sp_PhanCongMayIn @MaDonHang = @Don4, @MaMayIn = 1;
EXEC sp_CapNhatTrangThaiIn @MaDonHang = @Don4, @TrangThaiMoi = 'DANG_IN';
EXEC sp_CapNhatTrangThaiIn @MaDonHang = @Don4, @TrangThaiMoi = 'DA_IN';
BEGIN TRY
    EXEC sp_GiaoTaiQuay @MaDonHang = @Don4, @MaNhanVien = 5; -- nhan vien chi nhanh 2
END TRY
BEGIN CATCH PRINT N'Bi chan: ' + ERROR_MESSAGE(); END CATCH
EXEC sp_GiaoTaiQuay @MaDonHang = @Don4, @MaNhanVien = 4;
SELECT dh.TrangThai, tt.TrangThai AS TrangThaiThanhToan, tt.MaCa, ca.DoanhThuTrongCa
FROM DonHang dh
JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
LEFT JOIN CaLamViec ca ON ca.MaCa = tt.MaCa
WHERE dh.MaDonHang = @Don4;
SELECT MaCode, DaDung FROM KhuyenMai;

PRINT N'--- 9. Doi soat may in (may 1 da in 100 + 30 + 40 + 200 = 370 trang)';
EXEC sp_CapNhatMayIn @MaMayIn = 1, @TrangThai = N'HOAT_DONG', @SoTrangDaIn = 370, @MaQuanLy = 2;
SELECT * FROM vw_DoiSoatMayIn;

PRINT N'--- 10. Bao cao';
SELECT * FROM vw_DoanhThuNgay;
SELECT * FROM vw_DanhGiaChiNhanh;
SELECT * FROM vw_HieuSuatShipper;
SELECT * FROM vw_CanhBaoTonKho;
GO
