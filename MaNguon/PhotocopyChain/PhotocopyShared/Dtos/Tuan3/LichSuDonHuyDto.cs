namespace PhotocopyShared.Dtos.Tuan3;

public sealed class LichSuDonHuyDto
{
    public List<HoanTienChoDuyetDto> Items { get; set; } = new();
    public int Total { get; set; }
}
