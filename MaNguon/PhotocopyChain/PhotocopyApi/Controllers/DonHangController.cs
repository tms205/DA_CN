using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using PhotocopyApi.DuLieu;
using PhotocopyApi.Hubs;
using PhotocopyApi.MayInAo;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.DanhMuc;
using PhotocopyShared.Dtos.DatIn;
using PhotocopyShared.Dtos.DonHang;
using PhotocopyShared.Dtos.Tuan3;
using PhotocopyShared.MayInAo;
using PhotocopyShared.KiemTraDuLieu;
using PhotocopyShared.QuyCachIn;

namespace PhotocopyApi.Controllers;

// Đơn hàng: khách đặt đơn và theo dõi (F-09, F-11, F-12), quầy xử lý đơn (F-15, F-16, F-17, F-18, F-21, F-31).
// Mỗi action làm đúng 4 bước: lấy người gọi từ token → kiểm tra → gọi SQL / thủ tục → trả kết quả.
[ApiController]
[Route("api/don-hang")]
public class DonHangController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _boNhoTam;
    private readonly IHubContext<CapNhatHub> _hub;

    // Dùng chung 1 HttpClient để gọi Ahamove (tạo mới liên tục dễ hết cổng mạng).
    private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    public DonHangController(IConfiguration config, IMemoryCache boNhoTam, IHubContext<CapNhatHub> hub)
    {
        _config = config;
        _boNhoTam = boNhoTam;
        _hub = hub;
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    // =================================================================== PHÍA KHÁCH HÀNG (App)

    // GET api/don-hang/cua-toi — danh sách đơn của khách đang đăng nhập, đơn mới nhất lên đầu
    [HttpGet("cua-toi")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> DonCuaToi()
    {
        int maKhachHang = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);
        // SQL dài để riêng trong TruyVanSql; Controller chỉ thể hiện luồng của API.
        var danhSach = await conn.QueryAsync<DonHangTomTatDto>(
            TruyVanSql.DonHang.CuaKhachHang,
            new { MaKhachHang = maKhachHang });
        return Ok(danhSach);
    }

    // POST api/don-hang/chi-nhanh-phu-hop — bước 3: danh sách chi nhánh kèm cờ "in được cấu hình này không".
    // Bảng giá và máy in của mỗi chi nhánh có thể khác nhau nên phải làm mờ nơi không đáp ứng.
    [HttpPost("chi-nhanh-phu-hop")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> ChiNhanhPhuHop(ChiNhanhDatDonRequest req)
    {
        if (req.DsMaDichVu.Count == 0)
            return BadRequest(new { message = "Chưa chọn cấu hình in nào." });

        using var conn = new SqlConnection(_chuoiKetNoi);

        // Cấu hình khách chọn cần máy in như thế nào
        YeuCauMayIn yeuCau = await DocYeuCauMayIn(conn, req.DsMaDichVu);
        if (yeuCau == null)
            return BadRequest(new { message = "Cấu hình in không có trong bảng giá." });

        var danhSach = (await conn.QueryAsync<ChiNhanhDto>(
            TruyVanSql.DanhMuc.ChiNhanhDangHoatDong)).ToList();
        var dsMay = (await conn.QueryAsync<MayIn>("SELECT * FROM MayIn WHERE TrangThai = N'HOAT_DONG'")).ToList();
        var dsChiNhanhCoGia = (await conn.QueryAsync<int>(
            @"SELECT MaChiNhanh
              FROM BangGiaChiNhanh
              WHERE MaDichVu IN @Ds AND TrangThai = N'HIEN'
              GROUP BY MaChiNhanh
              HAVING COUNT(DISTINCT MaDichVu) = @SoDichVu",
            new { Ds = req.DsMaDichVu.Distinct().ToList(), SoDichVu = req.DsMaDichVu.Distinct().Count() })).ToHashSet();

        foreach (ChiNhanhDto cn in danhSach)
        {
            string lyDo = dsChiNhanhCoGia.Contains(cn.MaChiNhanh)
                ? LyDoKhongInDuoc(cn.TenChiNhanh, dsMay, cn.MaChiNhanh, yeuCau)
                : cn.TenChiNhanh + " chưa áp dụng đầy đủ các dịch vụ đã chọn.";
            cn.DapUngDuoc = lyDo == null;
            cn.LyDoKhongDapUng = lyDo;
        }

        return Ok(danhSach);
    }

    // POST api/don-hang — F-09: đặt đơn thật. Tính tiền vẫn do sp_TaoDonHang / sp_TinhTien làm,
    // API chỉ lo: lấy số trang trong CSDL, tính quãng đường giao hàng, chặn chi nhánh không in được.
    [HttpPost]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> DatHang(DatDonHangRequest req)
    {
        int maKhachHang = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);

        // Đơn đặt trên App LUÔN phải chuyển khoản VietQR trước (chốt 20/09/2026).
        // Tiền mặt chỉ dành cho khách tới tận tiệm ngồi đợi in — đơn đó nhân viên quầy lập, không đi qua đây.
        // Gán cứng ở máy chủ chứ không nhận từ App để không ai sửa App rồi đặt đơn nợ tiền được.
        string phuongThucThanhToan = "VIETQR";

        if (req.FileIn.Count == 0)
            return BadRequest(new { message = "Vui lòng chọn ít nhất 1 file để đặt in." });
        if (req.FileIn.Any(x => !QuyTacIn.HuongGiayHopLe(x.HuongGiay)))
            return BadRequest(new { message = "Hướng giấy phải là giấy đứng hoặc giấy ngang." });
        if (req.HinhThucNhan != "TAI_QUAY" && req.HinhThucNhan != "GIAO_HANG")
            return BadRequest(new { message = "Hình thức nhận hàng không hợp lệ." });

        using var conn = new SqlConnection(_chuoiKetNoi);

        // ---- Bước 1: chi nhánh phải đang hoạt động
        ChiNhanh chiNhanh = await conn.QueryFirstOrDefaultAsync<ChiNhanh>(
            "SELECT * FROM ChiNhanh WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = N'HOAT_DONG'",
            new { MaChiNhanh = req.MaChiNhanh });
        if (chiNhanh == null)
            return NotFound(new { message = "Chi nhánh không tồn tại hoặc đang tạm ngưng hoạt động." });

        // ---- Bước 2: lập 2 bảng truyền vào thủ tục (giống hệt BaoGiaController.TinhGia).
        // Các cột phải đúng thứ tự của kiểu bảng Kieu_DongDonHang / Kieu_DongGiaCong trong CSDL (file 04).
        var bangDongIn = new DataTable();
        bangDongIn.Columns.Add("MaFile", typeof(int));
        bangDongIn.Columns.Add("MaDichVu", typeof(int));
        bangDongIn.Columns.Add("SoTrang", typeof(int));
        bangDongIn.Columns.Add("SoBan", typeof(int));

        var bangGiaCong = new DataTable();
        bangGiaCong.Columns.Add("MaFile", typeof(int));
        bangGiaCong.Columns.Add("MaDichVu", typeof(int));

        var dsMaDichVu = new List<int>();

        foreach (DongInRequest file in req.FileIn)
        {
            // Số trang lấy trong CSDL (đã đếm lúc tải file lên), KHÔNG tin số trang App gửi
            FileInDto fileTrongCsdl = await conn.QueryFirstOrDefaultAsync<FileInDto>(
                @"SELECT MaFile AS MaFileIn, TenFile AS TenFileGoc, SoTrang, TrangThai AS TrangThaiFile
                  FROM FileIn
                  WHERE MaFile = @MaFile AND MaKhachHang = @MaKhachHang",
                new { MaFile = file.MaFileIn, MaKhachHang = maKhachHang });

            if (fileTrongCsdl == null)
                return NotFound(new { message = "Có file không tồn tại hoặc không thuộc tài khoản của bạn." });
            if (fileTrongCsdl.TrangThaiFile != "SAN_SANG" || fileTrongCsdl.SoTrang == null || fileTrongCsdl.SoTrang <= 0)
                return BadRequest(new { message = $"File \"{fileTrongCsdl.TenFileGoc}\" chưa sẵn sàng để in, vui lòng tải lại." });

            bangDongIn.Rows.Add(file.MaFileIn, file.MaDichVu, fileTrongCsdl.SoTrang, file.SoBanIn);
            dsMaDichVu.Add(file.MaDichVu);

            foreach (int maGiaCong in file.DsGiaCong)
                bangGiaCong.Rows.Add(file.MaFileIn, maGiaCong);
        }

        // ---- Bước 3: chi nhánh có máy in được cấu hình này không (kiểm lại ở API, không tin App đã lọc)
        YeuCauMayIn yeuCau = await DocYeuCauMayIn(conn, dsMaDichVu);
        if (yeuCau == null)
            return BadRequest(new { message = "Cấu hình in không có trong bảng giá." });

        var dsMay = (await conn.QueryAsync<MayIn>(
            "SELECT * FROM MayIn WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = N'HOAT_DONG'",
            new { MaChiNhanh = req.MaChiNhanh })).ToList();

        string lyDoKhongIn = LyDoKhongInDuoc(chiNhanh.TenChiNhanh, dsMay, req.MaChiNhanh, yeuCau);
        if (lyDoKhongIn != null)
            return BadRequest(new { message = lyDoKhongIn + " Vui lòng chọn chi nhánh khác." });

        NguoiDung khach = await conn.QueryFirstAsync<NguoiDung>(
            "SELECT HoTen, SoDienThoai FROM NguoiDung WHERE MaNguoiDung = @MaNguoiDung",
            new { MaNguoiDung = maKhachHang });
        string sdtNhan = string.IsNullOrWhiteSpace(req.SdtNhan) ? khach.SoDienThoai : req.SdtNhan.Trim();

        // ---- Bước 4 (Tuần 3): Ahamove trả quãng đường và phí giao hàng dự kiến
        decimal? khoangCachKm = null;
        decimal? phiShipApi = null;
        if (req.HinhThucNhan == "GIAO_HANG")
        {
            if (string.IsNullOrWhiteSpace(req.DiaChiGiao))
                return BadRequest(new { message = "Vui lòng nhập địa chỉ giao hàng." });
            if (chiNhanh.ViDo == null || chiNhanh.KinhDo == null)
                return BadRequest(new { message = "Chi nhánh chưa khai báo tọa độ nên chưa giao tận nơi được." });

            try
            {
                KetQuaPhiGiaoHang baoGiaGiaoHang = await TinhPhiGiaoHangAhamove(
                    chiNhanh, req.DiaChiGiao.Trim(), req.ViDoGiao, req.KinhDoGiao,
                    khach.HoTen, sdtNhan);
                khoangCachKm = baoGiaGiaoHang.KhoangCachKm;
                phiShipApi = baoGiaGiaoHang.PhiShip;
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ---- Bước 5: gọi sp_TaoDonHang (ghi DonHang + ChiTietDonHang + ChiTietGiaCong + ThanhToan)
        string maKhuyenMai = null;
        if (!string.IsNullOrWhiteSpace(req.MaCodeKhuyenMai))
            maKhuyenMai = req.MaCodeKhuyenMai.Trim().ToUpper();

        // Thủ tục trả mã đơn vừa tạo qua tham số OUTPUT nên phải khai báo kiểu này
        var thamSo = new DynamicParameters();
        thamSo.Add("@MaKhachHang", maKhachHang);
        thamSo.Add("@MaChiNhanh", req.MaChiNhanh);
        thamSo.Add("@HinhThucNhan", req.HinhThucNhan);
        thamSo.Add("@PhuongThuc", phuongThucThanhToan);
        thamSo.Add("@DiaChiGiao", req.HinhThucNhan == "GIAO_HANG" ? req.DiaChiGiao.Trim() : null);
        thamSo.Add("@SdtNhan", sdtNhan);
        thamSo.Add("@KhoangCach", khoangCachKm);
        thamSo.Add("@PhiShipApi", phiShipApi);
        thamSo.Add("@InGap", req.InGap);
        thamSo.Add("@MaCode", maKhuyenMai);
        thamSo.Add("@DsFile", bangDongIn.AsTableValuedParameter("Kieu_DongDonHang"));
        thamSo.Add("@DsGiaCong", bangGiaCong.AsTableValuedParameter("Kieu_DongGiaCong"));
        thamSo.Add("@HuongGiayJson", JsonSerializer.Serialize(req.FileIn.Select(x => new { maFile = x.MaFileIn, huongGiay = x.HuongGiay })));
        thamSo.Add("@MaDonHang", dbType: DbType.Int32, direction: ParameterDirection.Output);

        try
        {
            await conn.ExecuteAsync("sp_TaoDonHang", thamSo, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            // Thủ tục báo lỗi bằng câu tiếng Việt (vd "Địa chỉ giao hàng nằm ngoài bán kính...")
            return BadRequest(new { message = ex.Message });
        }

        int maDonHang = thamSo.Get<int>("@MaDonHang");

        // Ghi chú cho thợ in nhập ở bước 2, thủ tục không nhận nên cập nhật thêm ở đây
        if (!string.IsNullOrWhiteSpace(req.GhiChu))
            await conn.ExecuteAsync("UPDATE DonHang SET GhiChu = @GhiChu WHERE MaDonHang = @MaDonHang",
                new { GhiChu = req.GhiChu.Trim(), MaDonHang = maDonHang });

        // ---- Bước 6: đọc lại đơn vừa tạo để trả về + báo real-time cho quầy
        var don = await conn.QueryFirstAsync<DonHangTomTatDto>(
            @"SELECT MaDonHang, 'DH' + RIGHT('00000' + CAST(MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     MaChiNhanh, TongTien
              FROM DonHang WHERE MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });

        // Đơn COD vào hàng đợi ngay; đơn VietQR phải chuyển khoản xong quầy mới tiếp nhận được,
        // nhưng vẫn báo để quầy nhìn thấy đơn đang chờ thanh toán.
        await CapNhatHub.GuiChoChiNhanh(_hub, req.MaChiNhanh, HubEvents.OrderCreated, new TinCapNhat
        {
            MaDonHang = maDonHang,
            MaDonHangHienThi = don.MaDonHangHienThi,
            MaChiNhanh = req.MaChiNhanh,
            TrangThai = TrangThaiDonHang.ChoXacNhan,
            NoiDung = "Đơn " + don.MaDonHangHienThi + " vừa được đặt"
        });

        return Ok(new DatDonHangResponse
        {
            MaDonHang = maDonHang,
            MaDonHangHienThi = don.MaDonHangHienThi,
            PhuongThucThanhToan = phuongThucThanhToan,
            TongTien = don.TongTien,
            Message = "Đã tạo đơn, vui lòng chuyển khoản trong 15 phút."
        });
    }

    // POST api/don-hang/khach-tai-quay — cần tên hoặc SĐT. Có SĐT thì tìm khách cũ;
    // chỉ có tên thì tạo khách vãng lai riêng, không gộp hai người có cùng tên.
    [HttpPost("khach-tai-quay")]
    [Authorize(Roles = "BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> ChonKhachTaiQuay(KhachTaiQuayRequest req)
    {
        string loi = QuyTacNhap.LayLoi(req);
        if (loi != null) return BadRequest(new { message = loi });
        string hoTen = (req.HoTen ?? "").Trim();
        string soDienThoai = (req.SoDienThoai ?? "").Trim();

        using var conn = new SqlConnection(_chuoiKetNoi);
        KhachTaiQuayDto khach = null;
        if (soDienThoai.Length > 0)
            khach = await conn.QueryFirstOrDefaultAsync<KhachTaiQuayDto>(
                @"SELECT TOP 1 MaNguoiDung AS MaKhachHang, HoTen, SoDienThoai, Email
                  FROM NguoiDung WHERE SoDienThoai = @SoDienThoai AND VaiTro = 'CUSTOMER'
                  ORDER BY MaNguoiDung", new { SoDienThoai = soDienThoai });

        if (khach != null)
        {
            if (await conn.ExecuteScalarAsync<string>(
                    "SELECT TrangThai FROM NguoiDung WHERE MaNguoiDung = @MaKhachHang",
                    new { khach.MaKhachHang }) == "KHOA")
                return BadRequest(new { message = "Tài khoản khách hàng này đang bị khóa." });
            return Ok(khach);
        }

        if (hoTen.Length == 0) hoTen = "Khách tại quầy";
        string email = "khach." + (soDienThoai.Length > 0 ? soDienThoai : Guid.NewGuid().ToString("N")) + "@taiquay.local";
        int maKhachHang = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO NguoiDung (HoTen, Email, SoDienThoai, MatKhau, VaiTro)
              VALUES (@HoTen, @Email, @SoDienThoai, NULL, 'CUSTOMER');
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { HoTen = hoTen, Email = email, SoDienThoai = soDienThoai.Length > 0 ? soDienThoai : null });

        return Ok(new KhachTaiQuayDto
        {
            MaKhachHang = maKhachHang,
            HoTen = hoTen,
            SoDienThoai = soDienThoai,
            Email = email,
            VuaTao = true
        });
    }

    // POST api/don-hang/tai-quay — nhân viên lập đơn cho khách tới trực tiếp.
    // Khách chọn VietQR trả trước hoặc COD trả khi nhận tài liệu.
    [HttpPost("tai-quay")]
    [Authorize(Roles = "BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> DatDonTaiQuay(DatDonTaiQuayRequest req)
    {
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int maChiNhanh = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        string phuongThucThanhToan = (req.PhuongThucThanhToan ?? "").Trim().ToUpper();
        if (phuongThucThanhToan != "VIETQR" && phuongThucThanhToan != "COD")
            return BadRequest(new { message = "Phương thức thanh toán phải là VietQR hoặc COD." });
        if (req.FileIn.Count == 0)
            return BadRequest(new { message = "Vui lòng tải ít nhất 1 file để lập đơn." });
        if (req.FileIn.Any(x => !QuyTacIn.HuongGiayHopLe(x.HuongGiay)))
            return BadRequest(new { message = "Hướng giấy phải là giấy đứng hoặc giấy ngang." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        int? maCa = await LayCaDangMo(conn, maNhanVien, maChiNhanh);
        if (maCa == null)
            return BadRequest(new { message = "Bạn chưa mở ca làm việc. Hãy mở ca trước khi lập đơn tại quầy." });

        NguoiDung khach = await conn.QueryFirstOrDefaultAsync<NguoiDung>(
            "SELECT * FROM NguoiDung WHERE MaNguoiDung = @MaKhachHang AND VaiTro = 'CUSTOMER' AND TrangThai = N'HOAT_DONG'",
            new { req.MaKhachHang });
        if (khach == null)
            return BadRequest(new { message = "Khách hàng không tồn tại hoặc tài khoản đã bị khóa." });

        var bangDongIn = new DataTable();
        bangDongIn.Columns.Add("MaFile", typeof(int));
        bangDongIn.Columns.Add("MaDichVu", typeof(int));
        bangDongIn.Columns.Add("SoTrang", typeof(int));
        bangDongIn.Columns.Add("SoBan", typeof(int));
        var bangGiaCong = new DataTable();
        bangGiaCong.Columns.Add("MaFile", typeof(int));
        bangGiaCong.Columns.Add("MaDichVu", typeof(int));
        var dsMaDichVu = new List<int>();

        foreach (DongInRequest file in req.FileIn)
        {
            FileInDto fileTrongCsdl = await conn.QueryFirstOrDefaultAsync<FileInDto>(
                @"SELECT MaFile AS MaFileIn, TenFile AS TenFileGoc, SoTrang, TrangThai AS TrangThaiFile
                  FROM FileIn WHERE MaFile = @MaFile AND MaKhachHang = @MaKhachHang",
                new { MaFile = file.MaFileIn, req.MaKhachHang });
            if (fileTrongCsdl == null)
                return NotFound(new { message = "Có file không thuộc khách hàng đang chọn." });
            if (fileTrongCsdl.TrangThaiFile != "SAN_SANG" || fileTrongCsdl.SoTrang == null || fileTrongCsdl.SoTrang <= 0)
                return BadRequest(new { message = "File chưa sẵn sàng để in, vui lòng tải lại." });

            bangDongIn.Rows.Add(file.MaFileIn, file.MaDichVu, fileTrongCsdl.SoTrang, file.SoBanIn);
            dsMaDichVu.Add(file.MaDichVu);
            foreach (int maGiaCong in file.DsGiaCong)
                bangGiaCong.Rows.Add(file.MaFileIn, maGiaCong);
        }

        ChiNhanh chiNhanh = await conn.QueryFirstOrDefaultAsync<ChiNhanh>(
            "SELECT * FROM ChiNhanh WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = N'HOAT_DONG'", new { MaChiNhanh = maChiNhanh });
        YeuCauMayIn yeuCau = await DocYeuCauMayIn(conn, dsMaDichVu);
        if (yeuCau == null)
            return BadRequest(new { message = "Cấu hình in không có trong bảng giá." });
        var dsMay = (await conn.QueryAsync<MayIn>(
            "SELECT * FROM MayIn WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = N'HOAT_DONG'", new { MaChiNhanh = maChiNhanh })).ToList();
        string lyDoKhongIn = LyDoKhongInDuoc(chiNhanh.TenChiNhanh, dsMay, maChiNhanh, yeuCau);
        if (lyDoKhongIn != null)
            return BadRequest(new { message = lyDoKhongIn });

        string maKhuyenMai = string.IsNullOrWhiteSpace(req.MaCodeKhuyenMai)
            ? null : req.MaCodeKhuyenMai.Trim().ToUpper();
        var thamSo = new DynamicParameters();
        thamSo.Add("@MaKhachHang", req.MaKhachHang);
        thamSo.Add("@MaChiNhanh", maChiNhanh);
        thamSo.Add("@HinhThucNhan", "TAI_QUAY");
        thamSo.Add("@PhuongThuc", phuongThucThanhToan);
        thamSo.Add("@DiaChiGiao", null);
        thamSo.Add("@SdtNhan", khach.SoDienThoai);
        thamSo.Add("@KhoangCach", null);
        thamSo.Add("@PhiShipApi", null);
        thamSo.Add("@InGap", req.InGap);
        thamSo.Add("@MaCode", maKhuyenMai);
        thamSo.Add("@DsFile", bangDongIn.AsTableValuedParameter("Kieu_DongDonHang"));
        thamSo.Add("@DsGiaCong", bangGiaCong.AsTableValuedParameter("Kieu_DongGiaCong"));
        thamSo.Add("@HuongGiayJson", JsonSerializer.Serialize(req.FileIn.Select(x => new { maFile = x.MaFileIn, huongGiay = x.HuongGiay })));
        thamSo.Add("@MaDonHang", dbType: DbType.Int32, direction: ParameterDirection.Output);

        try
        {
            await conn.ExecuteAsync("sp_TaoDonHang", thamSo, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        int maDonHang = thamSo.Get<int>("@MaDonHang");
        string ghiChu = phuongThucThanhToan == "VIETQR"
            ? "Đơn VietQR lập tại quầy"
            : "Đơn COD lập tại quầy";
        if (!string.IsNullOrWhiteSpace(req.GhiChu)) ghiChu += " · " + req.GhiChu.Trim();
        await conn.ExecuteAsync(
            "UPDATE DonHang SET GhiChu = @GhiChu WHERE MaDonHang = @MaDonHang",
            new { GhiChu = ghiChu, MaDonHang = maDonHang });

        var don = await conn.QueryFirstAsync<DonHangTomTatDto>(
            @"SELECT MaDonHang, 'DH' + RIGHT('00000' + CAST(MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     MaChiNhanh, TongTien FROM DonHang WHERE MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });

        await CapNhatHub.GuiChoChiNhanh(_hub, maChiNhanh, HubEvents.OrderCreated, new TinCapNhat
        {
            MaDonHang = maDonHang,
            MaDonHangHienThi = don.MaDonHangHienThi,
            MaChiNhanh = maChiNhanh,
            TrangThai = TrangThaiDonHang.ChoXacNhan,
            NoiDung = "Đơn " + phuongThucThanhToan + " " + don.MaDonHangHienThi + " vừa được lập tại quầy"
        });

        return Ok(new DatDonHangResponse
        {
            MaDonHang = maDonHang,
            MaDonHangHienThi = don.MaDonHangHienThi,
            PhuongThucThanhToan = phuongThucThanhToan,
            TongTien = don.TongTien,
            Message = phuongThucThanhToan == "VIETQR"
                ? "Đã lập đơn VietQR tại quầy, vui lòng quét mã để thanh toán."
                : "Đã lập đơn COD tại quầy, khách thanh toán khi nhận tài liệu."
        });
    }

    // =================================================================== XEM CHI TIẾT 1 ĐƠN (App + Web)

    // GET api/don-hang/5 — F-11: chi tiết đơn + các mốc tiến độ.
    // Khách chỉ xem được đơn của mình; nhân viên xem được đơn của chi nhánh mình; Super Admin xem tất cả.
    [HttpGet("{maDonHang}")]
    [Authorize]
    public async Task<IActionResult> ChiTiet([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang)
    {
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;

        using var conn = new SqlConnection(_chuoiKetNoi);

        DonHangChiTietDto don = await conn.QueryFirstOrDefaultAsync<DonHangChiTietDto>(
            TruyVanSql.DonHang.ChiTiet,
            new { MaDonHang = maDonHang });
        if (don == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });

        // Ai được xem đơn này
        if (vaiTro == VaiTro.Customer)
        {
            if (don.MaKhachHang != maNguoiDung)
                return NotFound(new { message = "Không tìm thấy đơn hàng." });
        }
        else if (vaiTro != VaiTro.SuperAdmin)
        {
            int maChiNhanhNguoiXem = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
            if (don.MaChiNhanh != maChiNhanhNguoiXem)
                return StatusCode(403, new { message = "Đơn này không thuộc chi nhánh của bạn." });
        }

        // Tên các gia công đã được gom sẵn trong truy vấn đọc chi tiết dòng in.
        don.DongIn = (await conn.QueryAsync<DongInChiTietDto>(
            TruyVanSql.DonHang.DongInChiTiet,
            new { MaDonHang = maDonHang })).ToList();

        // Lúc shipper nhận đơn đi giao thì có ghi giờ, các mốc khác CSDL không lưu giờ riêng
        DateTime? gioNhanGiao = await conn.ExecuteScalarAsync<DateTime?>(
            "SELECT MAX(NgayNhan) FROM GiaoHang WHERE MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });

        don.TienDo = DungTienDo(don, gioNhanGiao);
        return Ok(don);
    }

    // =================================================================== PHÍA QUẦY (Web quản trị)

    // GET api/don-hang/hang-doi — F-15: bảng Hàng đợi đơn hàng.
    // Nhân viên / Quản lý chỉ thấy đơn chi nhánh mình, Super Admin thấy toàn chuỗi.
    [HttpGet("hang-doi")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> HangDoi()
    {
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int? maChiNhanh = null;
        if (User.FindFirst(TenClaim.MaChiNhanh) != null)
            maChiNhanh = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);

        // Đơn hỏa tốc xếp lên đầu, sau đó đơn đặt trước xếp trước.
        // Đơn đã hoàn tất / đã hủy chỉ lấy trong 7 ngày gần đây cho bảng khỏi dài.
        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<HangDoiDonDto>(TruyVanSql.DonHang.HangDoi, new
        {
            MaChiNhanh = vaiTro == VaiTro.SuperAdmin ? null : maChiNhanh
        });
        return Ok(danhSach);
    }

    // GET api/don-hang/5/may-in — F-21: các máy in của chi nhánh giữ đơn, kèm cờ có in được đơn này không
    [HttpGet("{maDonHang}/may-in")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> DanhSachMayIn([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);

        int? maChiNhanhDon = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaChiNhanh FROM DonHang WHERE MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });
        if (maChiNhanhDon == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (!DuocLamViecVoiChiNhanh(maChiNhanhDon.Value))
            return StatusCode(403, new { message = "Đơn này không thuộc chi nhánh của bạn." });

        // Đơn cần máy in như thế nào (giống 3 tiêu chí của sp_PhanCongMayIn)
        YeuCauMayIn yeuCau = await conn.QueryFirstAsync<YeuCauMayIn>(
            TruyVanSql.DonHang.YeuCauMayIn,
            new { MaDonHang = maDonHang });

        var dsMay = (await conn.QueryAsync<MayIn>(
            "SELECT * FROM MayIn WHERE MaChiNhanh = @MaChiNhanh ORDER BY MaMayIn",
            new { MaChiNhanh = maChiNhanhDon.Value })).ToList();

        var ketQua = new List<MayInDto>();
        foreach (MayIn may in dsMay)
        {
            var dto = new MayInDto
            {
                MaMayIn = may.MaMayIn,
                TenMay = may.TenMay,
                KhoGiay = may.KhoGiay,
                InMau = may.InMau,
                In2Mat = may.In2Mat,
                TocDo = may.TocDo,
                TrangThai = may.TrangThai,
                DapUngDuoc = true
            };

            if (may.TrangThai != "HOAT_DONG")
            {
                dto.DapUngDuoc = false;
                dto.LyDoKhongDapUng = may.TrangThai == "BAO_TRI" ? "Đang bảo trì" : "Máy hỏng";
            }
            else
            {
                string thieu = ThieuGi(may, yeuCau);
                if (thieu != null)
                {
                    dto.DapUngDuoc = false;
                    dto.LyDoKhongDapUng = "Không " + thieu;
                }
            }

            ketQua.Add(dto);
        }

        return Ok(ketQua);
    }

    // POST api/don-hang/5/tiep-nhan — F-15: thợ in nhận đơn vào xử lý
    [HttpPost("{maDonHang}/tiep-nhan")]
    [Authorize(Roles = "BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> TiepNhan([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang)
    {
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int maChiNhanh = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);

        using var conn = new SqlConnection(_chuoiKetNoi);

        int? maCaDangMo = await LayCaDangMo(conn, maNhanVien, maChiNhanh);
        if (maCaDangMo == null)
            return BadRequest(new { message = "Bạn chưa mở ca làm việc. Hãy mở ca trước khi tiếp nhận đơn hàng." });
        int? maChiNhanhDon = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaChiNhanh FROM DonHang WHERE MaDonHang=@MaDonHang", new { MaDonHang = maDonHang });
        if (maChiNhanhDon == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (!DuocLamViecVoiChiNhanh(maChiNhanhDon.Value))
            return StatusCode(403, new { message = "Đơn không thuộc phạm vi chi nhánh đang chọn." });

        try
        {
            // Thủ tục tự kiểm: đã mở ca, đơn đang chờ xác nhận, đã thanh toán (nếu VietQR), nhân viên đúng chi nhánh
            await conn.ExecuteAsync("sp_TiepNhanDon",
                new { MaDonHang = maDonHang, MaNhanVien = maNhanVien },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        await BaoDonDoiTrangThai(conn, maDonHang, "Đơn đã được tiếp nhận");
        return Ok(new KetQuaDto { Message = "Đã tiếp nhận đơn hàng." });
    }

    // Phục hồi đúng dòng của đơn đã trả tiền bị mất file, không tính lại tiền hoặc đổi quy cách.
    [HttpPut("{maDonHang:int}/chi-tiet/{maChiTiet:int}/file")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> PhucHoiFileIn([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, [Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maChiTiet,
        PhucHoiFileInRequest req, [FromServices] TuDongInService tuDongIn)
    {
        if (req.MaFileIn <= 0) return BadRequest(new { message = "Mã file mới không hợp lệ." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        var don = await conn.QueryFirstOrDefaultAsync<DonPhucHoiFile>(
            @"SELECT dh.MaChiNhanh, dh.MaKhachHang, dh.TrangThai,
                     tt.PhuongThuc, tt.TrangThai AS TrangThaiThanhToan
              FROM DonHang dh WITH (UPDLOCK, HOLDLOCK)
              JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang WHERE dh.MaDonHang=@MaDonHang",
            new { MaDonHang = maDonHang }, giaoDich);
        if (don == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (!DuocLamViecVoiChiNhanh(don.MaChiNhanh))
            return StatusCode(403, new { message = "Đơn không thuộc phạm vi chi nhánh của bạn." });
        if (don.TrangThai != "DA_XAC_NHAN" || don.PhuongThuc != "VIETQR" || don.TrangThaiThanhToan != "DA_THANH_TOAN")
            return BadRequest(new { message = "Chỉ phục hồi file cho đơn VietQR đã thanh toán và đang chờ in." });

        var fileCu = await conn.QueryFirstOrDefaultAsync<FilePhucHoi>(
            @"SELECT f.MaFile, f.SoTrang, f.DuongDan, f.TrangThai FROM ChiTietDonHang ct
              JOIN FileIn f ON f.MaFile=ct.MaFile WHERE ct.MaDonHang=@MaDonHang AND ct.MaChiTiet=@MaChiTiet",
            new { MaDonHang = maDonHang, MaChiTiet = maChiTiet }, giaoDich);
        if (fileCu == null) return NotFound(new { message = "Không tìm thấy dòng in trong đơn." });
        if (QuyTacMayInAo.TepSanSang(fileCu.TrangThai, fileCu.DuongDan, System.IO.File.Exists(fileCu.DuongDan)))
            return BadRequest(new { message = "File cũ vẫn còn sẵn sàng, không cần phục hồi." });

        var fileMoi = await conn.QueryFirstOrDefaultAsync<FilePhucHoi>(
            @"SELECT MaFile, SoTrang, DuongDan, TrangThai FROM FileIn WITH (UPDLOCK, HOLDLOCK)
              WHERE MaFile=@MaFile AND MaKhachHang=@MaKhachHang
                AND NOT EXISTS (SELECT 1 FROM ChiTietDonHang WHERE MaFile=@MaFile)",
            new { MaFile = req.MaFileIn, don.MaKhachHang }, giaoDich);
        if (fileMoi == null || !QuyTacMayInAo.FilePhucHoiHopLe(fileCu.SoTrang, fileMoi.SoTrang,
            fileMoi.TrangThai, System.IO.File.Exists(fileMoi.DuongDan)))
            return BadRequest(new { message = $"Hãy tải PDF mới của đúng khách, chưa dùng trong đơn khác và có đúng {fileCu.SoTrang} trang như file cũ." });

        await conn.ExecuteAsync("UPDATE ChiTietDonHang SET MaFile=@MaFile WHERE MaChiTiet=@MaChiTiet AND MaDonHang=@MaDonHang",
            new { MaFile = req.MaFileIn, MaChiTiet = maChiTiet, MaDonHang = maDonHang }, giaoDich);
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        string ghiChu = $"[Phục hồi file] Nhân viên {maNguoiDung}, dòng {maChiTiet}: file {fileCu.MaFile} -> {fileMoi.MaFile}, {DateTime.Now:dd/MM/yyyy HH:mm:ss}.";
        // Bảng thông báo sẵn có lưu được nhiều lần phục hồi; không cắt/ghi đè ghi chú 255 ký tự của khách.
        await conn.ExecuteAsync(
            @"INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
              VALUES (@MaNguoiDung, @MaDonHang, N'Nhật ký phục hồi file in', @GhiChu)",
            new { MaNguoiDung = maNguoiDung, GhiChu = ghiChu, MaDonHang = maDonHang }, giaoDich);
        await giaoDich.CommitAsync();
        await tuDongIn.XepInSauThanhToan(maDonHang);
        await BaoDonDoiTrangThai(conn, maDonHang, "Đã phục hồi file in, giữ nguyên tiền và quy cách của đơn");
        return Ok(new KetQuaDto { Message = "Đã phục hồi file; hệ thống tự xếp in khi đủ file, máy và vật tư." });
    }

    private sealed class DonPhucHoiFile
    {
        public int MaChiNhanh { get; set; }
        public int MaKhachHang { get; set; }
        public string TrangThai { get; set; }
        public string PhuongThuc { get; set; }
        public string TrangThaiThanhToan { get; set; }
    }

    private sealed class FilePhucHoi
    {
        public int MaFile { get; set; }
        public int SoTrang { get; set; }
        public string DuongDan { get; set; }
        public string TrangThai { get; set; }
    }

    // PUT api/don-hang/5/trang-thai-in — F-17 (+ F-21 phân công máy in).
    //   DA_XAC_NHAN → DANG_IN : thủ tục trừ vật tư trong kho
    //   DANG_IN     → DA_IN   : đánh dấu đã in xong; file vẫn giữ tới khi đơn hoàn tất / bị hủy
    [HttpPut("{maDonHang}/trang-thai-in")]
    [Authorize(Roles = "BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> CapNhatTrangThaiIn([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, CapNhatTrangThaiInRequest req)
    {
        if (req.TrangThaiMoi != "DANG_IN" && req.TrangThaiMoi != "DA_IN")
            return BadRequest(new { message = "Trạng thái in không hợp lệ." });

        using var conn = new SqlConnection(_chuoiKetNoi);

        int? maChiNhanhDon = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaChiNhanh FROM DonHang WHERE MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });
        if (maChiNhanhDon == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (!DuocLamViecVoiChiNhanh(maChiNhanhDon.Value))
            return StatusCode(403, new { message = "Đơn này không thuộc chi nhánh của bạn." });

        bool tuDong = await conn.ExecuteScalarAsync<bool>(
            @"SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM ThanhToan
              WHERE MaDonHang=@MaDonHang AND PhuongThuc='VIETQR' AND TrangThai='DA_THANH_TOAN') THEN 1 ELSE 0 END AS BIT)",
            new { MaDonHang = maDonHang });
        if (tuDong)
            return BadRequest(new { message = "Đơn VietQR đã trả tiền do connector tự in và báo hoàn tất; không xác nhận in thủ công." });

        if (req.TrangThaiMoi == "DANG_IN")
        {
            var dsFile = (await conn.QueryAsync<FilePhucHoi>(
                @"SELECT f.TrangThai, f.DuongDan FROM ChiTietDonHang ct JOIN FileIn f ON f.MaFile=ct.MaFile
                  WHERE ct.MaDonHang=@MaDonHang", new { MaDonHang = maDonHang })).ToList();
            if (dsFile.Count == 0 || dsFile.Any(x => !QuyTacMayInAo.TepSanSang(x.TrangThai, x.DuongDan, System.IO.File.Exists(x.DuongDan))))
                return BadRequest(new { message = "File in không còn sẵn sàng, chưa thể bắt đầu in." });
        }

        // F-21: chọn máy in trước khi bắt đầu in (thủ tục tự kiểm máy có đủ khổ giấy / màu / 2 mặt không)
        if (req.MaMayIn != null)
        {
            try
            {
                await conn.ExecuteAsync("sp_PhanCongMayIn",
                    new { MaDonHang = maDonHang, MaMayIn = req.MaMayIn.Value },
                    commandType: CommandType.StoredProcedure);
            }
            catch (SqlException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Giữ lại mã file + đường dẫn trước khi đổi sang DA_IN.
        // Dòng này giúp cả CSDL cũ (đang chạy) lẫn script mới đều giữ file tới khi đơn kết thúc.
        var dsFileCanGiu = new List<FileCanXoa>();
        if (req.TrangThaiMoi == "DA_IN")
        {
            dsFileCanGiu = (await conn.QueryAsync<FileCanXoa>(
                @"SELECT DISTINCT f.MaFile, f.DuongDan
                  FROM ChiTietDonHang ct JOIN FileIn f ON f.MaFile = ct.MaFile
                  WHERE ct.MaDonHang = @MaDonHang AND f.DuongDan IS NOT NULL",
                new { MaDonHang = maDonHang })).ToList();
        }

        try
        {
            await conn.ExecuteAsync("sp_CapNhatTrangThaiIn",
                new { MaDonHang = maDonHang, TrangThaiMoi = req.TrangThaiMoi },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // Nếu CSDL trên máy chưa nạp lại thủ tục mới, thủ tục cũ sẽ đánh dấu DA_XOA lúc in xong.
        // Ghi lại đường dẫn để đúng quy tắc mới: chỉ xóa khi đơn HOAN_THANH / DA_HUY.
        foreach (FileCanXoa file in dsFileCanGiu)
        {
            await conn.ExecuteAsync(
                "UPDATE FileIn SET TrangThai = N'SAN_SANG', DuongDan = @DuongDan WHERE MaFile = @MaFile",
                file);
        }

        LienKetPhatSinh phatSinh = req.TrangThaiMoi == "DA_IN"
            ? await conn.QueryFirstOrDefaultAsync<LienKetPhatSinh>(
                "SELECT MaDonHangGoc, LoaiPhatSinh FROM DonHang WHERE MaDonHang = @MaDonHang",
                new { MaDonHang = maDonHang })
            : null;
        bool laInLaiMienPhi = phatSinh?.LoaiPhatSinh == "IN_LAI_LOI_QUAY" && phatSinh.MaDonHangGoc != null;
        bool donGocTuHoanTat = false;
        if (laInLaiMienPhi)
        {
            await conn.ExecuteAsync(
                "UPDATE DonHang SET TrangThai = 'HOAN_THANH', NgayHoanThanh = GETDATE() WHERE MaDonHang = @MaDonHang",
                new { MaDonHang = maDonHang });

            // Với đơn gốc đã trả tiền (thường là VietQR), thao tác in lại diễn ra ngay lúc khách
            // đang nhận hàng nên hoàn tất luôn đơn gốc. COD chưa thu tuyệt đối không được tự chốt:
            // đơn gốc vẫn ở bàn giao để nhân viên thu tiền vào đúng ca.
            donGocTuHoanTat = await conn.ExecuteAsync(
                @"UPDATE dh
                  SET TrangThai = 'HOAN_THANH', NgayHoanThanh = GETDATE()
                  FROM DonHang dh
                  JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
                  WHERE dh.MaDonHang = @MaDonHangGoc
                    AND dh.TrangThai = 'DA_IN'
                    AND tt.TrangThai = 'DA_THANH_TOAN'", new { phatSinh.MaDonHangGoc }) > 0;

            // Đây là việc nội bộ của quầy, khách chỉ cần nhận thông báo gắn với đơn gốc.
            // Xóa các thông báo trạng thái mà stored procedure vừa tạo cho đơn in lại 0đ
            // để khách không thấy một mã đơn kỹ thuật không xuất hiện trong lịch sử của họ.
            await conn.ExecuteAsync(
                "DELETE FROM ThongBao WHERE MaDonHang = @MaDonHang",
                new { MaDonHang = maDonHang });
            await conn.ExecuteAsync(
                @"INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
                  SELECT MaKhachHang, MaDonHang,
                         CASE WHEN @DaHoanTat = 1 THEN N'Đơn hàng đã hoàn tất' ELSE N'Bản in lại đã sẵn sàng' END,
                         CASE WHEN @DaHoanTat = 1
                              THEN N'Quầy đã xử lý xong phần in lại miễn phí và hoàn tất đơn hàng.'
                              ELSE N'Quầy đã in lại phần bị lỗi miễn phí. Vui lòng thanh toán COD khi nhận tài liệu.' END
                  FROM DonHang WHERE MaDonHang = @MaDonHangGoc",
                new { phatSinh.MaDonHangGoc, DaHoanTat = donGocTuHoanTat });

            if (donGocTuHoanTat)
                await XoaFileCuaDonDaKetThuc(conn, phatSinh.MaDonHangGoc.Value);
        }

        string cau = req.TrangThaiMoi == "DANG_IN"
            ? "Đơn đang được in"
            : donGocTuHoanTat ? "Đã hoàn tất bản in lại và đơn gốc"
            : laInLaiMienPhi ? "Bản in lại đã xong; đơn gốc đang chờ thu COD và bàn giao"
            : "Đơn đã in xong";
        await BaoDonDoiTrangThai(conn, maDonHang, cau);
        if (laInLaiMienPhi)
            await BaoDonDoiTrangThai(conn, phatSinh.MaDonHangGoc.Value,
                donGocTuHoanTat ? "Đã xử lý xong bản in lại và hoàn tất đơn gốc"
                                : "Bản in lại đã xong; chờ thu COD và bàn giao cho khách");
        return Ok(new KetQuaDto { Message = cau + "." });
    }

    // GET api/don-hang/5/file/12 — F-16: thợ in tải file (bản PDF đã chuẩn hóa) về để in.
    // Đường dẫn file không bao giờ lộ ra ngoài, luôn phải đi qua đây để kiểm quyền.
    [HttpGet("{maDonHang}/file/{maFile}")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> TaiFileIn([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, [Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maFile)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);

        var dong = await conn.QueryFirstOrDefaultAsync<FileTrongDon>(
            TruyVanSql.DonHang.FileTrongDon,
            new { MaDonHang = maDonHang, MaFile = maFile });

        if (dong == null)
            return NotFound(new { message = "File không thuộc đơn hàng này." });
        if (!DuocLamViecVoiChiNhanh(dong.MaChiNhanh))
            return StatusCode(403, new { message = "Đơn này không thuộc chi nhánh của bạn." });
        if (dong.TrangThai != "SAN_SANG" || string.IsNullOrEmpty(dong.DuongDan) || !System.IO.File.Exists(dong.DuongDan))
            return NotFound(new { message = "File gốc đã được xóa khỏi máy chủ sau khi đơn hàng kết thúc." });

        // Trình duyệt tải về với đúng tên file khách đặt, nhưng nội dung luôn là PDF (đã chuẩn hóa lúc tải lên)
        string tenTaiVe = Path.GetFileNameWithoutExtension(dong.TenFile) + ".pdf";
        return PhysicalFile(dong.DuongDan, "application/pdf", tenTaiVe);
    }

    // POST api/don-hang/5/giao-tai-quay — F-18: bàn giao; đơn COD được thu vào ca tại bước này.
    [HttpPost("{maDonHang}/giao-tai-quay")]
    [Authorize(Roles = "BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> GiaoTaiQuay([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang)
    {
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);

        int? maChiNhanhDon = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaChiNhanh FROM DonHang WHERE MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });
        if (maChiNhanhDon == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (!DuocLamViecVoiChiNhanh(maChiNhanhDon.Value))
            return StatusCode(403, new { message = "Đơn này không thuộc chi nhánh của bạn." });

        try
        {
            // VietQR phải trả trước; đơn COD tại quầy thu vào ca đang mở ngay lúc bàn giao.
            await conn.ExecuteAsync("sp_GiaoTaiQuay",
                new { MaDonHang = maDonHang, MaNhanVien = maNhanVien },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        //đơn vừa HOAN_THANH thì xóa file vật lý ngay và chỉ giữ lại thông tin tra cứu.
        await XoaFileCuaDonDaKetThuc(conn, maDonHang);
        await BaoDonDoiTrangThai(conn, maDonHang, "Đơn đã bàn giao cho khách");
        return Ok(new KetQuaDto { Message = "Đã bàn giao đơn hàng cho khách." });
    }

    // POST api/don-hang/5/in-lai-tai-quay
    // Quầy in sai quy cách / lem mực / thiếu trang: tạo một việc in nội bộ 0đ liên kết đơn gốc.
    // Việc in lại vẫn đi qua hàng đợi và trừ kho thật, nhưng khách không bị thu thêm tiền.
    [HttpPost("{maDonHang}/in-lai-tai-quay")]
    [Authorize(Roles = "BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> TaoInLaiTaiQuay([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, TaoInLaiTaiQuayRequest req)
    {
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int maChiNhanh = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        string lyDo = (req.LyDo ?? "").Trim();
        if (lyDo.Length < 5)
            return BadRequest(new { message = "Vui lòng ghi rõ lỗi của quầy để chịu chi phí in lại." });
        if (lyDo.Length > 200)
            return BadRequest(new { message = "Lý do in lại không được vượt quá 200 ký tự." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        if (await LayCaDangMo(conn, maNhanVien, maChiNhanh) == null)
            return BadRequest(new { message = "Bạn phải mở ca làm việc trước khi lập yêu cầu in lại." });
        NguonPhatSinh nguon = await DocNguonPhatSinh(conn, maDonHang, req.MaChiTietGoc);
        IActionResult loiNguon = KiemTraNguonPhatSinh(nguon);
        if (loiNguon != null) return loiNguon;

        int tuTrangGoc = nguon.TuTrang ?? 1;
        int denTrangGoc = nguon.DenTrang ?? (tuTrangGoc + nguon.SoTrang - 1);
        string loiPhamVi = KiemTraPhamViTrang(req.TuTrang, req.DenTrang, req.SoBan, tuTrangGoc, denTrangGoc);
        if (loiPhamVi != null) return BadRequest(new { message = loiPhamVi });
        if (req.SoBan > nguon.SoBan)
            return BadRequest(new { message = $"Số bản in lại không được vượt quá {nguon.SoBan} bản của dòng gốc." });

        bool dangCoYeuCau = await conn.ExecuteScalarAsync<bool>(
            @"SELECT CAST(CASE WHEN EXISTS (
                  SELECT 1 FROM DonHang
                  WHERE MaDonHangGoc = @MaDonHang AND LoaiPhatSinh = 'IN_LAI_LOI_QUAY'
                    AND TrangThai NOT IN ('HOAN_THANH','DA_HUY')
              ) THEN 1 ELSE 0 END AS BIT)", new { MaDonHang = maDonHang });
        if (dangCoYeuCau)
            return BadRequest(new { message = "Đơn này đang có một yêu cầu in lại chưa hoàn tất." });

        List<int> dsGiaCong = (await conn.QueryAsync<int>(
            "SELECT MaDichVu FROM ChiTietGiaCong WHERE MaChiTiet = @MaChiTiet ORDER BY MaDichVu",
            new { MaChiTiet = req.MaChiTietGoc })).ToList();

        try
        {
            DonPhatSinhResponse ketQua = await TaoDonPhatSinhAsync(
                conn, nguon, maNhanVien, "IN_LAI_LOI_QUAY", lyDo,
                req.TuTrang, req.DenTrang, req.SoBan, nguon.MaDichVu, dsGiaCong, true);
            return Ok(ketQua);
        }
        catch (Exception ex) when (ex is SqlException || ex is InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/don-hang/5/bo-sung-tai-quay
    // Khách đổi ý hoặc mua thêm: tạo đơn COD mới, chỉ tính đúng phạm vi trang và gia công được chọn.
    [HttpPost("{maDonHang}/bo-sung-tai-quay")]
    [Authorize(Roles = "BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> TaoDonBoSungTaiQuay([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, TaoDonBoSungTaiQuayRequest req)
    {
        int maNhanVien = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int maChiNhanh = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        if (await LayCaDangMo(conn, maNhanVien, maChiNhanh) == null)
            return BadRequest(new { message = "Bạn phải mở ca làm việc trước khi lập đơn bổ sung." });
        NguonPhatSinh nguon = await DocNguonPhatSinh(conn, maDonHang, req.MaChiTietGoc);
        IActionResult loiNguon = KiemTraNguonPhatSinh(nguon);
        if (loiNguon != null) return loiNguon;

        string loiPhamVi = KiemTraPhamViTrang(req.TuTrang, req.DenTrang, req.SoBan, 1, nguon.TongSoTrangFile);
        if (loiPhamVi != null) return BadRequest(new { message = loiPhamVi });

        string ghiChu = string.IsNullOrWhiteSpace(req.GhiChu)
            ? $"Khách mua thêm từ trang {req.TuTrang} đến trang {req.DenTrang}"
            : req.GhiChu.Trim();
        if (ghiChu.Length > 200)
            return BadRequest(new { message = "Ghi chú đơn bổ sung không được vượt quá 200 ký tự." });

        try
        {
            DonPhatSinhResponse ketQua = await TaoDonPhatSinhAsync(
                conn, nguon, maNhanVien, "BO_SUNG", ghiChu,
                req.TuTrang, req.DenTrang, req.SoBan, req.MaDichVu,
                req.DsGiaCong?.Distinct().ToList() ?? new List<int>(), false);
            return Ok(ketQua);
        }
        catch (Exception ex) when (ex is SqlException || ex is InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/don-hang/5/huy — khách chỉ tự hủy khi quầy chưa tiếp nhận; quản lý hủy các bước còn lại.
    [HttpPost("{maDonHang}/huy")]
    [Authorize(Roles = "CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> HuyDon([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, HuyDonRequest req)
    {
        int maNguoiHuy = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        if (string.IsNullOrWhiteSpace(req.LyDo))
            return BadRequest(new { message = "Vui lòng nhập lý do hủy đơn." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        var don = await conn.QueryFirstOrDefaultAsync<DonPhucHoiFile>(
            @"SELECT dh.MaChiNhanh, dh.MaKhachHang, dh.TrangThai, tt.PhuongThuc,
                     tt.TrangThai AS TrangThaiThanhToan FROM DonHang dh WITH (UPDLOCK, HOLDLOCK)
              JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang WHERE dh.MaDonHang=@MaDonHang",
            new { MaDonHang = maDonHang }, giaoDich);
        if (don == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (User.IsInRole(VaiTro.Customer))
        {
            if (don.MaKhachHang != maNguoiHuy) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        }
        else if (!DuocLamViecVoiChiNhanh(don.MaChiNhanh))
            return StatusCode(403, new { message = "Đơn không thuộc phạm vi chi nhánh đang chọn." });
        if (don.TrangThai == "DANG_IN" && don.PhuongThuc == "VIETQR" && don.TrangThaiThanhToan == "DA_THANH_TOAN")
            return BadRequest(new { message = "Connector đang xử lý đơn; phải kiểm tra và dừng máy thực tế trước, không hủy lệnh đang in từ đây." });
        try
        {
            await conn.ExecuteAsync("sp_HuyDonHang", new
            {
                MaDonHang = maDonHang,
                MaNguoiHuy = maNguoiHuy,
                LyDo = req.LyDo.Trim()
            }, transaction: giaoDich, commandType: CommandType.StoredProcedure);
            await giaoDich.CommitAsync();
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        bool canHoanTien = await conn.ExecuteScalarAsync<bool>(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM ThanhToan WHERE MaDonHang = @MaDonHang AND TrangThai = 'DA_THANH_TOAN') THEN 1 ELSE 0 END AS BIT)",
            new { MaDonHang = maDonHang });
        await XoaFileCuaDonDaKetThuc(conn, maDonHang);
        await BaoDonDoiTrangThai(conn, maDonHang, "Đơn đã bị hủy");
        return Ok(new
        {
            message = canHoanTien
                ? "Đã hủy đơn. Đơn đã thanh toán đang chờ Super Admin xác nhận hoàn tiền."
                : "Đã hủy đơn.",
            canHoanTien
        });
    }

    // POST api/don-hang/5/hoan-tien — chỉ Super Admin xác nhận sau khi đã chuyển tiền lại cho khách.
    [HttpPost("{maDonHang}/hoan-tien")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> HoanTien([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang)
    {
        int maNguoiDuyet = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? maChiNhanhDon = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaChiNhanh FROM DonHang WHERE MaDonHang=@MaDonHang", new { MaDonHang = maDonHang });
        if (maChiNhanhDon == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (!DuocLamViecVoiChiNhanh(maChiNhanhDon.Value))
            return StatusCode(403, new { message = "Đơn không thuộc phạm vi chi nhánh đang chọn." });
        try
        {
            await conn.ExecuteAsync("sp_HoanTien",
                new { MaDonHang = maDonHang, MaNguoiDuyet = maNguoiDuyet },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        await BaoDonDoiTrangThai(conn, maDonHang, "Đơn đã được hoàn tiền");
        return Ok(new KetQuaDto { Message = "Đã xác nhận hoàn tiền cho khách." });
    }

    // POST api/don-hang/5/danh-gia — khách đánh giá một lần sau khi đơn hoàn thành.
    [HttpPost("{maDonHang}/danh-gia")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> DanhGia([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDonHang, DanhGiaRequest req)
    {
        int maKhachHang = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        if (req.SoSao < 1 || req.SoSao > 5)
            return BadRequest(new { message = "Số sao phải từ 1 đến 5." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        try
        {
            await conn.ExecuteAsync("sp_DanhGia", new
            {
                MaDonHang = maDonHang,
                MaKhachHang = maKhachHang,
                SoSao = req.SoSao,
                NhanXet = string.IsNullOrWhiteSpace(req.NhanXet) ? null : req.NhanXet.Trim()
            }, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        return Ok(new KetQuaDto { Message = "Cảm ơn bạn đã đánh giá dịch vụ." });
    }

    // GET api/don-hang/danh-gia — quản lý xem phản hồi; quản lý chi nhánh chỉ thấy chi nhánh mình.
    [HttpGet("danh-gia")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> DanhSachDanhGia()
    {
        int? maChiNhanh = User.FindFirst(TenClaim.MaChiNhanh) == null
            ? null : int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        var danhSach = await conn.QueryAsync<DanhGiaDto>(
            @"SELECT dg.MaDanhGia, dg.MaDonHang,
                     'DH' + RIGHT('00000' + CAST(dg.MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     dh.MaChiNhanh, cn.TenChiNhanh, kh.HoTen AS TenKhachHang,
                     dg.SoSao, dg.NhanXet, dg.TrangThai, dg.NgayDanhGia
              FROM DanhGia dg
              JOIN DonHang dh ON dh.MaDonHang = dg.MaDonHang
              JOIN ChiNhanh cn ON cn.MaChiNhanh = dh.MaChiNhanh
              JOIN NguoiDung kh ON kh.MaNguoiDung = dh.MaKhachHang
              WHERE (@MaChiNhanh IS NULL OR dh.MaChiNhanh = @MaChiNhanh)
              ORDER BY dg.NgayDanhGia DESC", new { MaChiNhanh = maChiNhanh });
        return Ok(danhSach);
    }

    [HttpPut("danh-gia/{maDanhGia}/an")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> AnDanhGia([Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int maDanhGia)
    {
        int? maChiNhanh = User.FindFirst(TenClaim.MaChiNhanh) == null
            ? null : int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        int soDong = await conn.ExecuteAsync(
            @"UPDATE dg SET TrangThai = N'AN'
              FROM DanhGia dg JOIN DonHang dh ON dh.MaDonHang = dg.MaDonHang
              WHERE dg.MaDanhGia = @MaDanhGia AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh = @MaChiNhanh)",
            new { MaDanhGia = maDanhGia, MaChiNhanh = maChiNhanh });
        if (soDong == 0)
            return NotFound(new { message = "Không tìm thấy đánh giá trong phạm vi quản lý." });
        return Ok(new KetQuaDto { Message = "Đã ẩn đánh giá." });
    }

    private async Task<NguonPhatSinh> DocNguonPhatSinh(SqlConnection conn, int maDonHang, int maChiTiet)
    {
        return await conn.QueryFirstOrDefaultAsync<NguonPhatSinh>(
            @"SELECT dh.MaDonHang, dh.MaKhachHang, dh.MaChiNhanh, dh.HinhThucNhan,
                     dh.TrangThai AS TrangThaiDonHang, kh.SoDienThoai AS SoDienThoaiKhach,
                     cn.TenChiNhanh, ct.MaChiTiet, ct.MaFile, ct.MaDichVu,
                     ct.SoTrang, ct.TuTrang, ct.DenTrang, ct.SoBan, ct.HuongGiay,
                     f.SoTrang AS TongSoTrangFile, f.TrangThai AS TrangThaiFile
              FROM DonHang dh
              JOIN ChiNhanh cn ON cn.MaChiNhanh = dh.MaChiNhanh
              JOIN NguoiDung kh ON kh.MaNguoiDung = dh.MaKhachHang
              JOIN ChiTietDonHang ct ON ct.MaDonHang = dh.MaDonHang
              JOIN FileIn f ON f.MaFile = ct.MaFile
              WHERE dh.MaDonHang = @MaDonHang AND ct.MaChiTiet = @MaChiTiet",
            new { MaDonHang = maDonHang, MaChiTiet = maChiTiet });
    }

    private IActionResult KiemTraNguonPhatSinh(NguonPhatSinh nguon)
    {
        if (nguon == null)
            return NotFound(new { message = "Không tìm thấy tài liệu thuộc đơn hàng này." });
        if (!DuocLamViecVoiChiNhanh(nguon.MaChiNhanh))
            return StatusCode(403, new { message = "Đơn này không thuộc chi nhánh của bạn." });
        if (nguon.TrangThaiDonHang != "DA_IN" || nguon.HinhThucNhan != "TAI_QUAY")
            return BadRequest(new { message = "Chỉ xử lý phát sinh khi đơn đã in xong và khách đang nhận tại quầy." });
        if (nguon.TrangThaiFile != "SAN_SANG" || nguon.TongSoTrangFile <= 0)
            return BadRequest(new { message = "File gốc không còn sẵn sàng để xử lý yêu cầu phát sinh." });
        return null;
    }

    private static string KiemTraPhamViTrang(int tuTrang, int denTrang, int soBan, int tuNhoNhat, int denLonNhat)
    {
        if (soBan < 1 || soBan > 100)
            return "Số bản phải từ 1 đến 100.";
        if (tuTrang < tuNhoNhat || denTrang > denLonNhat || tuTrang > denTrang)
            return $"Phạm vi trang phải nằm từ {tuNhoNhat} đến {denLonNhat}.";
        return null;
    }

    private async Task<DonPhatSinhResponse> TaoDonPhatSinhAsync(
        SqlConnection conn, NguonPhatSinh nguon, int maNhanVien,
        string loaiPhatSinh, string lyDo, int tuTrang, int denTrang, int soBan,
        int maDichVu, List<int> dsGiaCong, bool mienPhi)
    {
        DichVuDto dichVuIn = await conn.QueryFirstOrDefaultAsync<DichVuDto>(
            @"SELECT dv.MaDichVu, dv.TenDichVu, dv.LoaiDichVu, dv.KhoGiay, dv.LoaiGiay,
                     dv.MatIn, dv.MauSac, dv.NhomGiaCong, bg.DonGia
              FROM DichVu dv
              JOIN BangGiaChiNhanh bg ON bg.MaDichVu = dv.MaDichVu
              WHERE dv.MaDichVu = @MaDichVu AND bg.MaChiNhanh = @MaChiNhanh
                AND dv.LoaiDichVu = 'IN_AN' AND bg.TrangThai = N'HIEN'",
            new { MaDichVu = maDichVu, nguon.MaChiNhanh });
        if (dichVuIn == null)
            throw new InvalidOperationException("Quy cách in đã chọn không còn được bán.");

        dsGiaCong = dsGiaCong.Distinct().ToList();
        List<DichVuDto> giaCong = dsGiaCong.Count == 0
            ? new List<DichVuDto>()
            : (await conn.QueryAsync<DichVuDto>(
                @"SELECT dv.MaDichVu, dv.TenDichVu, dv.LoaiDichVu, dv.NhomGiaCong, bg.DonGia
                  FROM DichVu dv
                  JOIN BangGiaChiNhanh bg ON bg.MaDichVu = dv.MaDichVu
                  WHERE dv.MaDichVu IN @DsGiaCong AND bg.MaChiNhanh = @MaChiNhanh
                    AND dv.LoaiDichVu = 'GIA_CONG' AND bg.TrangThai = N'HIEN'",
                new { DsGiaCong = dsGiaCong, nguon.MaChiNhanh })).ToList();
        if (giaCong.Count != dsGiaCong.Count)
            throw new InvalidOperationException("Có dịch vụ hoàn thiện không tồn tại hoặc đã ngừng bán.");
        bool trungNhom = giaCong.Where(x => !string.IsNullOrWhiteSpace(x.NhomGiaCong))
            .GroupBy(x => x.NhomGiaCong, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1);
        if (trungNhom)
            throw new InvalidOperationException("Mỗi bản in chỉ được chọn một kiểu đóng gáy.");

        YeuCauMayIn yeuCau = await DocYeuCauMayIn(conn, new List<int> { maDichVu });
        List<MayIn> dsMay = (await conn.QueryAsync<MayIn>(
            "SELECT * FROM MayIn WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = N'HOAT_DONG'",
            new { nguon.MaChiNhanh })).ToList();
        string lyDoKhongIn = LyDoKhongInDuoc(nguon.TenChiNhanh, dsMay, nguon.MaChiNhanh, yeuCau);
        if (lyDoKhongIn != null)
            throw new InvalidOperationException(lyDoKhongIn);

        int soTrang = denTrang - tuTrang + 1;
        var bangDongIn = new DataTable();
        bangDongIn.Columns.Add("MaFile", typeof(int));
        bangDongIn.Columns.Add("MaDichVu", typeof(int));
        bangDongIn.Columns.Add("SoTrang", typeof(int));
        bangDongIn.Columns.Add("SoBan", typeof(int));
        bangDongIn.Rows.Add(nguon.MaFile, maDichVu, soTrang, soBan);

        var bangGiaCong = new DataTable();
        bangGiaCong.Columns.Add("MaFile", typeof(int));
        bangGiaCong.Columns.Add("MaDichVu", typeof(int));
        foreach (int maGiaCong in dsGiaCong)
            bangGiaCong.Rows.Add(nguon.MaFile, maGiaCong);

        var thamSo = new DynamicParameters();
        thamSo.Add("@MaKhachHang", nguon.MaKhachHang);
        thamSo.Add("@MaChiNhanh", nguon.MaChiNhanh);
        thamSo.Add("@HinhThucNhan", "TAI_QUAY");
        thamSo.Add("@PhuongThuc", "COD");
        thamSo.Add("@DiaChiGiao", null);
        thamSo.Add("@SdtNhan", nguon.SoDienThoaiKhach);
        thamSo.Add("@KhoangCach", null);
        thamSo.Add("@PhiShipApi", null);
        thamSo.Add("@InGap", false);
        thamSo.Add("@MaCode", null);
        thamSo.Add("@DsFile", bangDongIn.AsTableValuedParameter("Kieu_DongDonHang"));
        thamSo.Add("@DsGiaCong", bangGiaCong.AsTableValuedParameter("Kieu_DongGiaCong"));
        thamSo.Add("@HuongGiayJson", JsonSerializer.Serialize(new[] { new { maFile = nguon.MaFile, huongGiay = nguon.HuongGiay } }));
        thamSo.Add("@MaDonHang", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await using var giaoDich = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            await conn.ExecuteAsync("sp_TaoDonHang", thamSo, giaoDich, commandType: CommandType.StoredProcedure);
            int maDonMoi = thamSo.Get<int>("@MaDonHang");
            string ghiChu = loaiPhatSinh == "IN_LAI_LOI_QUAY"
                ? $"In lại miễn phí do lỗi quầy · {lyDo} · trang {tuTrang}-{denTrang}"
                : $"Đơn bổ sung của DH{nguon.MaDonHang:00000} · {lyDo} · trang {tuTrang}-{denTrang}";

            await conn.ExecuteAsync(
                @"UPDATE DonHang
                  SET MaDonHangGoc = @MaDonHangGoc, LoaiPhatSinh = @LoaiPhatSinh,
                      MaNguoiTaoPhatSinh = @MaNhanVien, LyDoPhatSinh = @LyDo,
                      GhiChu = @GhiChu
                  WHERE MaDonHang = @MaDonMoi;
                  UPDATE ChiTietDonHang
                  SET TuTrang = @TuTrang, DenTrang = @DenTrang
                  WHERE MaDonHang = @MaDonMoi;",
                new
                {
                    MaDonHangGoc = nguon.MaDonHang,
                    LoaiPhatSinh = loaiPhatSinh,
                    MaNhanVien = maNhanVien,
                    LyDo = lyDo,
                    GhiChu = ghiChu.Length > 255 ? ghiChu[..255] : ghiChu,
                    TuTrang = tuTrang,
                    DenTrang = denTrang,
                    MaDonMoi = maDonMoi
                }, giaoDich);

            if (mienPhi)
            {
                await conn.ExecuteAsync(
                    @"UPDATE DonHang
                      SET TienHang = 0, PhiShip = 0, GiamGia = 0, TongTien = 0,
                          TrangThai = 'DA_XAC_NHAN', MaNhanVien = @MaNhanVien
                      WHERE MaDonHang = @MaDonMoi;
                      UPDATE ChiTietDonHang SET DonGia = 0, ThanhTien = 0 WHERE MaDonHang = @MaDonMoi;
                      UPDATE ThanhToan
                      SET SoTien = 0, TrangThai = 'DA_THANH_TOAN', NgayThanhToan = GETDATE()
                      WHERE MaDonHang = @MaDonMoi;",
                    new { MaDonMoi = maDonMoi, MaNhanVien = maNhanVien }, giaoDich);
            }

            await conn.ExecuteAsync(
                @"INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
                  VALUES (@MaKhachHang, @MaDonLienKet, @TieuDe, @NoiDung)",
                new
                {
                    nguon.MaKhachHang,
                    MaDonLienKet = mienPhi ? nguon.MaDonHang : maDonMoi,
                    TieuDe = mienPhi ? "Quầy đã nhận in lại miễn phí" : "Đã tạo đơn bổ sung",
                    NoiDung = mienPhi
                        ? $"Quầy đang in lại trang {tuTrang}-{denTrang} và bạn không phải trả thêm tiền."
                        : $"Đơn bổ sung trang {tuTrang}-{denTrang} đã được tạo và sẽ thu tiền riêng khi bàn giao."
                }, giaoDich);

            DonPhatSinhResponse ketQua = await conn.QueryFirstAsync<DonPhatSinhResponse>(
                @"SELECT MaDonHang,
                         'DH' + RIGHT('00000' + CAST(MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                         LoaiPhatSinh, TongTien
                  FROM DonHang WHERE MaDonHang = @MaDonMoi",
                new { MaDonMoi = maDonMoi }, giaoDich);
            ketQua.Message = mienPhi
                ? $"Đã tạo việc in lại {ketQua.MaDonHangHienThi}; cửa hàng chịu toàn bộ chi phí."
                : $"Đã tạo đơn bổ sung {ketQua.MaDonHangHienThi}; khách thanh toán {ketQua.TongTien:#,##0}₫ khi nhận.";

            await giaoDich.CommitAsync();

            try
            {
                await CapNhatHub.GuiChoChiNhanh(_hub, nguon.MaChiNhanh, HubEvents.OrderCreated, new TinCapNhat
                {
                    MaDonHang = maDonMoi,
                    MaDonHangHienThi = ketQua.MaDonHangHienThi,
                    MaChiNhanh = nguon.MaChiNhanh,
                    TrangThai = mienPhi ? TrangThaiDonHang.DaXacNhan : TrangThaiDonHang.ChoXacNhan,
                    NoiDung = ketQua.Message
                });
            }
            catch { }

            return ketQua;
        }
        catch
        {
            try { await giaoDich.RollbackAsync(); } catch { }
            throw;
        }
    }

    // =================================================================== CÁC HÀM PHỤ

    // Cấu hình khách chọn đòi máy in phải có gì
    public class YeuCauMayIn
    {
        public bool CanA3 { get; set; }
        public bool CanMau { get; set; }
        public bool Can2Mat { get; set; }
    }

    public class FileTrongDon
    {
        public int MaChiNhanh { get; set; }
        public string TenFile { get; set; }
        public string DuongDan { get; set; }
        public string TrangThai { get; set; }
    }

    private sealed class NguonPhatSinh
    {
        public int MaDonHang { get; set; }
        public int MaKhachHang { get; set; }
        public int MaChiNhanh { get; set; }
        public string TenChiNhanh { get; set; } = "";
        public string HinhThucNhan { get; set; } = "";
        public string TrangThaiDonHang { get; set; } = "";
        public string SoDienThoaiKhach { get; set; }
        public int MaChiTiet { get; set; }
        public int MaFile { get; set; }
        public int MaDichVu { get; set; }
        public int SoTrang { get; set; }
        public int? TuTrang { get; set; }
        public int? DenTrang { get; set; }
        public int SoBan { get; set; }
        public int TongSoTrangFile { get; set; }
        public string HuongGiay { get; set; } = "DUNG";
        public string TrangThaiFile { get; set; } = "";
    }

    private sealed class LienKetPhatSinh
    {
        public int? MaDonHangGoc { get; set; }
        public string LoaiPhatSinh { get; set; }
    }

    // Đọc bảng giá để biết các dòng in khách chọn cần máy in thế nào.
    // Trả về null nếu có mã dịch vụ không phải dòng in ấn.
    private async Task<YeuCauMayIn> DocYeuCauMayIn(SqlConnection conn, List<int> dsMaDichVu)
    {
        var dsDong = (await conn.QueryAsync<DichVuDto>(
            "SELECT MaDichVu, KhoGiay, MatIn, MauSac FROM DichVu WHERE MaDichVu IN @Ds AND LoaiDichVu = 'IN_AN'",
            new { Ds = dsMaDichVu })).ToList();

        // Có mã không tìm thấy trong bảng giá in ấn
        foreach (int ma in dsMaDichVu)
        {
            bool coTrongBangGia = false;
            foreach (DichVuDto dong in dsDong)
            {
                if (dong.MaDichVu == ma) coTrongBangGia = true;
            }
            if (!coTrongBangGia) return null;
        }

        var yeuCau = new YeuCauMayIn();
        foreach (DichVuDto dong in dsDong)
        {
            if (dong.KhoGiay == "A3") yeuCau.CanA3 = true;
            if (dong.MauSac == "MAU") yeuCau.CanMau = true;
            if (dong.MatIn == "2_MAT") yeuCau.Can2Mat = true;
        }
        return yeuCau;
    }

    // Máy này thiếu khả năng gì so với yêu cầu của đơn? Trả null nếu in được.
    private string ThieuGi(MayIn may, YeuCauMayIn yeuCau)
    {
        if (yeuCau.CanA3 && may.KhoGiay != "A3") return "in được khổ A3";
        if (yeuCau.CanMau && !may.InMau) return "in được màu";
        if (yeuCau.Can2Mat && !may.In2Mat) return "in được 2 mặt";
        return null;
    }

    // Chi nhánh có máy in nào đáp ứng CẢ BA tiêu chí không (giống sp_PhanCongMayIn).
    // Trả null nếu in được, ngược lại trả câu lý do để App / Web hiện lên.
    private string LyDoKhongInDuoc(string tenChiNhanh, List<MayIn> dsMay, int maChiNhanh, YeuCauMayIn yeuCau)
    {
        var mayCuaChiNhanh = new List<MayIn>();
        foreach (MayIn may in dsMay)
        {
            if (may.MaChiNhanh == maChiNhanh && may.TrangThai == "HOAT_DONG") mayCuaChiNhanh.Add(may);
        }

        if (mayCuaChiNhanh.Count == 0)
            return tenChiNhanh + " hiện không có máy in nào hoạt động.";

        // Có máy nào làm được hết không
        foreach (MayIn may in mayCuaChiNhanh)
        {
            if (ThieuGi(may, yeuCau) == null) return null;
        }

        // Không máy nào làm được hết → tìm lý do dễ hiểu nhất để báo cho khách.
        // Nếu cả chi nhánh không máy nào làm được tiêu chí X thì báo đúng tiêu chí đó.
        bool coA3 = false, coMau = false, co2Mat = false;
        foreach (MayIn may in mayCuaChiNhanh)
        {
            if (may.KhoGiay == "A3") coA3 = true;
            if (may.InMau) coMau = true;
            if (may.In2Mat) co2Mat = true;
        }

        if (yeuCau.CanA3 && !coA3) return tenChiNhanh + " chưa có máy in khổ A3.";
        if (yeuCau.CanMau && !coMau) return tenChiNhanh + " chưa có máy in màu.";
        if (yeuCau.Can2Mat && !co2Mat) return tenChiNhanh + " chưa có máy in 2 mặt.";

        // Từng tiêu chí đều có máy làm được, nhưng không máy nào làm được cùng lúc
        return tenChiNhanh + " chưa có máy in đáp ứng cùng lúc các quy cách bạn chọn.";
    }

    // Người đang đăng nhập có được xử lý đơn của chi nhánh này không
    private bool DuocLamViecVoiChiNhanh(int maChiNhanhDon)
    {
        var claim = User.FindFirst(TenClaim.MaChiNhanh);
        if (claim != null) return int.Parse(claim.Value) == maChiNhanhDon;
        return User.IsInRole(VaiTro.SuperAdmin);
    }

    private static Task<int?> LayCaDangMo(SqlConnection conn, int maNhanVien, int maChiNhanh)
    {
        return conn.ExecuteScalarAsync<int?>(
            @"SELECT MaCa FROM CaLamViec
              WHERE MaNhanVien = @MaNhanVien AND MaChiNhanh = @MaChiNhanh AND TrangThai = 'DANG_MO'",
            new { MaNhanVien = maNhanVien, MaChiNhanh = maChiNhanh });
    }

    // Dựng các mốc tiến độ từ trạng thái hiện tại (CSDL không lưu bảng lịch sử trạng thái).
    // Mốc nào đã đi qua thì DaXong = true, mốc đơn đang đứng thì DangO = true.
    private List<MocTienDoDto> DungTienDo(DonHangChiTietDto don, DateTime? gioNhanGiao)
    {
        var dsMoc = new List<MocTienDoDto>();

        // Đơn bị hủy: chỉ hiện 2 mốc cho gọn
        if (don.TrangThaiDonHang == TrangThaiDonHang.DaHuy)
        {
            dsMoc.Add(new MocTienDoDto
            {
                Ma = TrangThaiDonHang.ChoXacNhan,
                TieuDe = "Đã đặt đơn",
                MoTa = "Đơn vào hàng đợi " + don.TenChiNhanh,
                ThoiDiem = don.NgayDat,
                DaXong = true
            });
            dsMoc.Add(new MocTienDoDto
            {
                Ma = TrangThaiDonHang.DaHuy,
                TieuDe = "Đơn đã hủy",
                MoTa = don.GhiChu,
                DangO = true
            });
            return dsMoc;
        }

        int buocHienTai = ThuTuTrangThai(don.TrangThaiDonHang);

        dsMoc.Add(new MocTienDoDto
        {
            Ma = TrangThaiDonHang.ChoXacNhan,
            TieuDe = "Chờ tiếp nhận",
            MoTa = don.TrangThaiThanhToan == "CHUA_THANH_TOAN" && don.PhuongThucThanhToan == "VIETQR"
                ? "Đang chờ bạn chuyển khoản"
                : "Đơn vào hàng đợi " + don.TenChiNhanh,
            ThoiDiem = don.NgayDat
        });

        dsMoc.Add(new MocTienDoDto
        {
            Ma = TrangThaiDonHang.DaXacNhan,
            TieuDe = "Đã tiếp nhận",
            MoTa = don.TenNhanVien == null ? "Nhân viên quầy nhận đơn" : don.TenNhanVien + " · Nhân viên in ấn"
        });

        dsMoc.Add(new MocTienDoDto
        {
            Ma = TrangThaiDonHang.DangIn,
            TieuDe = "Đang in ấn",
            MoTa = don.TenMayIn == null ? "Đang xếp máy in" : "Máy in " + don.TenMayIn
        });

        dsMoc.Add(new MocTienDoDto
        {
            Ma = TrangThaiDonHang.DaIn,
            TieuDe = "Đã in xong",
            MoTa = "File PDF được giữ tới khi đơn hàng hoàn tất"
        });

        if (don.HinhThucNhan == HinhThucNhan.GiaoHang)
        {
            dsMoc.Add(new MocTienDoDto
            {
                Ma = TrangThaiDonHang.DangGiao,
                TieuDe = don.TrangThaiDonHang == TrangThaiDonHang.GiaoThatBai ? "Giao thất bại" : "Đang giao",
                MoTa = don.TenShipper == null ? "Chờ shipper nhận đơn" : "Shipper " + don.TenShipper,
                ThoiDiem = gioNhanGiao
            });
        }

        dsMoc.Add(new MocTienDoDto
        {
            Ma = TrangThaiDonHang.HoanThanh,
            TieuDe = "Hoàn tất",
            MoTa = don.HinhThucNhan == HinhThucNhan.TaiQuay ? "Khách nhận tài liệu tại quầy" : "Khách đã nhận tài liệu",
            ThoiDiem = don.NgayHoanThanh
        });

        // So thứ tự để biết mốc nào đã qua, mốc nào đang đứng
        foreach (MocTienDoDto moc in dsMoc)
        {
            int thuTu = ThuTuTrangThai(moc.Ma);
            moc.DaXong = thuTu < buocHienTai;
            moc.DangO = thuTu == buocHienTai;
        }
        return dsMoc;
    }

    // Trạng thái đơn xếp theo thứ tự trước sau để so sánh
    private int ThuTuTrangThai(string trangThai)
    {
        if (trangThai == TrangThaiDonHang.ChoXacNhan) return 1;
        if (trangThai == TrangThaiDonHang.DaXacNhan) return 2;
        if (trangThai == TrangThaiDonHang.DangIn) return 3;
        if (trangThai == TrangThaiDonHang.DaIn) return 4;
        if (trangThai == TrangThaiDonHang.DangGiao) return 5;
        if (trangThai == TrangThaiDonHang.GiaoThatBai) return 5;
        if (trangThai == TrangThaiDonHang.DaGiao) return 6;
        if (trangThai == TrangThaiDonHang.HoanThanh) return 7;
        return 0;
    }

    // Sau khi đổi trạng thái đơn: báo real-time cho quầy và cho đúng khách hàng chủ đơn
    private async Task BaoDonDoiTrangThai(SqlConnection conn, int maDonHang, string noiDung)
    {
        var don = await conn.QueryFirstAsync<TinCapNhat>(
            @"SELECT MaDonHang, 'DH' + RIGHT('00000' + CAST(MaDonHang AS VARCHAR(10)), 5) AS MaDonHangHienThi,
                     MaChiNhanh, TrangThai
              FROM DonHang WHERE MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });
        don.NoiDung = noiDung;

        int maKhachHang = await conn.ExecuteScalarAsync<int>(
            "SELECT MaKhachHang FROM DonHang WHERE MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });

        await CapNhatHub.GuiChoChiNhanh(_hub, don.MaChiNhanh, HubEvents.OrderStatusChanged, don);
        await CapNhatHub.GuiChoNguoiDung(_hub, maKhachHang, HubEvents.OrderStatusChanged, don);
    }

    // Xóa file thật trên ổ đĩa. Nếu file đang bị chương trình khác giữ thì để job nền thử lại.
    private void XoaFile(string duongDan)
    {
        try
        {
            if (System.IO.File.Exists(duongDan))
                System.IO.File.Delete(duongDan);
        }
        catch { }
    }

    // Xóa file của một đơn đã HOAN_THANH / DA_HUY rồi cập nhật FileIn.
    // Job nền vẫn quét lại mỗi phút để xử lý các nhánh kết thúc khác (giao hàng, hủy quá hạn...).
    private async Task XoaFileCuaDonDaKetThuc(SqlConnection conn, int maDonHang)
    {
        string trangThai = await conn.ExecuteScalarAsync<string>(
            "SELECT TrangThai FROM DonHang WHERE MaDonHang = @MaDonHang", new { MaDonHang = maDonHang });
        if (trangThai != "HOAN_THANH" && trangThai != "DA_HUY") return;

        var dsFile = await conn.QueryAsync<FileCanXoa>(
            TruyVanSql.DonHang.FileCoTheXoa,
            new { MaDonHang = maDonHang });

        foreach (FileCanXoa file in dsFile)
        {
            XoaFile(file.DuongDan);
            if (!System.IO.File.Exists(file.DuongDan))
            {
                await conn.ExecuteAsync(
                    "UPDATE FileIn SET TrangThai = N'DA_XOA', DuongDan = NULL WHERE MaFile = @MaFile",
                    new { file.MaFile });
            }
        }
    }

    private class FileCanXoa
    {
        public int MaFile { get; set; }
        public string DuongDan { get; set; }
    }

    // ---- TUẦN 3 / F-34: lấy quãng đường và phí giao hàng từ API Ahamove ----

    private async Task<KetQuaPhiGiaoHang> TinhPhiGiaoHangAhamove(ChiNhanh chiNhanh, string diaChiKhach,
        decimal? viDoKhach, decimal? kinhDoKhach, string tenKhach, string sdtKhach)
    {
        if (string.IsNullOrWhiteSpace(chiNhanh.SoDienThoai) || string.IsNullOrWhiteSpace(sdtKhach))
            throw new Exception("Chi nhánh hoặc người nhận chưa có số điện thoại để Ahamove báo giá.");

        string khoaNho;
        if (viDoKhach != null && kinhDoKhach != null)
            khoaNho = $"ahamove|{chiNhanh.MaChiNhanh}|{Math.Round(viDoKhach.Value, 5)},{Math.Round(kinhDoKhach.Value, 5)}";
        else
            khoaNho = "ahamove|" + chiNhanh.MaChiNhanh + "|" + ChuanHoaDiaChi(diaChiKhach);

        if (_boNhoTam.TryGetValue(khoaNho, out KetQuaPhiGiaoHang ketQuaDaNho))
            return ketQuaDaNho;

        string token = _config["GiaoHang:AhamoveToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            decimal? kmGiaLap = _config.GetValue<decimal?>("GiaoHang:QuangDuongGiaLapKm");
            decimal? phiGiaLap = _config.GetValue<decimal?>("GiaoHang:PhiShipGiaLap");
            if (kmGiaLap != null && phiGiaLap != null)
                return new KetQuaPhiGiaoHang(kmGiaLap.Value, phiGiaLap.Value, 0);
            throw new Exception("Máy chủ chưa cấu hình token Ahamove nên chưa lấy được phí giao hàng. Vui lòng chọn nhận tại quầy.");
        }

        string nhomDichVu = _config["GiaoHang:NhomDichVu"] ?? "BIKE";
        string baseUrl = (_config["GiaoHang:AhamoveBaseUrl"] ?? "https://partner-apistg.ahamove.com/v3").TrimEnd('/');

        var diemLay = new Dictionary<string, object>
        {
            ["lat"] = chiNhanh.ViDo.Value,
            ["lng"] = chiNhanh.KinhDo.Value,
            ["address"] = chiNhanh.DiaChi,
            ["name"] = chiNhanh.TenChiNhanh,
            ["mobile"] = chiNhanh.SoDienThoai
        };
        var diemGiao = new Dictionary<string, object>
        {
            ["address"] = diaChiKhach,
            ["name"] = string.IsNullOrWhiteSpace(tenKhach) ? "Khách hàng" : tenKhach,
            ["mobile"] = sdtKhach
        };
        if (viDoKhach != null && kinhDoKhach != null)
        {
            diemGiao["lat"] = viDoKhach.Value;
            diemGiao["lng"] = kinhDoKhach.Value;
        }

        var duLieuGui = new
        {
            order_time = 0,
            path = new[] { diemLay, diemGiao },
            group_services = new[] { new { _id = nhomDichVu, group_requests = Array.Empty<object>() } },
            payment_method = "CASH",
            remarks = "Giao tài liệu in"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/orders/estimates");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(duLieuGui), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch
        {
            throw new Exception("Dịch vụ giao hàng Ahamove đang không phản hồi, vui lòng thử lại sau.");
        }

        string json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception(response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "Token Ahamove không hợp lệ hoặc đã hết hạn."
                : "Ahamove không tính được phí cho địa chỉ này. Vui lòng kiểm tra lại địa chỉ giao hàng.");

        JsonArray danhSach = JsonNode.Parse(json)?.AsArray();
        JsonNode duLieu = danhSach?.FirstOrDefault()?["data"];
        if (duLieu?["distance"] == null || duLieu["total_price"] == null)
            throw new Exception("Ahamove trả về kết quả không đầy đủ, vui lòng thử lại sau.");

        var ketQua = new KetQuaPhiGiaoHang(
            Math.Round(duLieu["distance"].GetValue<decimal>(), 2),
            Math.Round(duLieu["total_price"].GetValue<decimal>(), 0),
            duLieu["duration"]?.GetValue<int>() ?? 0);

        _boNhoTam.Set(khoaNho, ketQua, TimeSpan.FromMinutes(10));
        return ketQua;
    }

    // "  12 Lê   Lợi, Q.1 " → "12 le loi, q.1" (để 2 lần nhập hơi khác nhau vẫn dùng chung kết quả đã nhớ)
    public static string ChuanHoaDiaChi(string diaChi)
    {
        string gon = "";
        bool vuaCoDauCach = false;
        foreach (char c in diaChi.Trim().ToLower())
        {
            if (c == ' ' || c == '\t' || c == '\n')
            {
                if (!vuaCoDauCach) gon += ' ';
                vuaCoDauCach = true;
            }
            else
            {
                gon += c;
                vuaCoDauCach = false;
            }
        }
        return gon;
    }
}
