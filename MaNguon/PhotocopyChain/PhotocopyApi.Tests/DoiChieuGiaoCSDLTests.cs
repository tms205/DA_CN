using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyApi.GiaoHang;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.GiaoHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class DoiChieuGiaoCSDLTests
{
    [KiemThuGiaoHangGhi]
    public async Task PhanHoiDongThoi_DungChuDon_TachAnh_XuLyMotLan_KhongDoiDonVaTien()
    {
        var reader = typeof(GiaoHangController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var values = (Dictionary<string, string>)reader.GetMethod("Doc").Invoke(null, new object[] { Environment.GetEnvironmentVariable("PHOTOCOPY_DELIVERY_ENV_FILE") });
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PhotocopyDoiChieuTests", Guid.NewGuid().ToString("N")));
        config["GiaoHang:ThuMucBangChung"] = root;
        using var cache = new MemoryCache(new MemoryCacheOptions()); using var conn = new SqlConnection(config.GetConnectionString("PhotocopyChain")); await conn.OpenAsync();
        var khach = (await conn.QueryAsync<int>("SELECT TOP 2 MaNguoiDung FROM NguoiDung WHERE VaiTro='CUSTOMER' AND TrangThai=N'HOAT_DONG' ORDER BY MaNguoiDung")).ToList(); Assert.Equal(2, khach.Count);
        var ship = await conn.QueryFirstAsync("SELECT TOP 1 MaNguoiDung,MaChiNhanh FROM NguoiDung WHERE VaiTro='SHIPPER' AND TrangThai=N'HOAT_DONG' AND MaChiNhanh IS NOT NULL");
        int shipper = ship.MaNguoiDung, branch = ship.MaChiNhanh;
        int ceo = await conn.QueryFirstAsync<int>("SELECT TOP 1 MaNguoiDung FROM NguoiDung WHERE VaiTro='SUPER_ADMIN' AND TrangThai=N'HOAT_DONG'");
        var triggers = await conn.QueryAsync<string>("SELECT name FROM sys.triggers WHERE parent_id IN(OBJECT_ID('DonHang'),OBJECT_ID('ThanhToan'),OBJECT_ID('GiaoHang')) AND is_disabled=0");
        Assert.All(triggers, n => Assert.Contains(n, new[] { "trg_DonHang_HoanThanh", "trg_DonHang_Huy" }));
        int order = 0, attempt = 0; string marker = "TEST_DOI_CHIEU_" + Guid.NewGuid().ToString("N");
        GiaoHangController Api(string role, int id, int? cn) => new(config, cache, null, NullLogger<GiaoHangController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(TenClaim.MaNguoiDung, id.ToString()), new Claim(TenClaim.VaiTro, role), new Claim(TenClaim.HoTen, "Quản lý thử") }
                .Concat(cn == null ? Array.Empty<Claim>() : new[] { new Claim(TenClaim.MaChiNhanh, cn.ToString()) }), "fixture", ClaimTypes.Name, TenClaim.VaiTro))
                }
            }
        };
        FormFile Anh()
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
            using var image = new System.Drawing.Bitmap(8, 8); using var ms = new MemoryStream(); image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var data = ms.ToArray(); return new FormFile(new MemoryStream(data), 0, data.Length, "Anh", "call.png");
        }
        GuiDoiChieuRequest Request() => new() { NoiDung = "Tôi không nhận được cuộc gọi của shipper.", Anh = new() { Anh() } };
        try
        {
            order = await conn.ExecuteScalarAsync<int>(@"SET QUOTED_IDENTIFIER ON;SET ANSI_NULLS ON;
                INSERT DonHang(MaKhachHang,MaChiNhanh,HinhThucNhan,DiaChiGiao,SdtNhan,TienHang,PhiShip,TongTien,TrangThai,GhiChu)
                VALUES(@khach,@branch,'GIAO_HANG',N'ĐỊA CHỈ TEST KHÔNG GIAO THẬT','0900000000',20000,19000,39000,'GIAO_THAT_BAI',@marker);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", new { khach = khach[0], branch, marker });
            await conn.ExecuteAsync("INSERT ThanhToan(MaDonHang,PhuongThuc,SoTien,TrangThai,MaGiaoDich) VALUES(@order,'VIETQR',39000,'DA_THANH_TOAN',@marker)", new { order, marker });
            attempt = await conn.ExecuteScalarAsync<int>(@"INSERT GiaoHang(MaDonHang,MaShipper,LanGiao,TrangThai,LyDo) VALUES(@order,@shipper,1,'THAT_BAI',N'TEST không liên lạc được');SELECT CAST(SCOPE_IDENTITY() AS INT)", new { order, shipper });
            var store = new BangChungGiaoHangStore(config); var old = await store.Luu(attempt, new[] { Anh() }, default);
            string oldJson = JsonSerializer.Serialize(old); await conn.ExecuteAsync("UPDATE GiaoHang SET AnhBangChungJson=@oldJson WHERE MaGiaoHang=@attempt", new { oldJson, attempt });
            Assert.IsType<NotFoundObjectResult>(await Api(VaiTro.Customer, khach[1], null).GuiDoiChieu(attempt, Request()));
            var results = await Task.WhenAll(Api(VaiTro.Customer, khach[0], null).GuiDoiChieu(attempt, Request()), Api(VaiTro.Customer, khach[0], null).GuiDoiChieu(attempt, Request()));
            Assert.All(results, r => Assert.IsType<OkObjectResult>(r));
            var view = Assert.IsType<LuotDoiChieuGiaoDto>(Assert.IsType<OkObjectResult>(await Api(VaiTro.Customer, khach[0], null).XemDoiChieu(attempt)).Value);
            Assert.Equal(khach[0], view.PhanHoi.MaKhachHang); Assert.Single(view.PhanHoi.Anh);
            Assert.Equal(2, Directory.GetFiles(Path.Combine(root, attempt.ToString())).Length); // Một ảnh shipper + một ảnh khách; file request thua đã dọn.
            Assert.Equal(oldJson, await conn.ExecuteScalarAsync<string>("SELECT AnhBangChungJson FROM GiaoHang WHERE MaGiaoHang=@attempt", new { attempt }));
            Assert.IsType<NotFoundResult>(await Api(VaiTro.Customer, khach[0], null).XemAnhDoiChieu(attempt, old[0].MaAnh));
            Assert.IsType<NotFoundResult>(await Api(VaiTro.Shipper, shipper, branch).XemAnhBangChung(attempt, view.PhanHoi.Anh[0].MaAnh));
            var imageResult = Assert.IsType<FileStreamResult>(await Api(VaiTro.Customer, khach[0], null).XemAnhDoiChieu(attempt, view.PhanHoi.Anh[0].MaAnh)); await imageResult.FileStream.DisposeAsync();
            Assert.IsType<NotFoundObjectResult>(await Api(VaiTro.BranchAdmin, ceo, int.MaxValue).XemDoiChieu(attempt));
            var conclusion = new XuLyDoiChieuRequest { KetLuan = "Đã đối chiếu hai bên và liên hệ lại với khách." };
            Assert.IsType<OkObjectResult>(await Api(VaiTro.SuperAdmin, ceo, null).XuLyDoiChieu(attempt, conclusion));
            Assert.IsType<OkObjectResult>(await Api(VaiTro.SuperAdmin, ceo, null).XuLyDoiChieu(attempt, conclusion));
            Assert.IsType<ConflictObjectResult>(await Api(VaiTro.SuperAdmin, ceo, null).XuLyDoiChieu(attempt, new() { KetLuan = "Không được ghi đè kết luận trước." }));
            var done = Assert.IsType<LuotDoiChieuGiaoDto>(Assert.IsType<OkObjectResult>(await Api(VaiTro.Customer, khach[0], null).XemDoiChieu(attempt)).Value);
            Assert.Equal("DA_XU_LY", done.PhanHoi.TrangThai); Assert.Equal(ceo, done.PhanHoi.MaNguoiXuLy); Assert.Equal(conclusion.KetLuan, done.PhanHoi.KetLuan);
            Assert.Equal("GIAO_THAT_BAI", await conn.ExecuteScalarAsync<string>("SELECT TrangThai FROM DonHang WHERE MaDonHang=@order", new { order }));
            Assert.Equal(39000m, await conn.ExecuteScalarAsync<decimal>("SELECT SoTien FROM ThanhToan WHERE MaDonHang=@order", new { order }));
            await conn.ExecuteAsync("UPDATE DonHang SET TrangThai='DANG_GIAO' WHERE MaDonHang=@order", new { order });
            var list = Assert.IsAssignableFrom<IEnumerable<LuotDoiChieuGiaoDto>>(Assert.IsType<OkObjectResult>(await Api(VaiTro.Customer, khach[0], null).LuotDoiChieuCuaKhach(order)).Value);
            Assert.Single(list); // Vẫn xem được phản hồi lượt cũ khi đơn đã giao lại.
        }
        finally
        {
            if (order > 0)
            {
                Assert.Equal(marker, await conn.ExecuteScalarAsync<string>("SELECT GhiChu FROM DonHang WHERE MaDonHang=@order", new { order }));
                await conn.ExecuteAsync(@"SET QUOTED_IDENTIFIER ON;SET ANSI_NULLS ON;BEGIN TRAN;
                    DELETE ThongBao WHERE MaDonHang=@order;DELETE GiaoHang WHERE MaDonHang=@order;
                    DELETE ThanhToan WHERE MaDonHang=@order AND MaGiaoDich=@marker;DELETE DonHang WHERE MaDonHang=@order AND GhiChu=@marker;COMMIT;", new { order, marker });
                Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DonHang WHERE GhiChu=@marker", new { marker }));
            }
            Assert.StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PhotocopyDoiChieuTests")) + Path.DirectorySeparatorChar, root);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
