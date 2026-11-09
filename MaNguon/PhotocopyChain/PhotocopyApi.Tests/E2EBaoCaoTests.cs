using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using PayOS.Crypto;
using PayOS.Models.Webhooks;
using PhotocopyApi.ThanhToan;
using Xunit;
using Xunit.Abstractions;

namespace PhotocopyApi.Tests;

// Chạy qua HTTP với API thật và CSDL kiểm thử riêng, sử dụng dữ liệu tổng hợp.
public sealed class E2EBaoCaoTests(ITestOutputHelper output) : IAsyncLifetime
{
    private const string Database = "PhotocopyChain_E2E_20261109";
    private static string CauHinh => Environment.GetEnvironmentVariable("PHOTOCOPY_E2E_DB");
    private static string KhoaPayOs => Environment.GetEnvironmentVariable("PHOTOCOPY_E2E_PAYOS_KEY");
    private readonly HttpClient _http = new() { BaseAddress = new("http://127.0.0.1:15268/") };
    private SqlConnection _db;
    private readonly string _pass = "Test1" + Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, (int Id, string Email)> _taiKhoan = new();

    public async Task InitializeAsync()
    {
        var cs = new SqlConnectionStringBuilder(CauHinh);
        Assert.Equal(Database, cs.InitialCatalog, StringComparer.OrdinalIgnoreCase);
        _db = new SqlConnection(cs.ConnectionString);
        Assert.Equal(Database, await _db.ExecuteScalarAsync<string>("SELECT DB_NAME()"), StringComparer.OrdinalIgnoreCase);
        string hash = BCrypt.Net.BCrypt.HashPassword(_pass);
        foreach (string key in new[] { "A", "B", "STAFF", "SUPER_ADMIN", "BRANCH_ADMIN", "SHIPPER" })
        {
            string role = key is "A" or "B" ? "CUSTOMER" : key;
            int? branch = role is "STAFF" or "BRANCH_ADMIN" or "SHIPPER" ? 1 : null;
            string email = $"e2e-{Guid.NewGuid():N}@example.test";
            int id = await _db.ExecuteScalarAsync<int>(@"
                INSERT dbo.NguoiDung(HoTen,Email,MatKhau,VaiTro,MaChiNhanh,TrangThai)
                VALUES(N'Kiểm thử E2E',@email,@hash,@role,@branch,N'HOAT_DONG');
                SELECT CAST(SCOPE_IDENTITY() AS INT);", new { email, hash, role, branch });
            _taiKhoan[key] = (id, email);
        }
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        if (_db != null) await _db.DisposeAsync();
    }

    [ChayE2E]
    public async Task TC01_DangNhapSaiKenh()
    {
        foreach ((string key, bool admin) in new[] { ("A", true), ("STAFF", false) })
        {
            using var res = await DangNhap(key, admin);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.False((await res.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("accessToken", out _));
            using var dungKenh = await DangNhap(key, !admin);
            Assert.Equal(HttpStatusCode.OK, dungKenh.StatusCode);
        }
        output.WriteLine("Hai trường hợp sai kênh đều HTTP 403, không cấp token. Đúng kênh HTTP 200.");
    }

    [ChayE2E]
    public async Task TC02_FileSaiChu()
    {
        int file = await TaoFile(_taiKhoan["B"].Id, null);
        string jwt = await Token("A");
        using var cuaChu = await Gui(HttpMethod.Get, $"api/file-in/{file}", await Token("B"));
        Assert.Equal(HttpStatusCode.OK, cuaChu.StatusCode);
        using var doc = await Gui(HttpMethod.Get, $"api/file-in/{file}", jwt);
        Assert.Equal(HttpStatusCode.NotFound, doc.StatusCode);
        using var lap = await Gui(HttpMethod.Post, "api/don-hang", jwt, new
        {
            maChiNhanh = 1, hinhThucNhan = "TAI_QUAY", maYeuCauTao = Guid.NewGuid(),
            fileIn = new[] { new { maFileIn = file, maDichVu = 1, soBanIn = 1, huongGiay = "DUNG" } }
        });
        Assert.Equal(HttpStatusCode.NotFound, lap.StatusCode);
        Assert.Equal(0, await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DonHang WHERE MaKhachHang=@id", new { id = _taiKhoan["A"].Id }));
        output.WriteLine("Chủ file GET 200. Khách khác GET 404, POST đặt in 404. Số đơn mới bằng 0.");
    }

    [ChayE2E]
    public async Task TC05_WebhookSaiTien()
    {
        await Token("A"); // API chỉ nhận tài khoản ngẫu nhiên vừa tạo ở CSDL kiểm thử.
        int don = await TaoDon("CHO_XAC_NHAN");
        using var res = await GuiWebhook(don, 19000);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Đã nhận webhook nhưng số tiền không khớp.", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        await KiemTraDon(don, "CHO_XAC_NHAN", "CHUA_THANH_TOAN");
        KiemTraHangDoiRong();
        output.WriteLine("Webhook ký hợp lệ 19.000đ cho đơn 20.000đ bị bỏ qua. Chưa thanh toán, chưa gán máy, không có lệnh in.");
    }

    [ChayE2E]
    public async Task TC07_TienDenSauHuy()
    {
        string jwt = await Token("A");
        int don = await TaoDon("CHO_XAC_NHAN", kenh: "APP");
        using var huy = await Gui(HttpMethod.Post, $"api/don-hang/{don}/huy", jwt, new { lyDo = "Hủy đơn trong kiểm thử" });
        Assert.Equal(HttpStatusCode.OK, huy.StatusCode);
        await KiemTraDon(don, "DA_HUY", "CHUA_THANH_TOAN");
        using var tien = await GuiWebhook(don, 20000);
        Assert.Equal(HttpStatusCode.OK, tien.StatusCode);
        await KiemTraDon(don, "DA_HUY", "DA_THANH_TOAN");
        KiemTraHangDoiRong();
        output.WriteLine("Hủy HTTP 200. Webhook đủ tiền ghi nhận 20.000đ, đơn vẫn đã hủy và không có lệnh in.");
    }

    [ChayE2E]
    public async Task TC08_HuySauBatDauIn()
    {
        int don = await TaoDon("DANG_IN", daTra: true);
        using var res = await Gui(HttpMethod.Post, $"api/don-hang/{don}/huy", await Token("SUPER_ADMIN"),
            new { lyDo = "Thử hủy đơn đã bắt đầu in" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        await KiemTraDon(don, "DANG_IN", "DA_THANH_TOAN");
        await _db.ExecuteAsync("UPDATE DonHang SET TrangThai='DA_IN' WHERE MaDonHang=@don", new { don });
        output.WriteLine("CEO hủy đơn đang in nhận HTTP 400; trạng thái in và thanh toán giữ nguyên.");
    }

    [ChayE2E]
    public async Task TC09_HuySau120Giay()
    {
        int don = await TaoDon("CHO_XAC_NHAN", daTra: true, kenh: "APP");
        await _db.ExecuteAsync("UPDATE ThanhToan SET NgayThanhToan=DATEADD(SECOND,-120,GETDATE()) WHERE MaDonHang=@don", new { don });
        using var res = await Gui(HttpMethod.Post, $"api/don-hang/{don}/huy", await Token("A"),
            new { lyDo = "Thử hủy sau hai phút", nganHang = "Ngân hàng thử", soTaiKhoan = "123456789" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        await KiemTraDon(don, "CHO_XAC_NHAN", "DA_THANH_TOAN");
        output.WriteLine("Đơn APP đã thanh toán quá 120 giây. API từ chối hủy HTTP 400, giữ nguyên đơn và thanh toán.");
    }

    [ChayE2E]
    public async Task TC11_KhongCoConnectorPhuHop()
    {
        await Token("A");
        int don = await TaoDon("CHO_XAC_NHAN", kenh: "TAI_QUAY");
        var fileMau = Path.Combine(ThuMucThu, $"fixture-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(fileMau, "%PDF-1.4\n% Tệp giả lập cho bước kiểm tra tồn tại");
        int file = await TaoFile(_taiKhoan["A"].Id, fileMau);
        await _db.ExecuteAsync(@"INSERT ChiTietDonHang(MaDonHang,MaFile,MaDichVu,SoTrang,SoBan,DonGia,ThanhTien)
            VALUES(@don,@file,1,1,1,20000,20000)", new { don, file });
        using var res = await GuiWebhook(don, 20000);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("Đang chờ máy in phù hợp kết nối", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        await KiemTraDon(don, "DA_XAC_NHAN", "DA_THANH_TOAN");
        KiemTraHangDoiRong();
        output.WriteLine("API nhận tiền, đưa đơn vào chờ in, không gán máy hoặc sinh lệnh. Connector chủ động không chạy.");
    }

    [ChayE2E]
    public async Task TC13_BanGiaoCodDongThoi()
    {
        string jwt = await Token("STAFF");
        int id = _taiKhoan["STAFF"].Id;
        int ca = await _db.ExecuteScalarAsync<int>("SELECT MaCa FROM CaLamViec WHERE MaNhanVien=@id AND TrangThai='DANG_MO'", new { id });
        Assert.True(ca > 0);
        decimal truoc = await TienCa(ca);
        int don = await TaoDon("DA_IN", phuongThuc: "COD");
        var ketQua = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => Gui(HttpMethod.Post, $"api/don-hang/{don}/giao-tai-quay", jwt)));
        using var r1 = ketQua[0];
        using var r2 = ketQua[1];
        Assert.Single(ketQua, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(ketQua, r => r.StatusCode == HttpStatusCode.BadRequest);
        await KiemTraDon(don, "HOAN_THANH", "DA_THANH_TOAN");
        Assert.Equal(truoc + 20000, await TienCa(ca));
        output.WriteLine("Hai request đồng thời: một HTTP 200, một HTTP 400, tiền ca tăng đúng 20.000đ.");
    }

    [ChayE2E]
    public async Task TC15_NhanDonChuaThanhToan()
    {
        int don = await TaoDon("DA_IN", nhan: "GIAO_HANG");
        using var res = await Gui(HttpMethod.Post, $"api/giao-hang/{don}/nhan", await Token("SHIPPER"));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(0, await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GiaoHang WHERE MaDonHang=@don", new { don }));
        await KiemTraDon(don, "DA_IN", "CHUA_THANH_TOAN");
        output.WriteLine("Shipper nhận đơn chưa trả tiền bị HTTP 409. Không sinh lượt giao.");
    }

    [ChayE2E]
    public async Task TC17_BaoGiaoThatBaiThieuGpsHoacAnh()
    {
        int don = await TaoDon("DANG_GIAO", daTra: true, nhan: "GIAO_HANG");
        int maShipper = _taiKhoan["SHIPPER"].Id;
        int luot = await _db.ExecuteScalarAsync<int>(@"INSERT GiaoHang(MaDonHang,MaShipper,TrangThai)
            VALUES(@don,@maShipper,'DANG_GIAO'); SELECT CAST(SCOPE_IDENTITY() AS INT);", new { don, maShipper });
        string jwt = await Token("SHIPPER");
        foreach (bool coGps in new[] { false, true })
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("Kiểm thử thiếu bằng chứng"), "LyDo");
            if (coGps)
            {
                form.Add(new StringContent("10.8"), "ViDo");
                form.Add(new StringContent("106.6"), "KinhDo");
                form.Add(new StringContent("15"), "DoChinhXac");
                form.Add(new StringContent(DateTimeOffset.UtcNow.ToString("O")), "ThoiDiemGps");
            }
            else
            {
                // Có tệp đính kèm để cô lập trường hợp thiếu GPS; server chặn trước khi đọc ảnh.
                form.Add(new ByteArrayContent([0]), "Anh", "anh-test.png");
            }
            using var res = await Gui(HttpMethod.Post, $"api/giao-hang/luot/{luot}/giao-that-bai", jwt, form);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Equal("DANG_GIAO", await _db.ExecuteScalarAsync<string>("SELECT TrangThai FROM GiaoHang WHERE MaGiaoHang=@luot", new { luot }));
            Assert.Null(await _db.ExecuteScalarAsync<string>("SELECT AnhBangChungJson FROM GiaoHang WHERE MaGiaoHang=@luot", new { luot }));
        }
        await KiemTraDon(don, "DANG_GIAO", "DA_THANH_TOAN");
        output.WriteLine("Thiếu GPS hoặc thiếu ảnh đều HTTP 400. Đơn, lượt giao và bằng chứng giữ nguyên. Multipart mô phỏng, không đo GPS thiết bị.");
    }

    [ChayE2E]
    public async Task TC19_NhanKhoHaiLan()
    {
        string jwt = await Token("BRANCH_ADMIN");
        int vatTu = await _db.ExecuteScalarAsync<int>("SELECT TOP 1 MaVatTu FROM TonKho WHERE MaChiNhanh=1 ORDER BY MaVatTu");
        int ceo = _taiKhoan["SUPER_ADMIN"].Id;
        int phieu = await _db.ExecuteScalarAsync<int>(@"EXEC dbo.sp_TaoPhieuNhap 1,@vatTu,N'Nhà cung cấp thử',7,1000,@ceo;
            SELECT MAX(MaPhieuNhap) FROM PhieuNhap WHERE MaNguoiLap=@ceo;", new { vatTu, ceo });
        const string sql = "SELECT SoLuong FROM TonKho WHERE MaChiNhanh=1 AND MaVatTu=@vatTu";
        int truoc = await _db.ExecuteScalarAsync<int>(sql, new { vatTu });
        string url = $"api/quan-tri/phieu-nhap/{phieu}/nhan-hang";
        using var mot = await Gui(HttpMethod.Post, url, jwt, new { soLuongThucNhan = 7 });
        using var hai = await Gui(HttpMethod.Post, url, jwt, new { soLuongThucNhan = 7 });
        Assert.Equal(HttpStatusCode.OK, mot.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, hai.StatusCode);
        Assert.Equal(truoc + 7, await _db.ExecuteScalarAsync<int>(sql, new { vatTu }));
        Assert.Equal("DA_NHAP", await _db.ExecuteScalarAsync<string>("SELECT TrangThai FROM PhieuNhap WHERE MaPhieuNhap=@phieu", new { phieu }));
        output.WriteLine("Nhận lần đầu HTTP 200, lặp lại HTTP 400. Tồn kho chỉ tăng 7 đơn vị.");
    }

    private static string ThuMucThu => Environment.GetEnvironmentVariable("PHOTOCOPY_E2E_FILES");
    private void KiemTraHangDoiRong()
        => Assert.Empty(Directory.EnumerateFiles(Path.Combine(ThuMucThu, "lenh-in"), "*", SearchOption.AllDirectories));

    private Task<decimal> TienCa(int ca)
        => _db.ExecuteScalarAsync<decimal>("SELECT DoanhThuTrongCa FROM CaLamViec WHERE MaCa=@ca", new { ca });

    private Task<int> TaoFile(int khach, string path)
        => _db.ExecuteScalarAsync<int>(@"INSERT FileIn(MaKhachHang,TenFile,DinhDang,SoTrang,DuongDan)
            VALUES(@khach,N'fixture-e2e.pdf','pdf',1,@path); SELECT CAST(SCOPE_IDENTITY() AS INT);", new { khach, path });

    private Task<int> TaoDon(string state, bool daTra = false, string kenh = "LEGACY", string phuongThuc = "VIETQR", string nhan = "TAI_QUAY")
        => _db.ExecuteScalarAsync<int>(@"SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
            INSERT DonHang(MaKhachHang,MaChiNhanh,HinhThucNhan,TienHang,TongTien,TrangThai,KenhDatDon,NgayHenTra)
            VALUES(@khach,1,@nhan,20000,20000,@state,@kenh,DATEADD(HOUR,1,GETDATE()));
            DECLARE @id INT=CAST(SCOPE_IDENTITY() AS INT);
            INSERT ThanhToan(MaDonHang,PhuongThuc,SoTien,TrangThai,NgayThanhToan)
            VALUES(@id,@phuongThuc,20000,CASE WHEN @daTra=1 THEN 'DA_THANH_TOAN' ELSE 'CHUA_THANH_TOAN' END,
                CASE WHEN @daTra=1 THEN GETDATE() END);
            SELECT @id;", new { khach = _taiKhoan["A"].Id, state, daTra, kenh, phuongThuc, nhan });

    private async Task KiemTraDon(int don, string trangThai, string thanhToan)
    {
        var kq = await _db.QuerySingleAsync<(string Don, string Tien, int? May, decimal SoTien)>(
            @"SELECT d.TrangThai,t.TrangThai,d.MaMayIn,t.SoTien FROM DonHang d
              JOIN ThanhToan t ON d.MaDonHang=t.MaDonHang WHERE d.MaDonHang=@don", new { don });
        Assert.Equal((trangThai, thanhToan, (int?)null, 20000m), kq);
    }

    private Task<HttpResponseMessage> DangNhap(string key, bool admin)
        => Gui(HttpMethod.Post, admin ? "api/auth/admin/login" : "api/auth/login", null,
            new { taiKhoan = _taiKhoan[key].Email, matKhau = _pass });

    private async Task<string> Token(string key)
    {
        using var res = await DangNhap(key, key is not ("A" or "B" or "SHIPPER"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
    }

    private Task<HttpResponseMessage> GuiWebhook(int don, int tien)
    {
        var data = new WebhookData
        {
            OrderCode = don, Amount = tien, Code = "00", Description = QuyTacPayOS.TaoMoTaDon(don),
            Currency = "VND", Reference = "E2E-" + Guid.NewGuid().ToString("N"), AccountNumber = "000000",
            TransactionDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), PaymentLinkId = "fixture-e2e"
        };
        return Gui(HttpMethod.Post, "api/thanh-toan/webhook", null, new Webhook
        {
            Code = "00", Success = true, Description = "Kiểm thử",
            Data = data, Signature = new CryptoProvider().CreateSignatureFromObject(data, KhoaPayOs)
        });
    }

    private async Task<HttpResponseMessage> Gui(HttpMethod method, string route, string jwt, object body = null)
    {
        using var req = new HttpRequestMessage(method, route);
        if (jwt != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        if (body != null) req.Content = body as HttpContent ?? JsonContent.Create(body);
        return await _http.SendAsync(req);
    }

    private sealed class ChayE2EAttribute : FactAttribute
    {
        public ChayE2EAttribute()
        {
            if (new[] { CauHinh, KhoaPayOs, ThuMucThu }.Any(string.IsNullOrWhiteSpace))
                Skip = "Cần API localhost:15268, CSDL E2E riêng và khóa webhook thử.";
        }
    }
}
