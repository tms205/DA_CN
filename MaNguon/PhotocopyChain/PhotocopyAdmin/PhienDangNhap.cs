using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using PhotocopyShared.Constants;
using PhotocopyShared.Dtos.Auth;

namespace PhotocopyAdmin;

// Giữ phiên đăng nhập của Web: token + thông tin người đang đăng nhập.
//  - Lưu vào trình duyệt để bấm F5 không mất: tick "Duy trì phiên" → localStorage (đóng trình duyệt vẫn còn),
//    không tick → sessionStorage (đóng tab là mất).
//  - Gắn token vào HttpClient để mọi lần gọi API tự có "Authorization: Bearer <token>".
//  - Kế thừa AuthenticationStateProvider để Blazor biết ai đang đăng nhập → [Authorize(Roles = ...)] trên trang hoạt động.
public class PhienDangNhap : AuthenticationStateProvider
{
    private const string TenKhoa = "photocopychain.phien";

    // Tên thuộc tính dạng camelCase (accessToken, nguoiDung...) giống JSON của API
    private static readonly JsonSerializerOptions KieuJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private readonly PhamViChiNhanh _phamVi;
    private DangNhapResponse _phien;           // null = chưa đăng nhập
    private bool _daDocTuTrinhDuyet = false;

    public PhienDangNhap(IJSRuntime js, HttpClient http, PhamViChiNhanh phamVi)
    {
        _js = js;
        _http = http;
        _phamVi = phamVi;
    }

    // Người đang đăng nhập (null nếu chưa đăng nhập) — các trang đọc để hiện tên, vai trò, chi nhánh
    public NguoiDungDto NguoiDung
    {
        get { return _phien == null ? null : _phien.NguoiDung; }
    }

    public DateTime? HetHanLuc
    {
        get
        {
            if (_phien == null) return null;
            return _phien.HetHanLuc;
        }
    }

    // Token để nối SignalR (mở WebSocket không gửi được header Authorization nên phải đính vào địa chỉ)
    public string AccessToken
    {
        get { return _phien == null ? null : _phien.AccessToken; }
    }

    // Blazor gọi hàm này mỗi khi cần biết ai đang đăng nhập (mở trang có [Authorize]...)
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Khôi phục phạm vi trước request đầu tiên sau khi F5 để API lọc đúng chi nhánh ngay từ đầu.
        await _phamVi.KhoiTaoAsync();

        // Lần đầu mở web (hoặc bấm F5): đọc phiên đã lưu trong trình duyệt
        if (!_daDocTuTrinhDuyet)
        {
            _daDocTuTrinhDuyet = true;
            string json = await _js.InvokeAsync<string>("localStorage.getItem", TenKhoa);
            if (json == null)
                json = await _js.InvokeAsync<string>("sessionStorage.getItem", TenKhoa);

            if (json != null)
            {
                try
                {
                    _phien = JsonSerializer.Deserialize<DangNhapResponse>(json, KieuJson);
                    GanToken();
                }
                catch
                {
                    _phien = null;   // dữ liệu cũ bị hỏng thì coi như chưa đăng nhập
                }
            }
        }

        // Token hết hạn thì coi như chưa đăng nhập
        if (_phien != null && (_phien.NguoiDung == null || _phien.HetHanLuc <= DateTime.UtcNow))
            await XoaPhien();

        if (_phien == null)
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));   // người lạ, chưa đăng nhập

        return new AuthenticationState(TaoNguoiDung(_phien.NguoiDung));
    }

    // Gọi sau khi API đăng nhập trả về thành công
    public async Task DangNhap(DangNhapResponse phien, bool ghiNho)
    {
        _phien = phien;
        _daDocTuTrinhDuyet = true;

        string json = JsonSerializer.Serialize(phien, KieuJson);
        await _js.InvokeVoidAsync("localStorage.removeItem", TenKhoa);
        await _js.InvokeVoidAsync("sessionStorage.removeItem", TenKhoa);
        if (ghiNho)
            await _js.InvokeVoidAsync("localStorage.setItem", TenKhoa, json);
        else
            await _js.InvokeVoidAsync("sessionStorage.setItem", TenKhoa, json);

        GanToken();
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());   // báo Blazor: đã có người đăng nhập
    }

    // Sửa hồ sơ xong: đổi tên hiển thị ở sidebar ngay, giữ nguyên token (không phải đăng nhập lại)
    public async Task CapNhatNguoiDung(NguoiDungDto nd)
    {
        if (_phien == null) return;
        _phien.NguoiDung = nd;

        // Ghi đè vào đúng chỗ đang lưu (localStorage hoặc sessionStorage)
        string json = JsonSerializer.Serialize(_phien, KieuJson);
        string dangLuuTrongLocal = await _js.InvokeAsync<string>("localStorage.getItem", TenKhoa);
        if (dangLuuTrongLocal != null)
            await _js.InvokeVoidAsync("localStorage.setItem", TenKhoa, json);
        else
            await _js.InvokeVoidAsync("sessionStorage.setItem", TenKhoa, json);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task DangXuat()
    {
        await XoaPhien();
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task XoaPhien()
    {
        _phien = null;
        _http.DefaultRequestHeaders.Authorization = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", TenKhoa);
        await _js.InvokeVoidAsync("sessionStorage.removeItem", TenKhoa);
    }

    // Từ giờ mọi lần gọi API bằng HttpClient đều kèm header "Authorization: Bearer <token>"
    private void GanToken()
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _phien.AccessToken);
    }

    // Dựng "người dùng" cho Blazor: tên + vai trò (để [Authorize(Roles = ...)] so vai trò)
    private ClaimsPrincipal TaoNguoiDung(NguoiDungDto nd)
    {
        var claims = new List<Claim>();
        claims.Add(new Claim(TenClaim.MaNguoiDung, nd.MaNguoiDung.ToString()));
        claims.Add(new Claim(TenClaim.HoTen, nd.HoTen));
        claims.Add(new Claim(TenClaim.VaiTro, nd.TenVaiTro));

        // "jwt" = đã xác thực; 2 tham số cuối cho Blazor biết claim nào là tên, claim nào là vai trò
        var identity = new ClaimsIdentity(claims, "jwt", TenClaim.HoTen, TenClaim.VaiTro);
        return new ClaimsPrincipal(identity);
    }
}
