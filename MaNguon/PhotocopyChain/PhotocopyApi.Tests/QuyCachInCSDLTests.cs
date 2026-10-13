using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos.DatIn;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuyCachInCSDLTests
{
    private static SqlConnection KetNoi() => new(Environment.GetEnvironmentVariable("PHOTOCOPY_TEST_DB"));

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

    [KiemThuCSDL]
    public async Task BaoGia_KiemTraGiaCongVaGioiHanTungBan()
    {
        using var conn = KetNoi();
        await conn.OpenAsync();
        int cn = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaChiNhanh FROM ChiNhanh WHERE TrangThai=N'HOAT_DONG' ORDER BY MaChiNhanh");
        async Task<int> DichVu(string ten) => await conn.QuerySingleAsync<int>("SELECT MaDichVu FROM DichVu WHERE TenDichVu=@ten", new { ten });
        int kim = await DichVu("Bấm kim góc"), loXo = await DichVu("Đóng lò xo"), plastic = await DichVu("Ép plastic"), bia = await DichVu("Đóng bìa kiếng");
        async Task<BaoGiaResponse> BaoGia(int trang, string mat, params int[] gc)
        {
            int dv = await conn.QueryFirstAsync<int>("SELECT TOP 1 dv.MaDichVu FROM DichVu dv JOIN BangGiaChiNhanh bg ON bg.MaDichVu=dv.MaDichVu WHERE bg.MaChiNhanh=@cn AND bg.TrangThai=N'HIEN' AND dv.LoaiDichVu='IN_AN' AND dv.MatIn=@mat ORDER BY dv.MaDichVu", new { cn, mat });
            return await conn.QuerySingleAsync<BaoGiaResponse>("sp_BaoGia", new {
                MaChiNhanh = cn, HinhThucNhan = "TAI_QUAY", DsFile = Dong(1, dv, trang, 10).AsTableValuedParameter("Kieu_DongDonHang"),
                DsGiaCong = GiaCong(1, gc).AsTableValuedParameter("Kieu_DongGiaCong")
            }, commandType: CommandType.StoredProcedure);
        }
        // Báo giá không ghi dữ liệu. 10 bản vẫn giới hạn từng bản, không giới hạn tổng tờ.
        Assert.True((await BaoGia(50, "1_MAT", kim)).TongTien > 0);
        Assert.True((await BaoGia(100, "2_MAT", kim)).TongTien > 0);
        Assert.Equal(50111, (await Assert.ThrowsAsync<SqlException>(() => BaoGia(51, "1_MAT", kim))).Number);
        Assert.Equal(50111, (await Assert.ThrowsAsync<SqlException>(() => BaoGia(101, "2_MAT", kim))).Number);
        Assert.Equal(50110, (await Assert.ThrowsAsync<SqlException>(() => BaoGia(2, "2_MAT", kim, loXo))).Number);
        Assert.True((await BaoGia(2, "2_MAT", plastic, loXo)).TongTien > 0);
        Assert.True((await BaoGia(2, "2_MAT", bia, kim)).TongTien > 0);
        var caHai = await BaoGia(2, "2_MAT", bia, loXo);
        var chiBia = await BaoGia(2, "2_MAT", bia);
        var chiLoXo = await BaoGia(2, "2_MAT", loXo);
        Assert.Equal(chiBia.TienGiaCong + chiLoXo.TienGiaCong, caHai.TienGiaCong);
    }

    [KiemThuCSDL]
    public async Task TaoDon_LuuHuongGiayMacDinhVaNgang_GiaKhongDoi()
    {
        using var conn = KetNoi();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        int kh = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaNguoiDung FROM NguoiDung WHERE VaiTro='CUSTOMER' AND TrangThai=N'HOAT_DONG'", transaction: tx);
        var gia = await conn.QueryFirstAsync<Gia>("SELECT TOP 1 bg.MaChiNhanh,bg.MaDichVu FROM BangGiaChiNhanh bg JOIN DichVu dv ON dv.MaDichVu=bg.MaDichVu WHERE bg.TrangThai=N'HIEN' AND dv.LoaiDichVu='IN_AN'", transaction: tx);
        int file = await conn.ExecuteScalarAsync<int>("INSERT FileIn(MaKhachHang,TenFile,SoTrang) VALUES(@kh,N'KIEM_THU_QUY_CACH.pdf',2); SELECT CAST(SCOPE_IDENTITY() AS INT);", new { kh }, tx);
        async Task<int> Tao(string huong)
        {
            var p = new DynamicParameters(new { MaKhachHang = kh, gia.MaChiNhanh, HinhThucNhan = "TAI_QUAY", PhuongThuc = "COD" });
            p.Add("DsFile", Dong(file, gia.MaDichVu, 2, 1).AsTableValuedParameter("Kieu_DongDonHang"));
            p.Add("DsGiaCong", GiaCong(file).AsTableValuedParameter("Kieu_DongGiaCong"));
            if (huong != null) p.Add("HuongGiayJson", JsonSerializer.Serialize(new[] { new { maFile = file, huongGiay = huong } }));
            p.Add("MaDonHang", dbType: DbType.Int32, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("sp_TaoDonHang", p, tx, commandType: CommandType.StoredProcedure);
            return p.Get<int>("MaDonHang");
        }
        int dung = await Tao(null), ngang = await Tao("NGANG");
        Assert.Equal("DUNG", await conn.ExecuteScalarAsync<string>("SELECT HuongGiay FROM ChiTietDonHang WHERE MaDonHang=@dung", new { dung }, tx));
        Assert.Equal("NGANG", await conn.ExecuteScalarAsync<string>("SELECT HuongGiay FROM ChiTietDonHang WHERE MaDonHang=@ngang", new { ngang }, tx));
        Assert.Equal(await conn.ExecuteScalarAsync<decimal>("SELECT TongTien FROM DonHang WHERE MaDonHang=@dung", new { dung }, tx),
            await conn.ExecuteScalarAsync<decimal>("SELECT TongTien FROM DonHang WHERE MaDonHang=@ngang", new { ngang }, tx));
        // Dispose rollback toàn bộ file, đơn, thanh toán và thông báo thử.
    }

    private sealed class Gia { public int MaChiNhanh { get; set; } public int MaDichVu { get; set; } }

    [KiemThuCSDL]
    public async Task TaoDon_HuongGiaySaiThieuTrungHoacNgoaiDon_DeuBiChan()
    {
        foreach (string huong in new[] { "TUY_Y", "DUNG ", "ngang", "", "THIEU", "TRUNG", "NGOAI", "SAI_JSON" })
        {
            using var conn = KetNoi();
            await conn.OpenAsync();
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            int kh = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaNguoiDung FROM NguoiDung WHERE VaiTro='CUSTOMER' AND TrangThai=N'HOAT_DONG'", transaction: tx);
            var gia = await conn.QueryFirstAsync<Gia>("SELECT TOP 1 bg.MaChiNhanh,bg.MaDichVu FROM BangGiaChiNhanh bg JOIN DichVu dv ON dv.MaDichVu=bg.MaDichVu WHERE bg.TrangThai=N'HIEN' AND dv.LoaiDichVu='IN_AN'", transaction: tx);
            int file = await conn.ExecuteScalarAsync<int>("INSERT FileIn(MaKhachHang,TenFile,SoTrang) VALUES(@kh,N'KIEM_THU_QUY_CACH.pdf',2); SELECT CAST(SCOPE_IDENTITY() AS INT);", new { kh }, tx);
            string json = JsonSerializer.Serialize(new[] { new { maFile = file, huongGiay = huong } });
            if (huong == "THIEU") json = "[]";
            if (huong == "TRUNG") json = JsonSerializer.Serialize(new[] { new { maFile = file, huongGiay = "DUNG" }, new { maFile = file, huongGiay = "NGANG" } });
            if (huong == "NGOAI") json = JsonSerializer.Serialize(new[] { new { maFile = -1, huongGiay = "NGANG" } });
            if (huong == "SAI_JSON") json = "khong-phai-json";
            var p = new DynamicParameters(new { MaKhachHang = kh, gia.MaChiNhanh, HinhThucNhan = "TAI_QUAY", PhuongThuc = "COD", HuongGiayJson = json });
            p.Add("DsFile", Dong(file, gia.MaDichVu, 2, 1).AsTableValuedParameter("Kieu_DongDonHang"));
            p.Add("DsGiaCong", GiaCong(file).AsTableValuedParameter("Kieu_DongGiaCong"));
            p.Add("MaDonHang", dbType: DbType.Int32, direction: ParameterDirection.Output);
            Assert.Equal(50112, (await Assert.ThrowsAsync<SqlException>(() => conn.ExecuteAsync("sp_TaoDonHang", p, tx, commandType: CommandType.StoredProcedure))).Number);
        }
    }
}
