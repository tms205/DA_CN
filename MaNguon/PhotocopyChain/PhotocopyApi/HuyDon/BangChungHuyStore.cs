using System.Text.Json;
using PhotocopyApi.GiaoHang;
using PhotocopyShared.Dtos.GiaoHang;
namespace PhotocopyApi.HuyDon;

public sealed class HoSoHuyDon
{
 public string Loai {get;set;}="THU_CONG";
 public string MaHoSo {get;set;}
 public int MaDonHang {get;set;}
 public int MaNguoiGui {get;set;}
 public DateTimeOffset GuiLuc {get;set;}
 public List<AnhBangChungDto> Anh {get;set;}=new();
 public PhotocopyShared.Dtos.DonHang.BienLaiHoanTienDto BienLaiHoanTien {get;set;}
 public string LyDo {get;set;}
 public DateTime? NgayDat {get;set;}
 public DateTime? HetHanThanhToan {get;set;}
 public string TrangThaiThanhToan {get;set;}
}
public sealed class BangChungHuyStore
{
 private readonly string _root;
 public BangChungGiaoHangStore Anh {get;}
 public BangChungHuyStore(IConfiguration config) {
  _root=Path.GetFullPath(config["HuyDon:ThuMucBangChung"]??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PhotocopyChain","CancellationEvidence"));
  Anh=new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string>{{"GiaoHang:ThuMucBangChung",_root}}).Build());
 }
 private string PathHoSo(int order,string code) {
  if(order<1||!Guid.TryParseExact(code,"N",out _))throw new ArgumentException("Mã bằng chứng hủy không hợp lệ.");
  return Path.Combine(_root,order.ToString(),code+".json");
 }
 public async Task<HoSoHuyDon> Luu(int order,int actor,IReadOnlyList<IFormFile> files,CancellationToken ct) {
  var images=await Anh.Luu(order,files,ct);
  var value=new HoSoHuyDon{MaDonHang=order,MaNguoiGui=actor,MaHoSo=Guid.NewGuid().ToString("N"),GuiLuc=DateTimeOffset.UtcNow,Anh=images};
  try {await using var stream=new FileStream(PathHoSo(order,value.MaHoSo),FileMode.CreateNew,FileAccess.Write,FileShare.None);
   await JsonSerializer.SerializeAsync(stream,value,cancellationToken:ct);return value;
  }catch{Anh.XoaTepCuaYeuCau(order,images);throw;}
 }
 public HoSoHuyDon Doc(int order,int actor,string code) {
  string path=PathHoSo(order,code);
  if(!File.Exists(path))throw new ArgumentException("Vui lòng tải bằng chứng hủy lên trước.");
  var value=JsonSerializer.Deserialize<HoSoHuyDon>(File.ReadAllText(path));
  if(value?.MaDonHang!=order||value.MaNguoiGui!=actor||value.Anh.Count is <1 or >3)throw new ArgumentException("Bằng chứng không thuộc đơn/tài khoản này.");
  return value;
 }
}
