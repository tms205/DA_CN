# Báo Cáo Luồng Hoạt Động Của Hệ Thống Thanh Toán

> **Hệ thống:** Chuỗi Cửa Hàng Photocopy (PhotocopyChain)  
> **Phiên bản:** CNTT-KLCN031  
> **Ngày tổng hợp:** 09/10/2026  
> **Mục đích:** Đặc tả chi tiết cơ chế hoạt động, luồng nghiệp vụ và mã nguồn xử lý thanh toán (VietQR qua payOS và Tiền mặt / COD).

---

## 1. TỔNG QUAN VỀ KIẾN TRÚC THANH TOÁN

Hệ thống hỗ trợ 2 phương thức thanh toán chính trong bảng `ThanhToan`:
1. **`VIETQR` (Thanh toán trực tuyến tự động qua cổng payOS thật):**
   - Áp dụng cho cả khách đặt đơn từ App Mobile và khách đến lập đơn tại quầy (Web Admin).
   - Hệ thống tạo mã QR/link checkout chuẩn của payOS.
   - Khi khách quét mã và chuyển tiền qua Internet Banking, payOS gửi webhook về API để hệ thống tự động xác nhận đã thu tiền.
   - **Tự động hóa toàn trình:** Đơn đã trả tiền lập tức được kiểm tra file và phân công vào máy in phù hợp để chuyển sang trạng thái `DANG_IN` mà không cần nhân viên bấm xác nhận thủ công.
   - Tiền đi thẳng vào tài khoản ngân hàng của hệ thống nên **không** cộng vào két tiền mặt của ca làm việc.
2. **`COD` (Thanh toán tiền mặt khi nhận hàng / tại quầy):**
   - Áp dụng khi khách nhận tại quầy hoặc nhận qua Shipper.
   - Tài liệu được in xong trước, nhân viên/Shipper chỉ thu tiền lúc bàn giao.
   - Tiền mặt được cộng trực tiếp vào `CaLamViec.DoanhThuTrongCa` của nhân viên hoặc quản lý chi nhánh.

### Cấu trúc dữ liệu trong CSDL (`ThanhToan`)
```sql
CREATE TABLE ThanhToan (
    MaThanhToan   INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang     INT NOT NULL UNIQUE REFERENCES DonHang(MaDonHang),
    PhuongThuc    VARCHAR(10) NOT NULL CHECK (PhuongThuc IN ('VIETQR','COD')),
    SoTien        DECIMAL(12,0) NOT NULL,
    TrangThai     VARCHAR(20) DEFAULT 'CHUA_THANH_TOAN', -- 'CHUA_THANH_TOAN', 'DA_THANH_TOAN'
    MaGiaoDich    VARCHAR(50),                           -- Mã tham chiếu giao dịch ngân hàng / payOS
    MaCa          INT REFERENCES CaLamViec(MaCa),        -- Chỉ gắn khi thu tiền mặt vào két ca
    NgayThanhToan DATETIME
);
```

---

## 2. LUỒNG THANH TOÁN TỰ ĐỘNG VIETQR (QUA CỔNG PAYOS)

### 2.1. Sơ đồ luồng hoạt động

```text
Khách đặt đơn (App Mobile / Web Quầy)
                 │
                 ▼
          [sp_TaoDonHang]
     (Tạo Đơn Hàng + ThanhToan: CHUA_THANH_TOAN, hạn 15 phút)
                 │
                 ▼
 Client gọi: POST /api/thanh-toan/{id}/payos
     (Kiểm tra đơn, tạo PaymentLink qua SDK payOS)
                 │
                 ▼
       Khách quét VietQR thanh toán
                 │
  ┌──────────────┴──────────────────────────┐
  ▼ (Luồng chính tự động)                   ▼ (Luồng dự phòng khi mất mạng/tunnel)
payOS gửi Webhook:                     Client gọi: GET /api/thanh-toan/{id}
POST /api/thanh-toan/webhook           Hàm ThuDongBoTuPayOS kiểm tra SDK payOS
  │                                         │
  └───────────────────┬─────────────────────┘
                      ▼
         Xác minh chữ ký & kiểm tra số tiền
                      ▼
            [sp_XacNhanChuyenKhoan]
          (Cập nhật DA_THANH_TOAN)
                      │
                      ▼
         [TuDongInService.XepInSauThanhToan]
       Kiểm tra file PDF -> Chọn máy in -> DANG_IN
                      │
                      ▼
         [SignalR Hub: CapNhatHub]
   Bắn thông báo real-time tới Mobile & Web Admin
```

