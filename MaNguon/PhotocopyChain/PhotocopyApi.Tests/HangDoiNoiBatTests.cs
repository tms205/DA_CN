using System.Reflection;
using PhotocopyAdmin;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

public class HangDoiNoiBatTests
{
    [Fact]
    public void QueueSort_UrgentFifoThenNormalFifo_OtherStatesKeepNewestFirst()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var page = new HangDoiDon();
        var start = new DateTime(2026, 11, 4, 9, 0, 0);
        HangDoiDonDto Order(int id, string state, int minutes, bool urgent = false) => new() {
            MaDonHang = id, MaDonHangHienThi = $"DH{id:D5}", TenKhachHang = "Khách thử",
            TrangThaiDonHang = state, NgayDat = start.AddMinutes(minutes), InHoaToc = urgent
        };
        var orders = new List<HangDoiDonDto> {
            Order(12,"DA_IN",40,true), Order(6,"CHO_XAC_NHAN",20,true),
            Order(8,"DA_XAC_NHAN",5), Order(2,"DA_XAC_NHAN",10,true),
            Order(14,"DA_HUY",60), Order(1,"CHO_XAC_NHAN",10,true),
            Order(9,"CHO_XAC_NHAN",5), Order(4,"DA_XAC_NHAN",15,true),
            Order(13,"HOAN_THANH",40), Order(7,"CHO_XAC_NHAN",0),
            Order(10,"DANG_IN",50), Order(11,"DA_GIAO",30,true)
        };
        typeof(HangDoiDon).GetField("_ds",flags)!.SetValue(page,orders);
        var filter = typeof(HangDoiDon).GetMethod("LocDanhSach",flags)!;
        List<HangDoiDonDto> Read() => (List<HangDoiDonDto>)filter.Invoke(page,null)!;
        Assert.Equal(new[]{1,2,4,6,7,8,9,12,14,10,13,11},Read().Select(x=>x.MaDonHang));
        // New urgent/normal arrivals join their own queue; an existing first order stays first.
        orders.Add(Order(15,"CHO_XAC_NHAN",70,true));
        orders.Add(Order(16,"CHO_XAC_NHAN",80));
        Assert.Equal(new[]{1,2,4,6,15,7,8,9,16,12,14,10,13,11},Read().Select(x=>x.MaDonHang));
        var firstPage = (IEnumerable<HangDoiDonDto>)typeof(HangDoiDon).GetMethod("DonCuaTrangHienTai",flags)!.Invoke(page,null)!;
        Assert.Equal(new[]{1,2,4,6,15},firstPage.Select(x=>x.MaDonHang));
        typeof(HangDoiDon).GetField("_loc",flags)!.SetValue(page,"CHO_XAC_NHAN");
        Assert.Equal(new[]{1,6,15,7,9,16},Read().Select(x=>x.MaDonHang));
        typeof(HangDoiDon).GetField("_tuKhoa",flags)!.SetValue(page,"DH00015");
        Assert.Equal(15,Assert.Single(Read()).MaDonHang);
        typeof(HangDoiDon).GetField("_loc",flags)!.SetValue(page,"DA_IN");
        typeof(HangDoiDon).GetField("_tuKhoa",flags)!.SetValue(page,"");
        Assert.Equal(12,Assert.Single(Read()).MaDonHang);
        Assert.Equal("CHO_XAC_NHAN",orders.Single(x=>x.MaDonHang==1).TrangThaiDonHang);
    }

    [Fact]
    public void PrintedQueue_UrgentAndNormalEachFollowFifo_BeforePagination()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var page = new HangDoiDon();
        var start = new DateTime(2026,11,4,9,0,0);
        HangDoiDonDto Printed(int id,int minute,bool urgent) => new() {
            MaDonHang=id,MaDonHangHienThi=$"DH{id:D5}",TenKhachHang="Khách thử",
            TrangThaiDonHang="DA_IN",NgayDat=start.AddMinutes(minute),InHoaToc=urgent
        };
        var orders=new List<HangDoiDonDto> {
            Printed(6,30,true),Printed(4,0,false),Printed(3,20,true),
            Printed(5,10,false),Printed(2,10,true),Printed(1,10,true)
        };
        typeof(HangDoiDon).GetField("_ds",flags)!.SetValue(page,orders);
        typeof(HangDoiDon).GetField("_loc",flags)!.SetValue(page,"DA_IN");
        List<HangDoiDonDto> Read() => (List<HangDoiDonDto>)typeof(HangDoiDon).GetMethod("LocDanhSach",flags)!.Invoke(page,null)!;
        Assert.Equal(new[]{1,2,3,6,4,5},Read().Select(x=>x.MaDonHang));
        orders.Add(Printed(7,40,true));
        Assert.Equal(new[]{1,2,3,6,7,4,5},Read().Select(x=>x.MaDonHang));
        var firstPage=(IEnumerable<HangDoiDonDto>)typeof(HangDoiDon).GetMethod("DonCuaTrangHienTai",flags)!.Invoke(page,null)!;
        Assert.Equal(new[]{1,2,3,6,7},firstPage.Select(x=>x.MaDonHang));
        Assert.All(orders,x=>Assert.Equal("DA_IN",x.TrangThaiDonHang));
    }

    [Theory]
    [InlineData("CHO_XAC_NHAN", "COD", "CHUA_THANH_TOAN", "Cần tiếp nhận", "hd-cho-tiep-nhan")]
    [InlineData("CHO_XAC_NHAN", "TIEN_MAT", "DA_THANH_TOAN", "Cần tiếp nhận", "hd-cho-tiep-nhan")]
    [InlineData("CHO_XAC_NHAN", "VIETQR", "CHUA_THANH_TOAN", "Chờ thanh toán QR", "hd-cho-thanh-toan")]
    [InlineData("CHO_XAC_NHAN", "VIETQR", "DA_THANH_TOAN", "Chờ hệ thống tiếp nhận", "hd-cho-tu-dong")]
    [InlineData("DANG_IN", "COD", "CHUA_THANH_TOAN", "", "")]
    public void ChoTiepNhan_PhanBietViecNhanVienVaThanhToan(string trangThai, string cach, string tien, string nhan, string lopCss)
    {
        var lop = typeof(PhamViChiNhanh).Assembly.GetType("PhotocopyAdmin.QuyTacHangDoi");
        Assert.NotNull(lop);
        var don = new HangDoiDonDto { TrangThaiDonHang = trangThai, PhuongThucThanhToan = cach, TrangThaiThanhToan = tien };
        Assert.Equal(nhan, lop.GetMethod("NhanCho").Invoke(null, new object[] { don }));
        Assert.Equal(lopCss, lop.GetMethod("LopCho").Invoke(null, new object[] { don }));
    }

    [Fact]
    public void XemDonCho_XoaTuKhoaCu_VeTrangMot_KhongDoiDon()
    {
        const BindingFlags rieng = BindingFlags.Instance | BindingFlags.NonPublic;
        var trang = new HangDoiDon();
        var ds = new List<HangDoiDonDto> { new() { MaDonHang = 1, TrangThaiDonHang = "CHO_XAC_NHAN" } };
        typeof(HangDoiDon).GetField("_ds", rieng).SetValue(trang, ds);
        typeof(HangDoiDon).GetField("_tuKhoa", rieng).SetValue(trang, "khong-co-don-nay");
        typeof(HangDoiDon).GetField("_trangDon", rieng).SetValue(trang, 3);
        var ham = typeof(HangDoiDon).GetMethod("XemDonCho", rieng);
        Assert.NotNull(ham);
        ham.Invoke(trang, null);
        Assert.Equal("", typeof(HangDoiDon).GetField("_tuKhoa", rieng).GetValue(trang));
        Assert.Equal("CHO_XAC_NHAN", typeof(HangDoiDon).GetField("_loc", rieng).GetValue(trang));
        Assert.Equal(1, typeof(HangDoiDon).GetField("_trangDon", rieng).GetValue(trang));
        Assert.Equal("CHO_XAC_NHAN", ds[0].TrangThaiDonHang);
    }
}
