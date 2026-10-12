using System.ComponentModel.DataAnnotations;
using PhotocopyShared.Dtos.Auth;
using PhotocopyShared.Dtos.DatIn;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;
using PhotocopyShared.KiemTraDuLieu;

namespace PhotocopyApi.Tests;

public class KiemTraDuLieuTests
{
    public static IEnumerable<object[]> DuLieuSai()
    {
        yield return [new DangNhapRequest { TaiKhoan = "khong-phai-email", MatKhau = "old" }];
        yield return [new DangNhapRequest { TaiKhoan = "091234567", MatKhau = "old" }];
        yield return [new DangKyRequest { HoTen = "Nguyễn Văn A", Email = "a@gmail.com", SoDienThoai = "0912345678", MatKhau = "Aa12345\n" }];
        yield return [new CapNhatHoSoRequest { HoTen = "A", SoDienThoai = "0١٢٣٤٥٦٧٨٩" }];
        yield return [new CapNhatHoSoRequest { HoTen = "A", DiaChiMacDinh = new string('A', 256) }];
        yield return [new XacNhanDangKyRequest { Email = "a@gmail.com", MaOtp = "١٢٣٤٥٦" }];
        yield return [new XacMinhOtpDatLaiRequest { Email = "sv1@huit.edu.vn", MaOtp = "12345x" }];
        yield return [new DoiMatKhauRequest { MatKhauCu = "old", MatKhauMoi = "weak" }];
        yield return [new TaoNhanVienRequest { HoTen = "Nguyễn Văn A", Email = "a@gmail.com", MatKhau = "Abcd1234", VaiTro = "SUPER_ADMIN", MaChiNhanh = 1 }];
        yield return [new TaoNhanVienRequest { HoTen = "Nguyễn Văn A", Email = "a@gmail.com", MatKhau = "Abcd1234", SoDienThoai = "091234567", MaChiNhanh = 1 }];
        yield return [new HuyDonRequest { LyDo = "   " }];
        yield return [new DongInRequest { MaFileIn = 1, MaDichVu = 1, SoBanIn = 0 }];
        yield return [new DongInRequest { MaFileIn = 1, MaDichVu = 1, DsGiaCong = null }];
        yield return [new DongInRequest { MaFileIn = 1, MaDichVu = 1, DsGiaCong = [-1] }];
        yield return [new DatDonTaiQuayRequest { MaKhachHang = 1, PhuongThucThanhToan = "X", FileIn = [new() { MaFileIn = 1, MaDichVu = 1 }] }];
        yield return [new DatDonTaiQuayRequest { MaKhachHang = 1, GhiChu = new string('A', 256), FileIn = [new() { MaFileIn = 1, MaDichVu = 1 }] }];
        yield return [new BaoGiaRequest { MaChiNhanh = 1, FileIn = [new() { MaFileIn = 1, MaDichVu = 1 }, new() { MaFileIn = 1, MaDichVu = 1 }] }];
        yield return [new DatDonHangRequest { MaChiNhanh = 1, SdtNhan = "1234567890", FileIn = [new() { MaFileIn = 1, MaDichVu = 1 }] }];
        yield return [new BaoGiaRequest { MaChiNhanh = 1, MaCodeKhuyenMai = "ABC!", FileIn = [new() { MaFileIn = 1, MaDichVu = 1 }] }];
        yield return [new BaoGiaRequest { MaChiNhanh = 1, KinhDoGiao = 181, ViDoGiao = 10, FileIn = [new() { MaFileIn = 1, MaDichVu = 1 }] }];
        yield return [new ChiNhanhDatDonRequest { DsMaDichVu = [1, 1] }];
        yield return [new PhucHoiFileInRequest { MaFileIn = 0 }];
        yield return [new CapNhatTrangThaiInRequest { TrangThaiMoi = "HOAN_THANH" }];
        yield return [new LuuDichVuRequest { TenDichVu = "In", DonGia = -1 }];
        yield return [new LuuDichVuRequest { TenDichVu = "In", DonGia = 100 }];
        yield return [new LuuDichVuRequest { TenDichVu = "In", LoaiDichVu = "GIA_CONG", DonGia = 1.5m }];
        yield return [new LuuDichVuRequest { TenDichVu = "In", LoaiDichVu = "GIA_CONG", KhoGiay = "A5" }];
        yield return [new LuuKhuyenMaiRequest { MaCode = "A", PhanTramGiam = 0, NgayBatDau = DateTime.Today, NgayKetThuc = DateTime.Today }];
        yield return [new LuuKhuyenMaiRequest { MaCode = "A", PhanTramGiam = 10, NgayBatDau = default, NgayKetThuc = DateTime.Today }];
        yield return [new LuuKhuyenMaiRequest { MaCode = "A", PhanTramGiam = 10, NgayBatDau = DateTime.Today, NgayKetThuc = DateTime.Today, SoLuong = -1 }];
        yield return [new XacNhanCaRequest { TienQuanLyDemCuoiCa = null }];
        yield return [new XacNhanCaRequest { TienQuanLyDemCuoiCa = 0, TienChi = -1 }];
        yield return [new NhanHangRequest { SoLuongThucNhan = 0 }];
        yield return [new TaoDonBoSungTaiQuayRequest { MaChiTietGoc = 1, TuTrang = 2, DenTrang = 1, MaDichVu = 1 }];
    }