---

### 2.2. Chi tiết từng bước và đoạn code xử lý nghiệp vụ

#### Bước 1: Khởi tạo đơn và bản ghi thanh toán
Khi khách đặt in trên App hoặc nhân viên lập đơn tại quầy, thủ tục `sp_TaoDonHang` được gọi. Thủ tục này tự động chèn một dòng vào bảng `ThanhToan` với trạng thái ban đầu là `CHUA_THANH_TOAN`:

*Vị trí file:* `Database/003-CapNhat_CSDL_BangGia_KhuyenMai_ChiNhanh.sql` (dòng 244–246)
```sql
        -- Tự động khởi tạo bản ghi thanh toán theo đơn
        INSERT INTO ThanhToan (MaDonHang, PhuongThuc, SoTien)
        VALUES (@MaDonHang, @PhuongThuc, @TienHang + @PhiShip - @GiamGia);
```
> **Thời hạn thanh toán:** Đơn VietQR có hạn thanh toán là **15 phút** tính từ thời điểm đặt: `DATEADD(MINUTE, 15, dh.NgayDat)`.

---

#### Bước 2: Tạo liên kết thanh toán payOS (Checkout URL)
Client gọi endpoint `POST /api/thanh-toan/{maDonHang}/payos`. Controller tiến hành kiểm tra:
- Khách hàng có quyền xem đơn.
- Phương thức đơn bắt buộc là `VIETQR`.
- Đơn chưa thanh toán, chưa bị hủy và còn trong hạn 15 phút.

*Vị trí file:* `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs` (dòng 84–125)
```csharp
    // POST api/thanh-toan/5/payos — tạo mới hoặc lấy lại trang checkout của đúng đơn này.
    [HttpPost("{maDonHang:int}/payos")]
    [Authorize(Roles = "CUSTOMER,SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public async Task<IActionResult> TaoLienKetPayOS(int maDonHang)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        ThanhToanDto tt = await DocThanhToanDuocPhep(conn, maDonHang);

        if (tt == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (tt.PhuongThuc != "VIETQR")
            return BadRequest(new { message = "Đơn này không thanh toán bằng VietQR." });
        if (tt.TrangThai == "DA_THANH_TOAN")
            return BadRequest(new { message = "Đơn hàng đã được thanh toán." });
        if (tt.TrangThaiDonHang == TrangThaiDonHang.DaHuy)
            return BadRequest(new { message = "Đơn đã bị hủy, vui lòng đặt lại." });
        if (tt.HanThanhToan == null || tt.HanThanhToan <= DateTime.Now)
            return BadRequest(new { message = "Đơn đã quá hạn thanh toán, vui lòng đặt lại." });

        string diaChiGoc = QuyTacPayOS.ChuanHoaDiaChiGoc(_config["PayOS:PublicBaseUrl"]);
        if (diaChiGoc == null)
            return StatusCode(503, new { message = "Máy chủ chưa cấu hình địa chỉ HTTPS công khai cho payOS." });

        try
        {
            string kenh = User.IsInRole(VaiTro.Customer) ? "mobile" : "admin";
            string checkoutUrl = await TaoHoacLayCheckoutUrl(tt, diaChiGoc, kenh);
            return Ok(new LienKetPayOSDto
            {
                CheckoutUrl = checkoutUrl,
                HanThanhToan = tt.HanThanhToan.Value
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Không tạo/lấy được link payOS cho đơn {MaDonHang}.", maDonHang);
            return StatusCode(502, new { message = "Chưa kết nối được payOS, vui lòng thử lại." });
        }
    }
```

Hàm tạo hoặc lấy lại checkout URL từ SDK payOS:
- Nếu link đã tạo trước đó trên payOS, hệ thống tái sử dụng lại `CheckoutUrl` cũ.
- Nếu payOS trả mã `101` hoặc `404` (chưa có link), hệ thống gọi `_payOs.PaymentRequests.CreateAsync` để tạo link mới.
- Nội dung chuyển khoản chuẩn hóa dạng `DH` + mã đơn 5 số (ví dụ `DH00006`).

