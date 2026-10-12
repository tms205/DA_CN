using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyShared.Dtos.Tuan3;

// DTO dùng cho các chức năng Tuần 3. Tên thuộc tính bám sát tên cột CSDL để Dapper tự ánh xạ.

public sealed class KhachTaiQuayRequest
{
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
}

public sealed class KhachTaiQuayDto
{
    public int MaKhachHang { get; set; }
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public string Email { get; set; } = "";
    public bool VuaTao { get; set; }
}

public sealed class DatDonTaiQuayRequest
{
    public int MaKhachHang { get; set; }
    /// <summary>VIETQR hoặc COD — khách tại quầy tự chọn trả trước hay trả khi nhận.</summary>
    public string PhuongThucThanhToan { get; set; } = "COD";
    public bool InGap { get; set; }
    public string? MaCodeKhuyenMai { get; set; }
    public string? GhiChu { get; set; }
    public List<DongInRequest> FileIn { get; set; } = [];
}

public sealed class HuyDonRequest
{
    public string LyDo { get; set; } = "";
}

public sealed class TaoInLaiTaiQuayRequest
{
    public int MaChiTietGoc { get; set; }
    public int TuTrang { get; set; }
    public int DenTrang { get; set; }
    public int SoBan { get; set; } = 1;
    public string LyDo { get; set; } = "";
}

public sealed class TaoDonBoSungTaiQuayRequest
{
    public int MaChiTietGoc { get; set; }
    public int TuTrang { get; set; }
    public int DenTrang { get; set; }
    public int SoBan { get; set; } = 1;
    public int MaDichVu { get; set; }
    public List<int> DsGiaCong { get; set; } = [];
    public string? GhiChu { get; set; }
}

public sealed class DonPhatSinhResponse
{
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    public string LoaiPhatSinh { get; set; } = "";
    public decimal TongTien { get; set; }
    public string Message { get; set; } = "";
}

public sealed class DanhGiaRequest
{
    public int SoSao { get; set; }
    public string? NhanXet { get; set; }
}

public sealed class DanhGiaDto
{
    public int MaDanhGia { get; set; }
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public int SoSao { get; set; }
    public string? NhanXet { get; set; }
    public string TrangThai { get; set; } = "";
    public DateTime NgayDanhGia { get; set; }
}

