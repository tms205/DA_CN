using System.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using PhotocopyApi.Models;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyApi.Controllers;

//báo giá tạm tính + giờ hẹn trả
[ApiController]
[Route("api/bao-gia")]
[Authorize(Roles = "CUSTOMER,BRANCH_ADMIN,STAFF")]
public class BaoGiaController : ControllerBase
{
    private readonly string _chuoiKetNoi;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _boNhoTam;

    // Dùng chung 1 HttpClient để gọi Ahamove (tạo mới liên tục dễ hết cổng mạng).
    private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    public BaoGiaController(IConfiguration config, IMemoryCache boNhoTam)
    {
        _config = config;
        _boNhoTam = boNhoTam;
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
    }

    // POST api/bao-gia
    [HttpPost]
    public async Task<IActionResult> TinhGia(BaoGiaRequest req, [FromQuery] int? maKhachHang)
    {
        string vaiTro = User.FindFirst(TenClaim.VaiTro).Value;
        int maNguoiDung = int.Parse(User.FindFirst(TenClaim.MaNguoiDung).Value);
        int maKhachHangBaoGia = maNguoiDung;

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
                NguoiDung khach = await conn.QueryFirstAsync<NguoiDung>(
                    "SELECT HoTen, SoDienThoai FROM NguoiDung WHERE MaNguoiDung = @MaNguoiDung",
                    new { MaNguoiDung = maKhachHangBaoGia });

                KetQuaPhiGiaoHang baoGiaGiaoHang = await TinhPhiGiaoHangAhamove(
                    chiNhanh, req.DiaChiGiao.Trim(), req.ViDoGiao, req.KinhDoGiao,
                    khach.HoTen, khach.SoDienThoai);
                khoangCachKm = baoGiaGiaoHang.KhoangCachKm;
                phiShipApi = baoGiaGiaoHang.PhiShip;
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

            return Ok(ketQua);
        }
        catch (SqlException ex)
        {
            // Thủ tục báo lỗi
            return BadRequest(new { message = ex.Message });
        }
    }

    // TUẦN 3 — gọi API giao hàng Ahamove để lấy CẢ quãng đường lẫn phí giao dự kiến.
    // Dùng group_services = BIKE để Ahamove tự chọn mã xe máy đúng khu vực (SGN-BIKE, HAN-BIKE...).
    // Kết quả chỉ nhớ 10 phút vì giá giao hàng có thể thay đổi theo thời điểm.
    private async Task<KetQuaPhiGiaoHang> TinhPhiGiaoHangAhamove(ChiNhanh chiNhanh, string diaChiKhach,
        decimal? viDoKhach, decimal? kinhDoKhach, string tenKhach, string sdtKhach)
    {
        if (string.IsNullOrWhiteSpace(chiNhanh.SoDienThoai) || string.IsNullOrWhiteSpace(sdtKhach))
            throw new Exception("Chi nhánh hoặc người nhận chưa có số điện thoại để Ahamove báo giá.");

        string khoaNho;
        if (viDoKhach != null && kinhDoKhach != null)
            khoaNho = $"ahamove|{chiNhanh.MaChiNhanh}|{Math.Round(viDoKhach.Value, 5)},{Math.Round(kinhDoKhach.Value, 5)}";
        else
            khoaNho = "ahamove|" + chiNhanh.MaChiNhanh + "|" + DonHangController.ChuanHoaDiaChi(diaChiKhach);

        if (_boNhoTam.TryGetValue(khoaNho, out KetQuaPhiGiaoHang ketQuaDaNho))
            return ketQuaDaNho;

        string token = _config["GiaoHang:AhamoveToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            // Chỉ dùng khi demo nội bộ; phải cấu hình cả km và phí để không vô tình dùng giá cố định thật.
            decimal? kmGiaLap = _config.GetValue<decimal?>("GiaoHang:QuangDuongGiaLapKm");
            decimal? phiGiaLap = _config.GetValue<decimal?>("GiaoHang:PhiShipGiaLap");
            if (kmGiaLap != null && phiGiaLap != null)
                return new KetQuaPhiGiaoHang(kmGiaLap.Value, phiGiaLap.Value, 0);
            throw new Exception("Máy chủ chưa cấu hình token Ahamove nên chưa lấy được phí giao hàng. Vui lòng chọn nhận tại quầy.");
        }

        string nhomDichVu = _config["GiaoHang:NhomDichVu"] ?? "BIKE";
        string baseUrl = (_config["GiaoHang:AhamoveBaseUrl"] ?? "https://partner-apistg.ahamove.com/v3").TrimEnd('/');

        // Ahamove bắt buộc có địa chỉ, tên và SĐT ở cả điểm lấy lẫn điểm giao.
        // lat/lng là dữ liệu khuyến nghị: có tọa độ khách thì gửi, không có thì Ahamove tự định vị từ địa chỉ.
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
}
