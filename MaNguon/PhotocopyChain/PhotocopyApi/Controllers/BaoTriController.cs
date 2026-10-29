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
    public IActionResult Status() { Response.Headers.CacheControl = "no-store"; var s = _store.Read(); return Ok(new { s.DangBaoTri, s.LoiNhan }); }
    [HttpGet("phien"), Authorize]
    public IActionResult Session() { Response.Headers.CacheControl = "no-store"; return Ok(new { hopLe = true }); }
    [HttpPut, Authorize(Roles = "SUPER_ADMIN")]
    public IActionResult Set([FromBody] BaoTriRequest request)
    {
        try { var s = _store.Set(request.DangBaoTri, request.LoiNhan); if (s.DangBaoTri) _connections.DisconnectAll(); return Ok(new { s.DangBaoTri, s.LoiNhan }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
public record BaoTriRequest(bool DangBaoTri, string LoiNhan);
