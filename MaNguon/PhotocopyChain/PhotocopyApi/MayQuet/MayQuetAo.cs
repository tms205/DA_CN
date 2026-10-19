using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyApi.MayQuet;

public sealed class MayQuetAo : IMayQuet
{
    public async Task<AnhQuetDto> QuetAsync(LenhQuet lenh, CancellationToken dung)
    {
        // Chỉ là thời gian mô phỏng; không dùng đồng hồ này để báo máy thật đã quét xong.
        await Task.Delay(600, dung);
        dung.ThrowIfCancellationRequested();
        return AnhQuetXuLy.ChuanHoa(lenh.AnhMoPhong, lenh.QuyCach);
    }
}
