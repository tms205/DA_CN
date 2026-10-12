namespace PhotocopyShared.Constants;

/// <summary>Tên vai trò — trùng cột <c>VaiTro.TenVaiTro</c> trong CSDL.</summary>
public static class VaiTro
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string BranchAdmin = "BRANCH_ADMIN";
    public const string Staff = "STAFF";
    public const string Shipper = "SHIPPER";
    public const string Customer = "CUSTOMER";

    /// <summary>Dùng trong <c>[Authorize(Roles = VaiTro.NhomWeb)]</c>.</summary>
    public const string NhomWeb = SuperAdmin + "," + BranchAdmin + "," + Staff;
    public const string NhomMobile = Customer + "," + Shipper;

    /// <summary>Vai trò được đăng nhập Web quản trị.</summary>
    public static readonly string[] Web = [SuperAdmin, BranchAdmin, Staff];

    /// <summary>Vai trò được đăng nhập App (dùng chung 1 màn đăng nhập).</summary>
    public static readonly string[] Mobile = [Customer, Shipper];

    public static string TenHienThi(string vaiTro) => vaiTro switch
    {
        SuperAdmin => "Chủ chuỗi",
        BranchAdmin => "Quản lý chi nhánh",
        Staff => "Nhân viên in ấn",
        Shipper => "Nhân viên giao hàng",
        Customer => "Khách hàng",
        _ => vaiTro
    };
}
