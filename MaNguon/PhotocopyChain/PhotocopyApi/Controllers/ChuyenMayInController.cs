using System.ComponentModel.DataAnnotations;
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PhotocopyApi.MayInAo;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos;
using PhotocopyShared.Dtos.MayInAo;
using PhotocopyShared.Dtos.Tuan3;
using PhotocopyShared.MayInAo;

namespace PhotocopyApi.Controllers;

[ApiController]
[Route("api/don-hang/{maDonHang:int}/chuyen-may")]
[Authorize(Roles = "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
public sealed class ChuyenMayInController : ControllerBase
{
    private readonly string _cs;
    private readonly ChuyenMayInStore _store;
    private readonly QuyenNhanLenhInStore _claims;
    private readonly TrangThaiMayInAoStore _status;
    private readonly PhatHienMayInStore _devices;
    public ChuyenMayInController(IConfiguration config, ChuyenMayInStore store, QuyenNhanLenhInStore claims,
        TrangThaiMayInAoStore status, PhatHienMayInStore devices)
    { _cs=config.GetConnectionString("PhotocopyChain"); _store=store; _claims=claims; _status=status; _devices=devices; }

    private bool DuocXem(int nhanh)
    {
        bool coNhanh=int.TryParse(User.FindFirst(TenClaim.MaChiNhanh)?.Value,out int cn);
        return User.IsInRole(VaiTro.SuperAdmin) ? !coNhanh || cn==nhanh : coNhanh && cn==nhanh;
    }
    private int NguoiDung() => int.TryParse(User.FindFirst(TenClaim.MaNguoiDung)?.Value,out int id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> TrangThai([Range(1,int.MaxValue)] int maDonHang)
    {
        using var db = new SqlConnection(_cs);
        var don = await LayDon(db,maDonHang,null,false);
        if (don==null) return NotFound();
        if (!DuocXem(don.MaChiNhanh)) return StatusCode(403);
        var goc = await DocDong(db,maDonHang,null);
        var history=_store.LichSu(maDonHang);
        return Ok(new TrangThaiChuyenMayDto { MaDonHang=maDonHang,MaMayHienTai=don.MaMayIn,TrangThaiDon=don.TrangThai,
            PhanDangIn=PhanDangIn(history,don.MaMayIn,goc),
            LichSu=history.Select((x,i)=>new ChuyenMayInDto {
                MaYeuCau=x.MaYeuCau,MaMayCu=x.MaMayCu,MaMayMoi=x.MaMayMoi,LyDo=x.LyDo,
                LucYeuCau=x.LucYeuCau,LucDaDung=x.LucDaDung,MaNguoiYeuCau=x.MaNguoiYeuCau,
                TrangThai=x.TrangThai=="CHO_AP_DUNG" ? (don.MaMayIn==x.MaMayMoi || i<history.Count-1 ? "DA_CHUYEN":"DA_DUNG") : x.TrangThai,
                MaNguoiChuyen=x.MaNguoiChuyen,LucChuyen=x.LucChuyen,PhanConLai=x.PhanConLai,HaoHut=x.HaoHut
            }).ToList() });
    }

    [HttpPost("dung")]
    public async Task<IActionResult> YeuCauDung([Range(1,int.MaxValue)] int maDonHang,YeuCauDungChuyenMayRequest req)
    {
        if (req==null || req.MaMayCu<=0 || string.IsNullOrWhiteSpace(req.LyDo) || req.LyDo.Trim().Length is <3 or >255)
            return BadRequest(new {message="Nhập lý do sự cố từ 3 đến 255 ký tự."});
        if (NguoiDung()<=0) return StatusCode(403);
        using var db=new SqlConnection(_cs);
        await db.OpenAsync();
        await using var tx=await db.BeginTransactionAsync();
        var don=await LayDon(db,maDonHang,tx,true);
        if (don==null) return NotFound();
        if (!DuocXem(don.MaChiNhanh)) return StatusCode(403);
        if (don.TrangThai!="DANG_IN" || don.MaMayIn!=req.MaMayCu) return Conflict(new {message="Đơn/máy đã thay đổi. Tải lại trước khi dừng."});
        var cu=_store.LayHienTai(maDonHang);
        if (cu!=null && cu.MaMayCu==req.MaMayCu && (cu.TrangThai!="CHO_AP_DUNG" || don.MaMayIn!=cu.MaMayMoi))
            return Ok(new KetQuaDto {Message="Yêu cầu dừng đã tồn tại; chờ connector xác nhận rồi chọn máy mới."});
        var claim=_claims.LayLenhDangIn(req.MaMayCu);
        if (claim==null || claim.MaDonHang!=maDonHang)
            return Conflict(new {message="Chưa xác định được connector đang giữ đúng lệnh này. Không tự bỏ khóa hoặc gửi lại."});
        _store.YeuCauDung(maDonHang,don.MaChiNhanh,req.MaMayCu,claim.MaLenh,claim.ConnectorId,NguoiDung(),req.LyDo);
        _status.DieuKhien(req.MaMayCu,new(){ThaoTac="TAM_DUNG"});
        await tx.CommitAsync();
        return Ok(new KetQuaDto {Message="Đã yêu cầu dừng đúng lệnh cũ. Chưa gửi sang máy mới."});
    }

    [HttpPost]
    public async Task<IActionResult> ApDung([Range(1,int.MaxValue)] int maDonHang,ApDungChuyenMayRequest req)
    {
        if (req==null || !Guid.TryParseExact(req.MaYeuCau,"N",out _) || req.MaMayMoi<=0
            || !req.DaKiemTraMayCu || !req.DaDoiChieuBanGiay || req.DongIn==null || req.DongIn.Count is <1 or >200
            || req.HaoHut==null || req.HaoHut.Count>200 || req.HaoHut.Any(x=>x==null || x.MaChiTiet<=0 || x.SoToHong is <0 or >1000000)
            || req.HaoHut.GroupBy(x=>x.MaChiTiet).Any(g=>g.Count()>1))
            return BadRequest(new {message="Cần xác nhận máy cũ đã ngừng, đối chiếu bản giấy và chọn đúng phần còn lại/hao hụt."});
        if (NguoiDung()<=0) return StatusCode(403);
        using var db=new SqlConnection(_cs);
        await db.OpenAsync();
        await using var tx=await db.BeginTransactionAsync();
        var don=await LayDon(db,maDonHang,tx,true);
        if (don==null) return NotFound();
        if (!DuocXem(don.MaChiNhanh)) return StatusCode(403);
        if (don.TrangThai!="DANG_IN" || !QuyTacMayInAo.DuocNhanLenh(don.PhuongThuc,don.TrangThaiThanhToan,don.HinhThucNhan))
            return Conflict(new {message="Đơn không còn đủ điều kiện chuyển máy."});
        var phien=_store.LayHienTai(maDonHang);
        if (phien==null || phien.MaYeuCau!=req.MaYeuCau || phien.LucDaDung==null || phien.TrangThai=="DANG_DUNG")
            return Conflict(new {message="Connector chưa xác nhận dừng đúng lệnh cũ; không gửi sang máy mới."});
        if (phien.MaMayMoi==req.MaMayMoi && don.MaMayIn==req.MaMayMoi && phien.TrangThai=="CHO_AP_DUNG")
            return Ok(new KetQuaDto {Message="Lần chuyển này đã áp dụng; không gửi lặp hoặc trừ vật tư lần hai."});
        if (don.MaMayIn!=phien.MaMayCu || req.MaMayMoi==phien.MaMayCu)
            return Conflict(new {message="Máy hiện tại đã thay đổi hoặc trùng máy cũ. Tải lại đơn."});
        var may=await db.QuerySingleOrDefaultAsync<MayDich>(
            "SELECT MaMayIn,MaChiNhanh,TrangThai FROM MayIn WITH (UPDLOCK,HOLDLOCK) WHERE MaMayIn=@ma",new{ma=req.MaMayMoi},tx);
        if (may==null || may.MaChiNhanh!=don.MaChiNhanh || may.TrangThai!="HOAT_DONG")
            return BadRequest(new {message="Máy mới phải đang hoạt động và thuộc cùng chi nhánh."});
        var heartbeat=_status.LayTatCa().FirstOrDefault(x=>x.MaMayIn==req.MaMayMoi);
        if (heartbeat?.TrangThai!="SAN_SANG" || _claims.LayLenhDangIn(req.MaMayMoi)!=null)
            return Conflict(new {message="Máy mới chưa sẵn sàng hoặc đang giữ lệnh khác."});
        var history=_store.LichSu(maDonHang);
        // Không dùng intent của lần này (có thể là retry sau rollback) làm nguồn hạn mức.
        var truoc=history.Take(history.Count-1).ToList();
        var goc=PhanDangIn(truoc,don.MaMayIn,await DocDong(db,maDonHang,tx));
        string loi=QuyTacChuyenMay.KiemTra(goc,req.DongIn);
        if (loi!=null) return BadRequest(new {message=loi});
        var conLai=QuyTacChuyenMay.TaoPhanConLai(goc,req.DongIn);
        loi=_devices.KiemTraQuyCach(req.MaMayMoi,conLai);
        if (loi!=null) return Conflict(new {message=loi});
        var cu=_devices.LayLienKet(phien.MaMayCu);
        var moi=_devices.LayLienKet(req.MaMayMoi);
        if (cu?.CheDo=="THAT" && moi?.CheDo=="THAT" && cu.ConnectorId==moi.ConnectorId
            && string.Equals(cu.TenHangDoi,moi.TenHangDoi,StringComparison.OrdinalIgnoreCase))
            return BadRequest(new {message="Máy mới vẫn trỏ cùng queue vật lý của máy cũ."});
        var files=(await db.QueryAsync<FileIn>(@"SELECT f.TrangThai,f.DuongDan FROM ChiTietDonHang ct
            JOIN FileIn f ON f.MaFile=ct.MaFile WHERE ct.MaDonHang=@ma",new{ma=maDonHang},tx)).ToList();
        if (files.Count==0 || files.Any(x=>x.TrangThai!="SAN_SANG" || string.IsNullOrWhiteSpace(x.DuongDan) || !System.IO.File.Exists(x.DuongDan)))
            return Conflict(new {message="PDF không còn sẵn sàng. Phải phục hồi đúng file trước khi chuyển."});
        var vatTu=(await db.QueryAsync<VatTuDong>(@"SELECT ct.MaChiTiet,dv.MaVatTu FROM ChiTietDonHang ct
            JOIN DichVu dv ON dv.MaDichVu=ct.MaDichVu WHERE ct.MaDonHang=@ma",new{ma=maDonHang},tx)).ToList();
        foreach (var h in req.HaoHut)
        {
            var dong=goc.Where(x=>x.MaChiTiet==h.MaChiTiet).ToList();
            long gioiHan=dong.Sum(x=>(long)(x.MatIn=="2_MAT" ? (x.SoTrang+1L)/2 : x.SoTrang)*x.SoBan);
            if (dong.Count==0 || h.SoToHong>gioiHan || (h.SoToHong>0 && vatTu.FirstOrDefault(x=>x.MaChiTiet==h.MaChiTiet)?.MaVatTu==null))
                return BadRequest(new {message="Số tờ hỏng vượt phần đang in hoặc dịch vụ chưa gắn vật tư giấy."});
        }
        try
        {
            foreach (var h in req.HaoHut.Where(x=>x.SoToHong>0).GroupBy(x=>vatTu.First(v=>v.MaChiTiet==x.MaChiTiet).MaVatTu).OrderBy(g=>g.Key))
            {
                int changed=await db.ExecuteAsync(@"UPDATE TonKho SET SoLuong=SoLuong-@Hong
                    WHERE MaChiNhanh=@Nhanh AND MaVatTu=@VatTu AND SoLuong>=@Hong",
                    new{Hong=h.Sum(x=>x.SoToHong),Nhanh=don.MaChiNhanh,VatTu=h.Key},tx);
                if (changed!=1) return Conflict(new {message="Kho không đủ bù số tờ hỏng đã khai; chưa chuyển máy."});
            }
            // Ghi intent TRƯỚC commit SQL: nhận lệnh mới còn kiểm tra SQL.MaMayIn,
            // nên rollback/crash không thể cho máy mới in khi đơn vẫn ở máy cũ.
            _store.ChuanBiApDung(maDonHang,req.MaYeuCau,req.MaMayMoi,conLai,req.HaoHut,NguoiDung());
            await db.ExecuteAsync("sp_PhanCongMayIn",new{MaDonHang=maDonHang,MaMayIn=req.MaMayMoi},tx,commandType:CommandType.StoredProcedure);
            // KHÔNG gọi lại sp_CapNhatTrangThaiIn(DANG_IN): kho toàn đơn đã trừ ở lần đầu.
            await tx.CommitAsync();
            return Ok(new KetQuaDto {Message="Đã chuyển phần còn lại sang máy mới. Giữ nguyên đơn/tiền; chỉ ghi vật tư hỏng đã khai."});
        }
        catch (SqlException) { return Conflict(new {message="Chưa chuyển được máy hoặc ghi hao hụt. Tải lại đơn và thử lại; không tự gửi thêm lệnh."}); }
        catch (IOException) { return StatusCode(503,new{message="Chưa lưu được nhật ký chuyển máy; không áp dụng thay đổi database."}); }
    }

    private static Task<DonChuyen> LayDon(SqlConnection db,int ma,System.Data.Common.DbTransaction tx,bool khoa) =>
        db.QuerySingleOrDefaultAsync<DonChuyen>(@"SELECT dh.MaDonHang,dh.MaChiNhanh,dh.MaMayIn,dh.TrangThai,dh.HinhThucNhan,
            tt.PhuongThuc,tt.TrangThai AS TrangThaiThanhToan FROM DonHang dh " + (khoa?"WITH (UPDLOCK,HOLDLOCK)":"") +
            " JOIN ThanhToan tt ON tt.MaDonHang=dh.MaDonHang WHERE dh.MaDonHang=@ma",new{ma},tx);
    private static async Task<List<DongLenhInDto>> DocDong(SqlConnection db,int ma,System.Data.Common.DbTransaction tx) =>
        (await db.QueryAsync<DongLenhInDto>(@"SELECT ct.MaChiTiet,ct.MaFile,f.TenFile,dv.KhoGiay,dv.MauSac,dv.MatIn,
            ct.HuongGiay,ct.SoTrang,ct.SoBan,ct.TuTrang,ct.DenTrang FROM ChiTietDonHang ct
            JOIN FileIn f ON f.MaFile=ct.MaFile JOIN DichVu dv ON dv.MaDichVu=ct.MaDichVu
            WHERE ct.MaDonHang=@ma ORDER BY ct.MaChiTiet",new{ma},tx)).ToList();
    private static List<DongLenhInDto> PhanDangIn(List<PhienChuyenMay> history,int? may,List<DongLenhInDto> goc) =>
        history.LastOrDefault(x=>x.TrangThai=="CHO_AP_DUNG" && x.MaMayMoi==may)?.PhanConLai ?? goc;
    private sealed class DonChuyen
    { public int MaDonHang{get;set;} public int MaChiNhanh{get;set;} public int? MaMayIn{get;set;} public string TrangThai{get;set;}
      public string HinhThucNhan{get;set;} public string PhuongThuc{get;set;} public string TrangThaiThanhToan{get;set;} }
    private sealed class MayDich { public int MaMayIn{get;set;} public int MaChiNhanh{get;set;} public string TrangThai{get;set;} }
    private sealed class FileIn { public string TrangThai{get;set;} public string DuongDan{get;set;} }
    private sealed class VatTuDong { public int MaChiTiet{get;set;} public int? MaVatTu{get;set;} }
}
