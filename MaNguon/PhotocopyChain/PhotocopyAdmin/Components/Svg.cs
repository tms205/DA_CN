namespace PhotocopyAdmin.Components;

/// <summary>Icon nét (stroke) dùng chung, giữ cùng phong cách với bộ icon Figma.</summary>
public static class Svg
{
    public static string BieuMau(int co = 18) => Tao(co, """<path d="M8 3h9l4 4v14H8z"/><path d="M17 3v5h4M4 7H2v14h3M11 12h7M11 16h7"/>""");
    private static string Tao(int co, string duong, double net = 1.7) =>
        $"""<svg width="{co}" height="{co}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="{net.ToString(System.Globalization.CultureInfo.InvariantCulture)}" stroke-linecap="round" stroke-linejoin="round">{duong}</svg>""";

    public static string MayIn(int co = 24) => Tao(co, """<path d="M6 9V2h12v7"/><path d="M6 18H4a2 2 0 01-2-2v-5a2 2 0 012-2h16a2 2 0 012 2v5a2 2 0 01-2 2h-2"/><path d="M6 14h12v8H6z"/>""");
    public static string Khien(int co = 17) => Tao(co, """<path d="M12 2l8 4v6c0 5-3.4 9.2-8 10-4.6-.8-8-5-8-10V6z"/><path d="M9 12l2 2 4-4"/>""", 1.8);
    public static string ToaNha(int co = 17) => Tao(co, """<path d="M3 21h18"/><path d="M5 21V7l7-4 7 4v14"/><path d="M10 21v-5h4v5"/>""", 1.8);
    public static string XeGiao(int co = 17) => Tao(co, """<path d="M1 3h13v13H1z"/><path d="M14 8h4l3 3v5h-7z"/><circle cx="5.5" cy="18.5" r="2.5"/><circle cx="17.5" cy="18.5" r="2.5"/>""", 1.8);
    public static string Khoa(int co = 14) => Tao(co, """<rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0110 0v4"/>""", 1.9);
    public static string Thu(int co = 18) => Tao(co, """<rect x="2" y="4" width="20" height="16" rx="2"/><path d="M2 7l10 6 10-6"/>""");
    public static string Mat(int co = 18) => Tao(co, """<path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/>""");
    public static string MatGach(int co = 18) => Tao(co, """<path d="M3 3l18 18"/><path d="M10.6 5.1A10 10 0 0112 5c6.4 0 10 7 10 7a17 17 0 01-3.2 4.1"/><path d="M6.6 6.6A17 17 0 002 12s3.6 7 10 7a9.6 9.6 0 005.4-1.6"/><path d="M9.9 9.9a3 3 0 004.2 4.2"/>""");
    public static string DauTich(int co = 17) => Tao(co, """<path d="M20 6L9 17l-5-5"/>""", 2.6);
    public static string MuiTenPhai(int co = 18) => Tao(co, """<path d="M5 12h14"/><path d="M13 6l6 6-6 6"/>""", 2.2);
    public static string MuiTenTrai(int co = 16) => Tao(co, """<path d="M19 12H5"/><path d="M11 6l-6 6 6 6"/>""", 2.2);
    public static string CanhBao(int co = 16) => Tao(co, """<path d="M12 9v4"/><path d="M12 17v.01"/><path d="M10.3 3.9L2 18a2 2 0 001.7 3h16.6a2 2 0 001.7-3L13.7 3.9a2 2 0 00-3.4 0z"/>""", 2);
    public static string ChamThan(int co = 15) => Tao(co, """<circle cx="12" cy="12" r="10"/><path d="M12 8v5"/><path d="M12 16.5v.01"/>""", 2.1);
    public static string ThongTin(int co = 14) => Tao(co, """<circle cx="12" cy="12" r="10"/><path d="M12 16v-5"/><path d="M12 8v.01"/>""", 1.9);
    public static string CuaHang(int co = 15) => Tao(co, """<path d="M3 9l1.5-5h15L21 9"/><path d="M4 9v11h16V9"/><path d="M3 9a3 3 0 006 0 3 3 0 006 0 3 3 0 006 0"/>""");
    public static string MuiTenXuong(int co = 14) => Tao(co, """<path d="M6 9l6 6 6-6"/>""", 2);
    public static string NguoiMot(int co = 18) => Tao(co, """<path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2"/><circle cx="12" cy="7" r="4"/>""");
    public static string DienThoai(int co = 18) => Tao(co, """<path d="M22 16.9v3a2 2 0 01-2.2 2 19.8 19.8 0 01-8.6-3.1 19.5 19.5 0 01-6-6A19.8 19.8 0 012.1 4.2 2 2 0 014.1 2h3a2 2 0 012 1.7c.1.9.4 1.8.7 2.7a2 2 0 01-.5 2.1L8 9.8a16 16 0 006 6l1.3-1.3a2 2 0 012.1-.4c.9.3 1.8.6 2.7.7a2 2 0 011.7 2z"/>""");
    public static string KinhLup(int co = 16) => Tao(co, """<circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/>""", 1.9);
    public static string DangXuat(int co = 16) => Tao(co, """<path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4"/><path d="M16 17l5-5-5-5"/><path d="M21 12H9"/>""", 1.8);

