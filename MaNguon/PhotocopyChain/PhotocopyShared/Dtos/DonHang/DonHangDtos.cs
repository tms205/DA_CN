using PhotocopyShared.KiemTraDuLieu;
using System.ComponentModel.DataAnnotations;
using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyShared.Dtos.DonHang;

public sealed class PhucHoiFileInRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã file phải là số nguyên dương.")]
    public int MaFileIn { get; set; }
}

/// <summary>Một thẻ trong danh sách "Đơn hàng của tôi" (F-12) trên App.</summary>
public sealed class DonHangTomTatDto
{
    public int MaDonHang { get; set; }
    public string? MaDonHangHienThi { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    /// <summary>TAI_QUAY / GIAO_HANG.</summary>
    public string HinhThucNhan { get; set; } = "";
    public string? DiaChiGiao { get; set; }
    public bool InHoaToc { get; set; }
    public string TrangThaiDonHang { get; set; } = "";
    /// <summary>VIETQR / COD — để App biết đơn nào cần quay lại màn thanh toán.</summary>
    public string PhuongThucThanhToan { get; set; } = "";
    /// <summary>CHUA_THANH_TOAN / DA_THANH_TOAN / DA_HOAN_TIEN.</summary>
    public string TrangThaiThanhToan { get; set; } = "";
    public decimal TongTien { get; set; }
    public DateTime NgayDat { get; set; }
    public DateTime? NgayHoanTat { get; set; }
    public DateTime? ThoiGianDuKienHoanThanh { get; set; }
    public int SoFile { get; set; }
    /// <summary>Tổng trang in = Σ số trang × số bản của các dòng in.</summary>
    public int TongSoTrang { get; set; }
    public string? TenFileDau { get; set; }
    public bool DaDanhGia { get; set; }
}

/// <summary>F-13: một dòng trong trung tâm thông báo.</summary>
public sealed class ThongBaoDto
{
    public int MaThongBao { get; set; }
    public int? MaDonHang { get; set; }
    public string? MaDonHangHienThi { get; set; }
    public string TieuDe { get; set; } = "";
    public string NoiDung { get; set; } = "";
    /// <summary>DON_HANG / THANH_TOAN / KHO_VAT_TU / HE_THONG.</summary>
    public string LoaiThongBao { get; set; } = "";
    public bool DaDoc { get; set; }
    public DateTime NgayTao { get; set; }
}

public sealed class DanhSachThongBaoDto
{
    public int SoChuaDoc { get; set; }
    public List<ThongBaoDto> DanhSach { get; set; } = [];
}

// ==================================================================== F-09: ĐẶT ĐƠN (App gửi lên)

/// <summary>F-09 — bước 3/4 trên App: đặt đơn thật. Dùng lại <see cref="DongInRequest"/> của màn báo giá.
/// KHÔNG có trường phương thức thanh toán: đơn đặt trên App luôn phải chuyển khoản VietQR trước
/// (chốt 20/09/2026). Tiền mặt chỉ dành cho khách tới tận tiệm ngồi đợi in — đơn đó do nhân viên quầy lập.</summary>
public sealed partial class DatDonHangRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã chi nhánh phải là số nguyên dương.")]
    public int MaChiNhanh { get; set; }
    /// <summary>TAI_QUAY hoặc GIAO_HANG.</summary>
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(TAI_QUAY|GIAO_HANG)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
    public string HinhThucNhan { get; set; } = "TAI_QUAY";
    /// <summary>Bắt buộc khi GIAO_HANG.</summary>
    [MaxLength(255, ErrorMessage = "Địa chỉ giao tối đa 255 ký tự.")]
    public string? DiaChiGiao { get; set; }
    /// <summary>Tọa độ GPS khách bấm "Dùng vị trí hiện tại" — gửi cho Ahamove định vị chính xác hơn.</summary>
    [Range(typeof(decimal), "-90", "90", ErrorMessage = "Vĩ độ phải từ -90 đến 90.")]
    public decimal? ViDoGiao { get; set; }
    [Range(typeof(decimal), "-180", "180", ErrorMessage = "Kinh độ phải từ -180 đến 180.")]
    public decimal? KinhDoGiao { get; set; }
    [RegularExpression(QuyTacNhap.SoDienThoai, ErrorMessage = QuyTacNhap.LoiSoDienThoai)]
    public string? SdtNhan { get; set; }
    public bool InGap { get; set; }
    [RegularExpression(@"^[A-Za-z0-9_-]{1,30}$", ErrorMessage = "Mã khuyến mãi tối đa 30 ký tự, chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.")]
    public string? MaCodeKhuyenMai { get; set; }
    /// <summary>Ghi chú cho thợ in (nhập ở bước 2).</summary>
    [MaxLength(255, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    public string? GhiChu { get; set; }
    /// <summary>Danh sách file cùng cấu hình in mà khách đã chọn.</summary>
    [DanhSachFile]
    public List<DongInRequest> FileIn { get; set; } = [];
}

/// <summary>Kết quả đặt đơn — App dùng để mở màn Thanh toán.</summary>
public sealed class DatDonHangResponse
{
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    /// <summary>VIETQR / COD (TIEN_MAT chỉ còn ở dữ liệu cũ).</summary>
    public string PhuongThucThanhToan { get; set; } = "";
    public decimal TongTien { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>Bước 3 gửi lên các dòng giá đã chọn để hỏi chi nhánh nào in được.</summary>
public sealed class ChiNhanhDatDonRequest
{
    /// <summary>Các mã dịch vụ IN_AN khách đã chọn (mỗi file 1 mã).</summary>
    [DanhSachMa(BatBuoc = true)]
    public List<int> DsMaDichVu { get; set; } = [];
}

// ==================================================================== F-11 / F-12: CHI TIẾT ĐƠN + TIẾN ĐỘ

/// <summary>Một dòng in trong đơn (1 file + 1 cấu hình in + các gia công đi kèm).</summary>
public sealed class DongInChiTietDto
{
    public int MaChiTiet { get; set; }
    public int MaFile { get; set; }
    public int MaDichVu { get; set; }
    public string TenFile { get; set; } = "";
    public int? DungLuong { get; set; }
    public int TongSoTrangFile { get; set; }
    /// <summary>SAN_SANG / DA_XOA — file bị xóa khi đơn hoàn tất hoặc bị hủy (F-31).</summary>
    public string TrangThaiFile { get; set; } = "";
    public string TenDichVu { get; set; } = "";
    public string? KhoGiay { get; set; }
    public string? LoaiGiay { get; set; }
    public string? MatIn { get; set; }
    public string HuongGiay { get; set; } = "DUNG";
    public string? MauSac { get; set; }
    public int SoTrang { get; set; }
    public int? TuTrang { get; set; }
    public int? DenTrang { get; set; }
    public int SoBan { get; set; }
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }
    /// <summary>Tên các gia công của dòng này, vd "Đóng lò xo, Ép plastic".</summary>
    public string? TenGiaCong { get; set; }
}

/// <summary>Một mốc trên thanh tiến độ (F-11). API dựng sẵn để App / Web chỉ việc vẽ.</summary>
public sealed class MocTienDoDto
{
    /// <summary>CHO_XAC_NHAN, DA_XAC_NHAN, DANG_IN, DA_IN, DANG_GIAO, HOAN_THANH, DA_HUY.</summary>
    public string Ma { get; set; } = "";
    public string TieuDe { get; set; } = "";
    public string? MoTa { get; set; }
    public DateTime? ThoiDiem { get; set; }
    /// <summary>Mốc đã đi qua (tô đậm).</summary>
    public bool DaXong { get; set; }
    /// <summary>Mốc đơn đang đứng (chấm nhấp nháy).</summary>
    public bool DangO { get; set; }
}

/// <summary>F-11: chi tiết 1 đơn — dùng chung cho App (màn Theo dõi tiến độ) và Web (panel bên phải).</summary>
public sealed class DonHangChiTietDto
{
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    public int? MaDonHangGoc { get; set; }
    public string? MaDonHangGocHienThi { get; set; }
    public string? LoaiPhatSinh { get; set; }
    public string? LyDoPhatSinh { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public string DiaChiChiNhanh { get; set; } = "";
    public string? SoDienThoaiChiNhanh { get; set; }

    public int MaKhachHang { get; set; }
    public string TenKhachHang { get; set; } = "";
    public string? SoDienThoaiKhach { get; set; }

    public string HinhThucNhan { get; set; } = "";
    public string? DiaChiGiao { get; set; }
    public string? SdtNhan { get; set; }
    public bool InHoaToc { get; set; }
    public string TrangThaiDonHang { get; set; } = "";
    public string? GhiChu { get; set; }

    public decimal TienHang { get; set; }
    public decimal PhiShip { get; set; }
    public decimal GiamGia { get; set; }
    public decimal TongTien { get; set; }

    /// <summary>VIETQR / COD.</summary>
    public string PhuongThucThanhToan { get; set; } = "";
    /// <summary>CHUA_THANH_TOAN / DA_THANH_TOAN / DA_HOAN_TIEN.</summary>
    public string TrangThaiThanhToan { get; set; } = "";

    public string? TenNhanVien { get; set; }
    public string? TenMayIn { get; set; }
    public string? TenShipper { get; set; }
    public string? SoDienThoaiShipper { get; set; }

    public DateTime NgayDat { get; set; }
    public DateTime? NgayHenTra { get; set; }
    public DateTime? NgayHoanThanh { get; set; }

    public int TongSoTrang { get; set; }
    public bool DaDanhGia { get; set; }

    public List<DongInChiTietDto> DongIn { get; set; } = [];
    public List<MocTienDoDto> TienDo { get; set; } = [];
}

// ==================================================================== F-15 / F-17 / F-21: HÀNG ĐỢI TRÊN WEB

/// <summary>Một dòng trong bảng Hàng đợi đơn hàng của Web quản trị (Figma 04-Admin-HangDoiDon).</summary>
public sealed class HangDoiDonDto
{
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    public int? MaDonHangGoc { get; set; }
    public string? MaDonHangGocHienThi { get; set; }
    public string? LoaiPhatSinh { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string? SoDienThoaiKhach { get; set; }
    public string HinhThucNhan { get; set; } = "";
    public bool InHoaToc { get; set; }
    public string TrangThaiDonHang { get; set; } = "";
    public string PhuongThucThanhToan { get; set; } = "";
    public string TrangThaiThanhToan { get; set; } = "";
    public decimal TongTien { get; set; }
    public DateTime NgayDat { get; set; }
    public DateTime? NgayHenTra { get; set; }
    public int SoFile { get; set; }
    public int TongSoTrang { get; set; }
    /// <summary>Quy cách dòng in đầu tiên, vd "A4 70gsm · 2 mặt · Đen trắng".</summary>
    public string? QuyCach { get; set; }
    public string? TenNhanVien { get; set; }
    public string? TenMayIn { get; set; }
}

/// <summary>Một máy in của chi nhánh, kèm cờ có in được đơn đang chọn không (F-21).</summary>
public sealed class MayInDto
{
    public int MaMayIn { get; set; }
    public string TenMay { get; set; } = "";
    public string? KhoGiay { get; set; }
    public bool InMau { get; set; }
    public bool In2Mat { get; set; }
    public int TocDo { get; set; }
    public string TrangThai { get; set; } = "";
    /// <summary>Máy in được đủ khổ giấy / in màu / in 2 mặt của đơn không.</summary>
    public bool DapUngDuoc { get; set; }
    public string? LyDoKhongDapUng { get; set; }
}

/// <summary>Web gửi lên khi chuyển trạng thái in (F-17 / F-21).</summary>
public sealed class CapNhatTrangThaiInRequest
{
    /// <summary>DANG_IN hoặc DA_IN.</summary>
    [Required(ErrorMessage = "Vui lòng nhập giá trị.")]
    [RegularExpression(@"^(DANG_IN|DA_IN)$", ErrorMessage = "Lựa chọn không hợp lệ.")]
    public string TrangThaiMoi { get; set; } = "";
    /// <summary>Máy in được chọn khi bắt đầu in (F-21) — có thể bỏ trống.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Mã máy in phải là số nguyên dương.")]
    public int? MaMayIn { get; set; }
}

// ==================================================================== F-10: THANH TOÁN

/// <summary>Thông tin màn Thanh toán VietQR (Figma 02-Mobile-ThanhToan).</summary>
public sealed class ThanhToanDto
{
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    public string PhuongThuc { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public decimal SoTien { get; set; }
    public string TrangThaiDonHang { get; set; } = "";

    /// <summary>Tài khoản nhận tiền (khai trong appsettings mục ThanhToan).</summary>
    public string? TenNganHang { get; set; }
    public string? SoTaiKhoan { get; set; }
    public string? ChuTaiKhoan { get; set; }
    /// <summary>Nội dung chuyển khoản để đối chiếu tự động, vd "PCCDH00012".</summary>
    public string? NoiDungChuyenKhoan { get; set; }
    /// <summary>Ảnh QR sinh sẵn bởi img.vietqr.io — App chỉ việc tải về hiện lên.</summary>
    public string? AnhQr { get; set; }
    /// <summary>Quá giờ này mà chưa chuyển khoản thì đơn tự hủy (F-10).</summary>
    public DateTime? HanThanhToan { get; set; }
    /// <summary>Máy chủ có bật nút "giả lập đã chuyển khoản" để demo không.</summary>
    public bool ChoPhepGiaLap { get; set; }
}

/// <summary>Link và dữ liệu VietQR chính thức do payOS cấp cho một đơn.</summary>
public sealed class LienKetPayOSDto
{
    public string CheckoutUrl { get; set; } = "";
    public DateTime HanThanhToan { get; set; }
    public int MaDonHang { get; set; }
    public decimal SoTien { get; set; }
    public string QrCode { get; set; } = "";
    public string MaNganHang { get; set; } = "";
    public string SoTaiKhoan { get; set; } = "";
    public string ChuTaiKhoan { get; set; } = "";
    public string NoiDungChuyenKhoan { get; set; } = "";
}

// ==================================================================== F-24B: CA LÀM VIỆC

/// <summary>Một ca làm việc (két tiền) — dùng ở màn Ca làm việc của Web.</summary>
public sealed class CaLamViecDto
{
    public int MaCa { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = "";
    public DateTime GioMoCa { get; set; }
    public DateTime? GioDongCa { get; set; }
    public decimal TienDauCa { get; set; }
    /// <summary>Tiền mặt thu được trong ca (thủ tục tự cộng khi thu tiền).</summary>
    public decimal DoanhThuTrongCa { get; set; }
    /// <summary>Số tiền nhân viên tự khai sau khi đếm két lúc đóng ca.</summary>
    public decimal? TongDoanhThuCuoiCa { get; set; }
    /// <summary>Số tiền quản lý kiểm đếm độc lập khi duyệt đóng ca.</summary>
    public decimal? TienQuanLyDemCuoiCa { get; set; }
    public decimal TienChi { get; set; }
    public decimal TienBu { get; set; }
    public string? GhiChu { get; set; }
    /// <summary>DANG_MO / CHO_XAC_NHAN / DA_XAC_NHAN.</summary>
    public string TrangThai { get; set; } = "";
    public string? TenNguoiXacNhan { get; set; }
    /// <summary>Số đơn đã thu tiền mặt vào ca này.</summary>
    public int SoDonDaThu { get; set; }
    /// <summary>Số đơn VietQR đã thanh toán tại chi nhánh trong thời gian ca.</summary>
    public int SoDonVietQr { get; set; }
    /// <summary>Tổng tiền VietQR để đối chiếu; không cộng vào két tiền mặt.</summary>
    public decimal DoanhThuVietQr { get; set; }
}

/// <summary>Một đơn đã thanh toán trong khoảng thời gian của ca.</summary>
public sealed class DonHangTrongCaDto
{
    public int MaCa { get; set; }
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = "";
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = "";
    public DateTime GioMoCa { get; set; }
    public DateTime? GioDongCa { get; set; }
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    /// <summary>COD hoặc VIETQR.</summary>
    public string PhuongThuc { get; set; } = "";
    public decimal SoTien { get; set; }
    public DateTime NgayThanhToan { get; set; }
    public string TrangThaiDonHang { get; set; } = "";
}

public sealed class MoCaRequest
{
    /// <summary>Tiền lẻ có sẵn trong két lúc mở ca.</summary>
    [SoTien]
    public decimal TienDauCa { get; set; }
}

public sealed class DongCaRequest
{
    /// <summary>Số tiền mặt đếm được trong két lúc đóng ca.</summary>
    [SoTien]
    public decimal TongDoanhThuCuoiCa { get; set; }
}
