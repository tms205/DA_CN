using PhotocopyShared.Dtos.MayInAo;

namespace PhotocopyShared.MayInAo;

public static class QuyTacMayInThat
{
    // Không suy đoán năng lực từ tên model hoặc danh mục máy ảo.
    public static string? KiemTra(HangDoiWindowsDto? queue, IEnumerable<DongLenhInDto> dongIn)
    {
        if (queue == null || queue.LaMayAo) return "Chưa xác minh được queue máy in thật.";
        if (queue.KhoGiayHoTro == null) return "Driver chưa cung cấp khổ giấy. Làm mới thiết bị trước khi gửi in thật.";
        foreach (var dong in dongIn)
        {
            if (!queue.KhoGiayHoTro.Contains(dong.KhoGiay, StringComparer.OrdinalIgnoreCase))
                return $"Máy in thật không hỗ trợ khổ {dong.KhoGiay}; driver công bố: {string.Join(", ", queue.KhoGiayHoTro)}.";
            if (dong.MauSac == "MAU" && queue.InMau != true)
                return "Driver không xác nhận máy thật hỗ trợ in màu.";
            if (dong.MatIn == "2_MAT" && queue.InHaiMat != true)
                return "Driver không xác nhận máy thật hỗ trợ in hai mặt.";
        }
        return null;
    }
}
