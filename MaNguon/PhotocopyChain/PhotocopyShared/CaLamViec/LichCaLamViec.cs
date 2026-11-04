namespace PhotocopyShared.CaLamViec;

public static class LichCaLamViec
{
    // Giữ giờ mở thực tế để đối soát tiền; ngày nghiệp vụ chỉ dùng để nhóm/hiển thị ca.
    public static DateTime NgayLamViec(DateTime gioMo) => gioMo.TimeOfDay >= new TimeSpan(21,30,0)
        ? gioMo.Date.AddDays(1) : gioMo.Date;

    public static DateTime GioKetThuc(DateTime gioMo)
    {
        var t=gioMo.TimeOfDay;
        if(t>=new TimeSpan(21,30,0)) return gioMo.Date.AddDays(1).AddMinutes(750);
        return gioMo.Date.AddMinutes(t<new TimeSpan(12,30,0)?750:t<new TimeSpan(17,30,0)?1050:1290);
    }

    public static bool LaThongDem(DateTime gioMo) => gioMo.TimeOfDay < new TimeSpan(7,30,0)
        || gioMo.TimeOfDay >= new TimeSpan(21,30,0);
    public static string TenCa(DateTime gioMo) => LaThongDem(gioMo)?"Ca sáng · Thông đêm":
        gioMo.TimeOfDay < new TimeSpan(12,30,0)?"Ca sáng":gioMo.TimeOfDay < new TimeSpan(17,30,0)?"Ca chiều":"Ca tối";
}
