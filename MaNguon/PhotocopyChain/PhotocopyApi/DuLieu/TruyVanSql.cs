namespace PhotocopyApi.DuLieu;

// Chỉ tập trung các câu SELECT dài để Controller dễ đọc:
// Controller giữ luồng HTTP + quyền + nghiệp vụ, file này giữ chi tiết lấy dữ liệu.
// Các câu SQL một dòng đơn giản vẫn để gần nơi sử dụng để khỏi phải nhảy file vô ích.
internal static class TruyVanSql
{
    internal static class DanhMuc
    {
        public const string ChiNhanhDangHoatDong = @"
            SELECT cn.MaChiNhanh, cn.TenChiNhanh, cn.DiaChi, cn.SoDienThoai,
                   cn.GioMoCua, cn.GioDongCua, cn.BanKinhGiao,
                   dg.DiemTrungBinh AS DiemDanhGiaTB, dg.SoLuotDanhGia
            FROM ChiNhanh cn
            JOIN vw_DanhGiaChiNhanh dg ON dg.MaChiNhanh = cn.MaChiNhanh
            WHERE cn.TrangThai = N'HOAT_DONG'
            ORDER BY cn.MaChiNhanh";
    }

    internal static class DonHang
    {
        public const string CuaKhachHang = @"
            SELECT dh.MaDonHang,
                   'DH' + RIGHT('00000' + CAST(dh.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                   dh.MaChiNhanh, cn.TenChiNhanh, dh.HinhThucNhan, dh.DiaChiGiao,
                   dh.InGap AS InHoaToc, dh.TrangThai AS TrangThaiDonHang,
                   tt.PhuongThuc AS PhuongThucThanhToan,
                   tt.TrangThai AS TrangThaiThanhToan,
                   dh.TongTien, dh.NgayDat, dh.NgayHoanThanh AS NgayHoanTat,
                   dh.NgayHenTra AS ThoiGianDuKienHoanThanh,
                   (SELECT COUNT(*) FROM ChiTietDonHang c
                    WHERE c.MaDonHang = dh.MaDonHang) AS SoFile,
                   (SELECT ISNULL(SUM(c.SoTrang * c.SoBan), 0) FROM ChiTietDonHang c
                    WHERE c.MaDonHang = dh.MaDonHang) AS TongSoTrang,
                   (SELECT TOP 1 f.TenFile
                    FROM ChiTietDonHang c
                    JOIN FileIn f ON f.MaFile = c.MaFile
                    WHERE c.MaDonHang = dh.MaDonHang
                    ORDER BY c.MaChiTiet) AS TenFileDau,
                   CAST(CASE WHEN EXISTS (
                       SELECT 1 FROM DanhGia d WHERE d.MaDonHang = dh.MaDonHang
                   ) THEN 1 ELSE 0 END AS BIT) AS DaDanhGia
            FROM DonHang dh
            JOIN ChiNhanh cn ON cn.MaChiNhanh = dh.MaChiNhanh
            LEFT JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
            WHERE dh.MaKhachHang = @MaKhachHang
              AND ISNULL(dh.LoaiPhatSinh, '') <> 'IN_LAI_LOI_QUAY'
            ORDER BY dh.NgayDat DESC, dh.MaDonHang DESC";

        public const string ChiTiet = @"
            SELECT dh.MaDonHang,
                   'DH' + RIGHT('00000' + CAST(dh.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                   dh.MaDonHangGoc,
                   CASE WHEN dh.MaDonHangGoc IS NULL THEN NULL
                        ELSE 'DH' + RIGHT('00000' + CAST(dh.MaDonHangGoc AS VARCHAR(10)), 5) END AS MaDonHangGocHienThi,
                   dh.LoaiPhatSinh, dh.LyDoPhatSinh,
                   dh.MaChiNhanh, cn.TenChiNhanh,
                   cn.DiaChi AS DiaChiChiNhanh, cn.SoDienThoai AS SoDienThoaiChiNhanh,
                   dh.MaKhachHang, kh.HoTen AS TenKhachHang,
                   kh.SoDienThoai AS SoDienThoaiKhach,
                   dh.HinhThucNhan, dh.DiaChiGiao, dh.SdtNhan,
                   dh.InGap AS InHoaToc, dh.TrangThai AS TrangThaiDonHang, dh.GhiChu,
                   dh.TienHang, dh.PhiShip, dh.GiamGia, dh.TongTien,
                   tt.PhuongThuc AS PhuongThucThanhToan,
                   tt.TrangThai AS TrangThaiThanhToan,
                   nv.HoTen AS TenNhanVien, m.TenMay AS TenMayIn,
                   sp.HoTen AS TenShipper, sp.SoDienThoai AS SoDienThoaiShipper,
                   dh.NgayDat, dh.NgayHenTra, dh.NgayHoanThanh,
                   (SELECT ISNULL(SUM(c.SoTrang * c.SoBan), 0)
                    FROM ChiTietDonHang c
                    WHERE c.MaDonHang = dh.MaDonHang) AS TongSoTrang,
                   CAST(CASE WHEN EXISTS (
                       SELECT 1 FROM DanhGia d WHERE d.MaDonHang = dh.MaDonHang
                   ) THEN 1 ELSE 0 END AS BIT) AS DaDanhGia
            FROM DonHang dh
            JOIN ChiNhanh cn ON cn.MaChiNhanh = dh.MaChiNhanh
            JOIN NguoiDung kh ON kh.MaNguoiDung = dh.MaKhachHang
            LEFT JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
            LEFT JOIN NguoiDung nv ON nv.MaNguoiDung = dh.MaNhanVien
            LEFT JOIN MayIn m ON m.MaMayIn = dh.MaMayIn
            LEFT JOIN GiaoHang gh ON gh.MaGiaoHang = (
                SELECT MAX(MaGiaoHang) FROM GiaoHang g WHERE g.MaDonHang = dh.MaDonHang
            )
            LEFT JOIN NguoiDung sp ON sp.MaNguoiDung = gh.MaShipper
            WHERE dh.MaDonHang = @MaDonHang";

        public const string DongInChiTiet = @"
            SELECT ct.MaChiTiet, ct.MaFile, ct.MaDichVu, f.TenFile, f.DungLuong,
                   f.SoTrang AS TongSoTrangFile,
                   f.TrangThai AS TrangThaiFile,
                   dv.TenDichVu, dv.KhoGiay, dv.LoaiGiay, dv.MatIn, dv.MauSac,
                   ct.SoTrang, ct.TuTrang, ct.DenTrang, ct.SoBan, ct.HuongGiay, ct.DonGia, ct.ThanhTien,
                   STUFF((
                       SELECT N', ' + gc.TenDichVu
                       FROM ChiTietGiaCong g
                       JOIN DichVu gc ON gc.MaDichVu = g.MaDichVu
                       WHERE g.MaChiTiet = ct.MaChiTiet
                       ORDER BY gc.MaDichVu
                       FOR XML PATH(''), TYPE
                   ).value('.', 'NVARCHAR(500)'), 1, 2, '') AS TenGiaCong
            FROM ChiTietDonHang ct
            JOIN FileIn f ON f.MaFile = ct.MaFile
            JOIN DichVu dv ON dv.MaDichVu = ct.MaDichVu
            WHERE ct.MaDonHang = @MaDonHang
            ORDER BY ct.MaChiTiet";

        public const string HangDoi = @"
            SELECT dh.MaDonHang,
                   'DH' + RIGHT('00000' + CAST(dh.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                   dh.MaDonHangGoc,
                   CASE WHEN dh.MaDonHangGoc IS NULL THEN NULL
                        ELSE 'DH' + RIGHT('00000' + CAST(dh.MaDonHangGoc AS VARCHAR(10)), 5) END AS MaDonHangGocHienThi,
                   dh.LoaiPhatSinh,
                   dh.MaChiNhanh, cn.TenChiNhanh,
                   kh.HoTen AS TenKhachHang, kh.SoDienThoai AS SoDienThoaiKhach,
                   dh.HinhThucNhan, dh.InGap AS InHoaToc,
                   dh.TrangThai AS TrangThaiDonHang,
                   tt.PhuongThuc AS PhuongThucThanhToan,
                   tt.TrangThai AS TrangThaiThanhToan,
                   dh.TongTien, dh.NgayDat, dh.NgayHenTra,
                   nv.HoTen AS TenNhanVien, m.TenMay AS TenMayIn,
                   (SELECT COUNT(*) FROM ChiTietDonHang c
                    WHERE c.MaDonHang = dh.MaDonHang) AS SoFile,
                   (SELECT ISNULL(SUM(c.SoTrang * c.SoBan), 0) FROM ChiTietDonHang c
                    WHERE c.MaDonHang = dh.MaDonHang) AS TongSoTrang,
                   (SELECT TOP 1
                        dv.KhoGiay + ' ' + dv.LoaiGiay + N' · ' +
                        CASE WHEN dv.MatIn = '2_MAT' THEN N'2 mặt' ELSE N'1 mặt' END + N' · ' +
                        CASE WHEN dv.MauSac = 'MAU' THEN N'In màu' ELSE N'Đen trắng' END + N' · ' +
                        CASE WHEN c.HuongGiay = 'NGANG' THEN N'Giấy ngang' ELSE N'Giấy đứng' END
                    FROM ChiTietDonHang c
                    JOIN DichVu dv ON dv.MaDichVu = c.MaDichVu
                    WHERE c.MaDonHang = dh.MaDonHang
                    ORDER BY c.MaChiTiet) AS QuyCach
            FROM DonHang dh
            JOIN ChiNhanh cn ON cn.MaChiNhanh = dh.MaChiNhanh
            JOIN NguoiDung kh ON kh.MaNguoiDung = dh.MaKhachHang
            LEFT JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
            LEFT JOIN NguoiDung nv ON nv.MaNguoiDung = dh.MaNhanVien
            LEFT JOIN MayIn m ON m.MaMayIn = dh.MaMayIn
            WHERE (@MaChiNhanh IS NULL OR dh.MaChiNhanh = @MaChiNhanh)
              AND (dh.TrangThai NOT IN ('HOAN_THANH','DA_HUY')
                   OR dh.NgayDat > DATEADD(DAY, -7, GETDATE()))
            ORDER BY dh.NgayDat DESC, dh.MaDonHang DESC";

        public const string YeuCauMayIn = @"
            SELECT CAST(MAX(CASE WHEN dv.KhoGiay = 'A3' THEN 1 ELSE 0 END) AS BIT) AS CanA3,
                   CAST(MAX(CASE WHEN dv.MauSac = 'MAU' THEN 1 ELSE 0 END) AS BIT) AS CanMau,
                   CAST(MAX(CASE WHEN dv.MatIn = '2_MAT' THEN 1 ELSE 0 END) AS BIT) AS Can2Mat
            FROM ChiTietDonHang ct
            JOIN DichVu dv ON dv.MaDichVu = ct.MaDichVu
            WHERE ct.MaDonHang = @MaDonHang";

        public const string FileTrongDon = @"
            SELECT dh.MaChiNhanh, f.TenFile, f.DuongDan, f.TrangThai
            FROM ChiTietDonHang ct
            JOIN DonHang dh ON dh.MaDonHang = ct.MaDonHang
            JOIN FileIn f ON f.MaFile = ct.MaFile
            WHERE ct.MaDonHang = @MaDonHang AND ct.MaFile = @MaFile";

        public const string FileCoTheXoa = @"
            SELECT DISTINCT f.MaFile, f.DuongDan
            FROM ChiTietDonHang ct
            JOIN FileIn f ON f.MaFile = ct.MaFile
            WHERE ct.MaDonHang = @MaDonHang
              AND f.TrangThai = N'SAN_SANG'
              AND f.DuongDan IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM ChiTietDonHang ct2
                  JOIN DonHang dh2 ON dh2.MaDonHang = ct2.MaDonHang
                  WHERE ct2.MaFile = f.MaFile
                    AND dh2.TrangThai NOT IN ('HOAN_THANH','DA_HUY')
              )";
    }

    internal static class CaLamViec
    {
        // QR chưa lưu MaCa: chọn đúng một ca, ưu tiên nhân viên tiếp nhận đơn.
        // Đơn chưa có nhân viên thì lấy ca mở gần nhất; MaCa dùng để phân định khi trùng giờ.
        private const string CaCuaThanhToanVietQr = @"
            (SELECT TOP 1 c.MaCa
             FROM CaLamViec c
             WHERE c.MaChiNhanh = dh.MaChiNhanh
               AND tt.NgayThanhToan >= c.GioMoCa
               AND tt.NgayThanhToan <= ISNULL(c.GioDongCa, GETDATE())
             ORDER BY CASE WHEN c.MaNhanVien = dh.MaNhanVien THEN 0 ELSE 1 END,
                      c.GioMoCa DESC, c.MaCa DESC)";

        private const string ThongTin = @"
            SELECT ca.MaCa, ca.MaChiNhanh, cn.TenChiNhanh,
                   ca.MaNhanVien, nv.HoTen AS TenNhanVien,
                   ca.GioMoCa, ca.GioDongCa, ca.TienDauCa,
                   ca.DoanhThuTrongCa, ca.TongDoanhThuCuoiCa, ca.TienQuanLyDemCuoiCa,
                   ca.TienChi, ca.TienBu, ca.GhiChu, ca.TrangThai,
                   xn.HoTen AS TenNguoiXacNhan,
                   (SELECT COUNT(*) FROM ThanhToan tt
                    WHERE tt.MaCa = ca.MaCa) AS SoDonDaThu,
                   (SELECT COUNT(*)
                    FROM ThanhToan tt
                    JOIN DonHang dh ON dh.MaDonHang = tt.MaDonHang
                    WHERE dh.MaChiNhanh = ca.MaChiNhanh
                      AND tt.PhuongThuc = 'VIETQR'
                      AND tt.TrangThai = 'DA_THANH_TOAN'
                      AND tt.NgayThanhToan >= ca.GioMoCa
                      AND tt.NgayThanhToan <= ISNULL(ca.GioDongCa, GETDATE())
                      AND ca.MaCa = " + CaCuaThanhToanVietQr + @") AS SoDonVietQr,
                   (SELECT ISNULL(SUM(tt.SoTien), 0)
                    FROM ThanhToan tt
                    JOIN DonHang dh ON dh.MaDonHang = tt.MaDonHang
                    WHERE dh.MaChiNhanh = ca.MaChiNhanh
                      AND tt.PhuongThuc = 'VIETQR'
                      AND tt.TrangThai = 'DA_THANH_TOAN'
                      AND tt.NgayThanhToan >= ca.GioMoCa
                      AND tt.NgayThanhToan <= ISNULL(ca.GioDongCa, GETDATE())
                      AND ca.MaCa = " + CaCuaThanhToanVietQr + @") AS DoanhThuVietQr
            FROM CaLamViec ca
            JOIN ChiNhanh cn ON cn.MaChiNhanh = ca.MaChiNhanh
            JOIN NguoiDung nv ON nv.MaNguoiDung = ca.MaNhanVien
            LEFT JOIN NguoiDung xn ON xn.MaNguoiDung = ca.MaNguoiXacNhan ";

        public const string DangMoCuaNhanVien = ThongTin + @"
            WHERE ca.MaNhanVien = @MaNhanVien AND ca.TrangThai = 'DANG_MO'
              AND (@MaChiNhanh IS NULL OR ca.MaChiNhanh = @MaChiNhanh)";

        public const string ChuaKetThucCuaChiNhanh = ThongTin + @"
            WHERE ca.MaChiNhanh = @MaChiNhanh
              AND ca.TrangThai = 'DANG_MO'
              AND ca.MaNhanVien = @MaNhanVien
            ORDER BY CASE WHEN ca.TrangThai = 'DANG_MO' THEN 0 ELSE 1 END,
                     ca.MaCa DESC";

        public const string GanDayCuaChiNhanh = ThongTin + @"
            WHERE (@MaChiNhanh IS NULL OR ca.MaChiNhanh = @MaChiNhanh)
              AND (@MaNhanVien IS NULL OR ca.MaNhanVien = @MaNhanVien)
              AND ca.GioMoCa > DATEADD(DAY, -7, GETDATE())
            ORDER BY ca.GioMoCa DESC";

        public const string TheoMa = ThongTin + @"
            WHERE ca.MaCa = @MaCa";

        public const string ChoXacNhan = ThongTin + @"
            WHERE ca.TrangThai = 'CHO_XAC_NHAN' ";

        public const string DonHangTrongCa = @"
            SELECT ca.MaCa, ca.MaChiNhanh, cn.TenChiNhanh,
                   ca.MaNhanVien, nv.HoTen AS TenNhanVien,
                   ca.GioMoCa, ca.GioDongCa, tt.MaDonHang,
                   'DH' + RIGHT('00000' + CAST(tt.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                   kh.HoTen AS TenKhachHang, tt.PhuongThuc, tt.SoTien,
                   tt.NgayThanhToan, dh.TrangThai AS TrangThaiDonHang
            FROM CaLamViec ca
            JOIN ChiNhanh cn ON cn.MaChiNhanh = ca.MaChiNhanh
            JOIN NguoiDung nv ON nv.MaNguoiDung = ca.MaNhanVien
            JOIN DonHang dh ON dh.MaChiNhanh = ca.MaChiNhanh
            JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
            JOIN NguoiDung kh ON kh.MaNguoiDung = dh.MaKhachHang
            WHERE ca.MaCa = @MaCa AND (@MaChiNhanh IS NULL OR ca.MaChiNhanh = @MaChiNhanh)
              AND (@MaNhanVien IS NULL OR dh.MaNhanVien = @MaNhanVien)
              AND tt.TrangThai = 'DA_THANH_TOAN'
              AND (
                    (tt.PhuongThuc IN ('COD','TIEN_MAT') AND tt.MaCa = ca.MaCa)
                    OR
                    (tt.PhuongThuc = 'VIETQR'
                     AND tt.NgayThanhToan >= ca.GioMoCa
                     AND tt.NgayThanhToan <= ISNULL(ca.GioDongCa, GETDATE())
                     AND ca.MaCa = " + CaCuaThanhToanVietQr + @")
                  )
            ORDER BY tt.NgayThanhToan DESC, tt.MaThanhToan DESC";

        // Truy vấn lịch sử bắt đầu từ ThanhToan để mỗi giao dịch chỉ có một dòng.
        // Tiền mặt đã lưu MaCa nên ghép thẳng. VietQR không có MaCa, vì vậy CROSS APPLY
        // tìm một ca cùng chi nhánh bao phủ thời điểm trả tiền; TOP 1 chống lặp nếu ca chồng giờ.
        // Nếu nhân viên xử lý đơn cũng có ca phù hợp thì CASE trong ORDER BY ưu tiên ca của họ.
        public const string LichSuDonHangTrongCa = @"
            SELECT ca.MaCa, dh.MaChiNhanh, cn.TenChiNhanh,
                   ca.MaNhanVien, nv.HoTen AS TenNhanVien,
                   ca.GioMoCa, ca.GioDongCa, tt.MaDonHang,
                   'DH' + RIGHT('00000' + CAST(tt.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                   kh.HoTen AS TenKhachHang, tt.PhuongThuc, tt.SoTien,
                   tt.NgayThanhToan, dh.TrangThai AS TrangThaiDonHang
            FROM ThanhToan tt
            JOIN DonHang dh ON dh.MaDonHang = tt.MaDonHang
            JOIN ChiNhanh cn ON cn.MaChiNhanh = dh.MaChiNhanh
            JOIN NguoiDung kh ON kh.MaNguoiDung = dh.MaKhachHang
            CROSS APPLY (
                -- CROSS APPLY chạy phần này riêng cho từng dòng thanh toán tt.
                SELECT TOP 1 c.MaCa, c.MaNhanVien, c.GioMoCa, c.GioDongCa
                FROM CaLamViec c
                WHERE c.MaChiNhanh = dh.MaChiNhanh
                  AND (
                        (tt.PhuongThuc IN ('COD','TIEN_MAT') AND tt.MaCa = c.MaCa)
                        OR
                        (tt.PhuongThuc = 'VIETQR'
                         AND tt.NgayThanhToan >= c.GioMoCa
                         AND tt.NgayThanhToan <= ISNULL(c.GioDongCa, GETDATE()))
                      )
                -- Số 0 đứng trước số 1: ưu tiên ca của đúng người đã nhận xử lý đơn.
                ORDER BY CASE WHEN c.MaNhanVien = dh.MaNhanVien THEN 0 ELSE 1 END,
                         c.GioMoCa DESC, c.MaCa DESC
            ) ca
            JOIN NguoiDung nv ON nv.MaNguoiDung = ca.MaNhanVien
            WHERE (@MaChiNhanh IS NULL OR dh.MaChiNhanh = @MaChiNhanh)
              -- STAFF bị khóa ở tầng dữ liệu: chỉ đọc đơn do chính mình tiếp nhận.
              AND (@MaNhanVien IS NULL OR dh.MaNhanVien = @MaNhanVien)
              AND tt.TrangThai = 'DA_THANH_TOAN'
              AND tt.NgayThanhToan >= @TuNgay
              AND tt.NgayThanhToan < DATEADD(DAY, 1, @DenNgay)
            ORDER BY tt.NgayThanhToan DESC, tt.MaThanhToan DESC";
    }

    internal static class NguoiDung
    {
        public const string HoSo = @"
            SELECT nd.MaNguoiDung, nd.HoTen, nd.Email, nd.SoDienThoai, nd.AnhDaiDien,
                   nd.VaiTro AS TenVaiTro, nd.MaChiNhanh,
                   cn.TenChiNhanh, cn.DiaChi AS DiaChiChiNhanh,
                   CASE WHEN nd.VaiTro = 'CUSTOMER' THEN nd.DiaChi END AS DiaChiMacDinh
            FROM NguoiDung nd
            LEFT JOIN ChiNhanh cn ON cn.MaChiNhanh = nd.MaChiNhanh
            WHERE nd.MaNguoiDung = @MaNguoiDung";

        public const string DanhSachNhanVien = @"
            SELECT nd.MaNguoiDung, nd.HoTen, nd.Email, nd.SoDienThoai,
                   nd.VaiTro AS TenVaiTro, nd.MaChiNhanh,
                   cn.TenChiNhanh, nd.TrangThai, nd.NgayTao,
                   CAST(CASE WHEN nd.MaNguoiDung <> @MaNguoiXem
                                  AND nd.TrangThai = N'HOAT_DONG'
                                  AND nd.VaiTro <> 'SUPER_ADMIN'
                                  AND (@VaiTro = 'SUPER_ADMIN'
                                       OR (nd.VaiTro IN ('STAFF', 'SHIPPER')
                                           AND nd.MaChiNhanh = @MaChiNhanh))
                             THEN 1 ELSE 0 END AS BIT) AS DuocCapLai
            FROM NguoiDung nd
            LEFT JOIN ChiNhanh cn ON cn.MaChiNhanh = nd.MaChiNhanh
            WHERE nd.VaiTro <> 'CUSTOMER'
              AND (@VaiTro = 'SUPER_ADMIN' OR nd.MaChiNhanh = @MaChiNhanh)
            ORDER BY nd.MaChiNhanh, nd.VaiTro, nd.HoTen";
    }

    internal static class ThanhToan
    {
        public const string CuaDonHang = @"
            SELECT dh.MaDonHang,
                   'DH' + RIGHT('00000' + CAST(dh.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                   tt.PhuongThuc, tt.TrangThai, tt.SoTien,
                   dh.TrangThai AS TrangThaiDonHang,
                   DATEADD(MINUTE, @SoPhut, dh.NgayDat) AS HanThanhToan
            FROM DonHang dh
            JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
            WHERE dh.MaDonHang = @MaDonHang
              AND dh.MaKhachHang = @MaKhachHang";
    }
}
