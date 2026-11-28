using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotocopyApi.Controllers;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

// 28/11: kiểm kê kho (CSDL 034) và theo dõi hộp mực máy in (CSDL 035).
public class KiemKeHopMucTests
{
    [Theory]
    [InlineData(520, 500, -20)]   // thiếu 20 tờ so với sổ
    [InlineData(100, 130, 30)]    // thừa so với sổ
    [InlineData(80, 80, 0)]
    public void ChenhLech_BangSoDemTruTonTheoSo(int tonTheoSo, int soDem, int chenhLech)
        => Assert.Equal(chenhLech, QuyTacKiemKe.ChenhLech(tonTheoSo, soDem));

    [Fact]
    public void LechThiBatGhiLyDo_KhongLechThiKhongCan()
    {
        Assert.NotNull(QuyTacKiemKe.LoiLyDo(-20, null));
        Assert.NotNull(QuyTacKiemKe.LoiLyDo(5, "abc"));        // lý do quá ngắn
        Assert.Null(QuyTacKiemKe.LoiLyDo(-20, "Ẩm hỏng 1 ram"));
        Assert.Null(QuyTacKiemKe.LoiLyDo(0, null));
    }

    [Fact]
    public void Duyet_CongChenhLechVaoTonHienTai_KhongGhiDeBangSoDem()
    {
        // Đếm 500 khi sổ 520 (lệch -20). Trước lúc CEO duyệt in thêm 100 tờ → sổ còn 420.
        // Ghi đè thành 500 sẽ sai (100 tờ vừa in đã ra khỏi kho); cộng chênh lệch thì còn 400.
        Assert.Equal(400, QuyTacKiemKe.TonSauDuyet(420, -20));
    }

    [Fact]
    public void TienHaoHut_ChiTinhPhanThieu()
    {
        Assert.Equal(4000m, QuyTacKiemKe.TienHaoHut(-20, 200m));
        Assert.Equal(0m, QuyTacKiemKe.TienHaoHut(15, 200m));
    }

    [Theory]
    [InlineData(1799, 2000, false)]
    [InlineData(1800, 2000, true)]    // 90% năng suất thì báo sắp hết
    [InlineData(2500, 2000, true)]
    public void SapHetMuc_Tu90PhanTramNangSuat(int soTrang, int nangSuat, bool sapHet)
        => Assert.Equal(sapHet, QuyTacHopMuc.SapHet(soTrang, nangSuat));

    [Theory]
    [InlineData(300, 2000, true)]     // hộp cũ mới in 15% đã thay → nghi vấn
    [InlineData(999, 2000, true)]
    [InlineData(1000, 2000, false)]   // từ 50% trở lên là bình thường
    [InlineData(2100, 2000, false)]
    public void NghiVan_KhiHopCuHetDuoi50PhanTram(int soTrangHopCu, int nangSuat, bool nghiVan)
        => Assert.Equal(nghiVan, QuyTacHopMuc.NghiVan(soTrangHopCu, nangSuat));

    [Fact]
    public void HopMucDto_TinhSoTrangHopHienTaiTuBoDemMay()
    {
        var hop = new HopMucMayInDto { SoTrangDaIn = 12500, SoTrangLucThayMuc = 10700, NangSuatHopMuc = 2000 };
        Assert.Equal(1800, hop.SoTrangHopHienTai);
        Assert.Equal(90, hop.PhanTramDaDung);
        Assert.True(hop.SapHet);
    }

    [Theory]
    [InlineData(nameof(QuanTriController.TaoPhieuKiemKe), "kiem-ke", "SUPER_ADMIN,BRANCH_ADMIN")]
    [InlineData(nameof(QuanTriController.DuyetPhieuKiemKe), "kiem-ke/{maPhieuKiemKe}/duyet", "SUPER_ADMIN")]
    [InlineData(nameof(QuanTriController.ThayMuc), "may-in/{maMayIn}/thay-muc", "SUPER_ADMIN,BRANCH_ADMIN,STAFF")]
    public void Api_DungDuongDanVaVaiTro(string tenHam, string duongDan, string vaiTro)
    {
        MethodInfo m = typeof(QuanTriController).GetMethod(tenHam)!;
        Assert.Equal(duongDan, m.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal(vaiTro, m.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public void CauHinhHopMuc_ChiQuanLyVaCeo()
    {
        MethodInfo m = typeof(QuanTriController).GetMethod(nameof(QuanTriController.CauHinhHopMuc))!;
        Assert.Equal("SUPER_ADMIN,BRANCH_ADMIN", m.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Theory]
    [InlineData("034-PhieuKiemKe.sql", "UX_PhieuKiemKe_MotPhieuChoDuyet")]
    [InlineData("035-HopMucMayIn.sql", "LichSuThayMuc")]
    public void ScriptCsdl_CoMatVaChayDuocNhieuLan(string tenFile, string doiTuong)
    {
        // Đi ngược từ thư mục chứa file test này lên tới thư mục có Database/ (không phụ thuộc nơi build ra).
        string duongDan = null;
        for (var d = new DirectoryInfo(ThuMucFileNay()); d != null && duongDan == null; d = d.Parent)
        {
            string thu = Path.Combine(d.FullName, "Database", tenFile);
            if (File.Exists(thu)) duongDan = thu;
        }
        Assert.NotNull(duongDan);
        string sql = File.ReadAllText(duongDan);
        Assert.Contains(doiTuong, sql);
        Assert.Contains("IF OBJECT_ID", sql);              // chạy lại không tạo trùng
        Assert.Contains("SET QUOTED_IDENTIFIER ON", sql);
    }

    private static string ThuMucFileNay([System.Runtime.CompilerServices.CallerFilePath] string duongDan = "")
        => Path.GetDirectoryName(duongDan)!;
}
