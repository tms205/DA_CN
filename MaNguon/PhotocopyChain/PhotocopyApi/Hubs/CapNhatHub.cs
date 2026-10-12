using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PhotocopyShared.Constants;

namespace PhotocopyApi.Hubs;

// F-32: kênh real-time (SignalR). Web quản trị và App mở sẵn một đường nối tới /hubs/cap-nhat,
// mỗi khi có đơn mới / đổi trạng thái / nhận được tiền thì máy chủ "đẩy" tin xuống, màn hình tự cập nhật
// (không phải bấm F5, không phải hẹn giờ gọi lại API).
//
// Mỗi người nối vào được xếp vào các NHÓM để gửi cho đúng người:
//   - "nguoi-dung-8"  : riêng một người (khách hàng xem đơn của mình)
//   - "chi-nhanh-1"   : mọi nhân viên đang trực Cơ sở 1
//   - "toan-chuoi"    : Super Admin (xem được mọi chi nhánh)
[Authorize]
public class CapNhatHub : Hub
{
    // Blazor / App vừa nối vào: đọc token để biết là ai rồi xếp vào nhóm
    public override async Task OnConnectedAsync()
    {
        string maNguoiDung = Context.User.FindFirst(TenClaim.MaNguoiDung).Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, NhomNguoiDung(int.Parse(maNguoiDung)));

        // Nhân viên thì thuộc 1 chi nhánh, khách hàng và Super Admin thì không
        var claimChiNhanh = Context.User.FindFirst(TenClaim.MaChiNhanh);
        if (claimChiNhanh != null)
            await Groups.AddToGroupAsync(Context.ConnectionId, NhomChiNhanh(int.Parse(claimChiNhanh.Value)));

        if (Context.User.IsInRole(VaiTro.SuperAdmin))
            await Groups.AddToGroupAsync(Context.ConnectionId, NhomToanChuoi);

        await base.OnConnectedAsync();
    }

    // ---- Tên các nhóm (đặt một chỗ cho khỏi gõ sai) ----

    public const string NhomToanChuoi = "toan-chuoi";

    public static string NhomNguoiDung(int maNguoiDung)
    {
        return "nguoi-dung-" + maNguoiDung;
    }

    public static string NhomChiNhanh(int maChiNhanh)
    {
        return "chi-nhanh-" + maChiNhanh;
    }

    // ---- Hai hàm gửi tin, controller gọi sau khi ghi dữ liệu thành công ----
    // Viết sẵn ở đây để controller chỉ cần 1 dòng:
    //     await CapNhatHub.GuiChoChiNhanh(_hub, maChiNhanh, HubEvents.OrderCreated, tin);

    // Gửi cho nhân viên của 1 chi nhánh (Super Admin xem toàn chuỗi cũng nhận được)
    public static async Task GuiChoChiNhanh(IHubContext<CapNhatHub> hub, int maChiNhanh, string tenSuKien, object tin)
    {
        await hub.Clients.Group(NhomChiNhanh(maChiNhanh)).SendAsync(tenSuKien, tin);
        await hub.Clients.Group(NhomToanChuoi).SendAsync(tenSuKien, tin);
    }

    // Gửi riêng cho 1 người (khách hàng chủ đơn, shipper…)
    public static async Task GuiChoNguoiDung(IHubContext<CapNhatHub> hub, int maNguoiDung, string tenSuKien, object tin)
    {
        await hub.Clients.Group(NhomNguoiDung(maNguoiDung)).SendAsync(tenSuKien, tin);
    }
}

// Nội dung một tin real-time. Màn hình nhận được thì biết đơn nào vừa đổi để tải lại đúng chỗ.
public class TinCapNhat
{
    public int MaDonHang { get; set; }
    public string MaDonHangHienThi { get; set; }
    public int MaChiNhanh { get; set; }
    /// <summary>Trạng thái đơn sau khi đổi (CHO_XAC_NHAN, DANG_IN…).</summary>
    public string TrangThai { get; set; }
    /// <summary>Câu ngắn hiện lên góc màn hình, vd "Đơn DH00012 vừa được đặt".</summary>
    public string NoiDung { get; set; }
}