*Vị trí file:* `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs` (dòng 312–348)
```csharp
    private async Task<string> TaoHoacLayCheckoutUrl(ThanhToanDto tt, string diaChiGoc, string kenh)
    {
        try
        {
            PaymentLink linkCu = await _payOs.PaymentRequests.GetAsync(tt.MaDonHang);

            if (linkCu.Amount != decimal.ToInt64(tt.SoTien))
                throw new InvalidOperationException("Số tiền trên link payOS cũ không khớp với đơn hàng.");
            if (linkCu.Status is PaymentLinkStatus.Cancelled or PaymentLinkStatus.Expired or PaymentLinkStatus.Failed)
                throw new InvalidOperationException("Link payOS của đơn đã hết hiệu lực, vui lòng đặt lại đơn.");

            return QuyTacPayOS.TaoCheckoutUrl(linkCu.Id);
        }
        catch (ApiException ex) when (QuyTacPayOS.ChuaCoLienKet(ex.StatusCode, ex.ErrorCode))
        {
            // Chưa có link cho orderCode này thì tạo mới ở bên dưới.
        }

        string diaChiKetQua = string.Equals(kenh, "mobile", StringComparison.OrdinalIgnoreCase)
            ? diaChiGoc + "/api/thanh-toan/payos/ket-qua?kenh=mobile"
            : QuyTacPayOS.TaoDiaChiTrangChuWeb(_config["ThanhToan:WebAdminUrl"]);
        if (diaChiKetQua == null)
            throw new InvalidOperationException("Chưa cấu hình địa chỉ Web Admin hợp lệ.");

        var yeuCau = new CreatePaymentLinkRequest
        {
            OrderCode = tt.MaDonHang,
            Amount = decimal.ToInt64(tt.SoTien),
            Description = QuyTacPayOS.TaoMoTaDon(tt.MaDonHang),
            ReturnUrl = diaChiKetQua,
            CancelUrl = diaChiKetQua,
            ExpiredAt = new DateTimeOffset(tt.HanThanhToan.Value).ToUnixTimeSeconds()
        };

        CreatePaymentLinkResponse linkMoi = await _payOs.PaymentRequests.CreateAsync(yeuCau);
        return linkMoi.CheckoutUrl;
    }
```

---

#### Bước 3: Khách thực hiện quét mã QR payOS
- **Phía App Mobile:** Khách nhấn nút mở trình duyệt hoặc app ngân hàng để quét mã QR (`launchUrl(checkoutUrl)`).
- **Phía Web Admin:** Màn hình lập đơn tại quầy mở trang QR payOS chính thức cho khách quét.

---

#### Bước 4: Xử lý Webhook tự động từ payOS
Khi tài khoản nhận được tiền, payOS gửi request `POST /api/thanh-toan/webhook`. 
Backend thực hiện quy trình kiểm tra bảo mật:
1. **Xác minh chữ ký điện tử:** Dùng `ChecksumKey` qua SDK `_payOs.Webhooks.VerifyAsync(req)`.
2. **Khớp mã đơn và đối chiếu CSDL:** Đơn phải tồn tại, đang là `VIETQR`.
3. **Đối chiếu số tiền:** Số tiền nhận phải bằng 100% số tiền của đơn hàng (`QuyTacPayOS.SoTienKhop`).
4. **Khôi phục đơn quá hạn:** Nếu đơn bị hủy tự động do quá 15 phút nhưng ngân hàng vẫn báo tiền về thành công, hệ thống **tự động phục hồi** từ `DA_HUY` về `CHO_XAC_NHAN` để tiếp tục xử lý, không làm mất tiền của khách.

