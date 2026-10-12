import 'package:signalr_netcore/signalr_client.dart';

import 'api.dart';
import 'phien.dart';

// F-32: kênh real-time (SignalR). App giữ MỘT đường nối tới /hubs/cap-nhat của máy chủ,
// mở ngay sau khi đăng nhập và đóng khi đăng xuất.
//
// Máy chủ đẩy tin xuống khi đơn của khách đổi trạng thái (quầy tiếp nhận, in xong, giao xong)
// hoặc khi ngân hàng báo đã nhận được tiền. Màn hình nào cần biết thì đăng ký nghe:
//
//   @override
//   void initState() { super.initState(); Realtime.dangKyNghe(_khiCoTin); }
//   @override
//   void dispose()   { Realtime.boNghe(_khiCoTin); super.dispose(); }
//   void _khiCoTin(Map<String, dynamic> tin) { _taiLai(); }
//
// Nối không được (máy chủ tắt, mạng yếu) thì app vẫn chạy bình thường,
// chỉ là phải kéo xuống để tải lại.
class Realtime {
  static HubConnection? _ketNoi;

  // Các hàm đang chờ nghe tin — mỗi màn hình đăng ký 1 hàm
  static final List<void Function(Map<String, dynamic> tin)> _nguoiNghe = [];

  static bool get dangNoi => _ketNoi != null && _ketNoi!.state == HubConnectionState.Connected;

  // Gọi sau khi đăng nhập xong
  static Future<void> batDau() async {
    if (_ketNoi != null) return; // đã nối rồi

    try {
      HubConnection ketNoi = HubConnectionBuilder()
          .withUrl(
            '$diaChiApi/hubs/cap-nhat',
            options: HttpConnectionOptions(
              // Máy chủ đọc token này để biết ai đang nối (xếp vào nhóm nguoi-dung-<mã>)
              accessTokenFactory: () async => Phien.token ?? '',
            ),
          )
          .withAutomaticReconnect() // rớt mạng thì tự nối lại
          .build();

      // Ba tên sự kiện dưới đây do máy chủ đặt (PhotocopyShared/Constants/HubEvents.cs)
      ketNoi.on('OrderCreated', _khiNhanTin);
      ketNoi.on('OrderStatusChanged', _khiNhanTin);
      ketNoi.on('PaymentConfirmed', _khiNhanTin);

      await ketNoi.start();
      _ketNoi = ketNoi;
    } catch (e) {
      _ketNoi = null; // không nối được thì thôi, app vẫn dùng được
    }
  }

  // Gọi khi đăng xuất
  static Future<void> dung() async {
    try {
      await _ketNoi?.stop();
    } catch (e) {
      // bỏ qua
    }
    _ketNoi = null;
    _nguoiNghe.clear();
  }

  static void dangKyNghe(void Function(Map<String, dynamic> tin) hamNghe) {
    if (!_nguoiNghe.contains(hamNghe)) _nguoiNghe.add(hamNghe);
  }

  static void boNghe(void Function(Map<String, dynamic> tin) hamNghe) {
    _nguoiNghe.remove(hamNghe);
  }

  // Có tin từ máy chủ: báo lại cho mọi màn hình đang nghe
  static void _khiNhanTin(List<Object?>? thamSo) {
    Map<String, dynamic> tin = {};
    if (thamSo != null && thamSo.isNotEmpty && thamSo[0] is Map) {
      tin = Map<String, dynamic>.from(thamSo[0] as Map);
    }
    // Chép ra danh sách mới vì màn hình có thể tự bỏ nghe ngay trong lúc chạy
    for (var hamNghe in List.of(_nguoiNghe)) {
      hamNghe(tin);
    }
  }
}
