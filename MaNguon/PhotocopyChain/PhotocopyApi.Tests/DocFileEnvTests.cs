using System.Reflection;
using System.Runtime.ExceptionServices;
using PhotocopyApi.Controllers;
using Xunit;

namespace PhotocopyApi.Tests;

public sealed class DocFileEnvTests : IDisposable
{
    private readonly string _thuMuc = Path.Combine(Path.GetTempPath(), "photocopy-env-test-" + Guid.NewGuid());

    private object Goi(string ham, params object[] args)
    {
        var lop = typeof(AuthController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv");
        Assert.NotNull(lop);
        var method = lop.GetMethod(ham);
        Assert.Equal(args.Length, method.GetParameters().Length);
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private Dictionary<string, string> Doc(string noiDung)
    {
        Directory.CreateDirectory(_thuMuc);
        string path = Path.Combine(_thuMuc, ".env");
        File.WriteAllText(path, noiDung);
        return (Dictionary<string, string>)Goi("Doc", path);
    }

    [Fact]
    public void DocCauHinh_ChuyenHaiGachDuoiThanhDauHaiCham()
    {
        var config = Doc("# Không có bí mật thật\nEmail__Host=smtp.gmail.com\nEmail__Port=587\nEmail__MatKhau=\n");
        Assert.Equal("smtp.gmail.com", config["Email:Host"]);
        Assert.Equal("587", config["Email:Port"]);
        Assert.Equal("", config["Email:MatKhau"]);
    }

    [Fact]
    public void GiaTriCoNhay_GiuKhoangTrangDauBangVaDuongDanWindows()
    {
        var config = Doc("ConnectionStrings__PhotocopyChain='Server=localhost\\MSSQLSERVER01;Database=PhotocopyChain;'\nEmail__MatKhau=\" fake # secret = value \" # chú thích\n");
        Assert.Contains("localhost\\MSSQLSERVER01", config["ConnectionStrings:PhotocopyChain"]);
        Assert.Equal(" fake # secret = value ", config["Email:MatKhau"]);
    }

    [Fact]
    public void KhongThucThiHoacMoRongBienTrongGiaTri()
    {
        var config = Doc("Email__MatKhau='$(whoami) ${HOME} %USERPROFILE%'\nEmail__Host=smtp.gmail.com # chú thích\n");
        Assert.Equal("$(whoami) ${HOME} %USERPROFILE%", config["Email:MatKhau"]);
        Assert.Equal("smtp.gmail.com", config["Email:Host"]);
    }

    [Theory]
    [InlineData("Email__MatKhau='fake-secret'\nEmail__MatKhau=other")]
    [InlineData("fake-secret-without-equals")]
    [InlineData("Email__MatKhau='fake-secret")]
    [InlineData("Email__MatKhau='fake-secret' trailing")]
    [InlineData("invalid-key=fake-secret")]
    public void FileSai_BaoSoDongKhongLoGiaTri(string text)
    {
        var ex = Assert.Throws<FormatException>(() => Doc(text));
        Assert.Contains("dòng", ex.Message);
        Assert.DoesNotContain("fake-secret", ex.Message);
    }

    [Fact]
    public void ThieuFile_KhongAmThamQuayLaiCauHinhCu()
    {
        Assert.Throws<FileNotFoundException>(() => Goi("Doc", Path.Combine(_thuMuc, ".env")));
    }

    [Fact]
    public void DuongDanMacDinh_TaiGocSolution_KeCaChayTrongBin()
    {
        Directory.CreateDirectory(_thuMuc);
        File.WriteAllText(Path.Combine(_thuMuc, "PhotocopyChain.sln"), "");
        string bin = Path.Combine(_thuMuc, "PhotocopyApi", "bin", "Debug", "net8.0");
        Directory.CreateDirectory(bin);
        // Không lấy nhầm .env nằm trong thư mục con.
        File.WriteAllText(Path.Combine(bin, ".env"), "Email__Host=fake");
        Assert.Equal(Path.Combine(_thuMuc, ".env"), Goi("LayDuongDan", null, bin));
        Assert.Equal(Path.Combine(_thuMuc, ".env"), Goi("LayDuongDan", null, _thuMuc));
    }

    [Fact]
    public void DuongDanChiDinh_PhaiTuyetDoi_VaUuTienHonMacDinh()
    {
        string path = Path.Combine(_thuMuc, ".env");
        Assert.Equal(path, Goi("LayDuongDan", path, _thuMuc));
        Assert.Throws<ArgumentException>(() => Goi("LayDuongDan", ".env", _thuMuc));
    }

    [Fact]
    public void KhongTimThaySolution_YeuCauChiDinhDuongDan()
    {
        Directory.CreateDirectory(_thuMuc);
        Assert.Throws<DirectoryNotFoundException>(() => Goi("LayDuongDan", null, _thuMuc));
    }

    public void Dispose()
    {
        if (Directory.Exists(_thuMuc)) Directory.Delete(_thuMuc, recursive: true);
    }
}
