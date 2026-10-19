using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.Tuan3;

namespace PhotocopyApi.Controllers;

public partial class QuanTriController
{
    // HOLDLOCK giữ khóa kiểm tra trùng đến lúc tạo xong cả vật tư và kho.
    private const string SqlTaoVatTu = @"
        IF EXISTS (SELECT 1 FROM dbo.VatTu WITH (UPDLOCK, HOLDLOCK) WHERE LTRIM(RTRIM(TenVatTu))=@TenVatTu)
        BEGIN
            SELECT 0;
            RETURN;
        END;
        INSERT INTO dbo.VatTu (TenVatTu,DonViTinh,DonGia) VALUES (@TenVatTu,@DonViTinh,@DonGia);
        DECLARE @MaVatTu INT = CAST(SCOPE_IDENTITY() AS INT);
        INSERT INTO dbo.TonKho (MaChiNhanh,MaVatTu,SoLuong,MucToiThieu)
        SELECT MaChiNhanh,@MaVatTu,0,0 FROM dbo.ChiNhanh;
        SELECT @MaVatTu;";

    // Không đổi đơn vị đã phát sinh tồn/lịch sử; không sửa giá bán hay số lượng kho.
    private const string SqlCapNhatVatTu = @"
        DECLARE @DonViCu NVARCHAR(20);
        SELECT @DonViCu=DonViTinh FROM dbo.VatTu WITH (UPDLOCK,HOLDLOCK) WHERE MaVatTu=@MaVatTu;
        IF @@ROWCOUNT=0
        BEGIN SELECT 404; RETURN; END;
        IF EXISTS (SELECT 1 FROM dbo.VatTu WITH (UPDLOCK,HOLDLOCK)
                   WHERE MaVatTu<>@MaVatTu AND LTRIM(RTRIM(TenVatTu))=@TenVatTu)
        BEGIN SELECT 409; RETURN; END;
        IF ISNULL(LTRIM(RTRIM(@DonViCu)),N'')<>@DonViTinh AND (
            EXISTS (SELECT 1 FROM dbo.TonKho WHERE MaVatTu=@MaVatTu AND SoLuong<>0)
            OR EXISTS (SELECT 1 FROM dbo.PhieuNhap WHERE MaVatTu=@MaVatTu)
            OR EXISTS (SELECT 1 FROM dbo.DichVu WHERE MaVatTu=@MaVatTu))
        BEGIN SELECT 400; RETURN; END;
        UPDATE dbo.VatTu SET TenVatTu=@TenVatTu,DonViTinh=@DonViTinh,DonGia=@DonGia WHERE MaVatTu=@MaVatTu;
        SELECT 200;";

    private const string SqlCapNhatCanhBao = @"
        UPDATE dbo.TonKho SET MucToiThieu=@MucToiThieu WHERE MaChiNhanh=@MaChiNhanh AND MaVatTu=@MaVatTu;
        SELECT @@ROWCOUNT;";