    // Menu sidebar (Figma HangDoiDon)
    public static string DichVu(int co = 18) => Tao(co, """<path d="M4 3h16v18H4z"/><path d="M8 7h8M8 12h2M14 12h2M8 17h2M14 17h2"/>""");
    public static string BangDieuKhien(int co = 18) => Tao(co, """<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>""");
    public static string DongHoDo(int co = 18) => Tao(co, """<path d="M4.5 19a9 9 0 1115 0"/><path d="M12 3v3M3 12h3M18 12h3"/><path d="M12 12l4-4"/><circle cx="12" cy="12" r="1"/><path d="M7 19h10"/>""");
    public static string QuyTrinh(int co = 18) => Tao(co, """<rect x="3" y="3" width="6" height="6" rx="1"/><rect x="15" y="15" width="6" height="6" rx="1"/><path d="M9 6h6a3 3 0 013 3v6M15 12l3 3 3-3M6 9v12h6"/>""");
    public static string SoDoQuanLy(int co = 18) => Tao(co, """<rect x="9" y="3" width="6" height="5" rx="1"/><rect x="2" y="16" width="6" height="5" rx="1"/><rect x="16" y="16" width="6" height="5" rx="1"/><path d="M12 8v4M5 16v-4h14v4"/>""");
    public static string CaiDat(int co = 18) => Tao(co, """<path d="M10 2h4v2.5l2 1 2-2 2.5 2.5-2 2 1 2H22v4h-2.5l-1 2 2 2-2.5 2.5-2-2-2 1V22h-4v-2.5l-2-1-2 2-2.5-2.5 2-2-1-2H2v-4h2.5l1-2-2-2L6 3.5l2 2 2-1z"/><circle cx="12" cy="12" r="3"/>""");
    public static string ViTien(int co = 18) => Tao(co, """<path d="M20 7V5a2 2 0 00-2-2H5a3 3 0 000 6h15v11H5a3 3 0 01-3-3V6"/><path d="M20 11h-5a2 2 0 000 4h5"/><path d="M16 13h.01"/>""");
    public static string LichSu(int co = 18) => Tao(co, """<path d="M3 11a9 9 0 112.6 7"/><path d="M3 4v7h7M12 7v5l3 2"/>""");
    public static string NgoiSao(int co = 18) => Tao(co, """<path d="M12 3l2.8 5.7 6.2.9-4.5 4.4 1.1 6.2-5.6-2.9-5.6 2.9 1.1-6.2L3 9.6l6.2-.9z"/>""");
    public static string NhanGia(int co = 18) => Tao(co, """<path d="M3 3h8l10 10-8 8L3 11z"/><circle cx="7" cy="7" r="1"/>""");
    public static string VeKhuyenMai(int co = 18) => Tao(co, """<path d="M3 5h18v5a2 2 0 000 4v5H3v-5a2 2 0 000-4z"/><path d="M9 15l6-6"/><circle cx="9" cy="9" r="1"/><circle cx="15" cy="15" r="1"/>""");
    public static string DanhSach(int co = 17) => Tao(co, """<path d="M3 5h18"/><path d="M3 12h18"/><path d="M3 19h12"/>""", 1.8);
    public static string TaiLieu(int co = 17) => Tao(co, """<path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><path d="M14 2v6h6"/>""");
    public static string CapTap(int co = 17) => Tao(co, """<path d="M20 7H4a2 2 0 00-2 2v8a2 2 0 002 2h16a2 2 0 002-2V9a2 2 0 00-2-2z"/><path d="M16 7V5a2 2 0 00-2-2h-4a2 2 0 00-2 2v2"/>""");
    public static string TienMat(int co = 17) => Tao(co, """<rect x="2" y="6" width="20" height="12" rx="2"/><circle cx="12" cy="12" r="2.5"/>""");
    public static string HoaDon(int co = 17) => Tao(co, """<path d="M6 2h12v20l-3-2-3 2-3-2-3 2z"/><path d="M9 7h6"/><path d="M9 11h6"/><path d="M9 15h4"/>""");
    public static string DongHo(int co = 17) => Tao(co, """<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>""");
    public static string LopKho(int co = 17) => Tao(co, """<path d="M21 8l-9-5-9 5 9 5 9-5z"/><path d="M3 12l9 5 9-5"/><path d="M3 16l9 5 9-5"/>""");
    public static string BieuDoCot(int co = 17) => Tao(co, """<path d="M4 20V10"/><path d="M10 20V4"/><path d="M16 20v-7"/><path d="M22 20H2"/>""");
    public static string BieuDoDuong(int co = 17) => Tao(co, """<path d="M3 3v18h18"/><path d="M7 15l4-5 3 3 5-7"/>""");
    public static string NguoiDung(int co = 17) => Tao(co, """<path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 00-3-3.9"/><path d="M16 3.1a4 4 0 010 7.8"/>""");
    public static string DuyetHoSo(int co = 17) => Tao(co, """<path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11"/>""");

