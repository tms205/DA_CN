using System.Security.Claims;
using System.Text.Json;
using PhotocopyShared.Constants;
namespace PhotocopyApi.BaoTri;

public record TrangThaiBaoTri(bool DangBaoTri=false,string LoiNhan="",long PhienBanPhien=0,
    bool KhoaWeb=true,bool KhoaApp=true,string[] KhoaTinhNangWeb=null,string[] KhoaTinhNangApp=null,
    DateTimeOffset? BatDauLuc=null,DateTimeOffset? KetThucLuc=null,long PhienBanWeb=0,long PhienBanApp=0);
public record CauHinhBaoTri(bool KhoaWeb=true,bool KhoaApp=true,string[] KhoaTinhNangWeb=null,string[] KhoaTinhNangApp=null,double SoGio=2);

public sealed class BaoTriStore
{
    public static readonly string[] TinhNang = DanhMucBaoTri.Web.Keys.ToArray();
    private readonly object _lock=new();
    private readonly string _file;
    private readonly TimeProvider _clock;
    private TrangThaiBaoTri _state;
    public BaoTriStore(string file,TimeProvider clock=null) {
        _file=file;_clock=clock??TimeProvider.System;
        _state=File.Exists(file)?JsonSerializer.Deserialize<TrangThaiBaoTri>(File.ReadAllText(file))??throw new InvalidDataException("Trạng thái bảo trì không hợp lệ."):new();
    }
    public DateTimeOffset BayGio=>_clock.GetUtcNow();
    public static string NenTang(ClaimsPrincipal user)=>user.Identity?.IsAuthenticated==true
        ? user.FindFirst("client_platform")?.Value is "web" or "app" ? user.FindFirst("client_platform").Value
            : user.IsInRole(VaiTro.Customer)||user.IsInRole(VaiTro.Shipper)?"app":"web"
        : null;
    public static bool SessionValid(long issued,long current,bool ceo)=>ceo||issued==current;
    public static long Version(TrangThaiBaoTri state,string platform)=>Math.Max(state.PhienBanPhien,platform=="app"?state.PhienBanApp:state.PhienBanWeb);
    public static bool KhoaToanBo(TrangThaiBaoTri s,string platform)=>s.DangBaoTri && (platform=="web"?s.KhoaWeb:platform=="app"?s.KhoaApp:s.KhoaWeb||s.KhoaApp);
    public static bool KhoaTacVuNen(TrangThaiBaoTri s,string feature="")=>s?.DangBaoTri==true &&
        ((s.KhoaWeb&&s.KhoaApp) || (!string.IsNullOrEmpty(feature) && (s.KhoaWeb||ChucNang(s,"web").Contains(feature)) && (s.KhoaApp||ChucNang(s,"app").Contains(feature))));
    public static string[] ChucNang(TrangThaiBaoTri s,string platform) {
        if(!s.DangBaoTri)return Array.Empty<string>();
        var web=(s.KhoaTinhNangWeb??Array.Empty<string>()).Where(DanhMucBaoTri.Web.ContainsKey);
        var app=(s.KhoaTinhNangApp??Array.Empty<string>()).Where(DanhMucBaoTri.App.ContainsKey);
        return (platform=="web"?web:platform=="app"?app:web.Union(app)).ToArray();
    }
    public static string ThongBao(TrangThaiBaoTri s)=>s.LoiNhan+(s.KetThucLuc==null?"":"\nDự kiến mở lại lúc "+s.KetThucLuc.Value.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm",System.Globalization.CultureInfo.InvariantCulture)+" (giờ Việt Nam).");
    public TrangThaiBaoTri Read() {
        lock(_lock) {
            if(_state.DangBaoTri && _state.KetThucLuc<=BayGio) Luu(_state with {DangBaoTri=false});
            return _state;
        }
    }
    public TrangThaiBaoTri Set(bool enabled,string message,CauHinhBaoTri options=null) {
        message=(message??"").Trim();
        if(enabled&&(message.Length==0||message.Length>500))throw new ArgumentException("Lời nhắn phải có từ 1 đến 500 ký tự.");
        if(options!=null && enabled && (!double.IsFinite(options.SoGio)||options.SoGio<0.25||options.SoGio>168))throw new ArgumentException("Thời lượng bảo trì từ 0,25 đến 168 giờ.");
        string[] KiemTra(string[] list,IReadOnlyDictionary<string,string> danhMuc) {
            var result=(list??Array.Empty<string>()).Distinct().ToArray();
            if(result.Any(x=>!danhMuc.ContainsKey(x)))throw new ArgumentException("Chức năng bảo trì không có trên nền tảng đã chọn.");
            return result;
        }
        var web=KiemTra(options?.KhoaTinhNangWeb,DanhMucBaoTri.Web);var app=KiemTra(options?.KhoaTinhNangApp,DanhMucBaoTri.App);
        if(enabled&&options!=null&&!options.KhoaWeb&&!options.KhoaApp&&web.Length==0&&app.Length==0)throw new ArgumentException("Chọn ít nhất một nền tảng hoặc chức năng cần bảo trì.");
        lock(_lock) {
            var old=Read();
            var next=old with {DangBaoTri=enabled,LoiNhan=enabled?message:old.LoiNhan};
            if(enabled) {
                bool kw=options?.KhoaWeb??true,ka=options?.KhoaApp??true;
                next=next with {KhoaWeb=kw,KhoaApp=ka,KhoaTinhNangWeb=web,KhoaTinhNangApp=app,
                    BatDauLuc=BayGio,KetThucLuc=options==null?null:BayGio.AddHours(options.SoGio),
                    PhienBanPhien=old.PhienBanPhien+(options==null&&!old.DangBaoTri?1:0),
                    PhienBanWeb=Version(old,"web")+(kw&&!KhoaToanBo(old,"web")?1:0),
                    PhienBanApp=Version(old,"app")+(ka&&!KhoaToanBo(old,"app")?1:0)};
            }
            return Luu(next);
        }
    }
    private TrangThaiBaoTri Luu(TrangThaiBaoTri next) {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        string temp=_file+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(next));File.Move(temp,_file,true);
        return _state=next;
    }
}
