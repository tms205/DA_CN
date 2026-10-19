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
      'Điện thoại đang tắt vị trí. Hãy bật vị trí rồi bấm Dùng vị trí hiện tại để thử lại.',
    );
  }
  var quyen = await Geolocator.checkPermission();
  if (quyen == LocationPermission.denied) {
    quyen = await Geolocator.requestPermission();
  }
  if (quyen == LocationPermission.denied ||
      quyen == LocationPermission.deniedForever) {
    throw StateError(
      'Chưa được cấp quyền vị trí. Hãy cấp quyền và bật vị trí chính xác trong cài đặt ứng dụng rồi thử lại.',
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
      'Chưa lấy được vị trí hợp lệ. Hãy bật vị trí chính xác và thử lại.',
    );
  }
  return DiemGps(viTri.latitude, viTri.longitude, viTri.accuracy);
}