public sealed class XacNhanCaRequest
{
    /// <summary>Số tiền quản lý trực tiếp kiểm đếm, không lấy theo số nhân viên đã khai.</summary>
    public decimal? TienQuanLyDemCuoiCa { get; set; }
    public decimal TienChi { get; set; }
    public decimal TienBu { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class TaoNhanVienRequest
{
    public string HoTen { get; set; } = "";
    public string Email { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string MatKhau { get; set; } = "";
    public string VaiTro { get; set; } = "STAFF";
    public int? MaChiNhanh { get; set; }
}

public sealed class CapNhatTrangThaiRequest
{
    public string TrangThai { get; set; } = "";
}

public sealed class DichVuQuanTriDto
{
    public int MaDichVu { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public string TenDichVu { get; set; } = "";
    public string LoaiDichVu { get; set; } = "";
    public string? KhoGiay { get; set; }
    public string? LoaiGiay { get; set; }
    public string? MatIn { get; set; }
    public string? MauSac { get; set; }
    public string? NhomGiaCong { get; set; }
    public decimal DonGia { get; set; }
    public int? MaVatTu { get; set; }
    public string? TenVatTu { get; set; }
    public string TrangThai { get; set; } = "";
}

public sealed class LuuDichVuRequest
{
    public string TenDichVu { get; set; } = "";
    public string LoaiDichVu { get; set; } = "IN_AN";
    public string? KhoGiay { get; set; }
    public string? LoaiGiay { get; set; }
    public string? MatIn { get; set; }
    public string? MauSac { get; set; }
    public string? NhomGiaCong { get; set; }
    public decimal DonGia { get; set; }
    public int? MaVatTu { get; set; }
    public string TrangThai { get; set; } = "HIEN";
}

public sealed class ChiNhanhQuanTriDto
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public decimal? ViDo { get; set; }
    public decimal? KinhDo { get; set; }
    public TimeSpan GioMoCua { get; set; }
    public TimeSpan GioDongCua { get; set; }
    public decimal BanKinhGiao { get; set; }
    public string TrangThai { get; set; } = "";
}

public sealed class MayInQuanTriDto
{
    public int MaMayIn { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public string TenMay { get; set; } = "";
    public string KhoGiay { get; set; } = "";
    public bool InMau { get; set; }
    public bool In2Mat { get; set; }
    public int TocDo { get; set; }
    public int SoTrangDaIn { get; set; }
    public string TrangThai { get; set; } = "";
}

public sealed class CapNhatMayInRequest
{
    public string TrangThai { get; set; } = "HOAT_DONG";
    public int? SoTrangDaIn { get; set; }
}

public sealed class KhachHangQuanTriDto
{
    public int MaNguoiDung { get; set; }
    public string HoTen { get; set; } = "";
    public string Email { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public string TrangThai { get; set; } = "";
    public DateTime NgayTao { get; set; }
    public int SoDon { get; set; }
    public decimal TongChiTieu { get; set; }
}

public sealed class KhuyenMaiQuanTriDto
{
    public int MaKhuyenMai { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public string MaCode { get; set; } = "";
    public string? TenKhuyenMai { get; set; }
    public int PhanTramGiam { get; set; }
    public decimal? GiamToiDa { get; set; }
    public decimal DonToiThieu { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public int SoLuong { get; set; }
    public int DaDung { get; set; }
    public string TrangThai { get; set; } = "";
}

public sealed class LuuKhuyenMaiRequest
{
    public string MaCode { get; set; } = "";
    public string? TenKhuyenMai { get; set; }
    public int PhanTramGiam { get; set; }
    public decimal? GiamToiDa { get; set; }
    public decimal DonToiThieu { get; set; }
    public DateTime NgayBatDau { get; set; }
    public DateTime NgayKetThuc { get; set; }
    public int SoLuong { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
}

public sealed class TonKhoDto
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public int MaVatTu { get; set; }
    public string TenVatTu { get; set; } = "";
    public string? DonViTinh { get; set; }
    public int SoLuong { get; set; }
    public int MucToiThieu { get; set; }
    public decimal DonGia { get; set; }
    public bool CanhBao { get; set; }
}

public sealed class PhieuNhapDto
{
    public int MaPhieuNhap { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public int MaVatTu { get; set; }
    public string TenVatTu { get; set; } = "";
    public string? NhaCungCap { get; set; }
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public string? TenNguoiLap { get; set; }
    public string? TenNguoiDuyet { get; set; }
    public string TrangThai { get; set; } = "";
    public DateTime NgayLap { get; set; }
}

public sealed class TaoPhieuNhapRequest
{
    public int MaVatTu { get; set; }
    public string? NhaCungCap { get; set; }
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
}

public sealed class DuyetPhieuNhapRequest
{
    public bool DongY { get; set; }
}

public sealed class NhanHangRequest
{
    public int SoLuongThucNhan { get; set; }
}

public sealed class BaoCaoDoanhThuDto
{
    public DateTime Ngay { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public int SoDon { get; set; }
    public decimal DoanhThu { get; set; }
}

public sealed class DoiSoatMayInDto
{
    public int MaMayIn { get; set; }
    public string TenMay { get; set; } = "";
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public int SoTrangTrenMay { get; set; }
    public int SoTrangHeThong { get; set; }
    public int ChenhLech { get; set; }
}

public sealed class HoanTienChoDuyetDto
{
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public decimal SoTien { get; set; }
    public string? GhiChu { get; set; }
    public DateTime NgayDat { get; set; }
}

public sealed class KhoFileDto
{
    public int MaFile { get; set; }
    public string TenFile { get; set; } = "";
    public string? DinhDang { get; set; }
    public int? DungLuong { get; set; }
    public int? SoTrang { get; set; }
    public string TrangThai { get; set; } = "";
    public DateTime NgayTaiLen { get; set; }
    public string TenKhachHang { get; set; } = "";
    public int SoDonDangDung { get; set; }
}
