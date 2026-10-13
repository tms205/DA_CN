namespace PhotocopyApi.Auth;

public static class QuyTacDangNhap
{
    public static string KiemTraQuyen(string vaiTro, string trangThai, bool laWeb)
    {
        if (trangThai != "HOAT_DONG")
            return "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.";
        bool nhanSu = vaiTro is "SUPER_ADMIN" or "BRANCH_ADMIN" or "STAFF";
        bool khachHoacShipper = vaiTro is "CUSTOMER" or "SHIPPER";
        if (laWeb && nhanSu || !laWeb && khachHoacShipper) return null;
        return laWeb
            ? "Web chỉ dành cho tài khoản nhân sự được cấp. Khách hàng và Shipper sử dụng PhotocopyChain App."
            : "Tài khoản nhân viên vui lòng đăng nhập tại Web quản trị.";
    }
}
