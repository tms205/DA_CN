using PhotocopyAdmin;
using Xunit;

namespace PhotocopyApi.Tests;

public class TongQuanMocThoiGianTests
{
    [Theory]
    [InlineData("thang","2026-11-01","2026-11-02")]
    [InlineData("thang-truoc","2026-10-01","2026-10-31")]
    [InlineData("tuan","2026-11-02","2026-11-02")]
    [InlineData("tuan-truoc","2026-10-26","2026-11-01")]
    [InlineData("ngay","2026-11-02","2026-11-02")]
    public void NamMoc_DungNgayDauCuoi(string moc,string tu,string den)
    {
        var range=TongQuanBaoCao.KhoangThoiGian(new(2026,11,2),moc);
        Assert.Equal(DateTime.Parse(tu),range.TuNgay);Assert.Equal(DateTime.Parse(den),range.DenNgay);
    }
    [Theory]
    [InlineData("2024-03-01","thang-truoc","2024-02-01","2024-02-29")]
    [InlineData("2026-01-01","thang-truoc","2025-12-01","2025-12-31")]
    [InlineData("2026-01-01","tuan-truoc","2025-12-22","2025-12-28")]
    [InlineData("2026-11-01","tuan","2026-10-26","2026-11-01")]
    public void NamNhuan_DoiNam_VaChuNhat(string homNay,string moc,string tu,string den)
    {
        var range=TongQuanBaoCao.KhoangThoiGian(DateTime.Parse(homNay),moc);
        Assert.Equal(DateTime.Parse(tu),range.TuNgay);Assert.Equal(DateTime.Parse(den),range.DenNgay);
    }
    [Theory]
    [InlineData("31/02/2026","01/03/2026",false)]
    [InlineData("1/10/2026","02/10/2026",false)]
    [InlineData("","02/11/2026",false)]
    [InlineData("02/11/2026","01/11/2026",false)]
    [InlineData("02/11/2026","03/11/2026",false)]
    [InlineData("01/01/2020","02/11/2026",false)]
    [InlineData("01/01/1700","02/01/1700",false)]
    [InlineData("29/02/2024","01/03/2024",true)]
    [InlineData("02/11/2026","02/11/2026",true)]
    public void NgayNhap_KiemTraNgayThucThuTuTuongLaiVaDoDai(string tu,string den,bool valid)
    {
        string error=TongQuanBaoCao.KiemTraNgayNhap(tu,den,new(2026,11,2),out _,out _);
        Assert.Equal(valid,error==null);
    }
}