*Vị trí file:* `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs` (dòng 170–247)
```csharp
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(PayOSWebhook req)
    {
        PayOS.Models.Webhooks.WebhookData duLieu;

        try
        {
            // Chỉ nhận dữ liệu có chữ ký hợp lệ từ payOS
            duLieu = await _payOs.Webhooks.VerifyAsync(req);
        }
        catch (WebhookException ex)
        {
            _logger.LogWarning(ex, "Webhook payOS có chữ ký không hợp lệ.");
            return BadRequest(new { message = "Webhook payOS không hợp lệ." });
        }

        if (!req.Success || duLieu.Code != "00")
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });

        if (duLieu.OrderCode <= 0 || duLieu.OrderCode > int.MaxValue)
        {
            _logger.LogWarning("Webhook payOS có mã đơn không hợp lệ: {OrderCode}", duLieu.OrderCode);
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });
        }

        int maDonHang = (int)duLieu.OrderCode;

        // Luôn đối chiếu với CSDL, không tin số tiền hay trạng thái do phía giao diện gửi lên.
        using var conn = new SqlConnection(_chuoiKetNoi);
        DonWebhook don = await conn.QueryFirstOrDefaultAsync<DonWebhook>(
            @"SELECT dh.MaDonHang, dh.TrangThai AS TrangThaiDonHang, dh.GhiChu,
                     tt.PhuongThuc, tt.SoTien, tt.TrangThai AS TrangThaiThanhToan
              FROM DonHang dh
              JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
              WHERE dh.MaDonHang = @MaDonHang",
            new { MaDonHang = maDonHang });

        if (don == null)
        {
            _logger.LogWarning("Webhook payOS báo mã đơn không tồn tại: {MaDonHang}", maDonHang);
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });
        }

        bool biHuyThuCong = don.TrangThaiDonHang == TrangThaiDonHang.DaHuy
            && don.GhiChu != "Quá 15 phút chưa thanh toán";
        if (don.PhuongThuc != "VIETQR" || biHuyThuCong)
        {
            _logger.LogWarning("Webhook payOS báo cho đơn {MaDonHang} không còn được phép thanh toán.", maDonHang);
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });
        }

        if (!string.Equals(duLieu.Currency, "VND", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(duLieu.Description, QuyTacPayOS.TaoMoTaDon(maDonHang), StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(duLieu.Reference))
        {
            _logger.LogWarning("Webhook payOS của đơn {MaDonHang} có thông tin đối chiếu không hợp lệ.", maDonHang);
            return Ok(new KetQuaDto { Message = "Đã nhận webhook payOS." });
        }

        // Chỉ xác nhận khi số tiền ngân hàng nhận đúng bằng số tiền của đơn
        if (!QuyTacPayOS.SoTienKhop(don.SoTien, duLieu.Amount))
        {
            _logger.LogWarning(
                "Đơn {MaDonHang} nhận sai số tiền. Cần {SoTienCanTra}, thực nhận {SoTienNhan}.",
                maDonHang, don.SoTien, duLieu.Amount);

            return Ok(new KetQuaDto { Message = "Đã nhận webhook nhưng số tiền không khớp." });
        }

        _logger.LogInformation(
            "payOS báo đã nhận {SoTien} cho đơn {MaDonHang}, giao dịch {MaGiaoDich}.",
            duLieu.Amount, maDonHang, duLieu.Reference);

        return await XacNhanDaTra(maDonHang, duLieu.Reference);
    }
```

---

#### Bước 5: Cơ chế đối soát dự phòng khi Webhook bị gián đoạn
Khi Cloudflare Quick Tunnel tắt hoặc mạng rớt, Webhook không tới được máy chủ. Hệ thống thiết kế cơ chế đối soát dự phòng:
- App Mobile cứ 5 giây thăm dò `GET /api/thanh-toan/{id}` một lần.
- Web Admin khi quay lại từ cổng thanh toán hoặc khi bấm nút "Kiểm tra thanh toán" sẽ gọi `GET /api/thanh-toan/{id}`.
- Backend chủ động gọi `_payOs.PaymentRequests.GetAsync(maDonHang)` để lấy trạng thái thật từ máy chủ payOS:

*Vị trí file:* `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs` (dòng 251–280)
```csharp
    // Đối soát dự phòng khi webhook bị lỡ do tunnel/API tạm dừng.
    private async Task<bool> ThuDongBoTuPayOS(ThanhToanDto tt)
    {
        try
        {
            PaymentLink link = await _payOs.PaymentRequests.GetAsync(tt.MaDonHang);
            if (link.Status != PaymentLinkStatus.Paid
                || link.OrderCode != tt.MaDonHang
                || !QuyTacPayOS.SoTienKhop(tt.SoTien, link.Amount)
                || !QuyTacPayOS.SoTienKhop(tt.SoTien, link.AmountPaid))
                return false;

            string maGiaoDich = link.Transactions?
                .LastOrDefault(x => !string.IsNullOrWhiteSpace(x.Reference))?.Reference
                ?? "PAYOS-" + link.Id;
            if (maGiaoDich.Length > 50) maGiaoDich = maGiaoDich[..50];

            IActionResult ketQua = await XacNhanDaTra(tt.MaDonHang, maGiaoDich);
            return ketQua is OkObjectResult;
        }
        catch (ApiException ex) when (QuyTacPayOS.ChuaCoLienKet(ex.StatusCode, ex.ErrorCode))
        {
            return false;
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "Không đối soát được đơn {MaDonHang} với payOS.", tt.MaDonHang);
            return false;
        }
    }
```

