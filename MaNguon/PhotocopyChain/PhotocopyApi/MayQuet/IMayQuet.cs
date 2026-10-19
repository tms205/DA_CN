using PhotocopyShared.Dtos.DatIn;

namespace PhotocopyApi.MayQuet;

// Ranh giới nguồn ảnh. Adapter connector thật sau này lấy ảnh tại tiệm thay vì AnhMoPhong,
// rồi dùng chung AnhQuetXuLy và trả AnhQuetDto. Không gọi driver USB từ trình duyệt/API.
public sealed record LenhQuet(byte[] AnhMoPhong, QuyCachQuet QuyCach);

public interface IMayQuet
{
    Task<AnhQuetDto> QuetAsync(LenhQuet lenh, CancellationToken dung);
}
