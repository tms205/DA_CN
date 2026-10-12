namespace PhotocopyApi.Models;

// Kết quả Ahamove dùng chung cho BaoGiaController và DonHangController.
// Phải là cùng một kiểu để hai controller đọc được cùng bản ghi trong IMemoryCache.
public sealed record KetQuaPhiGiaoHang(
    decimal KhoangCachKm,
    decimal PhiShip,
    int ThoiGianDuKienGiay);
