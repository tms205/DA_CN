namespace PhotocopyShared.Dtos;

/// <summary>Mọi lỗi API trả về đúng một dạng: <c>{ "message": "..." }</c>.</summary>
public sealed class ApiError
{
    public string Message { get; set; } = "";
}

/// <summary>Kết quả chỉ có một câu thông báo (vd: đã gửi OTP).</summary>
public sealed class KetQuaDto
{
    public string Message { get; set; } = "";
}
