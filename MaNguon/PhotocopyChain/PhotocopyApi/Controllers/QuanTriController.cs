using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

// Các danh mục quản trị Tuần 3. Giữ xử lý ngay trong controller để cùng phong cách với phần Tuần 1–2.
[ApiController]
[Route("api/quan-tri")]
[Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
public class QuanTriController : ControllerBase
{
    private readonly string _chuoiKetNoi;

    public QuanTriController(IConfiguration config)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    // ================================================================ DỊCH VỤ / BẢNG GIÁ

    [HttpGet("dich-vu")]
    public async Task<IActionResult> DanhSachDichVu()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync<DichVuQuanTriDto>(
            @"SELECT dv.MaDichVu, bg.MaChiNhanh, cn.TenChiNhanh, dv.TenDichVu, dv.LoaiDichVu,
                     dv.KhoGiay, dv.LoaiGiay, dv.MatIn, dv.MauSac, dv.NhomGiaCong,
                     bg.DonGia, dv.MaVatTu, vt.TenVatTu, bg.TrangThai
              FROM BangGiaChiNhanh bg
              JOIN DichVu dv ON dv.MaDichVu = bg.MaDichVu
              JOIN ChiNhanh cn ON cn.MaChiNhanh = bg.MaChiNhanh
              LEFT JOIN VatTu vt ON vt.MaVatTu = dv.MaVatTu
              WHERE @MaChiNhanh IS NULL OR bg.MaChiNhanh = @MaChiNhanh
              ORDER BY cn.MaChiNhanh, dv.LoaiDichVu, dv.MaDichVu",
            new { MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    [HttpPost("dich-vu")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> TaoDichVu(LuuDichVuRequest req)
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        string loi = QuyTacQuanTriChiNhanh.KiemTraDichVu(req, maChiNhanh);
        if (loi != null) return BadRequest(new { message = loi });

        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        int ma = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO DichVu (TenDichVu, LoaiDichVu, KhoGiay, LoaiGiay, MatIn, MauSac,
                                  NhomGiaCong, DonGia, MaVatTu, TrangThai)
              VALUES (@TenDichVu, @LoaiDichVu, @KhoGiay, @LoaiGiay, @MatIn, @MauSac,
                      @NhomGiaCong, @DonGia, @MaVatTu, @TrangThai);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", req, giaoDich);
        await conn.ExecuteAsync(
            @"INSERT INTO BangGiaChiNhanh (MaChiNhanh, MaDichVu, DonGia, TrangThai)
              SELECT MaChiNhanh, @MaDichVu, @DonGia, @TrangThai FROM ChiNhanh",
            new { MaDichVu = ma, req.DonGia, req.TrangThai }, giaoDich);
        await giaoDich.CommitAsync();
        return Ok(new { maDichVu = ma, message = "Đã thêm dòng bảng giá." });
    }

    [HttpPut("dich-vu/{maDichVu}")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CapNhatDichVu(int maDichVu, LuuDichVuRequest req)
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        string loi = QuyTacQuanTriChiNhanh.KiemTraDichVu(req, maChiNhanh);
        if (loi != null) return BadRequest(new { message = loi });

        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        int soDong = await conn.ExecuteAsync(
            @"UPDATE DichVu SET TenDichVu=@TenDichVu, LoaiDichVu=@LoaiDichVu, KhoGiay=@KhoGiay,
                    LoaiGiay=@LoaiGiay, MatIn=@MatIn, MauSac=@MauSac, NhomGiaCong=@NhomGiaCong,
                    DonGia=@DonGia, MaVatTu=@MaVatTu, TrangThai=@TrangThai
              WHERE MaDichVu=@MaDichVu",
            new
            {
                MaDichVu = maDichVu, req.TenDichVu, req.LoaiDichVu, req.KhoGiay, req.LoaiGiay,
                req.MatIn, req.MauSac, req.NhomGiaCong, req.DonGia, req.MaVatTu, req.TrangThai
            }, giaoDich);
        if (soDong == 0) return NotFound(new { message = "Không tìm thấy dịch vụ." });
        await conn.ExecuteAsync(
            @"UPDATE BangGiaChiNhanh SET DonGia=@DonGia, TrangThai=@TrangThai
              WHERE MaChiNhanh=@MaChiNhanh AND MaDichVu=@MaDichVu",
            new { MaChiNhanh = maChiNhanh.Value, MaDichVu = maDichVu, req.DonGia, req.TrangThai }, giaoDich);
        await giaoDich.CommitAsync();
        return Ok(new KetQuaDto { Message = "Đã cập nhật bảng giá." });
    }

    // ================================================================ CHI NHÁNH / MÁY IN

    [HttpGet("chi-nhanh")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> DanhSachChiNhanh()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync<ChiNhanhQuanTriDto>(
            "SELECT * FROM ChiNhanh WHERE @MaChiNhanh IS NULL OR MaChiNhanh=@MaChiNhanh ORDER BY MaChiNhanh",
            new { MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    [HttpPut("chi-nhanh/{maChiNhanh}")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CapNhatChiNhanh(int maChiNhanh, ChiNhanhQuanTriDto req)
    {
        if (!DuocLamViecVoiChiNhanh(maChiNhanh))
            return StatusCode(403, new { message = "Chi nhánh không thuộc phạm vi đang chọn." });
        if (string.IsNullOrWhiteSpace(req.TenChiNhanh) || string.IsNullOrWhiteSpace(req.DiaChi))
            return BadRequest(new { message = "Tên và địa chỉ chi nhánh không được để trống." });
        if (req.BanKinhGiao < 0)
            return BadRequest(new { message = "Bán kính giao hàng không hợp lệ." });

        using var conn = new SqlConnection(_chuoiKetNoi);
        int soDong = await conn.ExecuteAsync(
            @"UPDATE ChiNhanh SET TenChiNhanh=@TenChiNhanh, DiaChi=@DiaChi, SoDienThoai=@SoDienThoai,
                    ViDo=@ViDo, KinhDo=@KinhDo, GioMoCua=@GioMoCua, GioDongCua=@GioDongCua,
                    BanKinhGiao=@BanKinhGiao, TrangThai=@TrangThai
              WHERE MaChiNhanh=@MaChiNhanh",
            new
            {
                MaChiNhanh = maChiNhanh, req.TenChiNhanh, req.DiaChi, req.SoDienThoai, req.ViDo,
                req.KinhDo, req.GioMoCua, req.GioDongCua, req.BanKinhGiao, req.TrangThai
            });
        if (soDong == 0) return NotFound(new { message = "Không tìm thấy chi nhánh." });
        return Ok(new KetQuaDto { Message = "Đã cập nhật chi nhánh và vùng giao hàng." });
    }

    [HttpGet("may-in")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> DanhSachMayIn()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync<MayInQuanTriDto>(
            @"SELECT m.*, cn.TenChiNhanh FROM MayIn m JOIN ChiNhanh cn ON cn.MaChiNhanh=m.MaChiNhanh
              WHERE @MaChiNhanh IS NULL OR m.MaChiNhanh=@MaChiNhanh ORDER BY m.MaChiNhanh, m.MaMayIn",
            new { MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    [HttpPut("may-in/{maMayIn}")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> CapNhatMayIn(int maMayIn, CapNhatMayInRequest req)
    {
        if (req.TrangThai != "HOAT_DONG" && req.TrangThai != "BAO_TRI" && req.TrangThai != "HONG")
            return BadRequest(new { message = "Trạng thái máy in không hợp lệ." });
        int maQuanLy = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? maChiNhanhMay = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaChiNhanh FROM MayIn WHERE MaMayIn=@MaMayIn", new { MaMayIn = maMayIn });
        if (maChiNhanhMay == null) return NotFound(new { message = "Không tìm thấy máy in." });
        if (!DuocLamViecVoiChiNhanh(maChiNhanhMay.Value))
            return StatusCode(403, new { message = "Máy in không thuộc phạm vi chi nhánh đang chọn." });
        try
        {
            await conn.ExecuteAsync("sp_CapNhatMayIn", new
            {
                MaMayIn = maMayIn, req.TrangThai, req.SoTrangDaIn, MaQuanLy = maQuanLy
            }, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        return Ok(new KetQuaDto { Message = "Đã cập nhật máy in." });
    }

    // ================================================================ KHÁCH HÀNG / KHUYẾN MÃI

    [HttpGet("khach-hang")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> DanhSachKhachHang()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync<KhachHangQuanTriDto>(
            @"SELECT nd.MaNguoiDung, nd.HoTen, nd.Email, nd.SoDienThoai, nd.DiaChi, nd.TrangThai, nd.NgayTao,
                     COUNT(dh.MaDonHang) AS SoDon,
                     ISNULL(SUM(CASE WHEN dh.TrangThai='HOAN_THANH' THEN dh.TongTien ELSE 0 END),0) AS TongChiTieu
              FROM NguoiDung nd LEFT JOIN DonHang dh ON dh.MaKhachHang=nd.MaNguoiDung
                   AND ISNULL(dh.LoaiPhatSinh, '') <> 'IN_LAI_LOI_QUAY'
                   AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh)
              WHERE nd.VaiTro='CUSTOMER'
                AND (@MaChiNhanh IS NULL OR EXISTS (
                    SELECT 1 FROM DonHang d WHERE d.MaKhachHang=nd.MaNguoiDung AND d.MaChiNhanh=@MaChiNhanh))
              GROUP BY nd.MaNguoiDung, nd.HoTen, nd.Email, nd.SoDienThoai, nd.DiaChi, nd.TrangThai, nd.NgayTao
              ORDER BY nd.NgayTao DESC", new { MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    [HttpPut("khach-hang/{maKhachHang}/trang-thai")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CapNhatKhachHang(int maKhachHang, CapNhatTrangThaiRequest req)
    {
        if (req.TrangThai != "HOAT_DONG" && req.TrangThai != "KHOA")
            return BadRequest(new { message = "Trạng thái tài khoản không hợp lệ." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? maChiNhanh = LayChiNhanhTuToken();
        if (maChiNhanh != null)
        {
            bool thuocPhamVi = await conn.ExecuteScalarAsync<bool>(
                @"SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM DonHang
                  WHERE MaKhachHang=@MaKhachHang AND MaChiNhanh=@MaChiNhanh) THEN 1 ELSE 0 END AS BIT)",
                new { MaKhachHang = maKhachHang, MaChiNhanh = maChiNhanh });
            if (!thuocPhamVi)
                return StatusCode(403, new { message = "Khách hàng không thuộc phạm vi chi nhánh đang chọn." });
        }
        int soDong = await conn.ExecuteAsync(
            "UPDATE NguoiDung SET TrangThai=@TrangThai WHERE MaNguoiDung=@MaKhachHang AND VaiTro='CUSTOMER'",
            new { req.TrangThai, MaKhachHang = maKhachHang });
        if (soDong == 0) return NotFound(new { message = "Không tìm thấy khách hàng." });
        return Ok(new KetQuaDto { Message = "Đã cập nhật tài khoản khách hàng." });
    }

    [HttpGet("khuyen-mai")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> DanhSachKhuyenMai()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        return Ok(await conn.QueryAsync<KhuyenMaiQuanTriDto>(
            @"SELECT km.*, cn.TenChiNhanh
              FROM KhuyenMai km JOIN ChiNhanh cn ON cn.MaChiNhanh = km.MaChiNhanh
              WHERE @MaChiNhanh IS NULL OR km.MaChiNhanh = @MaChiNhanh
              ORDER BY cn.MaChiNhanh, km.NgayKetThuc DESC, km.MaKhuyenMai DESC",
            new { MaChiNhanh = maChiNhanh }));
    }

    [HttpPost("khuyen-mai")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> TaoKhuyenMai(LuuKhuyenMaiRequest req)
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        string loi = QuyTacQuanTriChiNhanh.KiemTraKhuyenMai(req, maChiNhanh);
        if (loi != null) return BadRequest(new { message = loi });
        using var conn = new SqlConnection(_chuoiKetNoi);
        try
        {
            int ma = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO KhuyenMai (MaChiNhanh, MaCode, TenKhuyenMai, PhanTramGiam, GiamToiDa, DonToiThieu,
                                         NgayBatDau, NgayKetThuc, SoLuong, TrangThai)
                  VALUES (@MaChiNhanh,@MaCode,@TenKhuyenMai,@PhanTramGiam,@GiamToiDa,@DonToiThieu,
                          @NgayBatDau,@NgayKetThuc,@SoLuong,@TrangThai);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    MaChiNhanh = maChiNhanh.Value, MaCode = req.MaCode.Trim().ToUpper(), req.TenKhuyenMai, req.PhanTramGiam,
                    req.GiamToiDa, req.DonToiThieu, req.NgayBatDau, req.NgayKetThuc, req.SoLuong, req.TrangThai
                });
            return Ok(new { maKhuyenMai = ma, message = "Đã tạo mã khuyến mãi." });
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Number == 2601 || ex.Number == 2627 ? "Mã khuyến mãi đã tồn tại." : ex.Message });
        }
    }

    [HttpPut("khuyen-mai/{maKhuyenMai}")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CapNhatKhuyenMai(int maKhuyenMai, LuuKhuyenMaiRequest req)
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        string loi = QuyTacQuanTriChiNhanh.KiemTraKhuyenMai(req, maChiNhanh);
        if (loi != null) return BadRequest(new { message = loi });
        using var conn = new SqlConnection(_chuoiKetNoi);
        try
        {
            int soDong = await conn.ExecuteAsync(
                @"UPDATE KhuyenMai SET MaCode=@MaCode, TenKhuyenMai=@TenKhuyenMai, PhanTramGiam=@PhanTramGiam,
                        GiamToiDa=@GiamToiDa, DonToiThieu=@DonToiThieu, NgayBatDau=@NgayBatDau,
                        NgayKetThuc=@NgayKetThuc, SoLuong=@SoLuong, TrangThai=@TrangThai
                  WHERE MaKhuyenMai=@MaKhuyenMai AND MaChiNhanh=@MaChiNhanh",
                new
                {
                    MaKhuyenMai = maKhuyenMai, MaChiNhanh = maChiNhanh.Value,
                    MaCode = req.MaCode.Trim().ToUpper(), req.TenKhuyenMai,
                    req.PhanTramGiam, req.GiamToiDa, req.DonToiThieu, req.NgayBatDau,
                    req.NgayKetThuc, req.SoLuong, req.TrangThai
                });
            if (soDong == 0) return NotFound(new { message = "Không tìm thấy mã khuyến mãi trong chi nhánh đang chọn." });
            return Ok(new KetQuaDto { Message = "Đã cập nhật mã khuyến mãi." });
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Number == 2601 || ex.Number == 2627 ? "Mã khuyến mãi đã tồn tại trong chi nhánh này." : ex.Message });
        }
    }


    [HttpGet("kho")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> TonKho()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync<TonKhoDto>(
            @"SELECT tk.MaChiNhanh, cn.TenChiNhanh, tk.MaVatTu, vt.TenVatTu, vt.DonViTinh,
                     tk.SoLuong, tk.MucToiThieu, vt.DonGia,
                     CAST(CASE WHEN tk.SoLuong <= tk.MucToiThieu THEN 1 ELSE 0 END AS BIT) AS CanhBao
              FROM TonKho tk JOIN ChiNhanh cn ON cn.MaChiNhanh=tk.MaChiNhanh
              JOIN VatTu vt ON vt.MaVatTu=tk.MaVatTu
              WHERE @MaChiNhanh IS NULL OR tk.MaChiNhanh=@MaChiNhanh
              ORDER BY CanhBao DESC, cn.MaChiNhanh, vt.TenVatTu", new { MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    [HttpGet("phieu-nhap")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> DanhSachPhieuNhap()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync<PhieuNhapDto>(
            @"SELECT pn.MaPhieuNhap, pn.MaChiNhanh, cn.TenChiNhanh, pn.MaVatTu, vt.TenVatTu,
                     pn.NhaCungCap, pn.SoLuong, pn.DonGia, lap.HoTen AS TenNguoiLap,
                     duyet.HoTen AS TenNguoiDuyet, pn.TrangThai, pn.NgayLap
              FROM PhieuNhap pn JOIN ChiNhanh cn ON cn.MaChiNhanh=pn.MaChiNhanh
              JOIN VatTu vt ON vt.MaVatTu=pn.MaVatTu
              LEFT JOIN NguoiDung lap ON lap.MaNguoiDung=pn.MaNguoiLap
              LEFT JOIN NguoiDung duyet ON duyet.MaNguoiDung=pn.MaNguoiDuyet
              WHERE @MaChiNhanh IS NULL OR pn.MaChiNhanh=@MaChiNhanh
              ORDER BY pn.NgayLap DESC", new { MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    [HttpPost("phieu-nhap")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> TaoPhieuNhap(TaoPhieuNhapRequest req)
    {
        int maNguoiLap = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int? maChiNhanh = LayChiNhanhTuToken();
        if (maChiNhanh == null)
            return BadRequest(new { message = "Vui lòng chọn một chi nhánh trước khi lập phiếu nhập." });
        if (req.SoLuong <= 0 || req.DonGia < 0 || req.MaVatTu <= 0)
            return BadRequest(new { message = "Số lượng và đơn giá nhập không hợp lệ." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        try
        {
            await conn.ExecuteAsync("sp_TaoPhieuNhap", new
            {
                MaChiNhanh = maChiNhanh, req.MaVatTu, req.NhaCungCap, req.SoLuong, req.DonGia, MaNguoiLap = maNguoiLap
            }, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex) { return BadRequest(new { message = ex.Message }); }
        return Ok(new KetQuaDto { Message = User.IsInRole(VaiTro.SuperAdmin)
            ? "CEO đã lập và duyệt phiếu nhập trực tiếp. Vui lòng xác nhận nhận hàng khi vật tư về kho."
            : "Đã gửi đề xuất, chờ CEO duyệt." });
    }

    [HttpPost("phieu-nhap/{maPhieuNhap}/duyet")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> DuyetPhieuNhap(int maPhieuNhap, DuyetPhieuNhapRequest req)
    {
        int maNguoiDuyet = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        using var conn = new SqlConnection(_chuoiKetNoi);
        int? maChiNhanhPhieu = await conn.ExecuteScalarAsync<int?>(
            "SELECT MaChiNhanh FROM PhieuNhap WHERE MaPhieuNhap=@MaPhieuNhap", new { MaPhieuNhap = maPhieuNhap });
        if (maChiNhanhPhieu == null) return NotFound(new { message = "Không tìm thấy phiếu nhập." });
        if (!DuocLamViecVoiChiNhanh(maChiNhanhPhieu.Value))
            return StatusCode(403, new { message = "Phiếu nhập không thuộc phạm vi chi nhánh đang chọn." });
        try
        {
            await conn.ExecuteAsync("sp_DuyetPhieuNhap", new { MaPhieuNhap = maPhieuNhap, MaNguoiDuyet = maNguoiDuyet, req.DongY }, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex) { return BadRequest(new { message = ex.Message }); }
        return Ok(new KetQuaDto { Message = req.DongY ? "Đã duyệt phiếu nhập." : "Đã từ chối phiếu nhập." });
    }

    [HttpPost("phieu-nhap/{maPhieuNhap}/nhan-hang")]
    [Authorize(Roles = "BRANCH_ADMIN")]
    public async Task<IActionResult> NhanHang(int maPhieuNhap, NhanHangRequest req)
    {
        int maChiNhanh = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
        if (req.SoLuongThucNhan <= 0) return BadRequest(new { message = "Số lượng thực nhận phải lớn hơn 0." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        bool dungChiNhanh = await conn.ExecuteScalarAsync<bool>(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM PhieuNhap WHERE MaPhieuNhap=@MaPhieuNhap AND MaChiNhanh=@MaChiNhanh) THEN 1 ELSE 0 END AS BIT)",
            new { MaPhieuNhap = maPhieuNhap, MaChiNhanh = maChiNhanh });
        if (!dungChiNhanh) return StatusCode(403, new { message = "Phiếu nhập không thuộc chi nhánh của bạn." });
        try
        {
            await conn.ExecuteAsync("sp_NhapKho", new { MaPhieuNhap = maPhieuNhap, req.SoLuongThucNhan }, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex) { return BadRequest(new { message = ex.Message }); }
        return Ok(new KetQuaDto { Message = "Đã nhập vật tư vào kho." });
    }

    // ================================================================ BÁO CÁO / KHO FILE / HOÀN TIỀN

    [HttpGet("bao-cao/doanh-thu")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> BaoCaoDoanhThu(
        [FromQuery] DateTime? tuNgay,
        [FromQuery] DateTime? denNgay,
        [FromQuery] string nhomTheo = "ngay")
    {
        DateTime tu = (tuNgay ?? DateTime.Today.AddDays(-6)).Date;
        DateTime den = (denNgay ?? DateTime.Today).Date;
        if (tu > den) return BadRequest(new { message = "Từ ngày không được lớn hơn đến ngày." });

        // Chỉ ghép các biểu thức cố định do server chọn, tuyệt đối không đưa chuỗi của client thẳng vào SQL.
        string cachNhom = (nhomTheo ?? "ngay").Trim().ToLowerInvariant();
        string bieuThucNhom;
        switch (cachNhom)
        {
            case "ngay":
                bieuThucNhom = "CAST(dh.NgayHoanThanh AS DATE)";
                break;
            case "tuan":
                // 1900-01-01 là thứ Hai, nên biểu thức không phụ thuộc SET DATEFIRST của SQL Server.
                bieuThucNhom = "DATEADD(DAY, -(DATEDIFF(DAY, '19000101', CAST(dh.NgayHoanThanh AS DATE)) % 7), CAST(dh.NgayHoanThanh AS DATE))";
                break;
            case "thang":
                bieuThucNhom = "DATEFROMPARTS(YEAR(dh.NgayHoanThanh), MONTH(dh.NgayHoanThanh), 1)";
                break;
            case "quy":
                bieuThucNhom = "DATEFROMPARTS(YEAR(dh.NgayHoanThanh), ((DATEPART(QUARTER, dh.NgayHoanThanh) - 1) * 3) + 1, 1)";
                break;
            case "nam":
                bieuThucNhom = "DATEFROMPARTS(YEAR(dh.NgayHoanThanh), 1, 1)";
                break;
            default:
                return BadRequest(new { message = "Kiểu nhóm báo cáo phải là ngày, tuần, tháng, quý hoặc năm." });
        }

        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        string sql = $@"SELECT {bieuThucNhom} AS Ngay, dh.MaChiNhanh, cn.TenChiNhanh,
                               COUNT(*) AS SoDon, SUM(dh.TongTien) AS DoanhThu
                        FROM DonHang dh JOIN ChiNhanh cn ON cn.MaChiNhanh=dh.MaChiNhanh
                        WHERE dh.TrangThai='HOAN_THANH' AND dh.NgayHoanThanh>=@TuNgay
                          AND dh.NgayHoanThanh<DATEADD(DAY,1,@DenNgay)
                          AND ISNULL(dh.LoaiPhatSinh, '') <> 'IN_LAI_LOI_QUAY'
                          AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh)
                        GROUP BY {bieuThucNhom}, dh.MaChiNhanh, cn.TenChiNhanh
                        ORDER BY Ngay, dh.MaChiNhanh";
        var ds = await conn.QueryAsync<BaoCaoDoanhThuDto>(
            sql, new { TuNgay = tu, DenNgay = den, MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    [HttpGet("bao-cao/doi-soat-may-in")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> DoiSoatMayIn()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync<DoiSoatMayInDto>(
            @"SELECT ds.MaMayIn, ds.TenMay, ds.MaChiNhanh, cn.TenChiNhanh,
                     ds.SoTrangTrenMay, ds.SoTrangHeThong, ds.ChenhLech
              FROM vw_DoiSoatMayIn ds
              JOIN ChiNhanh cn ON cn.MaChiNhanh = ds.MaChiNhanh
              WHERE @MaChiNhanh IS NULL OR ds.MaChiNhanh = @MaChiNhanh
              ORDER BY ABS(ds.ChenhLech) DESC, ds.MaChiNhanh, ds.MaMayIn",
            new { MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    [HttpGet("file-in")]
    public async Task<IActionResult> KhoFile()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync(
            @"SELECT f.MaFile, f.TenFile, f.DinhDang, f.DungLuong, f.SoTrang, f.TrangThai, f.NgayTaiLen,
                     kh.HoTen AS TenKhachHang,
                     COUNT(DISTINCT ct.MaDonHang) AS SoDonDangDung
              FROM FileIn f JOIN NguoiDung kh ON kh.MaNguoiDung=f.MaKhachHang
              LEFT JOIN ChiTietDonHang ct ON ct.MaFile=f.MaFile
              LEFT JOIN DonHang dh ON dh.MaDonHang=ct.MaDonHang
              WHERE @MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh
              GROUP BY f.MaFile,f.TenFile,f.DinhDang,f.DungLuong,f.SoTrang,f.TrangThai,f.NgayTaiLen,kh.HoTen
              ORDER BY f.NgayTaiLen DESC", new { MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    [HttpGet("hoan-tien")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> DonChoHoanTien()
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        using var conn = new SqlConnection(_chuoiKetNoi);
        var ds = await conn.QueryAsync(
            @"SELECT dh.MaDonHang, 'DH'+RIGHT('00000'+CAST(dh.MaDonHang AS VARCHAR(10)),5) AS MaDonHangHienThi,
                     kh.HoTen AS TenKhachHang, kh.SoDienThoai, cn.TenChiNhanh, tt.SoTien, dh.GhiChu, dh.NgayDat
              FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang
              JOIN NguoiDung kh ON kh.MaNguoiDung=dh.MaKhachHang
              JOIN ChiNhanh cn ON cn.MaChiNhanh=dh.MaChiNhanh
              WHERE dh.TrangThai='DA_HUY' AND tt.TrangThai='DA_THANH_TOAN'
                AND (@MaChiNhanh IS NULL OR dh.MaChiNhanh=@MaChiNhanh)
              ORDER BY dh.NgayDat", new { MaChiNhanh = maChiNhanh });
        return Ok(ds);
    }

    private bool DuocLamViecVoiChiNhanh(int maChiNhanh)
    {
        int? phamVi = LayChiNhanhTuToken();
        if (phamVi != null) return phamVi == maChiNhanh;
        return User.IsInRole(VaiTro.SuperAdmin);
    }

    private int? LayChiNhanhTuToken()
    {
        var claim = User.FindFirst(TenClaim.MaChiNhanh);
        return claim == null ? null : int.Parse(claim.Value);
    }

}
