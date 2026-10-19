using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PhotocopyAdmin.Pages;
using PhotocopyShared.Dtos.DanhMuc;
using Xunit;

namespace PhotocopyApi.Tests;

public class ChonGiayVatTuTests
{
    [Fact]
    public void TaiQuay_HaiVatTuCungQuyCach_KhongGopThanhMotGiay()
    {
        var ds = JsonSerializer.Deserialize<List<DichVuDto>>(@"[
            {""MaDichVu"":101,""LoaiDichVu"":""IN_AN"",""KhoGiay"":""A4"",""LoaiGiay"":""100gsm"",""MatIn"":""1_MAT"",""MauSac"":""DEN_TRANG"",""MaVatTu"":21,""TenVatTu"":""Giấy A4 100gsm loại A""},
            {""MaDichVu"":102,""LoaiDichVu"":""IN_AN"",""KhoGiay"":""A4"",""LoaiGiay"":""100gsm"",""MatIn"":""1_MAT"",""MauSac"":""DEN_TRANG"",""MaVatTu"":22,""TenVatTu"":""Giấy A4 100gsm loại B""}
        ]");
        const BindingFlags rieng = BindingFlags.NonPublic | BindingFlags.Instance;
        var trang = new LapDonTaiQuay();
        typeof(LapDonTaiQuay).GetField("_dsDichVu", rieng).SetValue(trang, ds);
        var giay = (List<DichVuDto>)typeof(LapDonTaiQuay).GetMethod("DsKhoGiay", rieng).Invoke(trang, null);
        Assert.Equal(2, giay.Count);
        var khoa = typeof(LapDonTaiQuay).GetMethod("KhoaGiay", rieng);
        string khoaB = (string)khoa.Invoke(trang, new object[] { ds[1] });
        typeof(LapDonTaiQuay).GetMethod("DoiKhoGiay", rieng).Invoke(trang, new[] { new ChangeEventArgs { Value = khoaB } });
        Assert.Equal(102, typeof(LapDonTaiQuay).GetField("_maDichVu", rieng).GetValue(trang));
    }
}
