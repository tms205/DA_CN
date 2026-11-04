import 'dart:io';
import 'package:file_picker/file_picker.dart';

// Chọn theo lô, kiểm tra cả lô trước khi thêm. Giới hạn tính cả ảnh đã chọn.
Future<List<PlatformFile>> chonAnhBangChung({
  required Iterable<String> daChon,
  Future<List<PlatformFile>> Function()? chonNhieu,
  Future<PlatformFile?> Function()? chonMot,
}) async {
  final cu = daChon.toSet();
  if (cu.length >= 3) return [];
  List<PlatformFile> files;
  if (chonNhieu != null) {
    files = await chonNhieu();
  } else if (chonMot != null) {
    final file = await chonMot();
    files = file == null ? [] : [file];
  } else {
    files = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['jpg', 'jpeg', 'png'],
    );
  }
  final seen = {...cu};
  final added = <PlatformFile>[];
  for (final file in files) {
    if (file.path == null ||
        !RegExp(r'\.(jpe?g|png)$', caseSensitive: false).hasMatch(file.name)) {
      throw const FormatException('Chọn ảnh JPG/PNG có thể đọc được.');
    }
    if (seen.add(file.path!)) added.add(file);
  }
  if (cu.length + added.length > 3) {
    throw FormatException(
      'Tối đa 3 ảnh. Bạn đang có ${cu.length} ảnh; chỉ chọn thêm ${3 - cu.length} ảnh.',
    );
  }
  for (final file in added) {
    final size = await File(file.path!).length();
    if (size <= 0 || size > 5 * 1024 * 1024) {
      throw const FormatException('Mỗi ảnh phải có nội dung và tối đa 5 MB.');
    }
  }
  return added;
}
