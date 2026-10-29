using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyApi.MayInAo;
using PhotocopyShared.Dtos.MayInAo;
using PhotocopyShared.MayInAo;
using Xunit;

namespace PhotocopyApi.Tests;

public sealed class ChuyenMayApiTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void LoiTrongKhiChoDung_PhaiBaoDungLoi_KhongChoTokenDaDungHoacConnectorKhac(bool daDung, bool connectorKhac, bool chapNhan)
    {
        string dir=Path.Combine(Path.GetTempPath(),"photocopy-stop-error-"+Guid.NewGuid());
        try
        {
            string id=Guid.NewGuid().ToString("N");
            var claims=new QuyenNhanLenhInStore(Path.Combine(dir,"claims"));
            var claim=claims.NhanLenh(1,57,id);
            var transfer=new ChuyenMayInStore(Path.Combine(dir,"transfers"));
            transfer.YeuCauDung(57,1,1,claim.MaLenh,id,1,"Máy gặp sự cố");
            if(daDung) transfer.XacNhanDaDung(57,1,claim.MaLenh,id);
            var status=new TrangThaiMayInAoStore();
            status.CapNhat(1,"DANG_IN",57,baoCao:new(){CheDo="THAT"});
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string>{["PrinterConnector:ApiKey"]="test-only"}).Build();
            var api=new MayInAoController(config,status,null,NullLogger<MayInAoController>.Instance,claims,chuyenMay:transfer){
                ControllerContext=new(){HttpContext=new DefaultHttpContext()}
            };
            api.Request.Headers["X-Printer-Api-Key"]="test-only";
            api.Request.Headers["X-Printer-Connector-Id"]=connectorKhac?Guid.NewGuid().ToString("N"):id;
            var result=api.BaoLoi(57,new(){MaMayIn=1,MaLenh=claim.MaLenh,Loi="Không truy cập được queue cũ."});
            var current=Assert.Single(status.LayTatCa());
            if(chapNhan)
            {
                Assert.IsType<OkObjectResult>(result);
                Assert.Equal("LOI",current.TrangThai);
                Assert.Equal("Không truy cập được queue cũ.",current.Loi);
                Assert.Equal(57,current.MaDonHangDangIn);
                Assert.Equal("THAT",current.CheDo);
            }
            else
            {
                Assert.False(result is OkObjectResult);
                Assert.Equal("DANG_IN",current.TrangThai);
                Assert.Null(current.Loi);
            }
            Assert.NotNull(claims.LayLenhDangIn(1));
            Assert.Equal(daDung?"DA_DUNG":"DANG_DUNG",transfer.LayHienTai(57).TrangThai);
        }
        finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }

    [Fact]
    public void ControllerChuyenMay_PhaiBaoVeVaiTro_KhongCoAnonymous()
    {
        var type = typeof(DonHangController).Assembly.GetType("PhotocopyApi.Controllers.ChuyenMayInController");
        Assert.NotNull(type);
        var attr = type.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("SUPER_ADMIN,BRANCH_ADMIN,STAFF", attr.Roles);
        Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Fact]
    public void Store_KhongApDungTruocAckVaKhongApDungPhienCu()
    {
        string dir = Path.Combine(Path.GetTempPath(), "transfer-staging-" + Guid.NewGuid());
        try
        {
            var s = new ChuyenMayInStore(dir);
            var x = s.YeuCauDung(57, 1, 1, "token", "connector", 1, "Hết giấy");
            var lines = new List<DongLenhInDto> { new() { MaChiTiet = 1, SoTrang = 5, SoBan = 1 } };
            Assert.Throws<InvalidOperationException>(() => s.ChuanBiApDung(57, x.MaYeuCau, 2, lines, new(), 1));
            Assert.True(s.XacNhanDaDung(57, 1, "token", "connector"));
            Assert.Throws<InvalidOperationException>(() => s.ChuanBiApDung(57, "sai-phien", 2, lines, new(), 1));
            s.ChuanBiApDung(57, x.MaYeuCau, 2, lines, new(), 1);
            var restart = new ChuyenMayInStore(dir);
            Assert.True(restart.ChanNhanLenh(57, 1));
            Assert.False(restart.ChanNhanLenh(57, 2));
            Assert.True(restart.ChanLenhCu(57, 1, "token"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void PhamViTrung_CongSoBanTrenTungTrang_KhongChoInVuot()
    {
        var g = new[] { new DongLenhInDto { MaChiTiet = 1, SoTrang = 10, SoBan = 2 } };
        var ok = new[] { new DongChuyenMayRequest { MaChiTiet = 1, TuTrang = 1, DenTrang = 10, SoBan = 1 },
            new DongChuyenMayRequest { MaChiTiet = 1, TuTrang = 3, DenTrang = 10, SoBan = 1 } };
        Assert.Null(QuyTacChuyenMay.KiemTra(g, ok));
        ok[1].SoBan = 2;
        Assert.NotNull(QuyTacChuyenMay.KiemTra(g, ok));
    }

    [Theory]
    [InlineData(3,10,true)] [InlineData(2,10,false)] [InlineData(1,9,false)]
    public void HaiMat_PhaiChuyenTronTo(int tu, int den, bool ok)
    {
        var g = new[] { new DongLenhInDto { MaChiTiet = 1, SoTrang = 10, SoBan = 1, MatIn = "2_MAT" } };
        Assert.Equal(ok, QuyTacChuyenMay.KiemTra(g,new[] {new DongChuyenMayRequest {MaChiTiet=1,TuTrang=tu,DenTrang=den,SoBan=1}})==null);
    }

    [Fact]
    public void Claim_LanChuyenMoi_CapTokenMoi_ChanTokenCu()
    {
        string dir = Path.Combine(Path.GetTempPath(), "transfer-claim-" + Guid.NewGuid());
        try
        {
            var s = new QuyenNhanLenhInStore(dir);
            var old = s.NhanLenh(1, 57, "pc");
            s.HoanTat(1,57,"pc",old.MaLenh);
            var method = typeof(QuyenNhanLenhInStore).GetMethods().FirstOrDefault(m=>m.Name=="NhanLenh" && m.GetParameters().Length==4);
            Assert.NotNull(method);
            var next = (QuyenNhanLenhIn)method.Invoke(s,new object[]{1,57,"pc","phien-chuyen-moi"});
            Assert.NotNull(next);
            Assert.NotEqual(old.MaLenh,next.MaLenh);
            Assert.False(s.CoQuyen(1,57,"pc",old.MaLenh));
        }
        finally { Directory.Delete(dir,true); }
    }

    [Theory]
    [InlineData(false,true)]
    [InlineData(true,false)]
    public async Task Api_ThieuXacNhanKhongDuocChamDatabase(bool kiemTraMay,bool doiChieu)
    {
        var c=new ChuyenMayInController(new ConfigurationBuilder().Build(),null,null,null,null);
        var result=await c.ApDung(57,new(){MaYeuCau=new string('a',32),MaMayMoi=2,
            DaKiemTraMayCu=kiemTraMay,DaDoiChieuBanGiay=doiChieu,
            DongIn=new(){new(){MaChiTiet=1,TuTrang=1,DenTrang=5,SoBan=1}}});
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Api_ThieuNguoiThaoTacKhongDuocGhiNhatKyHoacSQL()
    {
        var c=new ChuyenMayInController(new ConfigurationBuilder().Build(),null,null,null,null){
            ControllerContext=new(){HttpContext=new DefaultHttpContext()}
        };
        var result=Assert.IsType<StatusCodeResult>(await c.YeuCauDung(57,new(){MaMayCu=1,LyDo="Hết giấy"}));
        Assert.Equal(403,result.StatusCode);
    }

    [Fact]
    public async Task HaiYeuCauDongThoi_ChiMotPhienDung_NhatKyKhongBiGhiDe()
    {
        string dir=Path.Combine(Path.GetTempPath(),"transfer-parallel-"+Guid.NewGuid());
        try
        {
            var tasks=Enumerable.Range(0,8).Select(_=>Task.Run(()=>new ChuyenMayInStore(dir)
                .YeuCauDung(57,1,1,"token","connector",1,"Hết giấy")));
            var all=await Task.WhenAll(tasks);
            Assert.Single(all.Select(x=>x.MaYeuCau).Distinct());
            Assert.Single(new ChuyenMayInStore(dir).LichSu(57));
        }
        finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }

    [Fact]
    public void AckTreCuaLanCu_KhongDungNhamLanChuyenMoi_SauRestartVanChanTokenCu()
    {
        string dir=Path.Combine(Path.GetTempPath(),"transfer-loop-"+Guid.NewGuid());
        try
        {
            var s=new ChuyenMayInStore(dir);
            var first=s.YeuCauDung(57,1,1,"token-1","pc",1,"Hết giấy");
            s.XacNhanDaDung(57,1,"token-1","pc");
            s.ChuanBiApDung(57,first.MaYeuCau,2,new(){new(){MaChiTiet=1,SoTrang=5,SoBan=1}},new(),1);
            var second=s.YeuCauDung(57,1,2,"token-2","pc",2,"Kẹt giấy");
            Assert.True(s.XacNhanDaDung(57,1,"token-1","pc"));
            var restart=new ChuyenMayInStore(dir);
            Assert.Equal(second.MaYeuCau,restart.LayHienTai(57).MaYeuCau);
            Assert.Equal("DANG_DUNG",restart.LayHienTai(57).TrangThai);
            Assert.True(restart.ChanLenhCu(57,1,"token-1"));
            Assert.True(restart.ChanLenhCu(57,2,"token-2"));
            Assert.True(restart.ChanNhanLenh(57,1));
            Assert.True(restart.ChanNhanLenh(57,2));
        }
        finally {Directory.Delete(dir,true);}
    }

    [Fact]
    public void ChuyenNhieuLan_KhongChoLayLaiPhamViDaBoQua_HaiMatGiuCapTheoTrangGoc()
    {
        var goc=new[]{new DongLenhInDto{MaChiTiet=1,MaFile=1,TuTrang=3,DenTrang=9,SoTrang=7,SoBan=1,MatIn="2_MAT"}};
        var lan1=QuyTacChuyenMay.TaoPhanConLai(goc,new[]{new DongChuyenMayRequest{MaChiTiet=1,TuTrang=5,DenTrang=9,SoBan=1}});
        Assert.Equal(5,Assert.Single(lan1).SoTrang);
        Assert.Null(QuyTacChuyenMay.KiemTra(lan1,new[]{new DongChuyenMayRequest{MaChiTiet=1,TuTrang=7,DenTrang=9,SoBan=1}}));
        Assert.NotNull(QuyTacChuyenMay.KiemTra(lan1,new[]{new DongChuyenMayRequest{MaChiTiet=1,TuTrang=3,DenTrang=9,SoBan=1}}));
        Assert.NotNull(QuyTacChuyenMay.KiemTra(lan1,new[]{new DongChuyenMayRequest{MaChiTiet=1,TuTrang=6,DenTrang=9,SoBan=1}}));
    }
}
