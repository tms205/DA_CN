using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace PhotocopyApi.Tests;

// Mọi bản ghi thử nằm trong transaction và rollback; không đổi phiếu/ca hiện có.
public class CaVaPhieuNhapCSDLTests
{
    private static SqlConnection KetNoi() => new(Environment.GetEnvironmentVariable("PHOTOCOPY_TEST_DB"));

    [KiemThuCSDL]
    public async Task CEOTaoPhieu_DaDuyetNgay_VaChuaCongTonKho()
    {
        using var conn = KetNoi();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        int ceo = await conn.QueryFirstAsync<int>("SELECT MaNguoiDung FROM NguoiDung WHERE VaiTro='SUPER_ADMIN' AND TrangThai='HOAT_DONG'", transaction: tx);
        var kho = await conn.QueryFirstAsync<Kho>("SELECT TOP 1 MaChiNhanh,MaVatTu,SoLuong FROM TonKho", transaction: tx);
        await conn.ExecuteAsync("sp_TaoPhieuNhap", new { kho.MaChiNhanh, kho.MaVatTu, NhaCungCap = "KIEM_THU_ROLLBACK", SoLuong = 2, DonGia = 1000, MaNguoiLap = ceo }, tx, commandType: CommandType.StoredProcedure);
        var p = await conn.QueryFirstAsync<Phieu>("SELECT TOP 1 MaPhieuNhap,TrangThai,MaNguoiDuyet FROM PhieuNhap WHERE NhaCungCap='KIEM_THU_ROLLBACK' ORDER BY MaPhieuNhap DESC", transaction: tx);
        Assert.Equal("DA_DUYET", p.TrangThai);
        Assert.Equal(ceo, p.MaNguoiDuyet);
        Assert.Equal(kho.SoLuong, await conn.ExecuteScalarAsync<int>("SELECT SoLuong FROM TonKho WHERE MaChiNhanh=@MaChiNhanh AND MaVatTu=@MaVatTu", kho, tx));
        await conn.ExecuteAsync("sp_NhapKho", new { p.MaPhieuNhap, SoLuongThucNhan = 2 }, tx, commandType: CommandType.StoredProcedure);
        Assert.Equal(kho.SoLuong + 2, await conn.ExecuteScalarAsync<int>("SELECT SoLuong FROM TonKho WHERE MaChiNhanh=@MaChiNhanh AND MaVatTu=@MaVatTu", kho, tx));
        Assert.Equal("DA_NHAP", await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM PhieuNhap WHERE MaPhieuNhap=@MaPhieuNhap", p, tx));
    }

    [KiemThuCSDL]
    public async Task QuanLyLapPhieu_PhaiChoCEO_VaCEOCoTheTuDuyetPhieuCu()
    {
        using var conn = KetNoi();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        var ql = await conn.QueryFirstAsync<QuanLy>("SELECT TOP 1 MaNguoiDung,MaChiNhanh FROM NguoiDung WHERE VaiTro='BRANCH_ADMIN' AND TrangThai='HOAT_DONG'", transaction: tx);
        int ceo = await conn.QueryFirstAsync<int>("SELECT MaNguoiDung FROM NguoiDung WHERE VaiTro='SUPER_ADMIN' AND TrangThai='HOAT_DONG'", transaction: tx);
        int vatTu = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaVatTu FROM VatTu", transaction: tx);
        await conn.ExecuteAsync("sp_TaoPhieuNhap", new { ql.MaChiNhanh, MaVatTu = vatTu, NhaCungCap = "KIEM_THU_ROLLBACK", SoLuong = 2, DonGia = 1000, MaNguoiLap = ql.MaNguoiDung }, tx, commandType: CommandType.StoredProcedure);
        var p = await conn.QueryFirstAsync<Phieu>("SELECT TOP 1 MaPhieuNhap,TrangThai,MaNguoiDuyet FROM PhieuNhap WHERE NhaCungCap='KIEM_THU_ROLLBACK' ORDER BY MaPhieuNhap DESC", transaction: tx);
        Assert.Equal("CHO_DUYET", p.TrangThai);
        Assert.Null(p.MaNguoiDuyet);
        await Assert.ThrowsAsync<SqlException>(() => conn.ExecuteAsync("sp_DuyetPhieuNhap",
            new { p.MaPhieuNhap, MaNguoiDuyet = ql.MaNguoiDung, DongY = true }, tx, commandType: CommandType.StoredProcedure));
        await conn.ExecuteAsync("sp_DuyetPhieuNhap", new { p.MaPhieuNhap, MaNguoiDuyet = ceo, DongY = true }, tx, commandType: CommandType.StoredProcedure);
        // Phiếu CEO đã tạo từ phiên bản cũ vẫn phải tự duyệt được.
        int phieuCu = await conn.ExecuteScalarAsync<int>(@"INSERT PhieuNhap(MaChiNhanh,MaVatTu,NhaCungCap,SoLuong,DonGia,MaNguoiLap) VALUES(@MaChiNhanh,@MaVatTu,'KIEM_THU_ROLLBACK',1,1000,@CEO); SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { ql.MaChiNhanh, MaVatTu = vatTu, CEO = ceo }, tx);
        await conn.ExecuteAsync("sp_DuyetPhieuNhap", new { MaPhieuNhap = phieuCu, MaNguoiDuyet = ceo, DongY = true }, tx, commandType: CommandType.StoredProcedure);
        Assert.Equal("DA_DUYET", await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM PhieuNhap WHERE MaPhieuNhap=@phieuCu", new { phieuCu }, tx));
    }

    [KiemThuCSDL]
    public async Task HaiNhanVienCungChiNhanh_CoCaRieng_DangNhapLaiKhongDoiTien()
    {
        using var conn = KetNoi();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        var nhanVien = (await conn.QueryAsync<int>(@"SELECT TOP 2 MaNguoiDung FROM NguoiDung WHERE VaiTro='STAFF' AND TrangThai='HOAT_DONG' AND MaChiNhanh=(SELECT TOP 1 MaChiNhanh FROM NguoiDung WHERE VaiTro='STAFF' AND TrangThai='HOAT_DONG' GROUP BY MaChiNhanh HAVING COUNT(*)>=2) ORDER BY MaNguoiDung", transaction: tx)).ToArray();
        Assert.Equal(2, nhanVien.Length);
        async Task<int> Mo(int nv, decimal tien)
        {
            var args = new DynamicParameters(new { MaNhanVien = nv, TienDauCa = tien });
            args.Add("MaCa", dbType: DbType.Int32, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("sp_MoCa", args, tx, commandType: CommandType.StoredProcedure);
            return args.Get<int>("MaCa");
        }
        int ca1 = await Mo(nhanVien[0], 200000);
        int ca2 = await Mo(nhanVien[1], 200000);
        Assert.NotEqual(ca1, ca2);
        decimal tienCu = await conn.ExecuteScalarAsync<decimal>("SELECT TienDauCa FROM CaLamViec WHERE MaCa=@ca1", new { ca1 }, tx);
        Assert.Equal(ca1, await Mo(nhanVien[0], 999000));
        Assert.Equal(tienCu, await conn.ExecuteScalarAsync<decimal>("SELECT TienDauCa FROM CaLamViec WHERE MaCa=@ca1", new { ca1 }, tx));

        // Đóng ca thử trong transaction để kiểm tra mở ca mới bằng giá trị mặc định của SQL.
        await conn.ExecuteAsync("UPDATE CaLamViec SET TrangThai='CHO_XAC_NHAN',GioDongCa=GETDATE() WHERE MaCa=@ca1", new { ca1 }, tx);
        var macDinh = new DynamicParameters(new { MaNhanVien = nhanVien[0] });
        macDinh.Add("MaCa", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await conn.ExecuteAsync("sp_MoCa", macDinh, tx, commandType: CommandType.StoredProcedure);
        int caMoi = macDinh.Get<int>("MaCa");
        Assert.NotEqual(ca1, caMoi);
        Assert.Equal(200000m, await conn.ExecuteScalarAsync<decimal>("SELECT TienDauCa FROM CaLamViec WHERE MaCa=@caMoi", new { caMoi }, tx));
    }

    [KiemThuCSDL]
    public async Task HaiCaChongGio_GiaoDichQRChiThuocMotCa()
    {
        using var conn = KetNoi();
        await conn.OpenAsync();
        // Bảng tạm riêng của kết nối: không tạo hoặc sửa dữ liệu nghiệp vụ thật.
        await conn.ExecuteAsync(@"
            CREATE TABLE #CaLamViec (MaCa INT, MaChiNhanh INT, MaNhanVien INT,
                GioMoCa DATETIME, GioDongCa DATETIME NULL, TienDauCa MONEY,
                DoanhThuTrongCa MONEY, TongDoanhThuCuoiCa MONEY, TienQuanLyDemCuoiCa MONEY,
                TienChi MONEY, TienBu MONEY, GhiChu NVARCHAR(200), TrangThai VARCHAR(30), MaNguoiXacNhan INT);
            CREATE TABLE #ChiNhanh (MaChiNhanh INT, TenChiNhanh NVARCHAR(100));
            CREATE TABLE #NguoiDung (MaNguoiDung INT, HoTen NVARCHAR(100));
            CREATE TABLE #DonHang (MaDonHang INT, MaChiNhanh INT, MaNhanVien INT, MaKhachHang INT, TrangThai VARCHAR(30));
            CREATE TABLE #ThanhToan (MaThanhToan INT, MaDonHang INT, MaCa INT NULL,
                PhuongThuc VARCHAR(30), TrangThai VARCHAR(30), NgayThanhToan DATETIME, SoTien MONEY);
            INSERT #ChiNhanh VALUES (1,N'Chi nhánh thử');
            INSERT #NguoiDung VALUES (11,N'Nhân viên A'),(12,N'Nhân viên B'),(13,N'Khách');
            INSERT #CaLamViec (MaCa,MaChiNhanh,MaNhanVien,GioMoCa,TienDauCa,TrangThai)
                VALUES (1,1,11,DATEADD(MINUTE,-10,GETDATE()),200000,'DANG_MO'),
                       (2,1,12,DATEADD(MINUTE,-10,GETDATE()),200000,'DANG_MO');
            INSERT #DonHang VALUES (1,1,11,13,'DA_XAC_NHAN');
            INSERT #ThanhToan VALUES (1,1,NULL,'VIETQR','DA_THANH_TOAN',DATEADD(MINUTE,-5,GETDATE()),7400);");

        string Sql(string ten)
        {
            var lop = typeof(PhotocopyApi.Controllers.CaController).Assembly.GetType("PhotocopyApi.DuLieu.TruyVanSql+CaLamViec");
            string sql = (string)lop.GetField(ten, BindingFlags.Public | BindingFlags.Static).GetRawConstantValue();
            return Regex.Replace(sql, @"\b(CaLamViec|ChiNhanh|NguoiDung|DonHang|ThanhToan)\b", "#$1");
        }

        var args = new { MaChiNhanh = 1, MaNhanVien = (int?)null };
        var ca = (await conn.QueryAsync(Sql("GanDayCuaChiNhanh"), args)).ToArray();
        Assert.Equal(1, ca.Sum(x => (int)x.SoDonVietQr));
        Assert.Equal(7400m, ca.Sum(x => (decimal)x.DoanhThuVietQr));
        Assert.Equal(1, (int)ca.Single(x => (int)x.MaCa == 1).SoDonVietQr);
        Assert.Single(await conn.QueryAsync(Sql("DonHangTrongCa"), new { MaCa = 1, args.MaChiNhanh, args.MaNhanVien }));
        Assert.Empty(await conn.QueryAsync(Sql("DonHangTrongCa"), new { MaCa = 2, args.MaChiNhanh, args.MaNhanVien }));
        var lichSu = await conn.QuerySingleAsync(Sql("LichSuDonHangTrongCa"),
            new { args.MaChiNhanh, args.MaNhanVien, TuNgay = DateTime.Today, DenNgay = DateTime.Today });
        Assert.Equal(1, (int)lichSu.MaCa);
    }

    private sealed class Kho { public int MaChiNhanh { get; set; } public int MaVatTu { get; set; } public int SoLuong { get; set; } }
    private sealed class QuanLy { public int MaNguoiDung { get; set; } public int MaChiNhanh { get; set; } }
    private sealed class Phieu { public int MaPhieuNhap { get; set; } public string TrangThai { get; set; } public int? MaNguoiDuyet { get; set; } }
}
