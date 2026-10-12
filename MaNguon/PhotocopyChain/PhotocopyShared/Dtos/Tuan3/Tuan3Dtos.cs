using PhotocopyShared.KiemTraDuLieu;
using System.ComponentModel.DataAnnotations;
using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyShared.Dtos.Tuan3;

// DTO dùng cho các chức năng Tuần 3. Tên thuộc tính bám sát tên cột CSDL để Dapper tự ánh xạ.

public sealed partial class KhachTaiQuayRequest
{
    [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string? HoTen { get; set; } = "";
    public string? SoDienThoai { get; set; } = "";
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
    [Range(1, int.MaxValue, ErrorMessage = "Mã khách hàng phải là số nguyên dương.")]
    public int MaKhachHang { get; set; }
    /// <summary>VIETQR hoặc COD — khách tại quầy tự chọn trả trước hay trả khi nhận.</summary>
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(COD|VIETQR)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
    public string PhuongThucThanhToan { get; set; } = "COD";
    public bool InGap { get; set; }
    [RegularExpression(@"^[A-Za-z0-9_-]{1,30}$", ErrorMessage = "Mã khuyến mãi tối đa 30 ký tự, chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.")]
    public string? MaCodeKhuyenMai { get; set; }
    [MaxLength(255, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    public string? GhiChu { get; set; }
    [DanhSachFile]
    public List<DongInRequest> FileIn { get; set; } = [];
}

public sealed class HuyDonRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do hủy.")]
    [MaxLength(255, ErrorMessage = "Lý do tối đa 255 ký tự.")]
    public string LyDo { get; set; } = "";
}

public sealed partial class TaoInLaiTaiQuayRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã chi tiết phải là số nguyên dương.")]
    public int MaChiTietGoc { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Trang bắt đầu phải là số nguyên dương.")]
    public int TuTrang { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Trang kết thúc phải là số nguyên dương.")]
    public int DenTrang { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Số bản phải là số nguyên dương.")]
    public int SoBan { get; set; } = 1;
    [Required(ErrorMessage = "Vui lòng nhập lý do in lại.")]
    [MaxLength(255, ErrorMessage = "Lý do tối đa 255 ký tự.")]
    public string LyDo { get; set; } = "";
}

public sealed partial class TaoDonBoSungTaiQuayRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã chi tiết phải là số nguyên dương.")]
    public int MaChiTietGoc { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Trang bắt đầu phải là số nguyên dương.")]
    public int TuTrang { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Trang kết thúc phải là số nguyên dương.")]
    public int DenTrang { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Số bản phải là số nguyên dương.")]
    public int SoBan { get; set; } = 1;
    [Range(1, int.MaxValue, ErrorMessage = "Mã dịch vụ phải là số nguyên dương.")]
    public int MaDichVu { get; set; }
    [DanhSachMa]
    public List<int> DsGiaCong { get; set; } = [];
    [MaxLength(255, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
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
    [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao.")]
    public int SoSao { get; set; }
    [MaxLength(500, ErrorMessage = "Nhận xét tối đa 500 ký tự.")]
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
    [Required(ErrorMessage = "Vui lòng nhập số tiền kiểm đếm.")]
    [SoTien]
    public decimal? TienQuanLyDemCuoiCa { get; set; }
    [SoTien]
    public decimal TienChi { get; set; }
    [SoTien]
    public decimal TienBu { get; set; }
    [MaxLength(255, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    public string? GhiChu { get; set; }
}

public sealed class TaoNhanVienRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string HoTen { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(100, ErrorMessage = "Email tối đa 100 ký tự.")]
    [Gmail]
    public string Email { get; set; } = "";
    [RegularExpression(QuyTacNhap.SoDienThoai, ErrorMessage = QuyTacNhap.LoiSoDienThoai)]
    public string? SoDienThoai { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MatKhauMoi]
    public string MatKhau { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(STAFF|SHIPPER|BRANCH_ADMIN)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
    public string VaiTro { get; set; } = "STAFF";
    [Range(1, int.MaxValue, ErrorMessage = "Mã chi nhánh phải là số nguyên dương.")]
    public int? MaChiNhanh { get; set; }
}

public sealed class CapNhatTrangThaiRequest
{
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(HOAT_DONG|KHOA)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
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

public sealed partial class LuuDichVuRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ.")]
    [MaxLength(100, ErrorMessage = "Tên dịch vụ tối đa 100 ký tự.")]
    public string TenDichVu { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(IN_AN|GIA_CONG)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
    public string LoaiDichVu { get; set; } = "IN_AN";
    [MaxLength(5, ErrorMessage = "Khổ giấy tối đa 5 ký tự.")]
    [RegularExpression(@"^(A4|A3)$", ErrorMessage = "Khổ giấy phải là A4 hoặc A3.")]
    public string? KhoGiay { get; set; }
    [MaxLength(20, ErrorMessage = "Loại giấy tối đa 20 ký tự.")]
    public string? LoaiGiay { get; set; }
    [RegularExpression(@"^(1_MAT|2_MAT)$", ErrorMessage = "Mặt in phải là 1 mặt hoặc 2 mặt.")]
    public string? MatIn { get; set; }
    [RegularExpression(@"^(MAU|DEN_TRANG)$", ErrorMessage = "Màu in không hợp lệ.")]
    public string? MauSac { get; set; }
    [MaxLength(20, ErrorMessage = "Nhóm gia công tối đa 20 ký tự.")]
    public string? NhomGiaCong { get; set; }
    [SoTien]
    public decimal DonGia { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Mã vật tư phải là số nguyên dương.")]
    public int? MaVatTu { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(HIEN|AN)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
    public string TrangThai { get; set; } = "HIEN";
}

public sealed partial class ChiNhanhQuanTriDto
{
    public int MaChiNhanh { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên chi nhánh.")]
    [MaxLength(100, ErrorMessage = "Tên chi nhánh tối đa 100 ký tự.")]
    public string TenChiNhanh { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
    [MaxLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự.")]
    public string DiaChi { get; set; } = "";
    [RegularExpression(QuyTacNhap.SoDienThoai, ErrorMessage = QuyTacNhap.LoiSoDienThoai)]
    public string? SoDienThoai { get; set; }
    [Range(typeof(decimal), "-90", "90", ErrorMessage = "Vĩ độ phải từ -90 đến 90.")]
    public decimal? ViDo { get; set; }
    [Range(typeof(decimal), "-180", "180", ErrorMessage = "Kinh độ phải từ -180 đến 180.")]
    public decimal? KinhDo { get; set; }
    public TimeSpan GioMoCua { get; set; }
    public TimeSpan GioDongCua { get; set; }
    [Range(typeof(decimal), "0", "999.99", ErrorMessage = "Bán kính giao phải từ 0 đến 999,99 km.")]
    public decimal BanKinhGiao { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(HOAT_DONG|TAM_NGUNG)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
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
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(HOAT_DONG|BAO_TRI|HONG)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
    public string TrangThai { get; set; } = "HOAT_DONG";
    [Range(0, int.MaxValue, ErrorMessage = "Chỉ số trang in phải là số nguyên không âm.")]
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

public sealed partial class LuuKhuyenMaiRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã khuyến mãi.")]
    [RegularExpression(@"^[A-Za-z0-9_-]{1,30}$", ErrorMessage = "Mã khuyến mãi tối đa 30 ký tự, chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.")]
    public string MaCode { get; set; } = "";
    [MaxLength(150, ErrorMessage = "Tên khuyến mãi tối đa 150 ký tự.")]
    public string? TenKhuyenMai { get; set; }
    [Range(1, 100, ErrorMessage = "Phần trăm giảm phải từ 1 đến 100.")]
    public int PhanTramGiam { get; set; }
    [SoTien]
    public decimal? GiamToiDa { get; set; }
    [SoTien]
    public decimal DonToiThieu { get; set; }
    public DateTime NgayBatDau { get; set; }
    public DateTime NgayKetThuc { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Số lượng khuyến mãi phải là số nguyên không âm.")]
    public int SoLuong { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(HOAT_DONG|TAM_DUNG)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
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
    [Range(1, int.MaxValue, ErrorMessage = "Mã vật tư phải là số nguyên dương.")]
    public int MaVatTu { get; set; }
    [MaxLength(150, ErrorMessage = "Nhà cung cấp tối đa 150 ký tự.")]
    public string? NhaCungCap { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng nhập phải là số nguyên dương.")]
    public int SoLuong { get; set; }
    [SoTien]
    public decimal DonGia { get; set; }
}

public sealed class DuyetPhieuNhapRequest
{
    public bool DongY { get; set; }
}

public sealed class NhanHangRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng thực nhận phải là số nguyên dương.")]
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
