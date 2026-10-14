import 'package:geolocator/geolocator.dart';

class DiemGps {
  const DiemGps(this.viDo, this.kinhDo, this.doChinhXac);
  final double viDo;
  final double kinhDo;
  final double doChinhXac;
}

// Không gọi lúc khởi động app. Khách chủ động bấm dùng vị trí mới hỏi quyền.
Future<DiemGps> layViTriHienTai() async {
  if (!await Geolocator.isLocationServiceEnabled()) {
    throw StateError(
      'Điện thoại đang tắt vị trí. Bạn có thể bật vị trí hoặc nhập địa chỉ bên dưới.',
    );
  }
  var quyen = await Geolocator.checkPermission();
  if (quyen == LocationPermission.denied) {
    quyen = await Geolocator.requestPermission();
  }
  if (quyen == LocationPermission.denied ||
      quyen == LocationPermission.deniedForever) {
    throw StateError(
      'Chưa được cấp quyền vị trí. Bạn vẫn có thể tìm địa chỉ bằng cách nhập tay, hoặc cấp quyền trong cài đặt ứng dụng.',
    );
  }
  final viTri = await Geolocator.getCurrentPosition(
    locationSettings: const LocationSettings(
      accuracy: LocationAccuracy.high,
      timeLimit: Duration(seconds: 15),
    ),
  );
  if (!viTri.latitude.isFinite ||
      !viTri.longitude.isFinite ||
      !viTri.accuracy.isFinite) {
    throw StateError(
      'Chưa lấy được vị trí hợp lệ. Hãy thử lại hoặc nhập địa chỉ.',
    );
  }
  return DiemGps(viTri.latitude, viTri.longitude, viTri.accuracy);
}
