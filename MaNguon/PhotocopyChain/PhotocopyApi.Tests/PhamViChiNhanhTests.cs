using Microsoft.JSInterop;
using PhotocopyAdmin;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;

public class PhamViChiNhanhTests
{
    [Fact]
    public async Task KhoiTaoAsync_KhoiPhucChiNhanhVaGanHeader()
    {
        var js = new BoNhoTrinhDuyetGia { GiaTri = "3" };
        var http = new HttpClient();
        var phamVi = new PhamViChiNhanh(js, http);

        await phamVi.KhoiTaoAsync();

        Assert.Equal(3, phamVi.MaChiNhanh);
        Assert.Equal("3", http.DefaultRequestHeaders.GetValues(TenHeader.MaChiNhanh).Single());
    }

    [Fact]
    public async Task ChonAsync_ChonToanChuoiThiXoaHeaderVaBoNho()
    {
        var js = new BoNhoTrinhDuyetGia { GiaTri = "3" };
        var http = new HttpClient();
        var phamVi = new PhamViChiNhanh(js, http);
        await phamVi.KhoiTaoAsync();

        await phamVi.ChonAsync(null);

        Assert.Null(phamVi.MaChiNhanh);
        Assert.False(http.DefaultRequestHeaders.Contains(TenHeader.MaChiNhanh));
        Assert.Null(js.GiaTri);
    }

    private sealed class BoNhoTrinhDuyetGia : IJSRuntime
    {
        public string GiaTri { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object[] args)
        {
            if (identifier == "localStorage.getItem")
                return ValueTask.FromResult((TValue)(object)GiaTri);
            if (identifier == "localStorage.setItem")
                GiaTri = args[1]?.ToString();
            if (identifier == "localStorage.removeItem")
                GiaTri = null;
            return ValueTask.FromResult(default(TValue));
        }
    }
}