---

#### Bước 6: Cập nhật CSDL, kích hoạt in tự động và bắn SignalR
Khi tiền được xác nhận hợp lệ:
1. Gọi Stored Procedure `sp_XacNhanChuyenKhoan` để cập nhật `TrangThai = 'DA_THANH_TOAN'`, ghi nhận `MaGiaoDich` và ngày thanh toán.
2. Gọi `_tuDongIn.XepInSauThanhToan(maDonHang)`: Hệ thống kiểm tra file PDF trên máy chủ, tìm máy in phù hợp (cùng chi nhánh, hỗ trợ in màu/khổ giấy/in hai mặt) và tự chuyển đơn sang `DANG_IN`.
3. Bắn SignalR Realtime qua `CapNhatHub` tới khách hàng và nhân viên chi nhánh để cập nhật giao diện ngay lập tức.

*Vị trí file:* `MaNguon/PhotocopyChain/PhotocopyApi/Controllers/ThanhToanController.cs` (dòng 351–416)
```csharp
    private async Task<IActionResult> XacNhanDaTra(int maDonHang, string maGiaoDich)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);

        var don = await conn.QueryFirstOrDefaultAsync<DonDeBao>(...);
        if (don == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });

        // Tự phục hồi đơn bị hủy tự động do quá 15 phút
        if (don.TrangThai == TrangThaiDonHang.DaHuy)
        {
            int daKhoiPhuc = await conn.ExecuteAsync(
                @"UPDATE DonHang
                  SET TrangThai = 'CHO_XAC_NHAN', GhiChu = NULL
                  WHERE MaDonHang = @MaDonHang AND TrangThai = 'DA_HUY'
                    AND GhiChu = N'Quá 15 phút chưa thanh toán'",
                new { MaDonHang = maDonHang });
            if (daKhoiPhuc == 0)
                return BadRequest(new { message = "Đơn đã bị hủy thủ công nên không thể tự khôi phục." });
            don.TrangThai = TrangThaiDonHang.ChoXacNhan;
        }

        if (don.TrangThaiThanhToan != "DA_THANH_TOAN")
        {
            try
            {
                // Thủ tục chỉ cập nhật đúng đơn VietQR đang "CHUA_THANH_TOAN" và tự ghi thông báo cho khách
                await conn.ExecuteAsync("sp_XacNhanChuyenKhoan",
                    new { MaDonHang = maDonHang, MaGiaoDich = maGiaoDich },
                    commandType: CommandType.StoredProcedure);
            }
            catch (SqlException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Tự động xếp máy in sau khi thanh toán
        KetQuaXepIn ketQuaIn = await _tuDongIn.XepInSauThanhToan(maDonHang);

        DonDeBao donSau = await conn.QueryFirstAsync<DonDeBao>(...);

        // Bắn thông báo Realtime SignalR
        var tin = new TinCapNhat
        {
            MaDonHang = donSau.MaDonHang,
            MaDonHangHienThi = donSau.MaDonHangHienThi,
            MaChiNhanh = donSau.MaChiNhanh,
            TrangThai = donSau.TrangThai,
            NoiDung = "Đơn " + donSau.MaDonHangHienThi + " đã thanh toán"
        };
        await CapNhatHub.GuiChoNguoiDung(_hub, donSau.MaKhachHang, HubEvents.PaymentConfirmed, tin);
        await CapNhatHub.GuiChoChiNhanh(_hub, donSau.MaChiNhanh, HubEvents.PaymentConfirmed, tin);

        string message = ketQuaIn.DaBatDauIn
            ? "Đã ghi nhận thanh toán và bắt đầu in tự động."
            : "Đã ghi nhận thanh toán. " + ketQuaIn.Message;
        return Ok(new KetQuaDto { Message = message });
    }
```

