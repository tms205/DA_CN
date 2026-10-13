using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class QuanLyDanhMucValidationTests
{
    [Theory]
    [InlineData("vi-VN")]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void BanKinhChiNhanh_GioiHanDungBatKeNgonNgu(string ngonNgu)
    {
        var cu = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(ngonNgu);
            var range = typeof(ChiNhanhQuanTriDto).GetProperty(nameof(ChiNhanhQuanTriDto.BanKinhGiao)).GetCustomAttribute<RangeAttribute>();
            Assert.True(range.IsValid(10.25m));
            Assert.True(range.IsValid(999.99m));
            Assert.False(range.IsValid(1000m));
            Assert.False(range.IsValid(-0.01m));
        }
        finally { CultureInfo.CurrentCulture = cu; }
    }
}
