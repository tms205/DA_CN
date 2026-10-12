using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos.DatIn;
using Xunit;

namespace PhotocopyApi.Tests;

public class AnhTheCSDLTests
{
    [KiemThuCSDL]
    public async Task GiaTungChiNhanh_VaGiaDaChotTrongDon_KhongBiGhiDe()
    {
        using var conn = new SqlConnection(Environment.GetEnvironmentVariable("PHOTOCOPY_TEST_DB"));
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        int kh = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaNguoiDung FROM NguoiDung WHERE VaiTro='CUSTOMER'", transaction: tx);
        int dv = await conn.QuerySingleAsync<int>("SELECT MaDichVu FROM DichVu WHERE KichThuocAnh='3X4'", transaction: tx);
        var dsCN = (await conn.QueryAsync<int>("SELECT MaChiNhanh FROM BangGiaChiNhanh WHERE MaDichVu=@dv ORDER BY MaChiNhanh", new { dv }, tx)).ToList();
        Assert.True(dsCN.Count >= 2, "Kiểm thử này cần hai chi nhánh có giá ảnh thẻ.");
        int cn = dsCN[0], cnKhac = dsCN[1];
        decimal giaKhac = await conn.ExecuteScalarAsync<decimal>("SELECT DonGia FROM BangGiaChiNhanh WHERE MaChiNhanh=@cnKhac AND MaDichVu=@dv", new { cnKhac, dv }, tx);
        int file = await conn.ExecuteScalarAsync<int>(
            "INSERT FileIn(MaKhachHang,TenFile,SoTrang,KichThuocAnh,SoLuongAnh) VALUES(@kh,N'KIEM_THU_ANH_THE.pdf',1,'3X4',8); SELECT CAST(SCOPE_IDENTITY() AS INT);", new { kh }, tx);
        await conn.ExecuteAsync("UPDATE BangGiaChiNhanh SET DonGia=6000 WHERE MaChiNhanh=@cn AND MaDichVu=@dv", new { cn, dv }, tx);
        var quote = await conn.QuerySingleAsync<BaoGiaResponse>("sp_BaoGia", new {
            MaChiNhanh = cn, HinhThucNhan = "TAI_QUAY",
            DsFile = Dong(file, dv, 1, 1).AsTableValuedParameter("Kieu_DongDonHang"),
            DsGiaCong = GiaCong(file).AsTableValuedParameter("Kieu_DongGiaCong")
        }, tx, commandType: CommandType.StoredProcedure);
        Assert.Equal(48000m, quote.TienIn);
        Assert.Equal(1, quote.TongSoTrang);
        var p = new DynamicParameters(new { MaKhachHang = kh, MaChiNhanh = cn, HinhThucNhan = "TAI_QUAY", PhuongThuc = "COD" });
        p.Add("DsFile", Dong(file, dv, 1, 1).AsTableValuedParameter("Kieu_DongDonHang"));
        p.Add("DsGiaCong", GiaCong(file).AsTableValuedParameter("Kieu_DongGiaCong"));
        p.Add("MaDonHang", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await conn.ExecuteAsync("sp_TaoDonHang", p, tx, commandType: CommandType.StoredProcedure);
        int don = p.Get<int>("MaDonHang");
        await conn.ExecuteAsync("UPDATE BangGiaChiNhanh SET DonGia=7000 WHERE MaChiNhanh=@cn AND MaDichVu=@dv", new { cn, dv }, tx);
        Assert.Equal(48000m, await conn.ExecuteScalarAsync<decimal>("SELECT ThanhTien FROM ChiTietDonHang WHERE MaDonHang=@don", new { don }, tx));
        Assert.Equal(8, await conn.ExecuteScalarAsync<int>("SELECT SoLuongAnh FROM ChiTietDonHang WHERE MaDonHang=@don", new { don }, tx));
        Assert.Equal(giaKhac, await conn.ExecuteScalarAsync<decimal>("SELECT DonGia FROM BangGiaChiNhanh WHERE MaChiNhanh=@cnKhac AND MaDichVu=@dv", new { cnKhac, dv }, tx));
        // Dispose rollback file, đơn, giá và các bản ghi đi kèm; không gọi cổng thanh toán.
    }

    [KiemThuCSDL]
    public async Task BaoGia_ChanSaiKichThuocSoBanPhamViVaGiaCong()
    {
        foreach (string truongHop in new[] { "SAI_SIZE", "TAI_LIEU", "NHIEU_BAN", "THIEU_TRANG", "GIA_CONG" })
        {
            using var conn = new SqlConnection(Environment.GetEnvironmentVariable("PHOTOCOPY_TEST_DB"));
            await conn.OpenAsync();
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            int kh = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaNguoiDung FROM NguoiDung WHERE VaiTro='CUSTOMER'", transaction: tx);
            int cn = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaChiNhanh FROM ChiNhanh ORDER BY MaChiNhanh", transaction: tx);
            int dv = await conn.QueryFirstAsync<int>(
                "SELECT TOP 1 MaDichVu FROM DichVu WHERE LoaiDichVu='IN_AN' AND ((@tc='SAI_SIZE' AND KichThuocAnh='4X6') OR (@tc='TAI_LIEU' AND KichThuocAnh IS NULL) OR (@tc NOT IN ('SAI_SIZE','TAI_LIEU') AND KichThuocAnh='3X4'))", new { tc = truongHop }, tx);
            int file = await conn.ExecuteScalarAsync<int>(
                "INSERT FileIn(MaKhachHang,TenFile,SoTrang,KichThuocAnh,SoLuongAnh) VALUES(@kh,N'KIEM_THU_ANH_THE.pdf',2,'3X4',40); SELECT CAST(SCOPE_IDENTITY() AS INT);", new { kh }, tx);
            int gc = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaDichVu FROM DichVu WHERE LoaiDichVu='GIA_CONG' ORDER BY MaDichVu", transaction: tx);
            int trang = truongHop == "THIEU_TRANG" ? 1 : 2;
            int ban = truongHop == "NHIEU_BAN" ? 2 : 1;
            var ex = await Assert.ThrowsAsync<SqlException>(() => conn.QuerySingleAsync<BaoGiaResponse>("sp_BaoGia", new {
                MaChiNhanh = cn, HinhThucNhan = "TAI_QUAY",
                DsFile = Dong(file, dv, trang, ban).AsTableValuedParameter("Kieu_DongDonHang"),
                DsGiaCong = GiaCong(file, truongHop == "GIA_CONG" ? new[] { gc } : Array.Empty<int>()).AsTableValuedParameter("Kieu_DongGiaCong")
            }, tx, commandType: CommandType.StoredProcedure));
            Assert.Equal(truongHop == "GIA_CONG" ? 50131 : 50130, ex.Number);
        }
    }

    private static DataTable Dong(int file, int dv, int trang, int ban)
    {
        var bang = new DataTable();
        foreach (string cot in new[] { "MaFile", "MaDichVu", "SoTrang", "SoBan" }) bang.Columns.Add(cot, typeof(int));
        bang.Rows.Add(file, dv, trang, ban);
        return bang;
    }

    private static DataTable GiaCong(int file, params int[] ds)
    {
        var bang = new DataTable();
        bang.Columns.Add("MaFile", typeof(int));
        bang.Columns.Add("MaDichVu", typeof(int));
        foreach (int dv in ds) bang.Rows.Add(file, dv);
        return bang;
    }
}
