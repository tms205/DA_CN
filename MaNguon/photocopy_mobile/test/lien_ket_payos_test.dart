import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/models.dart';

void main() {
  test('đọc link checkout và hạn thanh toán từ API', () {
    final link = LienKetPayOS.fromJson({
      'checkoutUrl': 'https://pay.payos.vn/web/abc123',
      'hanThanhToan': '2026-10-06T10:15:00',
    });

    expect(link.checkoutUrl, 'https://pay.payos.vn/web/abc123');
    expect(link.hanThanhToan, DateTime(2026, 10, 6, 10, 15));
  });
}