    [Theory]
    [MemberData(nameof(DuLieuSai))]
    public void MoiNhomNhap_DeuTuChoiDuLieuSai(object model) => Assert.False(HopLe(model));

    private static bool HopLe(object model) => Validator.TryValidateObject(model,
        new ValidationContext(model), new List<ValidationResult>(), true);

    [Fact]
    public void LoiDongIn_NoiRoTruongSai()
    {
        var loi = QuyTacNhap.LayLoi(new BaoGiaRequest { MaChiNhanh = 1,
            FileIn = [new() { MaFileIn = 1, MaDichVu = 1, SoBanIn = 0 }] });
        Assert.Contains("Số bản in", loi);
    }

    [Theory]
    [InlineData("091234567")]
    [InlineData("09123456789")]
    [InlineData("+84912345678")]
    [InlineData("1234567890")]
    [InlineData("٠١٢٣٤٥٦٧٨٩")]
    public void KhachTaiQuay_SdtPhaiDu10SoAsciiBatDau0(string sdt)
        => Assert.False(HopLe(new KhachTaiQuayRequest { HoTen = "Nguyễn Văn A", SoDienThoai = sdt }));

    [Theory]
    [InlineData("sv1@huit.edu.vn")]
    [InlineData("a@gmail.com.evil.com")]
    [InlineData("a@@gmail.com")]
    [InlineData("a..b@gmail.com")]
    public void EmailMoi_ChiNhanGmailHopLe(string email)
    {
        Assert.False(HopLe(new DangKyRequest { HoTen = "Nguyễn Văn A", Email = email,
            SoDienThoai = "0912345678", MatKhau = "Abcd1234" }));
        Assert.False(HopLe(new TaoNhanVienRequest { HoTen = "Nguyễn Văn A", Email = email,
            MatKhau = "Abcd1234", MaChiNhanh = 1 }));
    }

    [Fact]
    public void TaiKhoanCu_VanDangNhapVaQuenMatKhauDuoc()
    {
        Assert.True(HopLe(new DangNhapRequest { TaiKhoan = "sv1@huit.edu.vn", MatKhau = "old" }));
        Assert.True(HopLe(new QuenMatKhauRequest { Email = "sv1@huit.edu.vn" }));
        Assert.True(HopLe(new DangKyRequest { HoTen = "Nguyễn Văn A", Email = "User.Name@GMAIL.COM",
            SoDienThoai = "0912345678", MatKhau = "Abcd1234" }));
    }

