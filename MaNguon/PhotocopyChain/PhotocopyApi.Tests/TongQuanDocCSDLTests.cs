using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

// Chỉ gọi các API SELECT. Không bật PHOTOCOPY_TEST_DB vì các bài khác có tạo dữ liệu.
public class TongQuanDocCSDLTests
{
    [KiemThuBaoCaoDoc]
    public async Task BaoCaoMoi_KhopTongDonCu_TheoCaToanChuoiVaTungChiNhanh()
    {
        string envFile = Environment.GetEnvironmentVariable("PHOTOCOPY_REPORT_ENV_FILE");
        Type doc = typeof(QuanTriController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        var config = (Dictionary<string, string>)doc.GetMethod("Doc").Invoke(null, new object[] { envFile });
        // Chỉ chuyển connection string vào controller, không ghi toàn bộ cấu hình/secret ra log.
        var cauHinh = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> {
            ["ConnectionStrings:PhotocopyChain"] = config["ConnectionStrings:PhotocopyChain"]
        }).Build();
        DateTime tu = DateTime.Today.AddDays(-365), den = DateTime.Today;
        foreach (int? cn in new int?[] { null, 1, 2 })
        {
            var claims = new List<Claim> { new(TenClaim.VaiTro, VaiTro.SuperAdmin) };
            if (cn != null) claims.Add(new(TenClaim.MaChiNhanh, cn.Value.ToString()));
            var ctx = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, TenClaim.VaiTro))
            } };
            var api = new BaoCaoTongQuanController(cauHinh) { ControllerContext = ctx };
            var cu = new QuanTriController(cauHinh) { ControllerContext = ctx };
            var moi = Assert.IsType<TongQuanPhanTichDto>(Assert.IsType<OkObjectResult>(await api.PhanTich(tu, den)).Value);
            var ds = Assert.IsAssignableFrom<IEnumerable<BaoCaoDoanhThuDto>>(Assert.IsType<OkObjectResult>(await cu.BaoCaoDoanhThu(tu, den)).Value);
            Assert.True(moi.DoanhThu == ds.Sum(x => x.DoanhThu), "Tổng báo cáo mới phải khớp API doanh thu cùng phạm vi.");
            Assert.True(moi.ThanhToan.Sum(x => x.SoTien) == moi.DoanhThu, "Phương thức thanh toán không được cộng trùng đơn.");
            using var conn = new SqlConnection(cauHinh.GetConnectionString("PhotocopyChain"));
            decimal tienDong = await conn.ExecuteScalarAsync<decimal>(@"
                SELECT ISNULL(SUM(ct.ThanhTien),0) FROM ChiTietDonHang ct JOIN DonHang dh ON dh.MaDonHang=ct.MaDonHang
                WHERE dh.TrangThai='HOAN_THANH' AND dh.NgayHoanThanh>=@tu AND dh.NgayHoanThanh<@sau
                  AND ISNULL(dh.LoaiPhatSinh,'')<>'IN_LAI_LOI_QUAY'
                  AND (@cn IS NULL OR dh.MaChiNhanh=@cn)", new { tu, sau = den.AddDays(1), cn });
            Assert.True(moi.DichVu.Sum(x => x.SoTien) == tienDong, "Tách in/gia công phải khớp tổng chi tiết đã chốt.");
            Assert.True(moi.HieuQua.SoDon == ds.Sum(x => x.SoDon), "Mẫu hiệu quả dùng cùng tập đơn hoàn tất.");
            Assert.All(moi.GioCaoDiem, x => { Assert.InRange(x.Thu, 0, 6); Assert.InRange(x.Gio, 0, 23); });
            var hienTai = Assert.IsType<TongQuanHienTaiDto>(Assert.IsType<OkObjectResult>(await api.HienTai()).Value);
            Assert.True(hienTai.SoCaKiemDem >= hienTai.SoCaLech);
        }
    }
}

public sealed class KiemThuBaoCaoDocAttribute : FactAttribute
{
    public KiemThuBaoCaoDocAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PHOTOCOPY_REPORT_ENV_FILE")))
            Skip = "Cần PHOTOCOPY_REPORT_ENV_FILE để chạy SELECT trên cấu hình máy, không sửa database.";
    }
}
