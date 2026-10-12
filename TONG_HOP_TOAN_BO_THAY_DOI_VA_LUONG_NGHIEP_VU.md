# Báo Cáo Tổng Hợp Toàn Bộ Thay Đổi & Luồng Nghiệp Vụ Hệ Thống

> **Hệ thống:** Chuỗi Cửa Hàng Photocopy (PhotocopyChain)  
> **Phiên bản:** CNTT-KLCN031  
> **Ngày tổng hợp:** 09/10/2026  
> **Tài liệu tham chiếu:** `TONG_HOP_THAY_DOI.md`, `DacTaChucNang_CNTT-KLCN031.md`, `BAO_CAO_THANH_TOAN_TU_DONG_VA_MAY_IN_AO.md`, `HUONG_DAN_TICH_HOP_PAYOS.md`

---

## MỤC LỤC TỔNG QUAN

1. [CEO và Phân Quyền Chi Nhánh (Chọn một hoặc tất cả, thao tác từng bản ghi)](#1-ceo-và-phân-quyền-chi-nhánh)
2. [Bảng Giá và Khuyến Mãi Riêng Theo Chi Nhánh](#2-bảng-giá-và-khuyến-mãi-riêng-theo-chi-nhánh)
3. [Nghiệp Vụ Nhập Kho (CEO tự duyệt phiếu, Quản lý cần duyệt, Tồn kho chỉ tăng khi nhận hàng)](#3-nghiệp-vụ-nhập-kho)
4. [Quản Lý Ca Làm Việc (Tự mở ca riêng 200.000đ khi đăng nhập, không trùng ca)](#4-quản-lý-ca-làm-việc)
5. [Thanh Toán Tự Động (Tích hợp payOS, kiểm tra tiền thật, đối soát và tự động xếp in)](#5-thanh-toán-tự-động-payos)
6. [Hệ Thống Máy In Ảo (Mô phỏng tốc độ máy thật, quản lý hàng đợi và khả năng kết nối máy vật lý)](#6-hệ-thống-máy-in-ảo-printer-simulator)
7. [Chuẩn Hóa Giao Diện (Figma / Gentelella, mở rộng sidebar, tăng chữ, nhóm menu độc lập, chống tràn ngang)](#7-chuẩn-hóa-giao-diện-admin)
8. [Tập Trung Cấu Hình .env và Tối Ưu Script Chạy Hệ Thống](#8-tập-trung-cấu-hình-env-và-script-vận-hành)
9. [Tài Khoản và Khôi Phục Mật Khẩu Qua Gmail SMTP Thật](#9-tài-khoản-và-khôi-phục-mật-khẩu-qua-gmail)
10. [Màn Hình Đăng Nhập & Luồng Khôi Phục Mật Khẩu (Mở rộng 20%, OTP 6 gạch chân)](#10-đăng-nhập--luồng-khôi-phục-mật-khẩu)
11. [Hệ Thống Tài Liệu Đặc Tả & Báo Cáo Thay Đổi](#11-hệ-thống-tài-liệu-và-đặc-tả-chức-năng)

---

## 1. CEO VÀ PHÂN QUYỀN CHI NHÁNH

### 1.1. Luồng hoạt động
- **Mục tiêu:** CEO (`SUPER_ADMIN`) có quyền bao quát toàn chuỗi:
  1. Khi chọn `Tổng quan tất cả`: Xem và so sánh dữ liệu toàn chuỗi (hàng đợi đơn, bảng giá, doanh thu, kho). Ẩn các nút hành động mang tính cục bộ (như lập đơn tại quầy, tiếp nhận, mở ca).
  2. Khi chọn một chi nhánh cụ thể (Ví dụ: Cơ sở 1): Giao diện mở thêm các tính năng nghiệp vụ quầy; CEO có quyền thao tác trực tiếp trên từng bản ghi như một Quản lý chi nhánh (`BRANCH_ADMIN`).
  3. Phạm vi chi nhánh được ghi nhớ trong `localStorage` qua trình duyệt.

```text
[Giao diện MainLayout] 
   └─> Bộ chọn chi nhánh 
         ├─> "Tổng quan tất cả" (X-Ma-Chi-Nhanh = null) ──> Xem dữ liệu toàn chuỗi
         └─> "Cơ sở 1 / Cơ sở 2" (X-Ma-Chi-Nhanh = 1, 2) ──> Header X-Ma-Chi-Nhanh
                                                                    │
                                                                    ▼
                                                    [PhamViChiNhanhMiddleware]
                                              Gán Claim MaChiNhanh vào User Identity
                                                                    │
                                                                    ▼
                                              [Controller / Stored Procedure]
                                              Thao tác giới hạn trong đúng chi nhánh
```

### 1.2. Đoạn code logic nghiệp vụ

#### A. Middleware xử lý phạm vi chi nhánh của CEO trên API
*File:* `MaNguon/PhotocopyChain/PhotocopyApi/PhamViChiNhanhMiddleware.cs` (dòng 1–45)
```csharp
public class PhamViChiNhanhMiddleware
{
    private readonly RequestDelegate _next;

    public PhamViChiNhanhMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Chỉ xử lý nếu request đã xác thực và có role SUPER_ADMIN
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.IsInRole(VaiTro.SuperAdmin) &&
            context.Request.Headers.TryGetValue(TenHeader.MaChiNhanh, out var headerVal))
        {
            string strVal = headerVal.ToString().Trim();
            if (!string.IsNullOrEmpty(strVal))
            {
                if (int.TryParse(strVal, out int maChiNhanh) && maChiNhanh > 0)
                {
                    // Ghi đè hoặc thêm claim MaChiNhanh cho phiên làm việc hiện tại
                    var identity = (ClaimsIdentity)context.User.Identity;
                    var claimCu = identity.FindFirst(TenClaim.MaChiNhanh);
                    if (claimCu != null) identity.RemoveClaim(claimCu);
                    identity.AddClaim(new Claim(TenClaim.MaChiNhanh, maChiNhanh.ToString()));
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new { message = "Mã chi nhánh trong header không hợp lệ." });
                    return;
                }
            }
        }
        await _next(context);
    }
}
```

#### B. Lưu trữ và truyền header phạm vi trên Blazor Web Admin
*File:* `MaNguon/PhotocopyChain/PhotocopyAdmin/PhamViChiNhanh.cs` (dòng 15–35)
```csharp
public async Task ChonChiNhanhAsync(int? maChiNhanh)
{
    MaChiNhanh = maChiNhanh;
    if (maChiNhanh.HasValue)
        await _js.InvokeVoidAsync("localStorage.setItem", "admin_ma_chi_nhanh", maChiNhanh.Value.ToString());
    else
        await _js.InvokeVoidAsync("localStorage.removeItem", "admin_ma_chi_nhanh");

    CapNhatHeader();
    NotifyStateChanged();
}

public void CapNhatHeader()
{
    _http.DefaultRequestHeaders.Remove("X-Ma-Chi-Nhanh");
    if (MaChiNhanh.HasValue)
        _http.DefaultRequestHeaders.Add("X-Ma-Chi-Nhanh", MaChiNhanh.Value.ToString());
}
```

---

## 2. BẢNG GIÁ VÀ KHUYẾN MÃI RIÊNG THEO CHI NHÁNH

### 2.1. Luồng hoạt động
- **Trước đây:** Bảng giá gắn trực tiếp vào bảng dịch vụ (`DichVu.DonGia`), khuyến mãi có mã `MaCode` duy nhất toàn cục.
- **Sau cải tiến:** 
  1. Tách bảng giá thành `BangGiaChiNhanh (MaChiNhanh, MaDichVu, DonGia, TrangThai)`: Cho phép từng cơ sở áp dụng đơn giá khác nhau theo vị trí địa lý, chi phí mặt bằng.
  2. Bảng `KhuyenMai` có thêm cột `MaChiNhanh`, ràng buộc duy nhất chuyển thành `(MaChiNhanh, MaCode)`: Cùng mã giảm giá `CHAOTANXUAN` có thể giảm 20% tại Cơ sở 1 nhưng chỉ giảm 10% tại Cơ sở 2.
  3. Khi tạo đơn hoặc báo giá, Stored Procedure `sp_TinhTien` và `sp_TaoDonHang` đọc đúng đơn giá chi nhánh đang bán.

### 2.2. Đoạn code logic nghiệp vụ

#### A. Cấu trúc bảng và ràng buộc SQL
*File:* `Database/003-CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql` (dòng 6–25)
```sql
-- Tạo bảng giá riêng từng chi nhánh
CREATE TABLE BangGiaChiNhanh (
    MaChiNhanh INT NOT NULL REFERENCES ChiNhanh(MaChiNhanh),
    MaDichVu   INT NOT NULL REFERENCES DichVu(MaDichVu),
    DonGia     DECIMAL(12,0) NOT NULL DEFAULT 0,
    TrangThai  NVARCHAR(20) NOT NULL DEFAULT N'DANG_BAN',
    CONSTRAINT PK_BangGiaChiNhanh PRIMARY KEY (MaChiNhanh, MaDichVu)
);

-- Cập nhật ràng buộc duy nhất cho khuyến mãi theo chi nhánh
ALTER TABLE KhuyenMai ADD MaChiNhanh INT NOT NULL REFERENCES ChiNhanh(MaChiNhanh);
ALTER TABLE KhuyenMai ADD CONSTRAINT UQ_KhuyenMai_ChiNhanh_MaCode UNIQUE (MaChiNhanh, MaCode);
```

#### B. Thủ tục tính tiền theo bảng giá chi nhánh (`sp_TinhTien`)
*File:* `Database/003-CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql` (dòng 115–130)
```sql
    -- Tính tiền in theo bảng giá riêng của chi nhánh
    SELECT @TienIn = ISNULL(SUM(d.SoTrang * d.SoBan * bg.DonGia), 0)
    FROM @DsFile d
    JOIN BangGiaChiNhanh bg ON bg.MaChiNhanh = @MaChiNhanh AND bg.MaDichVu = d.MaDichVu
    WHERE bg.TrangThai = N'DANG_BAN';

    -- Tính tiền gia công theo bảng giá riêng của chi nhánh
    SELECT @TienGiaCong = ISNULL(SUM(ct.SoBan * bg.DonGia), 0)
    FROM @DsGiaCong g
    JOIN @DsFile ct ON ct.MaFile = g.MaFile
    JOIN BangGiaChiNhanh bg ON bg.MaChiNhanh = @MaChiNhanh AND bg.MaDichVu = g.MaDichVu
    WHERE bg.TrangThai = N'DANG_BAN';
```

---

## 3. NGHIỆP VỤ NHẬP KHO

### 3.1. Luồng hoạt động
Quy trình nhập vật tư (giấy in, mực in, bìa mica...) tuân thủ chặt chẽ nguyên tắc phân quyền và an toàn kho:
1. **Lập phiếu nhập (`sp_TaoPhieuNhap`):**
   - Nếu **CEO** (`SUPER_ADMIN`) lập phiếu: Hệ thống nhận diện và **tự động duyệt luôn** (`TrangThai = 'DA_DUYET'`).
   - Nếu **Quản lý chi nhánh** (`BRANCH_ADMIN`) lập phiếu: Phiếu ở trạng thái `CHO_DUYET`. Chỉ CEO mới có quyền duyệt (`sp_DuyetPhieuNhap`). Quản lý không được tự duyệt phiếu của chính mình (chặn bằng Trigger).
2. **Nhập kho thực tế (`sp_NhapKho`):**
   - **Duyệt phiếu KHÔNG làm tăng tồn kho.** Tồn kho chỉ tăng khi chi nhánh nhận được hàng thực tế và gọi `sp_NhapKho` với số lượng thực nhận `@SoLuongThucNhan` (có thể ít hơn đề xuất nếu nhà cung cấp giao thiếu).
   - Bản ghi chuyển sang `DA_NHAP`, số lượng cộng vào `TonKho`, và đơn giá vật tư được cập nhật.

### 3.2. Đoạn code logic nghiệp vụ

#### A. Stored Procedure lập phiếu nhập kho (`sp_TaoPhieuNhap`)
*File:* `Database/007-CaRiengNhanVien_CEODuyetPhieuNhap.sql` (dòng 77–97)
```sql
CREATE OR ALTER PROCEDURE sp_TaoPhieuNhap
    @MaChiNhanh INT, @MaVatTu INT, @NhaCungCap NVARCHAR(150), 
    @SoLuong INT, @DonGia DECIMAL(12,0), @MaNguoiLap INT
AS
BEGIN
    SET NOCOUNT ON;
    IF dbo.fn_LaQuanLy(@MaNguoiLap, @MaChiNhanh) = 0
        THROW 50060, N'Chỉ quản lý chi nhánh hoặc Super Admin mới được lập phiếu nhập.', 1;
    IF @SoLuong IS NULL OR @SoLuong <= 0 OR @DonGia IS NULL OR @DonGia < 0
        THROW 50064, N'Số lượng và đơn giá nhập không hợp lệ.', 1;

    DECLARE @LaCEO BIT = dbo.fn_LaSuperAdmin(@MaNguoiLap);
    
    -- CEO lập phiếu thì tự duyệt trực tiếp; Quản lý lập thì chờ duyệt
    INSERT INTO PhieuNhap (MaChiNhanh, MaVatTu, NhaCungCap, SoLuong, DonGia, MaNguoiLap, MaNguoiDuyet, TrangThai)
    VALUES (@MaChiNhanh, @MaVatTu, @NhaCungCap, @SoLuong, @DonGia, @MaNguoiLap,
            CASE WHEN @LaCEO = 1 THEN @MaNguoiLap ELSE NULL END,
            CASE WHEN @LaCEO = 1 THEN 'DA_DUYET' ELSE 'CHO_DUYET' END);
    -- Lưu ý: Duyệt không tăng tồn kho, phải xác nhận nhận hàng bằng sp_NhapKho.
END;
```

#### B. Thủ tục xác nhận nhận hàng tăng tồn kho (`sp_NhapKho`)
*File:* `Database/04_StoredProcedure.sql` (dòng 785–808)
```sql
CREATE PROCEDURE sp_NhapKho
    @MaPhieuNhap INT, @SoLuongThucNhan INT
AS
BEGIN
    SET XACT_ABORT ON;
    DECLARE @MaChiNhanh INT, @MaVatTu INT, @DonGia DECIMAL(12,0);
    SELECT @MaChiNhanh = MaChiNhanh, @MaVatTu = MaVatTu, @DonGia = DonGia
    FROM PhieuNhap WHERE MaPhieuNhap = @MaPhieuNhap AND TrangThai = 'DA_DUYET';

    IF @MaChiNhanh IS NULL
        THROW 50063, N'Phiếu nhập chưa được duyệt.', 1;

    BEGIN TRAN;
        -- Cập nhật số lượng thực tế nhận và đổi trạng thái thành DA_NHAP
        UPDATE PhieuNhap 
        SET SoLuong = @SoLuongThucNhan, TrangThai = 'DA_NHAP' 
        WHERE MaPhieuNhap = @MaPhieuNhap;

        -- Tồn kho chỉ được cộng tại thời điểm này
        IF EXISTS (SELECT 1 FROM TonKho WHERE MaChiNhanh = @MaChiNhanh AND MaVatTu = @MaVatTu)
            UPDATE TonKho SET SoLuong = SoLuong + @SoLuongThucNhan 
            WHERE MaChiNhanh = @MaChiNhanh AND MaVatTu = @MaVatTu;
        ELSE
            INSERT INTO TonKho (MaChiNhanh, MaVatTu, SoLuong) 
            VALUES (@MaChiNhanh, @MaVatTu, @SoLuongThucNhan);

        -- Cập nhật đơn giá vốn mới nhất cho vật tư
        UPDATE VatTu SET DonGia = @DonGia WHERE MaVatTu = @MaVatTu;
    COMMIT;
END;
```

---

## 4. QUẢN LÝ CA LÀM VIỆC

### 4.1. Luồng hoạt động
- **Trước đây:** Một chi nhánh chỉ có một ca làm việc chung, nhân viên dùng chung két tiền gây khó khăn khi đối soát thiếu hụt tiền mặt.
- **Sau cải tiến:**
  1. **Ca riêng từng nhân viên:** Mỗi nhân viên tại chi nhánh có một ca làm việc riêng độc lập (`UX_CaLamViec_MotCaDangMoMoiNguoiDung`).
  2. **Tự động mở ca khi đăng nhập:** Khi nhân viên (`STAFF`) đăng nhập Web Admin thành công, hệ thống tự động kích hoạt `sp_MoCa` với tiền đầu ca mặc định là **200.000đ**.
  3. **Chống trùng ca:** Nếu nhân viên đăng nhập lại (do F5, rớt mạng hoặc mở tab mới), hệ thống kiểm tra ca đang mở và trả về nguyên trạng ca cũ mà **không tạo ca mới** và **không thay đổi tiền đầu ca**.

### 4.2. Đoạn code logic nghiệp vụ

#### A. Stored Procedure mở ca độc lập và tái sử dụng ca (`sp_MoCa`)
*File:* `Database/007-CaRiengNhanVien_CEODuyetPhieuNhap.sql` (dòng 13–59)
```sql
CREATE OR ALTER PROCEDURE sp_MoCa
    @MaNhanVien INT, @TienDauCa DECIMAL(12,0) = 200000, @MaCa INT OUTPUT,
    @MaChiNhanhThayThe INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @MaCa = NULL;
    ...
    BEGIN TRAN;
        -- Khóa độc quyền theo nhân viên đang mở ca
        SELECT @MaCa = MaCa
        FROM CaLamViec WITH (UPDLOCK, HOLDLOCK)
        WHERE MaNhanVien = @MaNhanVien AND TrangThai = 'DANG_MO';

        IF @MaCa IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM CaLamViec WHERE MaCa = @MaCa AND MaChiNhanh = @MaChiNhanh)
            BEGIN
                ROLLBACK TRAN;
                THROW 50059, N'Bạn đang có ca mở tại một chi nhánh khác.', 1;
            END
            COMMIT TRAN;
            RETURN; -- Đã có ca đang mở -> Sử dụng lại ca cũ, không tạo trùng!
        END

        -- Tạo ca mới với tiền đầu ca mặc định 200.000đ
        INSERT INTO CaLamViec (MaChiNhanh, MaNhanVien, TienDauCa)
        VALUES (@MaChiNhanh, @MaNhanVien, @TienDauCa);
        SET @MaCa = SCOPE_IDENTITY();
    COMMIT TRAN;
END;
```

#### B. Logic tự động gọi mở ca trong AuthController
*File:* `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/AuthController.cs` (dòng 104–123)
```csharp
    // Nhân viên đăng nhập Web được tự mở ca với tiền đầu ca mặc định 200.000đ
    if (QuyTacCaDangNhap.CanTuDongMoCa(laWeb, nd.VaiTro))
    {
        var thamSo = new DynamicParameters();
        thamSo.Add("@MaNhanVien", nd.MaNguoiDung);
        thamSo.Add("@TienDauCa", QuyTacCaDangNhap.TienDauCaMacDinh); // 200.000đ
        thamSo.Add("@MaChiNhanhThayThe", null);
        thamSo.Add("@MaCa", dbType: DbType.Int32, direction: ParameterDirection.Output);

        try
        {
            await conn.ExecuteAsync("sp_MoCa", thamSo, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Không thể tự mở ca khi nhân viên {MaNhanVien} đăng nhập.", nd.MaNguoiDung);
            return Conflict(new { message = "Không thể tự mở ca làm việc: " + ex.Message });
        }
    }
```

---

## 5. THANH TOÁN TỰ ĐỘNG (PAYOS)

*(Đã được phân tích chi tiết trong báo cáo chuyên đề `BAO_CAO_LUONG_THANH_TOAN.md`)*

### 5.1. Tóm tắt các điểm cải tiến cốt lõi
1. **Tích hợp cổng payOS thật:** Thay thế toàn bộ mã QR giả lập bằng link thanh toán VietQR thật của payOS (kết nối ngân hàng Quân Đội MBBank).
2. **Bảo mật đa tầng:**
   - Xác thực chữ ký webhook bằng thuật toán HMAC SHA256 với `ChecksumKey`.
   - Đối chiếu số tiền thực nhận qua ngân hàng phải khớp 100% với giá trị đơn.
   - Tuyệt đối **không** tin tham số URL `status=PAID` khi trình duyệt redirect về; chỉ xác nhận khi có Webhook ký số hoặc đối soát trực tiếp API.
3. **Chuyển hướng Return URL độc lập:** Link thanh toán từ Web Admin cấu hình quay thẳng về `http://localhost:5064/`, không bị phụ thuộc vào Cloudflare Quick Tunnel khi tunnel bị ngắt.
4. **Đối soát tự động phục hồi:** Nếu Webhook bị trễ hoặc rớt mạng, hàm `ThuDongBoTuPayOS` tự động kéo trạng thái từ máy chủ payOS về. Đơn bị hủy do quá 15 phút nếu có tiền về muộn sẽ được hệ thống **tự động phục hồi** sang `CHO_XAC_NHAN` và tiếp tục quy trình in.
5. **Kích hoạt tự động hóa in:** Sau khi ghi nhận `DA_THANH_TOAN`, hệ thống lập tức gọi `TuDongInService.XepInSauThanhToan` để đưa đơn sang `DANG_IN`.

---

## 6. HỆ THỐNG MÁY IN ẢO (PRINTER SIMULATOR)

### 6.1. Kiến trúc tách rời chuẩn công nghiệp
Hệ thống thiết kế một project Console riêng biệt `PhotocopyPrinterSimulator` kết nối với `PhotocopyApi` qua giao thức HTTP RESTful:
- API cung cấp các endpoint:
  + `POST /api/may-in-ao/{maMayIn}/heartbeat`: Báo trạng thái máy (`SAN_SANG`, `DANG_IN`, `BAO_TRI`).
  + `GET /api/may-in-ao/{maMayIn}/lenh-in`: Lấy lệnh in tiếp theo trong hàng đợi.
  + `GET /api/may-in-ao/don/{maDonHang}/file/{maFile}`: Tải file PDF vật lý cần in (xác thực bằng `X-Printer-Api-Key` và Token lệnh in).
  + `POST /api/may-in-ao/don/{maDonHang}/hoan-tat`: Báo in xong, chuyển đơn sang `DA_IN`.
- **Khả năng kết nối máy in thật:** Khi đưa máy in vật lý vào vận hành, API không cần thay đổi. Chỉ cần thay thế lệnh `Task.Delay` trong module connector bằng lệnh gửi vào hàng đợi Windows Print Spooler hoặc giao thức IPP (Internet Printing Protocol).

### 6.2. Thuật toán tính toán thời gian in theo thông số máy
*File:* `MaNguon/PhotocopyChain/PhotocopyShared/MayInAo/QuyTacMayInAo.cs` (dòng 36–51)
```csharp
    public static double TinhSoGiay(CauHinhTocDoMayIn may, IEnumerable<DongLenhInDto> dsDong)
    {
        // Thời gian khởi động và kéo trang đầu tiên
        double tongSoGiay = may.SoGiayRaTrangDau;

        foreach (DongLenhInDto dong in dsDong)
        {
            // Tốc độ thay đổi linh hoạt theo khổ giấy (A3/A4) và in 2 mặt (Duplex)
            double tocDo = ChonTocDo(may, dong);
            if (tocDo <= 0)
                throw new InvalidOperationException("Tốc độ máy in phải lớn hơn 0.");

            int soMatIn = Math.Max(0, dong.SoTrang) * Math.Max(0, dong.SoBan);
            // Công thức: Tổng số mặt in * 60 giây / Tốc độ trang mỗi phút (PPM)
            tongSoGiay += soMatIn * 60d / tocDo;
        }

        return Math.Max(1, tongSoGiay);
    }
```

*File:* `MaNguon/PhotocopyChain/PhotocopyPrinterSimulator/MayInSimulator.cs` (dòng 71–95)
```csharp
    // Vòng lặp mô phỏng và lưu trữ tiến trình phòng mất điện/khởi động lại
    double soGiay = QuyTacMayInAo.TinhSoGiay(may, lenhDaLuu.DsDongIn);
    dangIn.DuKienXong = DateTime.Now.AddSeconds(soGiay);
    dangIn.DaBatDau = true;
    await LuuTienTrinh(may.MaMayIn, dangIn, dung);

    TimeSpan conLai = dangIn.DuKienXong - DateTime.Now;
    if (conLai > TimeSpan.Zero)
        await Task.Delay(TimeSpan.FromSeconds(Math.Min(conLai.TotalSeconds, 5)), dung);
    else
        await HoanTat(may, dangIn, dung);
```

---

## 7. CHUẨN HÓA GIAO DIỆN (ADMIN)

### 7.1. Các cải tiến giao diện theo chuẩn Figma / Gentelella
1. **Sidebar mở rộng và tăng cỡ chữ:**
   - Chiều rộng sidebar được mở rộng lên **361px** ở chế độ mở đầy đủ, cỡ chữ các mục menu tăng thêm **1px** (lên 17.5px) giúp nhân viên tại quầy dễ đọc trên màn hình cảm ứng hoặc khoảng cách xa.
   - Hỗ trợ chế độ thu gọn **Gentelella Rail Mode** (thu gọn sidebar chỉ còn hàng biểu tượng icon khi cần tối đa hóa không gian làm việc).
2. **Nhóm menu mở độc lập:**
   - Thay vì cơ chế đóng/mở đơn lẻ (accordion bắt buộc đóng nhóm cũ), các nhóm menu (Đơn hàng, Báo cáo, Quản trị) có thể mở độc lập cùng lúc.
3. **Bảo vệ bảng chống tràn ngang (No Horizontal Overflow):**
   - Mọi bảng dữ liệu lớn đều được bọc trong container `.the-bang` $\rightarrow$ `.bang-cuon` với `overflow-x: auto` và thanh cuộn mảnh.
   - Header bảng có hiệu ứng `position: sticky; top: 0` khi cuộn trang dài.
   - Kiểm thử hiển thị tự động trên Playwright (`verification/content-responsive.cjs`) chạy qua tất cả các màn hình từ 360px đến 1920px để đảm bảo không bị vỡ layout.

*File:* `MaNguon/PhotocopyChain/PhotocopyAdmin/wwwroot/css/app-ui-v2.css`
```css
/* Sidebar mở rộng 361px */
.app-sidebar {
    width: 361px;
    font-size: 17.5px;
    transition: width var(--toc-do-chuyen) ease;
}

/* Container cuộn bảng chống tràn ngang */
.bang-cuon {
    width: 100%;
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
}

.bang {
    width: 100%;
    border-collapse: collapse;
    white-space: nowrap;
}
```

---

## 8. TẬP TRUNG CẤU HÌNH .ENV VÀ SCRIPT VẬN HÀNH

### 8.1. File cấu hình tập trung `.env`
Toàn bộ chuỗi kết nối CSDL, tài khoản SMTP Gmail, khóa bí mật JWT, API Key của payOS và Connector máy in được gom tập trung vào một file duy nhất:  
`MaNguon/PhotocopyChain/.env`

```ini
ConnectionStrings__PhotocopyChain='Server=localhost\MSSQLSERVER01;Database=PhotocopyChain;Trusted_Connection=True;TrustServerCertificate=True;'
Email__Host='smtp.gmail.com'
Email__MatKhau='xnnt arju skki emyd'
Email__Port='587'
Email__TenDangNhap='manh27062005@gmail.com'
Email__TenNguoiGui='PhotocopyChain'
Jwt__Key='DEV-ONLY-khoa-ky-JWT-PhotocopyChain-KLCN031-khong-dung-o-may-that'
PayOS__ApiKey='f87e4bb5-73e3-4bb2-84ef-60ac7b53de27'
PayOS__ChecksumKey='46255288138d059036dc79d964da1947ef3dd42592d4a135f97dac8cd7166df9'
PayOS__ClientId='c9890061-87b7-4a8b-b8a0-74c303a821fc'
PayOS__PublicBaseUrl='https://friend-pharmacies-nation-chart.trycloudflare.com'
PrinterConnector__ApiKey='59597407c67b46809426571958fa0712795e03005c50404ab8824dd39d04a7e6'
```

### 8.2. Script chạy hệ thống thông minh `chay_web.cmd`
*File:* `MaNguon/scripts/chay_web.cmd`
Script tự động hóa các tác vụ khởi động phức tạp:
1. Chỉ định biến môi trường `PHOTOCOPY_ENV_FILE` tới đúng file `.env` nội bộ của dự án.
2. Dọn dẹp và ngắt các tiến trình chạy ngầm mồ côi chiếm cổng (`5268`, `5064`).
3. Tự động kiểm tra kết nối SQL Server trước khi bật code.
4. Biên dịch sạch sẽ cả 3 dự án: API, Web Admin, Simulator.
5. Khởi động API trước, kiểm tra endpoint Swagger trả về HTTP 200 mới tiếp tục mở Web Admin và Máy in ảo để tránh lỗi mất kết nối ban đầu.

---

## 9. TÀI KHOẢN VÀ KHÔI PHỤC MẬT KHẨU QUA GMAIL

### 9.1. Cập nhật email thử nghiệm
Migration `Database/008-CapNhatEmailTaiKhoanKiemThu.sql` chuẩn hóa email thật cho 3 tài khoản nghiệp vụ đại diện:
- `MaNguoiDung = 1` (CEO / Super Admin): `manh27062005@gmail.com`
- `MaNguoiDung = 2` (Quản lý chi nhánh 1): `gg.tms205@gmail.com`
- `MaNguoiDung = 4` (Thợ in chi nhánh 1): `gg.tms2765@gmail.com`

### 9.2. Gửi mã OTP xác thực qua SMTP Gmail
Khi người dùng quên mật khẩu, hệ thống sinh mã OTP ngẫu nhiên gồm 6 chữ số, lưu vào bộ nhớ tạm thời có hiệu lực trong 10 phút, và gửi email qua SMTP Google (`smtp.gmail.com:587`):

*File:* `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/AuthController.cs` (dòng 404–436)
```csharp
    // Gửi OTP khôi phục mật khẩu qua email
    string maOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    LuuOtpKhoiPhuc(email, maOtp, thoiHan: TimeSpan.FromMinutes(10));

    await _emailSender.SendEmailAsync(
        email,
        "Mã xác thực khôi phục mật khẩu - PhotocopyChain",
        $"Mã xác thực OTP của bạn là: {maOtp}. Mã có hiệu lực trong vòng 10 phút. Tuyệt đối không chia sẻ mã này với bất kỳ ai."
    );
```

---

## 10. ĐĂNG NHẬP & LUỒNG KHÔI PHỤC MẬT KHẨU

### 10.1. Mở rộng khung đăng nhập 20%
Khung biểu mẫu `.dn-form-khung` được thiết kế lại:
- Chiều rộng `max-width: 480px` (mở rộng 20% so với kích thước cũ 404px).
- Khoảng cách `padding: 36px`, `gap: 24px`.
- Kích thước font chữ tăng thêm 1px giúp các ô nhập liệu rõ ràng, chuyên nghiệp.

### 10.2. Luồng khôi phục mật khẩu 3 bước hoàn chỉnh (`QuenMatKhau.razor`)
*File:* `MaNguon/PhotocopyChain/PhotocopyAdmin/Pages/QuenMatKhau.razor`

```text
[Bước 1: Nhập Email] ──> Gửi mã OTP về Gmail ──> Chuyển bước 2
           │
           ▼
[Bước 2: Xác minh OTP 6 số] ──> Giao diện 6 gạch chân hiển thị trực quan
           │                    Nhập đủ 6 số ──> Xác minh với API lấy Reset Token
           ▼
[Bước 3: Nhập Mật khẩu mới] ──> Nhập 2 lần (Mật khẩu mới + Nhập lại mật khẩu)
                                Kiểm tra khớp ──> Cập nhật mật khẩu ──> Về Đăng nhập
```

*Giao diện 6 ô gạch chân OTP:*
```html
<div class="qmk-otp">
    <input id="otp" @ref="_oOtp" type="text" inputmode="numeric" maxlength="6"
           @bind:get="_otp.MaOtp" @bind:set="NhapOtp" @bind:event="oninput" />
    <div class="qmk-otp-so" aria-hidden="true">
        @for (int i = 0; i < 6; i++)
        {
            <span class="@(i == Math.Min(_otp.MaOtp.Length, 5) ? "dang-nhap" : "")">
                @(i < _otp.MaOtp.Length ? _otp.MaOtp[i].ToString() : "")
            </span>
        }
    </div>
</div>
```

---

## 11. HỆ THỐNG TÀI LIỆU VÀ ĐẶC TẢ CHỨC NĂNG

Dự án duy trì bộ tài liệu kỹ thuật đồng bộ, đặt tại thư mục gốc và thư mục `docs/`:

| Tài liệu | Nội dung chính |
| :--- | :--- |
| **`DacTaChucNang_CNTT-KLCN031.md`** | Đặc tả phân tích nghiệp vụ chi tiết toàn bộ các chức năng từ `F-01` đến `F-36` (Khách hàng, Quầy, Xưởng in, Shipper, Quản lý, CEO). |
| **`TONG_HOP_THAY_DOI.md`** | Nhật ký theo dõi quá trình refactor, chi tiết số dòng code thay đổi, lịch sử migration và kết quả kiểm thử đơn vị. |
| **`HUONG_DAN_CHAY_TREN_MAY_NAY.md`** | Cẩm nang vận hành hệ thống trên môi trường máy tính cục bộ (chạy API, Web Admin, Simulator, Database). |
| **`HUONG_DAN_TICH_HOP_PAYOS.md`** | Hướng dẫn chuyên sâu về cấu hình cổng thanh toán payOS, mở Cloudflare Quick Tunnel, và đăng ký webhook. |
| **`BAO_CAO_THANH_TOAN_TU_DONG_VA_MAY_IN_AO.md`** | Báo cáo kiểm thử thực tế quy trình thanh toán ngân hàng thật và điều phối máy in ảo. |
| **`BAO_CAO_LUONG_THANH_TOAN.md`** | Báo cáo bóc tách chi tiết từng dòng code logic của luồng thanh toán VietQR và COD. |
