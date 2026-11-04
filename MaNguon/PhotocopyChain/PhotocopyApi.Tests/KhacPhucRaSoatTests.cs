using System.Security.Claims;
using PhotocopyApi.Auth;
using PhotocopyApi.DichVu;
using PhotocopyApi.Models;
using PhotocopyApi.CaLamViec;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;

public class KhacPhucRaSoatTests
{
    [Fact]
    public async Task TaiLenDongThoi_CungTenGoc_KhongChungNguonHayPdf()
    {
        string root = Path.Combine(Path.GetTempPath(), "UploadRace-" + Guid.NewGuid());
        try
        {
            var uploads = await Task.WhenAll(Enumerable.Range(0, 30).Select(i => Task.Run(() =>
            {
                var paths = DuongDanTaiLen.Tao(root, new DateTime(2026, 10, 29), "docx");
                File.WriteAllText(paths.Goc, "nguon-" + i);
                File.WriteAllText(paths.Pdf, "pdf-" + i);
                return (paths, i);
            })));
            Assert.Equal(30, uploads.Select(x => x.paths.Goc).Distinct().Count());
            Assert.Equal(30, uploads.Select(x => x.paths.Pdf).Distinct().Count());
            foreach (var (paths, i) in uploads)
            {
                Assert.Equal("nguon-" + i, File.ReadAllText(paths.Goc));
                Assert.Equal("pdf-" + i, File.ReadAllText(paths.Pdf));
            }
            File.Delete(uploads[0].paths.Pdf);
            Assert.True(File.Exists(uploads[1].paths.Pdf));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void PdfCoSan_DungChinhDuongDanGoc()
    {
        string root = Path.Combine(Path.GetTempPath(), "UploadPdf-" + Guid.NewGuid());
        try { var p = DuongDanTaiLen.Tao(root, DateTime.Today, "pdf"); Assert.Equal(p.Goc, p.Pdf); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void DoiMatKhauKhoaTaiKhoanVaDoiQuyen_LamPhienCuKhongHopLe()
    {
        string root = Path.Combine(Path.GetTempPath(), "SessionTest-" + Guid.NewGuid());
        try
        {
            var store = new PhienDangNhapStore(Path.Combine(root, "sessions.json"), "test-key-khong-phai-secret-thuc-te");
            var user = new NguoiDung { MaNguoiDung = 10, VaiTro = VaiTro.Staff, MaChiNhanh = 1, TrangThai = "HOAT_DONG", MatKhau = "hash-cu" };
            ClaimsPrincipal principal = Principal(user, store.TaoDauPhien(user));
            Assert.True(store.HopLe(principal, user));
            user.MatKhau = "hash-moi";
            Assert.False(store.HopLe(principal, user));
            user.MatKhau = "hash-cu";
            user.TrangThai = "KHOA";
            Assert.False(store.HopLe(principal, user));
            user.TrangThai = "HOAT_DONG";
            store.ThuHoi(user.MaNguoiDung);
            Assert.False(store.HopLe(principal, user));
            var restarted = new PhienDangNhapStore(Path.Combine(root, "sessions.json"), "test-key-khong-phai-secret-thuc-te");
            Assert.False(restarted.HopLe(principal, user));
            Assert.True(restarted.HopLe(Principal(user, restarted.TaoDauPhien(user)), user));
            user.MaChiNhanh = 2;
            Assert.False(restarted.HopLe(Principal(user, store.TaoDauPhien(new NguoiDung { MaNguoiDung = 10, VaiTro = VaiTro.Staff, MaChiNhanh = 1, MatKhau = "hash-cu" })), user));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(VaiTro.SuperAdmin)]
    [InlineData(VaiTro.Customer)]
    public void ThieuDauPhien_HoacSaiNguoiDung_KhongDuocBoQuaKeCaCEO(string role)
    {
        var store = new PhienDangNhapStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".json"), "test-key");
        var user = new NguoiDung { MaNguoiDung = 10, VaiTro = role, TrangThai = "HOAT_DONG" };
        Assert.False(store.HopLe(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(TenClaim.MaNguoiDung, "10") })), user));
        var principal = Principal(user, store.TaoDauPhien(user));
        Assert.True(store.HopLe(principal, user)); // Tài khoản Google có thể chưa có mật khẩu.
        Assert.False(store.HopLe(principal, null));
        user.MaNguoiDung = 11;
        Assert.False(store.HopLe(principal, user));
    }

    private static ClaimsPrincipal Principal(NguoiDung user, string stamp) => new(new ClaimsIdentity(new[]
    {
        new Claim(TenClaim.MaNguoiDung, user.MaNguoiDung.ToString()),
        new Claim(PhienDangNhapStore.TenClaimDauPhien, stamp)
    }, "test"));

    [Fact]
    public void OfficeKhongDuocNhanTienTrinhCoSan_HoacChiDuaVaoTen()
    {
        var now = DateTime.UtcNow;
        Assert.False(TienTrinhOffice.CoTheNhan(20, true, "EXCEL", "EXCEL", now, now));
        Assert.False(TienTrinhOffice.CoTheNhan(20, false, "WINWORD", "EXCEL", now, now));
        Assert.False(TienTrinhOffice.CoTheNhan(20, false, "EXCEL", "EXCEL", now.AddMinutes(-2), now));
        Assert.True(TienTrinhOffice.CoTheNhan(20, false, "EXCEL", "EXCEL", now, now));
    }

    [Theory]
    [InlineData(7,30,12,30)]
    [InlineData(12,29,12,30)]
    [InlineData(12,30,17,30)]
    [InlineData(17,29,17,30)]
    [InlineData(17,30,21,30)]
    [InlineData(21,29,21,30)]
    public void CaKetThucDungMoc_KeCaDangNhapGiuaCa(int hour,int minute,int endHour,int endMinute)
    {
        var open = new DateTime(2026,10,29,hour,minute,0);
        Assert.Equal(new DateTime(2026,10,29,endHour,endMinute,0), LichCa.GioKetThuc(open));
        Assert.True(LichCa.TrongGio(open));
    }

    [Theory]
    [InlineData(7,29)]
    [InlineData(21,30)]
    [InlineData(23,59)]
    public void NgoaiGioThuocCaSangSapToi_KetThuc1230(int hour,int minute)
    {
        var open = new DateTime(2026,10,29,hour,minute,0);
        Assert.False(LichCa.TrongGio(open));
        Assert.Equal(open.Date.AddDays(hour>=21?1:0).AddMinutes(750), LichCa.GioKetThuc(open));
    }
}
