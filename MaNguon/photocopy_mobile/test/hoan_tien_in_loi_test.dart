import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/widgets/khieu_nai_hoan_tien.dart';

// Đơn hủy vì quầy in lại 3 lần vẫn lỗi: khách thấy hồ sơ hoàn tiền cửa hàng lập.
http.Response _json(Object value) => http.Response(
  jsonEncode(value),
  200,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

Future<void> _ve(WidgetTester t, Object phanHoi) => http.runWithClient(
  () async {
    await t.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: SingleChildScrollView(
            child: KhieuNaiHoanTien(maDonHang: 21, anKhiKhongCoHoSo: true),
          ),
        ),
      ),
    );
    await t.pumpAndSettle();
  },
  () => MockClient((r) async => _json(phanHoi)),
);

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);

  testWidgets('Đơn hủy thường không có hồ sơ thì không hiện thẻ', (t) async {
    await _ve(t, {'choPhepGui': false, 'hoSo': null});
    expect(find.byType(Card), findsNothing);
    expect(find.textContaining('không đủ điều kiện'), findsNothing);
  });

  testWidgets('Đơn hủy do in lỗi 3 lần hiện hồ sơ cửa hàng lập', (t) async {
    await _ve(t, {
      'choPhepGui': false,
      'hoSo': {
        'trangThai': 'CHO_DUYET',
        'nguon': 'IN_LOI_3_LAN',
        'moTa': 'Quầy đã in lại 3 lần nhưng tài liệu vẫn lỗi',
        'nganHang': 'Vietcombank',
        'soTaiKhoan': '0123456789',
        'chuTaiKhoan': 'Khach Thu',
        'anh': [],
        'lichSu': [],
      },
    });
    expect(find.text('Hoàn tiền do cửa hàng in lỗi'), findsOneWidget);
    expect(find.text('Cửa hàng lập thay bạn'), findsOneWidget);
    expect(find.text('Chờ CEO xem xét'), findsOneWidget);
    expect(find.text('Gửi ý kiến về sản phẩm'), findsNothing);
    expect(t.takeException(), isNull);
  });
}
