using Dapper;
using PhotocopyApi.DichVu;
using PhotocopyApi.BaoTri;
using Microsoft.Data.SqlClient;

namespace PhotocopyApi;

// mỗi phút hủy đơn VietQR quá hạn và dọn file của mọi đơn đã kết thúc.
// File chỉ bị xóa khi đơn HOAN_THANH / DA_HUY và không còn đơn đang xử lý nào khác dùng file đó.
// Đơn VietQR tự hủy do quá hạn được giữ file thêm 24 giờ để còn đối soát khi webhook tới muộn.
//
// Đây KHÔNG phải lớp Service kiểu kiến trúc nhiều tầng — chỉ là "hẹn giờ chạy ngầm" mà ASP.NET Core bắt buộc
// phải viết thành class (đăng ký bằng AddHostedService trong Program.cs).
public class DonQuaHanJob : BackgroundService
{
    private readonly string _chuoiKetNoi;
    private readonly ILogger<DonQuaHanJob> _logger;
    private readonly BaoTriStore _baoTri;
    private readonly FileInLifecycleService _files;

    public DonQuaHanJob(IConfiguration config, ILogger<DonQuaHanJob> logger, BaoTriStore baoTri = null)
    {
        _chuoiKetNoi = config.GetConnectionString("PhotocopyChain");
        _files = new FileInLifecycleService(config);
        _logger = logger;
        _baoTri = baoTri;
    }

    // Hàm này chạy suốt từ lúc API bật tới lúc tắt
    protected override async Task ExecuteAsync(CancellationToken dungLai)
    {
        while (!dungLai.IsCancellationRequested)
        {
            try
            {
                if (BaoTriStore.KhoaTacVuNen(_baoTri?.Read(),"DON_HANG")) { await Task.Delay(TimeSpan.FromMinutes(1), dungLai); continue; }
                using var conn = new SqlConnection(_chuoiKetNoi);
                await conn.ExecuteAsync("sp_HuyDonQuaHan", commandType: System.Data.CommandType.StoredProcedure);
                if(!BaoTriStore.KhoaTacVuNen(_baoTri?.Read(),"FILE_IN")) {
                    await _files.Quet(dungLai);
                }
            }
            catch (Exception ex)
            {
                // Mất kết nối CSDL thì chỉ ghi ra cửa sổ console rồi phút sau thử lại, không được làm sập API
                _logger.LogWarning("Không chạy được job hủy đơn quá hạn / dọn file: {LoiChiTiet}", ex.Message);
            }

            // Nghỉ 1 phút rồi chạy tiếp. Lúc tắt API thì Task.Delay bị hủy → thoát vòng lặp.
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), dungLai);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

}
