using System.Reflection;
using System.Runtime.ExceptionServices;
using PhotocopyAdmin;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class TongQuanBaoCaoTests
{
    private static object Goi(string ham, params object[] args)
    {
        var lop = typeof(PhamViChiNhanh).Assembly.GetType("PhotocopyAdmin.TongQuanBaoCao");
        Assert.NotNull(lop);
        try { return lop.GetMethod(ham).Invoke(null, args); }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    [Theory]
    [InlineData("ngay", "2026-01-01", "2026-01-01")]
    [InlineData("tuan", "2026-01-01", "2025-12-29")]
    [InlineData("tuan", "2026-10-12", "2026-10-12")]
    [InlineData("tuan", "2026-10-18", "2026-10-12")]
    [InlineData("thang", "2024-02-29", "2024-02-01")]
    public void BatDauKy_DungNgayThuHaiVaDauThang(string nhom, string ngay, string mongDoi)
        => Assert.Equal(DateTime.Parse(mongDoi), (DateTime)Goi("DauKy", DateTime.Parse(ngay), nhom));

    [Fact]
    public void KyTruoc_CungSoNgay_QuaNamNhuan()
    {
        var ky = ((DateTime TuNgay, DateTime DenNgay))Goi("KyTruoc", new DateTime(2024, 3, 1), new DateTime(2024, 3, 31));
        Assert.Equal(new DateTime(2024, 1, 30), ky.TuNgay);
        Assert.Equal(new DateTime(2024, 2, 29), ky.DenNgay);
        Assert.Equal(31, (ky.DenNgay - ky.TuNgay).Days + 1);
    }

    [Theory]
    [InlineData("2026-10-13", "2026-10-12")]
    [InlineData("2025-01-01", "2026-01-02")]
    [InlineData("1753-01-01", "1753-01-02")]
    [InlineData("1754-01-01", "1755-01-01")]
    public void KhoangKhongHopLe_BiTuChoi(string tu, string den)
        => Assert.Throws<ArgumentException>(() => Goi("KyTruoc", DateTime.Parse(tu), DateTime.Parse(den)));

    [Theory]
    [InlineData("ngay", 3)]
    [InlineData("tuan", 2)]
    [InlineData("thang", 2)]
    public void NhomKy_CongChiNhanh_VaBoSungNgayTrong(string nhom, int soKy)
    {
        var ds = new List<BaoCaoDoanhThuDto> {
            new() { Ngay = new DateTime(2026, 5, 31), MaChiNhanh = 1, SoDon = 2, DoanhThu = 100000 },
            new() { Ngay = new DateTime(2026, 5, 31), MaChiNhanh = 2, SoDon = 1, DoanhThu = 50000 },
            new() { Ngay = new DateTime(2026, 6, 2), MaChiNhanh = 1, SoDon = 1, DoanhThu = 20000 },
            new() { Ngay = new DateTime(2026, 6, 3), MaChiNhanh = 1, SoDon = 9, DoanhThu = 900000 }
        };
        var nhomKy = (List<BaoCaoDoanhThuDto>)Goi("NhomKy", ds, new DateTime(2026, 5, 31), new DateTime(2026, 6, 2), nhom);
        Assert.Equal(soKy, nhomKy.Count);
        Assert.Equal(170000, nhomKy.Sum(x => x.DoanhThu));
        Assert.Equal(4, nhomKy.Sum(x => x.SoDon));
        if (nhom == "ngay") Assert.Equal(0, nhomKy[1].DoanhThu);
    }

    [Theory]
    [InlineData(150, 100, 50)]
    [InlineData(0, 100, -100)]
    [InlineData(0, 0, 0)]
    [InlineData(100, 0, null)]
    public void TyLe_KhongChiaChoKhong(int hienTai, int truoc, int? mongDoi)
        => Assert.Equal(mongDoi.HasValue ? (decimal?)mongDoi.Value : null, (decimal?)Goi("TyLeThayDoi", (decimal)hienTai, (decimal)truoc));

    [Fact]
    public void NhomThang_CanNgayToiDa_KhongCongVuotDateTime()
    {
        var ds = (List<BaoCaoDoanhThuDto>)Goi("NhomKy", new List<BaoCaoDoanhThuDto>(), new DateTime(9999, 12, 1), new DateTime(9999, 12, 30), "thang");
        Assert.Equal(new DateTime(9999, 12, 1), Assert.Single(ds).Ngay);
    }
}