    [Fact]
    public void ChuoiVaMatKhau_KhongVuotKichThuocLuuTru()
    {
        Assert.False(HopLe(new KhachTaiQuayRequest { HoTen = new string('A', 101), SoDienThoai = "0912345678" }));
        Assert.False(HopLe(new TaoNhanVienRequest { HoTen = "A", Email = "a@gmail.com",
            MatKhau = "Aa1" + new string('á', 40), MaChiNhanh = 1 }));
        Assert.False(HopLe(new HuyDonRequest { LyDo = new string('A', 256) }));
        Assert.False(HopLe(new DanhGiaRequest { SoSao = 6 }));
        Assert.False(HopLe(new DanhGiaRequest { SoSao = 5, NhanXet = new string('A', 501) }));
    }

    [Fact]
    public void TienVaSoLuong_KhongAmKhongLeKhongTranCot()
    {
        Assert.False(HopLe(new TaoPhieuNhapRequest { MaVatTu = 1, SoLuong = 0, DonGia = 100 }));
        Assert.False(HopLe(new TaoPhieuNhapRequest { MaVatTu = 1, SoLuong = 1, DonGia = 1.5m }));
        Assert.False(HopLe(new MoCaRequest { TienDauCa = -1 }));
        Assert.False(HopLe(new DongCaRequest { TongDoanhThuCuoiCa = 1000000000000m }));
        Assert.False(HopLe(new CapNhatMayInRequest { SoTrangDaIn = -1 }));
        Assert.True(HopLe(new TaoPhieuNhapRequest { MaVatTu = 1, SoLuong = 1, DonGia = 0 }));
    }

    [Fact]
    public void NgayKhuyenMaiVaGioChiNhanh_PhaiHopLe()
    {
        Assert.False(HopLe(new LuuKhuyenMaiRequest { MaCode = "ABC", PhanTramGiam = 10,
            NgayBatDau = new DateTime(2026, 10, 12, 12, 0, 0),
            NgayKetThuc = new DateTime(2026, 10, 12, 11, 0, 0), SoLuong = 1 }));
        Assert.False(HopLe(new ChiNhanhQuanTriDto { TenChiNhanh = "A", DiaChi = "B",
            GioMoCua = TimeSpan.FromHours(18), GioDongCua = TimeSpan.FromHours(8), TrangThai = "HOAT_DONG" }));
        Assert.False(HopLe(new ChiNhanhQuanTriDto { TenChiNhanh = "A", DiaChi = "B",
            GioMoCua = TimeSpan.FromHours(8), GioDongCua = TimeSpan.FromHours(18), ViDo = 91, TrangThai = "HOAT_DONG" }));
    }

    [Fact]
    public void GiaoHang_CanDiaChiVaSdtVaGpsDuCap()
    {
        var req = new DatDonHangRequest { MaChiNhanh = 1, HinhThucNhan = "GIAO_HANG",
            FileIn = [new DongInRequest { MaFileIn = 1, MaDichVu = 1 }] };
        Assert.False(HopLe(req));
        req.DiaChiGiao = "140 Lê Trọng Tấn"; req.SdtNhan = "0912345678";
        Assert.True(HopLe(req));
        req.ViDoGiao = 10;
        Assert.False(HopLe(req));
    }

    [Fact]
    public void DanhSachVaQuyCach_KhongNullTrungHoacSaiEnum()
    {
        Assert.False(HopLe(new BaoGiaRequest { MaChiNhanh = 1, FileIn = null }));
        Assert.False(HopLe(new BaoGiaRequest { MaChiNhanh = 1, FileIn = [] }));
        Assert.False(HopLe(new BaoGiaRequest { MaChiNhanh = 1, FileIn = [null] }));
        Assert.False(HopLe(new DongInRequest { MaFileIn = 1, MaDichVu = 1, DsGiaCong = [1, 1] }));
        Assert.False(HopLe(new DongInRequest { MaFileIn = 1, MaDichVu = 1, HuongGiay = "dung" }));
        Assert.False(HopLe(new CapNhatTrangThaiRequest { TrangThai = "DA_HUY" }));
        Assert.False(HopLe(new TaoInLaiTaiQuayRequest { MaChiTietGoc = 1, TuTrang = 5, DenTrang = 2,
            LyDo = "In lỗi" }));
    }
}