Stored Procedure cập nhật trong CSDL:
*Vị trí file:* `Database/04_StoredProcedure.sql` (dòng 243–255)
```sql
CREATE PROCEDURE sp_XacNhanChuyenKhoan
    @MaDonHang INT, @MaGiaoDich VARCHAR(50)
AS
BEGIN
    UPDATE ThanhToan
    SET TrangThai = 'DA_THANH_TOAN', MaGiaoDich = @MaGiaoDich, NgayThanhToan = GETDATE()
    WHERE MaDonHang = @MaDonHang AND PhuongThuc = 'VIETQR' AND TrangThai = 'CHUA_THANH_TOAN';

    IF @@ROWCOUNT > 0
        INSERT INTO ThongBao (MaNguoiDung, MaDonHang, TieuDe, NoiDung)
        SELECT MaKhachHang, MaDonHang, N'Thanh toán thành công', N'Đơn hàng của bạn đã được thanh toán.'
        FROM DonHang WHERE MaDonHang = @MaDonHang;
END;
```

---

#### Bước 7: Hủy đơn quá hạn thanh toán (`DonQuaHanJob`)
Background service chạy định kỳ mỗi phút một lần để hủy các đơn VietQR chưa thanh toán quá 15 phút.

*Vị trí file:* `Database/04_StoredProcedure.sql` (dòng 259–267)
```sql
CREATE PROCEDURE sp_HuyDonQuaHan
AS
BEGIN
    UPDATE dh SET TrangThai = 'DA_HUY', GhiChu = N'Quá 15 phút chưa thanh toán'
    FROM DonHang dh JOIN ThanhToan tt ON tt.MaDonHang = dh.MaDonHang
    WHERE dh.TrangThai = 'CHO_XAC_NHAN' AND tt.PhuongThuc = 'VIETQR'
      AND tt.TrangThai = 'CHUA_THANH_TOAN' AND DATEADD(MINUTE, 15, dh.NgayDat) < GETDATE();
END;
```
> **Cơ chế lưu trữ file:** File in của các đơn bị hủy do quá hạn 15 phút vẫn được lưu trên máy chủ thêm 24 giờ để đảm bảo nếu có webhook/đối soát đến muộn thì đơn vẫn có file sẵn sàng để đưa vào in ngay.

---

## 3. LUỒNG THANH TOÁN TIỀN MẶT / COD

Với phương thức này, tài liệu được in ấn hoàn tất trước, việc thu tiền được thực hiện khi bàn giao tài liệu.

### 3.1. Thu tiền mặt tại quầy (`sp_GiaoTaiQuay`)
Khách đến quầy lấy hàng (`HinhThucNhan = 'TAI_QUAY'`). Nhân viên thu tiền mặt và hoàn tất đơn hàng:
- Kiểm tra nhân viên phải đang có ca làm việc mở (`CaLamViec`) tại đúng chi nhánh đó.
- Cập nhật `ThanhToan` thành `DA_THANH_TOAN` và gắn `MaCa` của nhân viên.
- Cộng số tiền thu được vào `CaLamViec.DoanhThuTrongCa`.
- Chuyển trạng thái đơn sang `HOAN_THANH`.

*Vị trí file:* `Database/04_StoredProcedure.sql` (dòng 419–444)
```sql
        IF @PhuongThuc = 'COD'
        BEGIN
            -- Kiểm tra ca đang mở của nhân viên
            SELECT @MaCa = MaCa
            FROM CaLamViec
            WHERE MaNhanVien = @MaNhanVien AND MaChiNhanh = @MaChiNhanhDon
              AND TrangThai = 'DANG_MO';

            IF @MaCa IS NULL
                THROW 50028, N'Bạn chưa mở ca tại chi nhánh này.', 1;

            -- Cập nhật thanh toán và ghi nhận mã ca
            UPDATE ThanhToan
            SET TrangThai = 'DA_THANH_TOAN', MaCa = @MaCa, NgayThanhToan = GETDATE()
            WHERE MaDonHang = @MaDonHang AND TrangThai = 'CHUA_THANH_TOAN';

            IF @@ROWCOUNT = 0 AND @TrangThaiThanhToan <> 'DA_THANH_TOAN'
                THROW 50029, N'Không thể ghi nhận thanh toán COD.', 1;

            -- Cộng tiền mặt vào doanh thu của ca
            UPDATE CaLamViec
            SET DoanhThuTrongCa = DoanhThuTrongCa + @SoTien
            WHERE MaCa = @MaCa;
        END

        UPDATE DonHang
        SET TrangThai = 'HOAN_THANH', NgayHoanThanh = GETDATE()
        WHERE MaDonHang = @MaDonHang;
```

