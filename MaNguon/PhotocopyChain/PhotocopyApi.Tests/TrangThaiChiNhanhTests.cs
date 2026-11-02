using System.ComponentModel.DataAnnotations;
using PhotocopyShared.Dtos.Tuan3;
using Xunit;

namespace PhotocopyApi.Tests;

public class TrangThaiChiNhanhTests
{
    [Theory]
    [InlineData("HOAT_DONG",true)]
    [InlineData("TAM_NGUNG",true)]
    [InlineData("DONG_CUA",true)]
    [InlineData("DONG",false)]
    [InlineData("",false)]
    public void ChoPhepDongCua_VaVanChanTrangThaiSai(string trangThai,bool hopLe)
    {
        var req = new ChiNhanhQuanTriDto {
            TenChiNhanh="Chi nhánh thử",DiaChi="123 Đường thử",TrangThai=trangThai,
            GioMoCua=TimeSpan.FromHours(7.5),GioDongCua=TimeSpan.FromHours(21.5),BanKinhGiao=10
        };
        var errors=new List<ValidationResult>();
        Assert.Equal(hopLe,Validator.TryValidateObject(req,new ValidationContext(req),errors,true));
    }
}