    // Màn Hàng đợi đơn / Bàn giao / Ca làm việc (Tuần 2)
    public static string TaiXuong(int co = 14) => Tao(co, """<path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/><path d="M7 10l5 5 5-5"/><path d="M12 15V3"/>""", 1.9);
    public static string TaiLai(int co = 15) => Tao(co, """<path d="M21 12a9 9 0 11-2.6-6.4L21 8"/><path d="M21 3v5h-5"/>""", 1.9);
    public static string TiaSet(int co = 13) => Tao(co, """<path d="M13 2L4.5 13.5H11l-1 8.5 8.5-11.5H12l1-8.5z"/>""", 1.8);
    public static string Song(int co = 13) => Tao(co, """<path d="M5 12.5a9.5 9.5 0 0114 0"/><path d="M8.5 16a5 5 0 017 0"/><path d="M12 19.5v.01"/>""", 2);

    public const string Google = """<svg width="20" height="20" viewBox="0 0 48 48"><path fill="#4285F4" d="M45.1 24.5c0-1.6-.1-3.1-.4-4.5H24v8.5h11.8c-.5 2.7-2.1 5-4.4 6.6v5.5h7.1c4.2-3.8 6.6-9.5 6.6-16.1z"/><path fill="#34A853" d="M24 46c6 0 11-2 14.6-5.4l-7.1-5.5c-2 1.3-4.5 2.1-7.5 2.1-5.8 0-10.7-3.9-12.4-9.1H4.3v5.7C7.9 41 15.3 46 24 46z"/><path fill="#FBBC05" d="M11.6 28.1c-.4-1.3-.7-2.7-.7-4.1s.3-2.8.7-4.1v-5.7H4.3C2.8 17.2 2 20.5 2 24s.8 6.8 2.3 9.8l7.3-5.7z"/><path fill="#EA4335" d="M24 10.8c3.3 0 6.2 1.1 8.5 3.3l6.3-6.3C35 4.2 30 2 24 2 15.3 2 7.9 7 4.3 14.2l7.3 5.7c1.7-5.2 6.6-9.1 12.4-9.1z"/></svg>""";
}