---

### 3.2. Thu tiền COD qua Shipper giao hàng tận nơi
Quy trình gồm 2 giai đoạn:

#### Giai đoạn A: Shipper giao hàng và thu tiền của khách (`sp_CapNhatGiaoHang`)
Khi giao thành công, số tiền cần thu được ghi vào `GiaoHang.TienThu`. Trạng thái đơn chuyển sang `DA_GIAO` (chờ nộp tiền về chi nhánh).

*Vị trí file:* `Database/04_StoredProcedure.sql` (dòng 488–496)
```sql
    IF @ThanhCong = 1
    BEGIN
        UPDATE GiaoHang SET TrangThai = 'THANH_CONG', TienThu = @ChuaTra, NgayGiao = GETDATE() 
        WHERE MaGiaoHang = @MaGiaoHang;

        -- Nếu là COD thì chuyển sang DA_GIAO để chờ shipper nộp tiền về két chi nhánh
        UPDATE DonHang SET TrangThai = CASE WHEN @ChuaTra = 0 THEN 'HOAN_THANH' ELSE 'DA_GIAO' END
        WHERE MaDonHang = @MaDonHang;
    END
```

#### Giai đoạn B: Shipper nộp tiền về chi nhánh cho Quản lý (`sp_NhanTienCOD`)
Quản lý chi nhánh nhận tiền COD từ Shipper. Tiền được nạp vào ca đang mở của Quản lý, bản ghi `ThanhToan` chuyển sang `DA_THANH_TOAN`, và đơn hàng chuyển sang `HOAN_THANH`.

*Vị trí file:* `Database/04_StoredProcedure.sql` (dòng 527–536)
```sql
    BEGIN TRAN;
        -- Cộng tiền vào ca của Quản lý
        UPDATE ca SET DoanhThuTrongCa = DoanhThuTrongCa + tt.SoTien
        FROM CaLamViec ca JOIN ThanhToan tt ON tt.MaDonHang = @MaDonHang
        WHERE ca.MaCa = @MaCa;

        -- Cập nhật thanh toán và hoàn tất đơn hàng
        UPDATE ThanhToan SET TrangThai = 'DA_THANH_TOAN', MaCa = @MaCa, NgayThanhToan = GETDATE()
        WHERE MaDonHang = @MaDonHang;
        UPDATE GiaoHang SET DaNopTien = 1 WHERE MaDonHang = @MaDonHang AND TrangThai = 'THANH_CONG';
        UPDATE DonHang SET TrangThai = 'HOAN_THANH' WHERE MaDonHang = @MaDonHang;
    COMMIT;
```

---

## 4. TỔNG KẾT & SO SÁNH 2 LUỒNG THANH TOÁN

| Tiêu chí | VietQR (payOS) | COD / Tiền mặt |
| :--- | :--- | :--- |
| **Kênh xử lý** | Cổng thanh toán trực tuyến payOS thật | Nhân viên quầy / Shipper |
| **Thời điểm thu tiền** | **Trước khi in** (Thu đủ tiền mới cho phép in) | **Sau khi in** (Thu tiền lúc bàn giao tài liệu) |
| **Cơ chế xác nhận** | Webhook có chữ ký `ChecksumKey` & Đối soát SDK | Stored Procedure gắn với ca làm việc |
| **Dòng tiền & Ca làm việc** | Chuyển trực tiếp vào tài khoản ngân hàng, **không** cộng vào `CaLamViec` | Cộng trực tiếp vào `CaLamViec.DoanhThuTrongCa` của nhân viên/quản lý |
| **Bảo mật** | Không tin tham số URL (`status=PAID`), chỉ tin chữ ký/SDK | Kiểm tra chặt chẽ ca làm việc đang mở và đúng chi nhánh |
| **Xử lý quá hạn** | Tự hủy sau 15 phút (`sp_HuyDonQuaHan`), tự khôi phục nếu tiền về muộn | Không áp dụng hạn 15 phút trực tuyến |
| **Tự động hóa tiếp theo** | Tự động phân công máy in ảo/thật và chuyển sang `DANG_IN` | Nhân viên/thợ in tiếp nhận và phân công thủ công |
