namespace PhotocopyApi.Models;

// Quãng đường Geoapify và phí giao nội bộ dùng chung cho báo giá/đặt đơn.
// Phải là cùng một kiểu để hai controller đọc được cùng bản ghi trong IMemoryCache.
public sealed record KetQuaPhiGiaoHang(
    decimal KhoangCachKm,
    decimal PhiShip,
    int ThoiGianDuKienGiay);
