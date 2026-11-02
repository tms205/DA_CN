using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotocopyApi.BaoTri;
namespace PhotocopyApi.Controllers;
[ApiController, Route("api/bao-tri")]
public class BaoTriController : ControllerBase
{
    private readonly BaoTriStore _store;
    private readonly KetNoiBaoTri _connections;
    public BaoTriController(BaoTriStore store, KetNoiBaoTri connections) => (_store, _connections) = (store, connections);
    [HttpGet("trang-thai"), AllowAnonymous]
    public IActionResult Status([FromQuery]string nenTang=null) { Response.Headers.CacheControl="no-store";var s=_store.Read();string p=nenTang is "web" or "app"?nenTang:BaoTriStore.NenTang(User);
        return Ok(new {DangBaoTri=BaoTriStore.KhoaToanBo(s,p),LoiNhan=BaoTriStore.ThongBao(s),s.KetThucLuc,TinhNangKhoa=BaoTriStore.ChucNang(s,p)}); }
    [HttpGet,Authorize(Roles="SUPER_ADMIN")]
    public IActionResult Config() {Response.Headers.CacheControl="no-store";return Ok(new {trangThai=_store.Read(),gioHeThong=_store.BayGio});}
    [HttpGet("phien"), Authorize]
    public IActionResult Session() { Response.Headers.CacheControl = "no-store"; return Ok(new { hopLe = true }); }
    [HttpPut, Authorize(Roles = "SUPER_ADMIN")]
    public IActionResult Set([FromBody] BaoTriRequest request)
    {
        try { var s=_store.Set(request.DangBaoTri,request.LoiNhan,new(request.KhoaWeb,request.KhoaApp,request.KhoaTinhNangWeb,request.KhoaTinhNangApp,request.SoGio));
            if(s.DangBaoTri)_connections.DisconnectWhere(c=>BaoTriStore.KhoaToanBo(s,BaoTriStore.NenTang(c.User)));
            return Ok(new {trangThai=s,gioHeThong=_store.BayGio}); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
public record BaoTriRequest(bool DangBaoTri,string LoiNhan,bool KhoaWeb=true,bool KhoaApp=true,string[] KhoaTinhNangWeb=null,string[] KhoaTinhNangApp=null,double SoGio=2);