    [HttpPut("danh-muc-vat-tu/{maVatTu}")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CapNhatVatTu([Range(1, int.MaxValue)] int maVatTu, TaoVatTuRequest req)
    {
        req.TenVatTu = req.TenVatTu.Trim().Normalize();
        req.DonViTinh = req.DonViTinh.Trim().Normalize();
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        int ketQua = await conn.ExecuteScalarAsync<int>(SqlCapNhatVatTu,
            new { MaVatTu = maVatTu, req.TenVatTu, req.DonViTinh, req.DonGia }, giaoDich);
        if (ketQua == 404) return NotFound(new { message = "Không tìm thấy vật tư." });
        if (ketQua == 409) return Conflict(new { message = "Tên vật tư đã tồn tại. Vui lòng chọn tên khác." });
        if (ketQua == 400) return BadRequest(new { message = "Không đổi đơn vị tính của vật tư đã có tồn kho, phiếu nhập hoặc đang gắn với dịch vụ." });
        await giaoDich.CommitAsync();
        return Ok(new KetQuaDto { Message = "Đã sửa thông tin vật tư dùng chung; giá bán dịch vụ và số lượng tồn không thay đổi." });
    }

    [HttpPut("kho/{maChiNhanh}/{maVatTu}/muc-canh-bao")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> CapNhatCanhBaoKho(
        [Range(1, int.MaxValue)] int maChiNhanh, [Range(1, int.MaxValue)] int maVatTu, CapNhatMucCanhBaoRequest req)
    {
        if (!DuocLamViecVoiChiNhanh(maChiNhanh))
            return StatusCode(403, new { message = "Vật tư không thuộc phạm vi chi nhánh đang chọn." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        int soDong = await conn.ExecuteScalarAsync<int>(SqlCapNhatCanhBao,
            new { MaChiNhanh = maChiNhanh, MaVatTu = maVatTu, req.MucToiThieu });
        if (soDong == 0) return NotFound(new { message = "Không tìm thấy vật tư tại chi nhánh." });
        return Ok(new KetQuaDto { Message = "Đã cập nhật mức cảnh báo của chi nhánh; số lượng tồn không thay đổi." });
    }

    [HttpPost("danh-muc-vat-tu")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> TaoVatTu(TaoVatTuRequest req)
    {
        req.TenVatTu = req.TenVatTu.Trim().Normalize();
        req.DonViTinh = req.DonViTinh.Trim().Normalize();
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        int maVatTu = await conn.ExecuteScalarAsync<int>(SqlTaoVatTu, req, giaoDich);
        if (maVatTu == 0)
            return Conflict(new { message = "Tên vật tư đã tồn tại. Hãy dùng vật tư có sẵn; không tạo lại với đơn vị tính khác." });
        await giaoDich.CommitAsync();
        return Ok(new { maVatTu, message = "Đã thêm vật tư và khởi tạo tồn 0 tại các chi nhánh. Nhận hàng mới tăng tồn; cấu hình Dịch vụ để bán loại giấy này." });
    }

    [HttpGet("danh-muc-vat-tu")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> DanhMucVatTu()
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        return Ok(await conn.QueryAsync<VatTuDanhMucDto>("SELECT MaVatTu,TenVatTu,DonViTinh FROM VatTu ORDER BY TenVatTu"));
    }

    [HttpGet("danh-muc-dich-vu")]
    [Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN")]
    public async Task<IActionResult> DanhMucDichVu()
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        return Ok(await conn.QueryAsync<DichVuQuanTriDto>(
            @"SELECT dv.*, vt.TenVatTu FROM DichVu dv
              LEFT JOIN VatTu vt ON vt.MaVatTu=dv.MaVatTu ORDER BY dv.LoaiDichVu, dv.MaDichVu"));
    }

    [HttpPut("danh-muc-dich-vu/{maDichVu}")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CapNhatDanhMucDichVu([Range(1, int.MaxValue)] int maDichVu, LuuDichVuRequest req)
        => await LuuDichVuAsync(maDichVu, req, true);

    // Sửa giá không được tác động định nghĩa dùng chung hay giá của chi nhánh khác.
    [HttpPut("bang-gia/{maDichVu}")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> CapNhatGiaChiNhanh([Range(1, int.MaxValue)] int maDichVu, CapNhatGiaChiNhanhRequest req)
    {
        int? maChiNhanh = LayChiNhanhTuToken();
        if (maChiNhanh == null) return BadRequest(new { message = "Hãy chọn chi nhánh trước khi sửa giá." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        int soDong = await conn.ExecuteAsync(
            @"UPDATE BangGiaChiNhanh SET DonGia=@DonGia, TrangThai=@TrangThai
              WHERE MaChiNhanh=@MaChiNhanh AND MaDichVu=@MaDichVu",
            new { MaChiNhanh = maChiNhanh.Value, MaDichVu = maDichVu, req.DonGia, req.TrangThai });
        if (soDong == 0) return NotFound(new { message = "Không tìm thấy giá dịch vụ tại chi nhánh." });
        return Ok(new KetQuaDto { Message = "Đã cập nhật giá và trạng thái bán tại chi nhánh." });
    }

    [HttpPost("chi-nhanh")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> TaoChiNhanh(ChiNhanhQuanTriDto req)
    {
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        int ma = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO ChiNhanh (TenChiNhanh,DiaChi,SoDienThoai,ViDo,KinhDo,GioMoCua,GioDongCua,BanKinhGiao,TrangThai)
              VALUES (@TenChiNhanh,@DiaChi,@SoDienThoai,@ViDo,@KinhDo,@GioMoCua,@GioDongCua,@BanKinhGiao,@TrangThai);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", req, giaoDich);
        await conn.ExecuteAsync(
            @"INSERT INTO BangGiaChiNhanh (MaChiNhanh,MaDichVu,DonGia,TrangThai)
              SELECT @MaChiNhanh,MaDichVu,DonGia,TrangThai FROM DichVu;
              INSERT INTO TonKho (MaChiNhanh,MaVatTu,SoLuong,MucToiThieu)
              SELECT @MaChiNhanh,MaVatTu,0,0 FROM VatTu;",
            new { MaChiNhanh = ma }, giaoDich);
        await giaoDich.CommitAsync();
        return Ok(new { maChiNhanh = ma, message = "Đã thêm chi nhánh, giá mặc định và kho trống." });
    }

    [HttpPost("may-in")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<IActionResult> TaoMayIn(TaoMayInRequest req)
    {
        if (!DuocLamViecVoiChiNhanh(req.MaChiNhanh))
            return StatusCode(403, new { message = "Máy in không thuộc phạm vi chi nhánh đang chọn." });
        using var conn = new SqlConnection(_chuoiKetNoi);
        await conn.OpenAsync();
        await using var giaoDich = await conn.BeginTransactionAsync();
        bool tonTai = await conn.ExecuteScalarAsync<bool>(
            "SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM ChiNhanh WITH (UPDLOCK,HOLDLOCK) WHERE MaChiNhanh=@MaChiNhanh) THEN 1 ELSE 0 END AS BIT)",
            new { req.MaChiNhanh }, giaoDich);
        if (!tonTai) return NotFound(new { message = "Không tìm thấy chi nhánh." });
        int ma = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO MayIn (MaChiNhanh,TenMay,KhoGiay,InMau,In2Mat,TocDo,SoTrangDaIn,TrangThai)
              VALUES (@MaChiNhanh,@TenMay,@KhoGiay,@InMau,@In2Mat,@TocDo,@SoTrangDaIn,@TrangThai);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", req, giaoDich);
        await giaoDich.CommitAsync();
        return Ok(new { maMayIn = ma, message = "Đã đăng ký máy in. Cần kết nối gateway/máy in ảo để nhận lệnh in." });
    }
}
