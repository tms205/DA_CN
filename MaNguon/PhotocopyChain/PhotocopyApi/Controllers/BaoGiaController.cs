using System.ComponentModel.DataAnnotations;
using System.Data;
using PhotocopyApi.GiaoHang;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DatIn;
using PhotocopyShared.QuyCachIn;

namespace PhotocopyApi.Controllers;

//báo giá tạm tính + giờ hẹn trả
[ApiController]
[Route("api/bao-gia")]
[Authorize(Roles = "CUSTOMER,BRANCH_ADMIN,STAFF")]
public class BaoGiaController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly GeoapifyService _giaoHang;

    public BaoGiaController(IConfiguration config, IMemoryCache boNhoTam, GeoapifyService giaoHang = null)
    {
        _giaoHang = giaoHang ?? new GeoapifyService(config, boNhoTam);
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    // POST api/bao-gia
    [HttpPost]
    public async Task<IActionResult> TinhGia(BaoGiaRequest req, [FromQuery] [Range(1, int.MaxValue, ErrorMessage = "Mã bản ghi phải là số nguyên dương.")] int? maKhachHang)
    {
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int maKhachHangBaoGia = maNguoiDung;

        if (req.HinhThucNhan == "GIAO_HANG")
        {
            try { _giaoHang.KiemTraViTri(req.MaViTriGiao, maNguoiDung, req.DiaChiGiao, req.ViDoGiao, req.KinhDoGiao); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        if (vaiTro != VaiTro.Customer)
        {
            if (maKhachHang == null)
                return BadRequest(new { message = "Vui lòng chọn khách hàng trước khi báo giá tại quầy." });
            maKhachHangBaoGia = maKhachHang.Value;

            int maChiNhanhNhanVien = int.Parse(User.FindFirst(TenClaim.MaChiNhanh).Value);
            if (req.MaChiNhanh != maChiNhanhNhanVien)
                return StatusCode(403, new { message = "Bạn chỉ được báo giá cho chi nhánh của mình." });
            if (req.HinhThucNhan != "TAI_QUAY")
                return BadRequest(new { message = "Đơn do quầy lập chỉ áp dụng hình thức nhận tại quầy." });
        }

        if (req.FileIn.Count == 0)
            return BadRequest(new { message = "Vui lòng chọn ít nhất 1 file để báo giá." });
        if (req.FileIn.Any(x => !QuyTacIn.HuongGiayHopLe(x.HuongGiay)))
            return BadRequest(new { message = "Hướng giấy phải là giấy đứng hoặc giấy ngang." });
        if (req.HinhThucNhan != "TAI_QUAY" && req.HinhThucNhan != "GIAO_HANG")
            return BadRequest(new { message = "Hình thức nhận hàng không hợp lệ." });

        using var conn = new SqlConnection(_chuoiKetNoi);

        //lập bảng các dòng in để truyền vào thủ tục.
        // Các cột phải đúng thứ tự của kiểu bảng CTDH trong CSDL (file 04).
        var bangDongIn = new DataTable();
        bangDongIn.Columns.Add("MaFile", typeof(int));
        bangDongIn.Columns.Add("MaDichVu", typeof(int));
        bangDongIn.Columns.Add("SoTrang", typeof(int));
        bangDongIn.Columns.Add("SoBan", typeof(int));

        // Mỗi file chọn được nhiều gia công nên gia công đi bảng riêng (Kieu_DongGiaCong),
        // mỗi dòng là 1 gia công của 1 file. Thủ tục tự chặn chọn 2 kiểu đóng gáy cùng lúc.
        var bangGiaCong = new DataTable();
        bangGiaCong.Columns.Add("MaFile", typeof(int));
        bangGiaCong.Columns.Add("MaDichVu", typeof(int));

        foreach (var file in req.FileIn)
        {
            FileInDto fileTrongCsdl = await conn.QueryFirstOrDefaultAsync<FileInDto>(
                @"SELECT MaFile AS MaFileIn, TenFile AS TenFileGoc, SoTrang, TrangThai AS TrangThaiFile
                  FROM FileIn
                  WHERE MaFile = @MaFile AND MaKhachHang = @MaKhachHang",
                new { MaFile = file.MaFileIn, MaKhachHang = maKhachHangBaoGia });

            if (fileTrongCsdl == null)
                return NotFound(new { message = "Có file không tồn tại hoặc không thuộc tài khoản của bạn." });
            if (fileTrongCsdl.TrangThaiFile != "SAN_SANG" || fileTrongCsdl.SoTrang == null || fileTrongCsdl.SoTrang <= 0)
                return BadRequest(new { message = $"File \"{fileTrongCsdl.TenFileGoc}\" chưa sẵn sàng để in, vui lòng tải lại." });

            bangDongIn.Rows.Add(file.MaFileIn, file.MaDichVu, fileTrongCsdl.SoTrang, file.SoBanIn);

            // Danh sách rỗng nghĩa là file này không gia công gì
            foreach (int maGiaCong in file.DsGiaCong)
                bangGiaCong.Rows.Add(file.MaFileIn, maGiaCong);
        }

        //Bước 2: giao tận nơi thì tính quãng đường từ chi nhánh tới nhà khách (để tính phí ship)
        decimal? khoangCachKm = null;
        decimal? phiShipApi = null;
        KetQuaPhiGiaoHang phiGiao = null;
        string diaChiDayDu = null;
        if (req.HinhThucNhan == "GIAO_HANG")
        {
            if (string.IsNullOrWhiteSpace(req.DiaChiGiao))
                return BadRequest(new { message = "Vui lòng nhập địa chỉ giao hàng." });

            ChiNhanh chiNhanh = await conn.QueryFirstOrDefaultAsync<ChiNhanh>(
                "SELECT * FROM ChiNhanh WHERE MaChiNhanh = @MaChiNhanh AND TrangThai = N'HOAT_DONG'",
                new { MaChiNhanh = req.MaChiNhanh });

            if (chiNhanh == null)
                return NotFound(new { message = "Chi nhánh không tồn tại hoặc đang tạm ngưng hoạt động." });
            if (chiNhanh.ViDo == null || chiNhanh.KinhDo == null)
                return BadRequest(new { message = "Chi nhánh chưa khai báo tọa độ nên chưa giao tận nơi được." });

            try
            {
                diaChiDayDu = QuyTacGiaoHang.GhepDiaChi(req.DiaChiGiao, req.DiaChiChiTiet, req.GhiChuGiao);
                phiGiao = await _giaoHang.TinhPhi(chiNhanh, req.DiaChiGiao.Trim(), req.ViDoGiao, req.KinhDoGiao, HttpContext.RequestAborted);
                khoangCachKm = phiGiao.KhoangCachKm;
                phiShipApi = phiGiao.PhiShip;
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        //Bước 3: gọi thủ tục sp_BaoGia tính tiền (cùng công thức với lúc tạo đơn thật)
        string maKhuyenMai = null;
        if (!string.IsNullOrWhiteSpace(req.MaCodeKhuyenMai))
            maKhuyenMai = req.MaCodeKhuyenMai.Trim().ToUpper();

        try
        {
            BaoGiaResponse ketQua = await conn.QueryFirstAsync<BaoGiaResponse>("sp_BaoGia", new
            {
                MaChiNhanh = req.MaChiNhanh,
                HinhThucNhan = req.HinhThucNhan,
                KhoangCach = khoangCachKm,
                PhiShipApi = phiShipApi,
                InGap = req.InGap,
                MaCode = maKhuyenMai,
                DsFile = bangDongIn.AsTableValuedParameter("Kieu_DongDonHang"),
                DsGiaCong = bangGiaCong.AsTableValuedParameter("Kieu_DongGiaCong")
            }, commandType: CommandType.StoredProcedure);

            if (phiGiao != null)
            {
                ketQua.MaBaoGiaGiao = _giaoHang.GiuBaoGia(maKhachHangBaoGia, req.MaChiNhanh,
                    diaChiDayDu, req.ViDoGiao, req.KinhDoGiao, phiGiao);
                ketQua.KhoangCachKm = phiGiao.KhoangCachKm;
                ketQua.PhutDiChuyenDuKien = (int)Math.Ceiling(phiGiao.ThoiGianDuKienGiay / 60.0);
            }
            return Ok(ketQua);
        }
        catch (SqlException ex)
        {
            // Thủ tục báo lỗi
            return BadRequest(new { message = ex.Message });
        }
    }

}
